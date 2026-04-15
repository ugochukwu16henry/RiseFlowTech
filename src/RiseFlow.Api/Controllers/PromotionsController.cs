using System.Security.Claims;
using System.Text.Json;
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
    private readonly IConfiguration _configuration;

    public PromotionsController(RiseFlowDbContext db, ITenantContext tenant, IConfiguration configuration)
    {
        _db = db;
        _tenant = tenant;
        _configuration = configuration;
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
        var sourceClass = await _db.Classes
            .AsNoTracking()
            .Include(c => c.Grade)
            .FirstOrDefaultAsync(c => c.Id == fromClassId && c.SchoolId == schoolId, ct);
        var targetClass = await _db.Classes
            .AsNoTracking()
            .Include(c => c.Grade)
            .FirstOrDefaultAsync(c => c.Id == toClassId && c.SchoolId == schoolId, ct);

        if (sourceClass == null || targetClass == null)
            return ("Invalid source or destination class for this school.", new List<Guid>(), 0, 0);

        var strictValidationEnabled = _configuration.GetValue<bool>("Features:EnableStrictAcademicPromotionValidation", false);
        if (strictValidationEnabled)
        {
            var validationError = await ValidatePromotionPathAsync(schoolId, sourceClass, targetClass, ct);
            if (!string.IsNullOrWhiteSpace(validationError))
                return (validationError, new List<Guid>(), 0, 0);
        }

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

    private async Task<string?> ValidatePromotionPathAsync(Guid schoolId, Class sourceClass, Class targetClass, CancellationToken ct)
    {
        if (sourceClass.Grade == null || targetClass.Grade == null)
            return "Promotion validation failed because one of the classes has no grade linked.";

        var sourceLevel = sourceClass.Grade.LevelOrder;
        var targetLevel = targetClass.Grade.LevelOrder;

        if (targetLevel < sourceLevel)
            return "Invalid promotion path: destination grade is lower than source grade.";

        var school = await _db.Schools
            .AsNoTracking()
            .Include(s => s.AcademicSystemProfile)
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);

        var academicProfile = school?.AcademicSystemProfile;
        var profileCode = academicProfile?.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(profileCode))
            return null;

        var transitionError = ValidateMatrixPromotion(
            school?.PromotionTransitionOverrideJson ?? academicProfile?.PromotionTransitionJson,
            sourceClass.Grade.Name,
            targetClass.Grade.Name);
        if (!string.IsNullOrWhiteSpace(transitionError))
            return transitionError;

        var sourceStage = ResolveStageKey(profileCode, sourceClass.Grade.Name);
        var targetStage = ResolveStageKey(profileCode, targetClass.Grade.Name);

        if (sourceStage == null || targetStage == null)
            return null;

        var stageOrder = GetStageOrder(profileCode);
        var sourceIndex = stageOrder.IndexOf(sourceStage);
        var targetIndex = stageOrder.IndexOf(targetStage);
        if (sourceIndex < 0 || targetIndex < 0)
            return null;

        if (targetIndex < sourceIndex)
            return "Invalid promotion path: destination stage is lower than source stage for your academic profile.";

        if (targetIndex > sourceIndex + 1)
            return "Invalid promotion path: destination stage skips one or more stages for your academic profile.";

        return null;
    }

    private static string? ValidateMatrixPromotion(string? transitionJson, string? sourceGradeName, string? targetGradeName)
    {
        if (string.IsNullOrWhiteSpace(transitionJson)
            || string.IsNullOrWhiteSpace(sourceGradeName)
            || string.IsNullOrWhiteSpace(targetGradeName))
            return null;

        try
        {
            var transitions = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(transitionJson);
            if (transitions == null || transitions.Count == 0)
                return null;

            var source = sourceGradeName.Trim();
            var target = targetGradeName.Trim();
            var sourceEntry = transitions.FirstOrDefault(x => string.Equals(x.Key, source, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(sourceEntry.Key))
                return null;

            var allowedTargets = sourceEntry.Value ?? new List<string>();
            if (allowedTargets.Any(x => string.Equals(x, target, StringComparison.OrdinalIgnoreCase)))
                return null;

            return $"Invalid promotion path: {source} can only promote to [{string.Join(", ", allowedTargets)}] for this academic profile.";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolveStageKey(string profileCode, string? gradeName)
    {
        var value = (gradeName ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (profileCode == "NG_6334")
        {
            if (value.Contains("nursery")) return "nursery";
            if (value.Contains("primary")) return "primary";
            if (value.Contains("jss") || value.Contains("junior secondary")) return "junior";
            if (value.Contains("ss") || value.Contains("senior secondary")) return "senior";
            return null;
        }

        if (profileCode == "GH_633")
        {
            if (value.Contains("kindergarten") || value.Contains("kg")) return "kindergarten";
            if (value.Contains("primary")) return "primary";
            if (value.Contains("jhs") || value.Contains("junior high")) return "junior";
            if (value.Contains("shs") || value.Contains("senior high")) return "senior";
            return null;
        }

        if (profileCode == "KE_844")
        {
            if (value.Contains("grade")) return "primary";
            if (value.Contains("form") || value.Contains("secondary")) return "secondary";
            return null;
        }

        return null;
    }

    private static List<string> GetStageOrder(string profileCode)
    {
        return profileCode switch
        {
            "GH_633" => new List<string> { "kindergarten", "primary", "junior", "senior" },
            "KE_844" => new List<string> { "primary", "secondary" },
            _ => new List<string> { "nursery", "primary", "junior", "senior" },
        };
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
