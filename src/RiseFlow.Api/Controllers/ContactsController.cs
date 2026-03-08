using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
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
    /// Returns teacher name, email, phone, WhatsApp for authorized parents.
    /// </summary>
    [HttpGet("teachers")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(List<TeacherContactDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TeacherContactDto>>> GetTeachersForMyChildren(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var studentIds = await GetParentLinkedStudentIdsAsync(ct);
        if (studentIds.Count == 0)
            return Ok(new List<TeacherContactDto>());
        var classIds = await _db.Students
            .Where(s => studentIds.Contains(s.Id) && s.ClassId != null)
            .Select(s => s.ClassId!.Value)
            .Distinct()
            .ToListAsync(ct);
        if (classIds.Count == 0)
            return Ok(new List<TeacherContactDto>());
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

public record TeacherContactDto(Guid TeacherId, string FullName, string? Email, string? Phone, string? WhatsAppNumber);
