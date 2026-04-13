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
[Route("api/exams")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public ExamsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    public async Task<ActionResult<List<Exam>>> List([FromQuery] Guid? classId, [FromQuery] Guid? subjectId, [FromQuery] Guid? termId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var query = _db.Exams
            .AsNoTracking()
            .Include(x => x.Class)
            .Include(x => x.Subject)
            .Include(x => x.Term)
            .AsQueryable();

        if (classId.HasValue)
            query = query.Where(x => x.ClassId == classId.Value);
        if (subjectId.HasValue)
            query = query.Where(x => x.SubjectId == subjectId.Value);
        if (termId.HasValue)
            query = query.Where(x => x.TermId == termId.Value);

        var list = await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    public async Task<ActionResult<Exam>> Create([FromBody] CreateExamRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var createdByTeacherId = await ResolveCurrentTeacherIdAsync(ct);
        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            Name = request.Name.Trim(),
            ClassId = request.ClassId,
            SubjectId = request.SubjectId,
            TermId = request.TermId,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            CreatedByTeacherId = createdByTeacherId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Exams.Add(exam);
        await _db.SaveChangesAsync(ct);
        return Ok(exam);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    public async Task<ActionResult<Exam>> Update(Guid id, [FromBody] UpdateExamRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var exam = await _db.Exams.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exam == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        exam.Name = request.Name.Trim();
        exam.ClassId = request.ClassId;
        exam.SubjectId = request.SubjectId;
        exam.TermId = request.TermId;
        exam.StartDateUtc = request.StartDateUtc;
        exam.EndDateUtc = request.EndDateUtc;
        exam.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(exam);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var exam = await _db.Exams.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (exam == null)
            return NotFound();

        _db.Exams.Remove(exam);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("submission-window")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    public async Task<ActionResult<MarkSubmissionWindow>> GetSubmissionWindow([FromQuery] Guid termId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var window = await _db.MarkSubmissionWindows
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TermId == termId, ct);

        if (window == null)
            return Ok(new MarkSubmissionWindow
            {
                Id = Guid.Empty,
                SchoolId = _tenant.CurrentSchoolId.Value,
                TermId = termId,
                IsOpen = false,
                CreatedAtUtc = DateTime.UtcNow
            });

        return Ok(window);
    }

    [HttpPut("submission-window")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<MarkSubmissionWindow>> UpsertSubmissionWindow([FromBody] UpdateMarkSubmissionWindowRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var window = await _db.MarkSubmissionWindows
            .FirstOrDefaultAsync(x => x.TermId == request.TermId, ct);

        if (window == null)
        {
            window = new MarkSubmissionWindow
            {
                Id = Guid.NewGuid(),
                SchoolId = _tenant.CurrentSchoolId.Value,
                TermId = request.TermId,
                IsOpen = request.IsOpen,
                OpenedAtUtc = request.IsOpen ? DateTime.UtcNow : null,
                ClosedAtUtc = request.IsOpen ? null : DateTime.UtcNow,
                UpdatedByUserId = TryGetCurrentUserId(),
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.MarkSubmissionWindows.Add(window);
        }
        else
        {
            window.IsOpen = request.IsOpen;
            if (request.IsOpen)
            {
                window.OpenedAtUtc = DateTime.UtcNow;
                window.ClosedAtUtc = null;
            }
            else
            {
                window.ClosedAtUtc = DateTime.UtcNow;
            }
            window.UpdatedByUserId = TryGetCurrentUserId();
            window.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(window);
    }

    private Guid? TryGetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
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
}
