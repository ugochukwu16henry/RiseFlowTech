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
[Authorize(Roles = Roles.SchoolAdmin)]
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
    public async Task<ActionResult<object>> BulkPromote([FromBody] BulkPromoteStudentsRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (request.FromClassId == Guid.Empty || request.ToClassId == Guid.Empty || request.StudentIds == null || request.StudentIds.Count == 0)
            return BadRequest(new { message = "FromClassId, ToClassId, and StudentIds are required." });

        if (request.FromClassId == request.ToClassId)
            return BadRequest(new { message = "Source class and destination class must differ." });

        var schoolId = _tenant.CurrentSchoolId.Value;

        var sourceClassExists = await _db.Classes.AnyAsync(c => c.Id == request.FromClassId && c.SchoolId == schoolId, ct);
        var targetClassExists = await _db.Classes.AnyAsync(c => c.Id == request.ToClassId && c.SchoolId == schoolId, ct);
        if (!sourceClassExists || !targetClassExists)
            return BadRequest(new { message = "Invalid source or destination class for this school." });

        var studentSet = request.StudentIds.Distinct().ToList();
        var students = await _db.Students
            .Where(s => s.SchoolId == schoolId && studentSet.Contains(s.Id))
            .ToListAsync(ct);

        if (students.Count == 0)
            return BadRequest(new { message = "No valid students found for promotion." });

        var inWrongClass = students.Where(s => s.ClassId != request.FromClassId).Select(s => s.Id).ToList();
        if (inWrongClass.Count > 0)
            return BadRequest(new { message = "Some selected students are not in the source class.", studentIds = inWrongClass });

        var promotedBy = TryGetCurrentUserId();
        var now = DateTime.UtcNow;
        var promotedCount = 0;
        var skippedCount = 0;

        foreach (var student in students)
        {
            var alreadyPromoted = await _db.StudentPromotions.AnyAsync(
                p => p.SchoolId == schoolId &&
                     p.StudentId == student.Id &&
                     p.FromClassId == request.FromClassId &&
                     p.FromTermId == request.FromTermId,
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
                FromClassId = request.FromClassId,
                ToClassId = request.ToClassId,
                FromTermId = request.FromTermId,
                PromotionSessionLabel = request.PromotionSessionLabel,
                PromotedByUserId = promotedBy,
                PromotedAtUtc = now,
                Notes = request.Notes
            };

            student.ClassId = request.ToClassId;
            student.UpdatedAtUtc = now;
            _db.StudentPromotions.Add(promotion);
            promotedCount++;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            promotedCount,
            skippedCount,
            message = $"Promotion completed: {promotedCount} promoted, {skippedCount} skipped."
        });
    }

    [HttpGet("history")]
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

    private Guid? TryGetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }
}
