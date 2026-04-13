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
[Route("api/routines")]
[Authorize]
public class RoutinesController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public RoutinesController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    public async Task<ActionResult<List<ClassRoutine>>> List([FromQuery] Guid classId, [FromQuery] int? weekday, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (classId == Guid.Empty)
            return BadRequest(new { message = "classId is required." });

        var query = _db.ClassRoutines
            .AsNoTracking()
            .Include(r => r.Class)
            .Include(r => r.Subject)
            .Include(r => r.Teacher)
            .Where(r => r.ClassId == classId)
            .AsQueryable();

        if (weekday.HasValue)
            query = query.Where(r => r.Weekday == weekday.Value);

        var data = await query
            .OrderBy(r => r.Weekday)
            .ThenBy(r => r.StartTime)
            .ToListAsync(ct);

        return Ok(data);
    }

    [HttpPost]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<ClassRoutine>> Create([FromBody] CreateClassRoutineRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var validation = await ValidateRoutineInputAsync(schoolId, request.ClassId, request.SubjectId, request.TeacherId, request.Weekday, request.StartTime, request.EndTime, null, ct);
        if (validation != null)
            return validation;

        var entity = new ClassRoutine
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            ClassId = request.ClassId,
            SubjectId = request.SubjectId,
            TeacherId = request.TeacherId,
            Weekday = request.Weekday,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Room = request.Room,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ClassRoutines.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<ClassRoutine>> Update(Guid id, [FromBody] UpdateClassRoutineRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var entity = await _db.ClassRoutines.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null)
            return NotFound();

        var validation = await ValidateRoutineInputAsync(schoolId, request.ClassId, request.SubjectId, request.TeacherId, request.Weekday, request.StartTime, request.EndTime, id, ct);
        if (validation != null)
            return validation;

        entity.ClassId = request.ClassId;
        entity.SubjectId = request.SubjectId;
        entity.TeacherId = request.TeacherId;
        entity.Weekday = request.Weekday;
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.Room = request.Room;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var entity = await _db.ClassRoutines.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity == null)
            return NotFound();

        _db.ClassRoutines.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult?> ValidateRoutineInputAsync(
        Guid schoolId,
        Guid classId,
        Guid subjectId,
        Guid? teacherId,
        int weekday,
        string startTime,
        string endTime,
        Guid? editingId,
        CancellationToken ct)
    {
        if (classId == Guid.Empty || subjectId == Guid.Empty)
            return BadRequest(new { message = "ClassId and SubjectId are required." });

        if (weekday < 0 || weekday > 6)
            return BadRequest(new { message = "Weekday must be between 0 and 6." });

        if (!TimeSpan.TryParse(startTime, out var start) || !TimeSpan.TryParse(endTime, out var end) || end <= start)
            return BadRequest(new { message = "Provide valid StartTime and EndTime in HH:mm format with EndTime after StartTime." });

        var classExists = await _db.Classes.AnyAsync(c => c.Id == classId && c.SchoolId == schoolId, ct);
        var subjectExists = await _db.Subjects.AnyAsync(s => s.Id == subjectId && s.SchoolId == schoolId, ct);
        if (!classExists || !subjectExists)
            return BadRequest(new { message = "Class or subject not found for this school." });

        if (teacherId.HasValue)
        {
            var teacherExists = await _db.Teachers.AnyAsync(t => t.Id == teacherId.Value && t.SchoolId == schoolId, ct);
            if (!teacherExists)
                return BadRequest(new { message = "Teacher not found for this school." });
        }

        var overlaps = await _db.ClassRoutines
            .AsNoTracking()
            .Where(r => r.SchoolId == schoolId && r.ClassId == classId && r.Weekday == weekday && (!editingId.HasValue || r.Id != editingId.Value))
            .ToListAsync(ct);

        var conflict = overlaps.Any(r =>
        {
            var existingStart = TimeSpan.TryParse(r.StartTime, out var st) ? st : TimeSpan.Zero;
            var existingEnd = TimeSpan.TryParse(r.EndTime, out var et) ? et : TimeSpan.Zero;
            return start < existingEnd && end > existingStart;
        });

        if (conflict)
            return BadRequest(new { message = "Routine time overlaps with an existing class slot." });

        return null;
    }
}
