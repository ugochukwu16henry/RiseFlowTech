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
[Route("api/promotions")]
[Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
public class PromotionsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public PromotionsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpPost("bulk")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<object>> BulkPromote([FromBody] BulkPromoteStudentsRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (request.FromClassId == Guid.Empty || request.ToClassId == Guid.Empty || request.StudentIds == null || request.StudentIds.Count == 0)
            return BadRequest(new { message = "FromClassId, ToClassId, and StudentIds are required." });

        if (request.FromClassId == request.ToClassId)
            return BadRequest(new { message = "Source class and destination class must differ." });

        var schoolId = _tenant.CurrentSchoolId.Value;

        var result = await ApplyPromotionsAsync(
            schoolId,
            request.FromClassId,
            request.ToClassId,
            request.FromTermId,
            request.PromotionSessionLabel,
            request.StudentIds,
            request.Notes,
            TryGetCurrentUserId(),
            ct);

        if (!string.IsNullOrWhiteSpace(result.Error))
            return BadRequest(new { message = result.Error, studentIds = result.InvalidStudentIds });

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            promotedCount = result.PromotedCount,
            skippedCount = result.SkippedCount,
            message = $"Promotion completed: {result.PromotedCount} promoted, {result.SkippedCount} skipped."
        });
    }

    [HttpPost("requests")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<ActionResult<object>> SubmitRequest([FromBody] SubmitPromotionRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (request.FromClassId == Guid.Empty || request.ToClassId == Guid.Empty || request.StudentIds == null || request.StudentIds.Count == 0)
            return BadRequest(new { message = "FromClassId, ToClassId, and StudentIds are required." });
        if (request.FromClassId == request.ToClassId)
            return BadRequest(new { message = "Source class and destination class must differ." });

        var schoolId = _tenant.CurrentSchoolId.Value;
        var teacher = await GetCurrentTeacherAsync(schoolId, ct);
        if (teacher == null)
            return Forbid();

        var assignedClassIds = await GetAssignedClassIdsAsync(teacher.Id, ct);
        if (!assignedClassIds.Contains(request.FromClassId))
            return BadRequest(new { message = "You can only request promotions for classes assigned to you." });

        var studentIds = request.StudentIds.Distinct().ToList();
        var students = await _db.Students
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId && studentIds.Contains(s.Id))
            .ToListAsync(ct);

        if (students.Count == 0)
            return BadRequest(new { message = "No valid students found for this request." });

        var invalidStudents = students.Where(s => s.ClassId != request.FromClassId).Select(s => s.Id).ToList();
        if (invalidStudents.Count > 0)
            return BadRequest(new { message = "Some selected students are not in the source class.", studentIds = invalidStudents });

        var promotionRequest = new ClassPromotionRequest
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            TeacherId = teacher.Id,
            FromClassId = request.FromClassId,
            ToClassId = request.ToClassId,
            FromTermId = request.FromTermId,
            PromotionSessionLabel = request.PromotionSessionLabel,
            Notes = request.Notes,
            Status = "Pending",
            RequestedAtUtc = DateTime.UtcNow
        };

        promotionRequest.Items = studentIds
            .Select(studentId => new ClassPromotionRequestItem
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                RequestId = promotionRequest.Id,
                StudentId = studentId
            })
            .ToList();

        _db.ClassPromotionRequests.Add(promotionRequest);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Promotion recommendation submitted for School Admin approval.", requestId = promotionRequest.Id });
    }

    [HttpGet("requests/my")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<ActionResult<List<PromotionRequestRowDto>>> MyRequests(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        var teacher = await GetCurrentTeacherAsync(schoolId, ct);
        if (teacher == null)
            return Ok(new List<PromotionRequestRowDto>());

        var list = await BuildPromotionRequestsQuery(schoolId)
            .Where(r => r.TeacherId == teacher.Id)
            .OrderByDescending(r => r.RequestedAtUtc)
            .ToListAsync(ct);

        return Ok(list.Select(ToPromotionRequestRowDto).ToList());
    }

    [HttpGet("requests/pending")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<List<PromotionRequestRowDto>>> PendingRequests(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        var list = await BuildPromotionRequestsQuery(schoolId)
            .Where(r => r.Status == "Pending")
            .OrderBy(r => r.RequestedAtUtc)
            .ToListAsync(ct);

        return Ok(list.Select(ToPromotionRequestRowDto).ToList());
    }

    [HttpPost("requests/{id:guid}/approve")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<object>> ApproveRequest(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        var request = await _db.ClassPromotionRequests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id && r.SchoolId == schoolId, ct);

        if (request == null)
            return NotFound(new { message = "Promotion request not found." });
        if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only pending requests can be approved." });

        var result = await ApplyPromotionsAsync(
            schoolId,
            request.FromClassId,
            request.ToClassId,
            request.FromTermId,
            request.PromotionSessionLabel,
            request.Items.Select(i => i.StudentId).ToList(),
            request.Notes,
            TryGetCurrentUserId(),
            ct);

        if (!string.IsNullOrWhiteSpace(result.Error))
            return BadRequest(new { message = result.Error, studentIds = result.InvalidStudentIds });

        request.Status = "Approved";
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ReviewedByUserId = TryGetCurrentUserId();

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            promotedCount = result.PromotedCount,
            skippedCount = result.SkippedCount,
            message = $"Request approved: {result.PromotedCount} promoted, {result.SkippedCount} skipped."
        });
    }

    [HttpGet("history")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<List<StudentPromotionHistoryDto>>> History([FromQuery] Guid? classId, [FromQuery] Guid? termId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;

        var query = _db.StudentPromotions
            .AsNoTracking()
            .Include(p => p.Student)
            .Include(p => p.FromClass)
            .Include(p => p.ToClass)
            .Where(p => p.SchoolId == schoolId)
            .AsQueryable();

        if (classId.HasValue)
            query = query.Where(p => p.FromClassId == classId.Value || p.ToClassId == classId.Value);

        if (termId.HasValue)
            query = query.Where(p => p.FromTermId == termId.Value);

        var list = await query
            .OrderByDescending(p => p.PromotedAtUtc)
            .Select(p => new StudentPromotionHistoryDto(
                p.Id,
                p.StudentId,
                (p.Student.FirstName + " " + p.Student.LastName).Trim(),
                p.FromClassId,
                p.FromClass.Name,
                p.ToClassId,
                p.ToClass.Name,
                p.FromTermId,
                p.PromotionSessionLabel,
                p.PromotedAtUtc,
                p.Notes))
            .ToListAsync(ct);

        return Ok(list);
    }

    private async Task<(string? Error, List<Guid> InvalidStudentIds, int PromotedCount, int SkippedCount)> ApplyPromotionsAsync(
        Guid schoolId,
        Guid fromClassId,
        Guid toClassId,
        Guid? fromTermId,
        string? promotionSessionLabel,
        IReadOnlyCollection<Guid> studentIds,
        string? notes,
        Guid? promotedByUserId,
        CancellationToken ct)
    {
        var sourceClassExists = await _db.Classes.AnyAsync(c => c.Id == fromClassId && c.SchoolId == schoolId, ct);
        var targetClassExists = await _db.Classes.AnyAsync(c => c.Id == toClassId && c.SchoolId == schoolId, ct);
        if (!sourceClassExists || !targetClassExists)
            return ("Invalid source or destination class for this school.", new List<Guid>(), 0, 0);

        var studentSet = studentIds.Distinct().ToList();
        var students = await _db.Students
            .Where(s => s.SchoolId == schoolId && studentSet.Contains(s.Id))
            .ToListAsync(ct);

        if (students.Count == 0)
            return ("No valid students found for promotion.", new List<Guid>(), 0, 0);

        var inWrongClass = students.Where(s => s.ClassId != fromClassId).Select(s => s.Id).ToList();
        if (inWrongClass.Count > 0)
            return ("Some selected students are not in the source class.", inWrongClass, 0, 0);

        var now = DateTime.UtcNow;
        var promotedCount = 0;
        var skippedCount = 0;

        foreach (var student in students)
        {
            var alreadyPromoted = await _db.StudentPromotions.AnyAsync(
                p => p.SchoolId == schoolId &&
                     p.StudentId == student.Id &&
                     p.FromClassId == fromClassId &&
                     p.FromTermId == fromTermId,
                ct);

            if (alreadyPromoted)
            {
                skippedCount++;
                continue;
            }

            var promotion = new StudentPromotion
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                StudentId = student.Id,
                FromClassId = fromClassId,
                ToClassId = toClassId,
                FromTermId = fromTermId,
                PromotionSessionLabel = promotionSessionLabel,
                PromotedByUserId = promotedByUserId,
                PromotedAtUtc = now,
                Notes = notes
            };

            student.ClassId = toClassId;
            student.UpdatedAtUtc = now;
            _db.StudentPromotions.Add(promotion);
            promotedCount++;
        }

        return (null, new List<Guid>(), promotedCount, skippedCount);
    }

    private async Task<Teacher?> GetCurrentTeacherAsync(Guid schoolId, CancellationToken ct)
    {
        var email = _tenant.CurrentUserEmail ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToLowerInvariant();
        return await _db.Teachers
            .FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.Email != null && t.Email.ToLower() == normalized, ct);
    }

    private async Task<HashSet<Guid>> GetAssignedClassIdsAsync(Guid teacherId, CancellationToken ct)
    {
        var direct = await _db.TeacherClasses
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId)
            .Select(x => x.ClassId)
            .ToListAsync(ct);

        var subjects = await _db.TeacherClassSubjects
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId)
            .Select(x => x.ClassId)
            .ToListAsync(ct);

        var classIds = new HashSet<Guid>(direct);
        foreach (var classId in subjects)
            classIds.Add(classId);
        return classIds;
    }

    private IQueryable<ClassPromotionRequest> BuildPromotionRequestsQuery(Guid schoolId)
    {
        return _db.ClassPromotionRequests
            .AsNoTracking()
            .Where(r => r.SchoolId == schoolId)
            .Include(r => r.Teacher)
            .Include(r => r.FromClass)
            .Include(r => r.ToClass)
            .Include(r => r.Items)
                .ThenInclude(i => i.Student);
    }

    private static PromotionRequestRowDto ToPromotionRequestRowDto(ClassPromotionRequest request)
    {
        var teacherName = string.Join(" ", new[] { request.Teacher.FirstName, request.Teacher.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        return new PromotionRequestRowDto(
            request.Id,
            request.TeacherId,
            string.IsNullOrWhiteSpace(teacherName) ? "Teacher" : teacherName,
            request.FromClassId,
            request.FromClass.Name,
            request.ToClassId,
            request.ToClass.Name,
            request.FromTermId,
            request.PromotionSessionLabel,
            request.Status,
            request.Items.Count,
            request.RequestedAtUtc,
            request.Notes,
            request.Items
                .OrderBy(i => i.Student.LastName)
                .ThenBy(i => i.Student.FirstName)
                .Select(i => new PromotionRequestStudentDto(
                    i.StudentId,
                    string.Join(" ", new[] { i.Student.FirstName, i.Student.MiddleName, i.Student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim(),
                    i.Student.AdmissionNumber))
                .ToList());
    }

    private Guid? TryGetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }
}
