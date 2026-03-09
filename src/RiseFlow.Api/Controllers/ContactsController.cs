using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public ContactsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// Contact directory: teachers who teach the current user's children (Parent only).
    /// Optional studentId: when provided, returns only teachers for that child's class with Subject (for Family View).
    /// </summary>
    [HttpGet("teachers")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(List<TeacherContactDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TeacherContactDto>>> GetTeachersForMyChildren([FromQuery] Guid? studentId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var linkedIds = await GetParentLinkedStudentIdsAsync(ct);
        if (linkedIds.Count == 0)
            return Ok(new List<TeacherContactDto>());

        List<Guid> classIds;
        if (studentId.HasValue)
        {
            if (!linkedIds.Contains(studentId.Value))
                return Forbid();
            var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId.Value, ct);
            if (student?.ClassId == null)
                return Ok(new List<TeacherContactDto>());
            classIds = new List<Guid> { student.ClassId.Value };
        }
        else
        {
            classIds = await _db.Students
                .Where(s => linkedIds.Contains(s.Id) && s.ClassId != null)
                .Select(s => s.ClassId!.Value)
                .Distinct()
                .ToListAsync(ct);
        }

        if (classIds.Count == 0)
            return Ok(new List<TeacherContactDto>());

        if (studentId.HasValue)
        {
            var tcsList = await _db.Set<TeacherClassSubject>()
                .AsNoTracking()
                .Include(tcs => tcs.Teacher)
                .Include(tcs => tcs.Subject)
                .Where(tcs => classIds.Contains(tcs.ClassId) && tcs.Teacher.IsActive)
                .ToListAsync(ct);
            var list = tcsList
                .Select(tcs => new TeacherContactDto(
                    tcs.TeacherId,
                    (tcs.Teacher.FirstName + " " + tcs.Teacher.LastName + (tcs.Teacher.MiddleName != null ? " " + tcs.Teacher.MiddleName : "")).Trim(),
                    tcs.Subject.Name,
                    tcs.Teacher.Email,
                    tcs.Teacher.Phone,
                    tcs.Teacher.WhatsAppNumber ?? tcs.Teacher.Phone))
                .ToList();
            return Ok(list);
        }

        var teacherIds = await _db.TeacherClasses
            .Where(tc => classIds.Contains(tc.ClassId))
            .Select(tc => tc.TeacherId)
            .Distinct()
            .ToListAsync(ct);
        if (teacherIds.Count == 0)
            return Ok(new List<TeacherContactDto>());
        var teachers = await _db.Teachers
            .AsNoTracking()
            .Where(t => teacherIds.Contains(t.Id) && t.IsActive)
            .Select(t => new TeacherContactDto(
                t.Id,
                t.FirstName + " " + t.LastName + (t.MiddleName != null ? " " + t.MiddleName : ""),
                null,
                t.Email,
                t.Phone,
                t.WhatsAppNumber ?? t.Phone))
            .ToListAsync(ct);
        return Ok(teachers);
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
}

public record TeacherContactDto(Guid TeacherId, string FullName, string? Subject, string? Email, string? Phone, string? WhatsAppNumber);
