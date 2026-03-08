using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubjectsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public SubjectsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Subject>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Subject>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var list = await _db.Subjects.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Subject), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Subject>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var subject = await _db.Subjects
            .AsNoTracking()
            .Include(s => s.TeacherSubjects).ThenInclude(ts => ts.Teacher)
            .Include(s => s.ClassSubjects).ThenInclude(cs => cs.Class)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subject == null)
            return NotFound();
        return Ok(subject);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Subject), StatusCodes.Status201Created)]
    public async Task<ActionResult<Subject>> Create([FromBody] CreateSubjectRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            Name = request.Name,
            Code = request.Code,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = subject.Id }, subject);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Subject), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Subject>> Update(Guid id, [FromBody] UpdateSubjectRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subject == null)
            return NotFound();
        subject.Name = request.Name;
        subject.Code = request.Code;
        subject.IsActive = request.IsActive;
        subject.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(subject);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subject == null)
            return NotFound();
        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("teachers/{teacherId:guid}/subjects/{subjectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> AssignTeacherToSubject(Guid teacherId, Guid subjectId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (await _db.TeacherSubjects.AnyAsync(ts => ts.TeacherId == teacherId && ts.SubjectId == subjectId, ct))
            return NoContent();
        _db.TeacherSubjects.Add(new TeacherSubject { TeacherId = teacherId, SubjectId = subjectId, AssignedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("teachers/{teacherId:guid}/subjects/{subjectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UnassignTeacherFromSubject(Guid teacherId, Guid subjectId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var link = await _db.TeacherSubjects.FirstOrDefaultAsync(ts => ts.TeacherId == teacherId && ts.SubjectId == subjectId, ct);
        if (link != null) { _db.TeacherSubjects.Remove(link); await _db.SaveChangesAsync(ct); }
        return NoContent();
    }

    [HttpPost("classes/{classId:guid}/subjects/{subjectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> AssignSubjectToClass(Guid classId, Guid subjectId, [FromQuery] int? order, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (await _db.ClassSubjects.AnyAsync(cs => cs.ClassId == classId && cs.SubjectId == subjectId, ct))
            return NoContent();
        _db.ClassSubjects.Add(new ClassSubject { ClassId = classId, SubjectId = subjectId, Order = order, CreatedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("classes/{classId:guid}/subjects/{subjectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UnassignSubjectFromClass(Guid classId, Guid subjectId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var link = await _db.ClassSubjects.FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.SubjectId == subjectId, ct);
        if (link != null) { _db.ClassSubjects.Remove(link); await _db.SaveChangesAsync(ct); }
        return NoContent();
    }

    [HttpPost("teachers/{teacherId:guid}/classes/{classId:guid}/subjects/{subjectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> AssignTeacherToClassSubject(Guid teacherId, Guid classId, Guid subjectId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (await _db.TeacherClassSubjects.AnyAsync(tcs => tcs.TeacherId == teacherId && tcs.ClassId == classId && tcs.SubjectId == subjectId, ct))
            return NoContent();
        _db.TeacherClassSubjects.Add(new TeacherClassSubject { TeacherId = teacherId, ClassId = classId, SubjectId = subjectId, AssignedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("teachers/{teacherId:guid}/classes/{classId:guid}/subjects/{subjectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UnassignTeacherFromClassSubject(Guid teacherId, Guid classId, Guid subjectId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var link = await _db.TeacherClassSubjects.FirstOrDefaultAsync(tcs => tcs.TeacherId == teacherId && tcs.ClassId == classId && tcs.SubjectId == subjectId, ct);
        if (link != null) { _db.TeacherClassSubjects.Remove(link); await _db.SaveChangesAsync(ct); }
        return NoContent();
    }
}
