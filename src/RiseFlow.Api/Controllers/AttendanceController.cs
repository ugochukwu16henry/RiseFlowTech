using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

/// <summary>
/// Central attendance endpoints.
/// These are designed so offline clients with local SQLite can:
/// - Fetch class rosters + existing attendance for a day.
/// - Upsert batches of attendance when they come back online.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Teacher / SchoolAdmin; SuperAdmin can also be allowed if needed.
public class AttendanceController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public AttendanceController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// Upsert a batch of attendance records for the current school.
    /// Offline clients should send only PENDING/CHANGED records; this endpoint is idempotent per (StudentId, Date, Period).
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    [ProducesResponseType(typeof(AttendanceBatchUpsertResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceBatchUpsertResponse>> UpsertBatch(
        [FromBody] AttendanceBatchUpsertRequest request,
        CancellationToken ct)
    {
        var schoolId = _tenant.CurrentSchoolId;
        if (!schoolId.HasValue)
            return Forbid();

        if (request.Items == null || request.Items.Count == 0)
            return Ok(new AttendanceBatchUpsertResponse(Array.Empty<AttendanceUpsertResultItem>()));

        var now = DateTime.UtcNow;
        var results = new List<AttendanceUpsertResultItem>(request.Items.Count);

        // Preload valid students for this tenant to protect against cross-tenant abuse.
        var studentIds = request.Items.Select(i => i.StudentId).Distinct().ToList();
        var validStudentIds = await _db.Students
            .Where(s => s.SchoolId == schoolId.Value && studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(ct);
        var validSet = validStudentIds.ToHashSet();

        foreach (var item in request.Items)
        {
            if (!validSet.Contains(item.StudentId))
                continue; // ignore invalid / foreign students

            var period = string.IsNullOrWhiteSpace(item.Period) ? null : item.Period.Trim();
            var status = string.IsNullOrWhiteSpace(item.Status) ? "Present" : item.Status.Trim();

            var existing = await _db.AttendanceRecords
                .FirstOrDefaultAsync(a =>
                    a.SchoolId == schoolId.Value &&
                    a.StudentId == item.StudentId &&
                    a.Date == item.Date &&
                    a.Period == period,
                    ct);

            if (existing == null)
            {
                existing = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId.Value,
                    StudentId = item.StudentId,
                    Date = item.Date,
                    Period = period,
                    Status = status,
                    Note = item.Note,
                    SourceDeviceId = item.SourceDeviceId,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = null
                };
                _db.AttendanceRecords.Add(existing);

                results.Add(new AttendanceUpsertResultItem(
                    item.StudentId,
                    item.Date,
                    period,
                    status,
                    Created: true,
                    ServerTimestampUtc: now));
            }
            else
            {
                existing.Status = status;
                existing.Note = item.Note;
                existing.SourceDeviceId = item.SourceDeviceId;
                existing.UpdatedAtUtc = now;

                results.Add(new AttendanceUpsertResultItem(
                    item.StudentId,
                    item.Date,
                    period,
                    status,
                    Created: false,
                    ServerTimestampUtc: now));
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new AttendanceBatchUpsertResponse(results));
    }

    /// <summary>
    /// Get roster + attendance status for a class on a given date.
    /// This is what a classroom device would call to pre-fill its local SQLite tables before going offline.
    /// </summary>
    [HttpGet("class/{classId:guid}")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    public async Task<ActionResult<object>> GetClassAttendance(
        Guid classId,
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        var schoolId = _tenant.CurrentSchoolId;
        if (!schoolId.HasValue)
            return Forbid();

        // Students in this class for this school
        var students = await _db.Students
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId.Value && s.ClassId == classId && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new
            {
                s.Id,
                s.FirstName,
                s.MiddleName,
                s.LastName,
                s.AdmissionNumber
            })
            .ToListAsync(ct);

        var studentIds = students.Select(s => s.Id).ToList();

        var attendance = await _db.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId.Value && a.Date == date && studentIds.Contains(a.StudentId))
            .ToListAsync(ct);

        var attendanceLookup = attendance
            .GroupBy(a => a.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc).First());

        var items = students.Select(s =>
        {
            attendanceLookup.TryGetValue(s.Id, out var a);
            return new
            {
                s.Id,
                s.FirstName,
                s.MiddleName,
                s.LastName,
                s.AdmissionNumber,
                Attendance = a == null
                    ? null
                    : new
                    {
                        a.Status,
                        a.Period,
                        a.Note,
                        a.Date,
                        a.CreatedAtUtc,
                        a.UpdatedAtUtc
                    }
            };
        });

        return Ok(new
        {
            ClassId = classId,
            Date = date,
            Students = items
        });
    }
}

