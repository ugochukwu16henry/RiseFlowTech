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

    public ResultsController(RiseFlowDbContext db, ITenantContext tenant, IAuditLogService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
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
        var teacherId = await ResolveCurrentTeacherIdAsync(ct);
        var result = new StudentResult
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            StudentId = request.StudentId,
            SubjectId = request.SubjectId,
            TermId = request.TermId,
            AssessmentType = request.AssessmentType ?? "Exam",
            Score = request.Score,
            MaxScore = request.MaxScore,
            GradeLetter = request.GradeLetter,
            Comment = request.Comment,
            EnteredByTeacherId = teacherId,
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
            .Include(r => r.EnteredByTeacher)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (result == null)
            return NotFound();
        if (User.IsInRole(Roles.Parent) && !await CanParentAccessStudentAsync(result.StudentId, ct))
            return Forbid();
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
        var result = await _db.StudentResults.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (result == null)
            return NotFound();
        var oldScore = result.Score;
        var oldMax = result.MaxScore;
        result.AssessmentType = request.AssessmentType;
        result.Score = request.Score;
        result.MaxScore = request.MaxScore;
        result.GradeLetter = request.GradeLetter;
        result.Comment = request.Comment;
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
        var result = await _db.StudentResults.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (result == null)
            return NotFound();
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
            .Include(r => r.Term);
        if (User.IsInRole(Roles.Parent))
        {
            var allowedStudentIds = await GetParentLinkedStudentIdsAsync(ct);
            if (allowedStudentIds.Count == 0)
                return Ok(new List<StudentResult>());
            query = query.Where(r => allowedStudentIds.Contains(r.StudentId));
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
            .Where(r => allowedStudentIds.Contains(r.StudentId));
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

    private async Task<bool> CanParentAccessStudentAsync(Guid studentId, CancellationToken ct)
    {
        var ids = await GetParentLinkedStudentIdsAsync(ct);
        return ids.Contains(studentId);
    }
}

public record ClassRankingDto(Guid StudentId, string StudentName, decimal TotalScore, decimal MaxTotal, decimal Percentage, int PositionInClass);
