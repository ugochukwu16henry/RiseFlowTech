using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ResultsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogService _audit;
    private readonly IConfiguration _configuration;
    private readonly StaffPermissionService _staffPermissions;

    public ResultsController(RiseFlowDbContext db, ITenantContext tenant, IAuditLogService audit, IConfiguration configuration, StaffPermissionService staffPermissions)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
        _configuration = configuration;
        _staffPermissions = staffPermissions;
    }

    /// <summary>Teachers/SchoolAdmin: upload or update a result. EnteredBy is set from current user (teacher by email).</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    [ProducesResponseType(typeof(StudentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StudentResult>> Create([FromBody] CreateResultRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanApproveResults, "StudentResult", "Create", null, ct))
            return Forbid();

        var submissionWindowBlock = await ValidateTeacherSubmissionWindowAsync(request.TermId, ct);
        if (submissionWindowBlock != null)
            return submissionWindowBlock;

        var teacherId = await ResolveCurrentTeacherIdAsync(ct);
        var resolvedGradeLetter = await ResolveGradeLetterAsync(request.StudentId, request.TermId, request.Score, request.MaxScore, request.GradeLetter, ct);
        var result = new StudentResult
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            StudentId = request.StudentId,
            SubjectId = request.SubjectId,
            TermId = request.TermId,
            ExamId = request.ExamId,
            AssessmentType = request.AssessmentType ?? "Exam",
            Score = request.Score,
            MaxScore = request.MaxScore,
            GradeLetter = resolvedGradeLetter,
            Comment = request.Comment,
            EnteredByTeacherId = teacherId,
            WorkflowStatus = ResultWorkflowStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.StudentResults.Add(result);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(
            _tenant.CurrentSchoolId,
            "Created",
            "StudentResult",
            result.Id.ToString(),
            _tenant.CurrentUserEmail,
            User.Identity?.Name,
            $"Result created: Student {request.StudentId:N}, Score {request.Score}/{request.MaxScore}",
            ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StudentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentResult>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var result = await _db.StudentResults
            .AsNoTracking()
            .Include(r => r.Student)
            .Include(r => r.Subject)
            .Include(r => r.Term)
            .Include(r => r.Exam)
            .Include(r => r.EnteredByTeacher)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (result == null)
            return NotFound();
        if (User.IsInRole(Roles.Parent) && !await CanParentAccessStudentAsync(result.StudentId, ct))
            return Forbid();
        if (User.IsInRole(Roles.Student) && !await CanStudentAccessStudentAsync(result.StudentId, ct))
            return Forbid();
        if ((User.IsInRole(Roles.Parent) || User.IsInRole(Roles.Student)) && result.WorkflowStatus != ResultWorkflowStatus.ApprovedLocked)
            return NotFound();
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    [ProducesResponseType(typeof(StudentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentResult>> Update(Guid id, [FromBody] UpdateResultRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanApproveResults, "StudentResult", "Update", id.ToString(), ct))
            return Forbid();
        var result = await _db.StudentResults.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (result == null)
            return NotFound();
        if (result.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();
        if (result.WorkflowStatus == ResultWorkflowStatus.ApprovedLocked || result.LockedAtUtc.HasValue)
            return BadRequest(new { message = "Result is locked after final approval and cannot be edited." });

        var submissionWindowBlock = await ValidateTeacherSubmissionWindowAsync(result.TermId, ct);
        if (submissionWindowBlock != null)
            return submissionWindowBlock;

        var oldScore = result.Score;
        var oldMax = result.MaxScore;
        var resolvedGradeLetter = await ResolveGradeLetterAsync(result.StudentId, result.TermId, request.Score, request.MaxScore, request.GradeLetter, ct);
        result.AssessmentType = request.AssessmentType;
        result.Score = request.Score;
        result.MaxScore = request.MaxScore;
        result.GradeLetter = resolvedGradeLetter;
        result.Comment = request.Comment;
        result.ExamId = request.ExamId;
        result.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(
            _tenant.CurrentSchoolId,
            "Updated",
            "StudentResult",
            result.Id.ToString(),
            _tenant.CurrentUserEmail,
            User.Identity?.Name,
            $"Score {oldScore}/{oldMax} → {request.Score}/{request.MaxScore}",
            ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanApproveResults, "StudentResult", "Delete", id.ToString(), ct))
            return Forbid();
        var result = await _db.StudentResults.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (result == null)
            return NotFound();
        if (result.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();
        if (result.WorkflowStatus == ResultWorkflowStatus.ApprovedLocked || result.LockedAtUtc.HasValue)
            return BadRequest(new { message = "Result is locked after final approval and cannot be deleted." });
        var details = $"Result deleted: Student {result.StudentId:N}, Score {result.Score}/{result.MaxScore}";
        _db.StudentResults.Remove(result);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(
            _tenant.CurrentSchoolId,
            "Deleted",
            "StudentResult",
            id.ToString(),
            _tenant.CurrentUserEmail,
            User.Identity?.Name,
            details,
            ct);
        return NoContent();
    }

    /// <summary>Teacher/SchoolAdmin: submit a drafted result for review.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    [ProducesResponseType(typeof(StudentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentResult>> Submit(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanApproveResults, "StudentResult", "Submit", id.ToString(), ct))
            return Forbid();

        var result = await _db.StudentResults.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (result == null)
            return NotFound();
        if (result.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();
        if (result.WorkflowStatus == ResultWorkflowStatus.ApprovedLocked || result.LockedAtUtc.HasValue)
            return BadRequest(new { message = "Result is locked after final approval." });

        var teacherId = await ResolveCurrentTeacherIdAsync(ct);
        result.WorkflowStatus = ResultWorkflowStatus.Submitted;
        result.SubmittedAtUtc = DateTime.UtcNow;
        result.SubmittedByTeacherId = teacherId;
        result.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(result);
    }

    /// <summary>Class-head review stage before final approval.</summary>
    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    [ProducesResponseType(typeof(StudentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentResult>> Review(Guid id, [FromBody] ReviewResultRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanApproveResults, "StudentResult", "Review", id.ToString(), ct))
            return Forbid();

        var result = await _db.StudentResults.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (result == null)
            return NotFound();
        if (result.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();
        if (result.WorkflowStatus == ResultWorkflowStatus.ApprovedLocked || result.LockedAtUtc.HasValue)
            return BadRequest(new { message = "Result is locked after final approval." });

        result.WorkflowStatus = ResultWorkflowStatus.Reviewed;
        result.ReviewedAtUtc = DateTime.UtcNow;
        result.ReviewedByUserId = TryGetCurrentUserId();
        result.ReviewComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        result.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(result);
    }

    /// <summary>SchoolAdmin final approval and immutable lock.</summary>
    [HttpPost("{id:guid}/final-approve")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(StudentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentResult>> FinalApprove(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanApproveResults, "StudentResult", "FinalApprove", id.ToString(), ct))
            return Forbid();

        var result = await _db.StudentResults.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (result == null)
            return NotFound();
        if (result.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();

        var approvedAt = DateTime.UtcNow;
        result.WorkflowStatus = ResultWorkflowStatus.ApprovedLocked;
        result.FinalApprovedAtUtc = approvedAt;
        result.FinalApprovedByUserId = TryGetCurrentUserId();
        result.LockedAtUtc = approvedAt;
        result.UpdatedAtUtc = approvedAt;
        await _db.SaveChangesAsync(ct);
        return Ok(result);
    }

    /// <summary>List results: by student and optional term (teachers/schooladmin), or for parent's children only.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<StudentResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StudentResult>>> List([FromQuery] Guid? studentId, [FromQuery] Guid? termId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        IQueryable<StudentResult> query = _db.StudentResults
            .Include(r => r.Student)
            .Include(r => r.Subject)
            .Include(r => r.Term)
            .Include(r => r.Exam);
        if (User.IsInRole(Roles.Parent))
        {
            var allowedStudentIds = await GetParentLinkedStudentIdsAsync(ct);
            if (allowedStudentIds.Count == 0)
                return Ok(new List<StudentResult>());
            query = query.Where(r => allowedStudentIds.Contains(r.StudentId));
            query = query.Where(r => r.WorkflowStatus == ResultWorkflowStatus.ApprovedLocked);
        }
        else if (User.IsInRole(Roles.Student))
        {
            var allowedStudentIds = await GetStudentLinkedStudentIdsAsync(ct);
            if (allowedStudentIds.Count == 0)
                return Ok(new List<StudentResult>());
            query = query.Where(r => allowedStudentIds.Contains(r.StudentId));
            query = query.Where(r => r.WorkflowStatus == ResultWorkflowStatus.ApprovedLocked);
        }
        else if (studentId.HasValue)
            query = query.Where(r => r.StudentId == studentId.Value);
        if (termId.HasValue)
            query = query.Where(r => r.TermId == termId.Value);
        var list = await query.OrderBy(r => r.Term!.StartDate).ThenBy(r => r.Subject!.Name).ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>Parent-only: results for all my children, optionally filtered by term.</summary>
    [HttpGet("my-children")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(List<StudentResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StudentResult>>> MyChildrenResults([FromQuery] Guid? termId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var allowedStudentIds = await GetParentLinkedStudentIdsAsync(ct);
        if (allowedStudentIds.Count == 0)
            return Ok(new List<StudentResult>());
        var query = _db.StudentResults
            .Include(r => r.Student)
            .Include(r => r.Subject)
            .Include(r => r.Term)
            .Include(r => r.Exam)
            .Where(r => allowedStudentIds.Contains(r.StudentId));
        query = query.Where(r => r.WorkflowStatus == ResultWorkflowStatus.ApprovedLocked);
        if (termId.HasValue)
            query = query.Where(r => r.TermId == termId.Value);
        var list = await query.OrderBy(r => r.Student!.LastName).ThenBy(r => r.Subject!.Name).ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>Class rankings for a term: total score and position in class. Teachers/SchoolAdmin. Requires termId; classId optional (filter by class).</summary>
    [HttpGet("class-rankings")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    [ProducesResponseType(typeof(List<ClassRankingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ClassRankingDto>>> ClassRankings([FromQuery] Guid termId, [FromQuery] Guid? classId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var results = await _db.StudentResults
            .AsNoTracking()
            .Include(r => r.Student)
            .Where(r => r.TermId == termId)
            .ToListAsync(ct);
        if (classId.HasValue)
            results = results.Where(r => r.Student != null && r.Student.ClassId == classId.Value).ToList();
        var byStudent = results
            .GroupBy(r => r.StudentId)
            .Select(g =>
            {
                var first = g.First();
                var totalScore = g.Sum(r => r.Score);
                var maxTotal = g.Sum(r => r.MaxScore);
                var pct = maxTotal > 0 ? Math.Round((totalScore / maxTotal) * 100, 1) : 0m;
                var name = first.Student == null ? "—" : $"{first.Student.LastName} {first.Student.FirstName}".Trim();
                return (StudentId: first.StudentId, StudentName: name, TotalScore: totalScore, MaxTotal: maxTotal, Percentage: pct);
            })
            .OrderByDescending(x => x.TotalScore)
            .ToList();

        // Dense ranking: students with the same total share the same position; next distinct score increments by 1 (1,2,2,3…)
        var rankings = new List<ClassRankingDto>();
        int position = 0;
        decimal? lastScore = null;
        for (var i = 0; i < byStudent.Count; i++)
        {
            var current = byStudent[i];
            if (lastScore == null || current.TotalScore < lastScore.Value)
                position++;
            rankings.Add(new ClassRankingDto(current.StudentId, current.StudentName, current.TotalScore, current.MaxTotal, current.Percentage, position));
            lastScore = current.TotalScore;
        }
        return Ok(rankings);
    }

    private async Task<Guid?> ResolveCurrentTeacherIdAsync(CancellationToken ct)
    {
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrEmpty(email) || !_tenant.CurrentSchoolId.HasValue)
            return null;
        var teacher = await _db.Teachers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SchoolId == _tenant.CurrentSchoolId && t.Email == email, ct);
        return teacher?.Id;
    }

    private async Task<List<Guid>> GetParentLinkedStudentIdsAsync(CancellationToken ct)
    {
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrEmpty(email) || !_tenant.CurrentSchoolId.HasValue)
            return new List<Guid>();
        var parent = await _db.Parents
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SchoolId == _tenant.CurrentSchoolId && p.Email == email, ct);
        if (parent == null)
            return new List<Guid>();
        return await _db.StudentParents
            .Where(sp => sp.ParentId == parent.Id)
            .Select(sp => sp.StudentId)
            .ToListAsync(ct);
    }

    private async Task<List<Guid>> GetStudentLinkedStudentIdsAsync(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return new List<Guid>();
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return new List<Guid>();
        return await _db.StudentPortalAccesses
            .AsNoTracking()
            .Where(spa => spa.SchoolId == _tenant.CurrentSchoolId.Value && spa.UserId == userId && spa.IsEnabled)
            .Select(spa => spa.StudentId)
            .ToListAsync(ct);
    }

    private async Task<bool> CanParentAccessStudentAsync(Guid studentId, CancellationToken ct)
    {
        var ids = await GetParentLinkedStudentIdsAsync(ct);
        return ids.Contains(studentId);
    }

    private async Task<bool> CanStudentAccessStudentAsync(Guid studentId, CancellationToken ct)
    {
        var ids = await GetStudentLinkedStudentIdsAsync(ct);
        return ids.Contains(studentId);
    }

    private async Task<string?> ResolveGradeLetterAsync(
        Guid studentId,
        Guid termId,
        decimal score,
        decimal maxScore,
        string? fallbackGradeLetter,
        CancellationToken ct)
    {
        if (!_configuration.GetValue<bool>("Features:EnableGradingSystemV1"))
            return fallbackGradeLetter;

        if (!_tenant.CurrentSchoolId.HasValue || maxScore <= 0)
            return fallbackGradeLetter;

        var studentClassId = await _db.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId && s.SchoolId == _tenant.CurrentSchoolId.Value)
            .Select(s => s.ClassId)
            .FirstOrDefaultAsync(ct);

        var candidateSystems = await _db.GradingSystems
            .AsNoTracking()
            .Where(g => g.SchoolId == _tenant.CurrentSchoolId.Value && g.IsActive)
            .Where(g => (g.ClassId == null || g.ClassId == studentClassId) && (g.TermId == null || g.TermId == termId))
            .OrderByDescending(g => g.ClassId == studentClassId)
            .ThenByDescending(g => g.TermId == termId)
            .ThenByDescending(g => g.UpdatedAtUtc ?? g.CreatedAtUtc)
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (candidateSystems.Count == 0)
            return fallbackGradeLetter;

        var percentage = (score / maxScore) * 100m;
        var gradeRule = await _db.GradeRules
            .AsNoTracking()
            .Where(r => candidateSystems.Contains(r.GradingSystemId))
            .Where(r => percentage >= r.MinPercent && percentage <= r.MaxPercent)
            .OrderByDescending(r => r.MinPercent)
            .FirstOrDefaultAsync(ct);

        return gradeRule?.GradeLetter ?? fallbackGradeLetter;
    }

    private async Task<ActionResult?> ValidateTeacherSubmissionWindowAsync(Guid termId, CancellationToken ct)
    {
        if (!_configuration.GetValue<bool>("Features:EnableExamWindowV1"))
            return null;

        if (User.IsInRole(Roles.SchoolAdmin) || !User.IsInRole(Roles.Teacher) || !_tenant.CurrentSchoolId.HasValue)
            return null;

        var window = await _db.MarkSubmissionWindows
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolId == _tenant.CurrentSchoolId.Value && x.TermId == termId, ct);

        if (window == null || !window.IsOpen)
        {
            return BadRequest(new
            {
                message = "Mark submission window is closed for this term."
            });
        }

        return null;
    }

    private Guid? TryGetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }
}

public record ClassRankingDto(Guid StudentId, string StudentName, decimal TotalScore, decimal MaxTotal, decimal Percentage, int PositionInClass);
