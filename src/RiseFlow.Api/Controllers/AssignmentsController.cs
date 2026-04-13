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
[Route("api/assignments")]
[Authorize]
public class AssignmentsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public AssignmentsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AssignmentListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AssignmentListItemDto>>> List(
        [FromQuery] Guid? classId,
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? termId,
        [FromQuery] Guid? studentId,
        CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var query = _db.TeacherAssignments
            .AsNoTracking()
            .Include(a => a.Class)
            .Include(a => a.Subject)
            .Include(a => a.Term)
            .Include(a => a.Teacher)
            .Include(a => a.FileAsset)
            .Where(a => a.SchoolId == schoolId)
            .AsQueryable();

        if (User.IsInRole(Roles.Parent) || User.IsInRole(Roles.Student))
        {
            var allowedStudentIds = User.IsInRole(Roles.Parent)
                ? await GetParentLinkedStudentIdsAsync(ct)
                : await GetStudentLinkedStudentIdsAsync(ct);

            if (allowedStudentIds.Count == 0)
                return Ok(new List<AssignmentListItemDto>());

            if (studentId.HasValue && !allowedStudentIds.Contains(studentId.Value))
                return Forbid();

            var targetStudentIds = studentId.HasValue ? new List<Guid> { studentId.Value } : allowedStudentIds;
            var classIds = await _db.Students
                .AsNoTracking()
                .Where(s => s.SchoolId == schoolId && targetStudentIds.Contains(s.Id) && s.ClassId.HasValue)
                .Select(s => s.ClassId!.Value)
                .Distinct()
                .ToListAsync(ct);

            if (classIds.Count == 0)
                return Ok(new List<AssignmentListItemDto>());

            query = query.Where(a => classIds.Contains(a.ClassId));
        }
        else
        {
            if (classId.HasValue) query = query.Where(a => a.ClassId == classId.Value);
        }

        if (subjectId.HasValue) query = query.Where(a => a.SubjectId == subjectId.Value);
        if (termId.HasValue) query = query.Where(a => a.TermId == termId.Value);

        var data = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AssignmentListItemDto(
                a.Id,
                a.ClassId,
                a.Class.Name,
                a.SubjectId,
                a.Subject.Name,
                a.TermId,
                (a.Term.Name + " " + a.Term.AcademicYear).Trim(),
                a.TeacherId,
                (a.Teacher.FirstName + " " + a.Teacher.LastName).Trim(),
                a.Title,
                a.Description,
                a.FileAssetId,
                a.FileAsset.OriginalFileName,
                a.DueDateUtc,
                a.CreatedAtUtc))
            .ToListAsync(ct);

        return Ok(data);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    public async Task<ActionResult<TeacherAssignment>> Create([FromBody] CreateAssignmentRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        if (request.ClassId == Guid.Empty || request.SubjectId == Guid.Empty || request.TermId == Guid.Empty || request.FileAssetId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Class, subject, term, title, and file are required." });

        var classExists = await _db.Classes.AnyAsync(c => c.Id == request.ClassId && c.SchoolId == schoolId, ct);
        var subjectExists = await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId && s.SchoolId == schoolId, ct);
        var termExists = await _db.AcademicTerms.AnyAsync(t => t.Id == request.TermId && t.SchoolId == schoolId, ct);
        var fileExists = await _db.FileAssets.AnyAsync(f => f.Id == request.FileAssetId && f.SchoolId == schoolId, ct);
        if (!classExists || !subjectExists || !termExists || !fileExists)
            return BadRequest(new { message = "Invalid class, subject, term, or file for this school." });

        var teacherId = await ResolveCurrentTeacherIdAsync(ct);
        if (!teacherId.HasValue)
            return BadRequest(new { message = "Teacher profile not found for current user." });

        var assignment = new TeacherAssignment
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            TeacherId = teacherId.Value,
            ClassId = request.ClassId,
            SubjectId = request.SubjectId,
            TermId = request.TermId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            FileAssetId = request.FileAssetId,
            DueDateUtc = request.DueDateUtc,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.TeacherAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);
        return Ok(assignment);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var assignment = await _db.TeacherAssignments.FirstOrDefaultAsync(a => a.Id == id && a.SchoolId == schoolId, ct);
        if (assignment == null)
            return NotFound();

        if (User.IsInRole(Roles.Teacher))
        {
            var teacherId = await ResolveCurrentTeacherIdAsync(ct);
            if (!teacherId.HasValue || teacherId.Value != assignment.TeacherId)
                return Forbid();
        }

        _db.TeacherAssignments.Remove(assignment);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Guid?> ResolveCurrentTeacherIdAsync(CancellationToken ct)
    {
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrWhiteSpace(email) || !_tenant.CurrentSchoolId.HasValue)
            return null;

        var teacher = await _db.Teachers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SchoolId == _tenant.CurrentSchoolId && t.Email == email, ct);

        return teacher?.Id;
    }

    private async Task<List<Guid>> GetParentLinkedStudentIdsAsync(CancellationToken ct)
    {
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrWhiteSpace(email) || !_tenant.CurrentSchoolId.HasValue)
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
}
