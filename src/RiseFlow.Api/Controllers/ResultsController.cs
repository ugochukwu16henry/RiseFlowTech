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

    public ResultsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
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
        result.AssessmentType = request.AssessmentType;
        result.Score = request.Score;
        result.MaxScore = request.MaxScore;
        result.GradeLetter = request.GradeLetter;
        result.Comment = request.Comment;
        result.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
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
        _db.StudentResults.Remove(result);
        await _db.SaveChangesAsync(ct);
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
