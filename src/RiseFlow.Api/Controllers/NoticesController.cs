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
[Route("api/notices")]
[Authorize]
public class NoticesController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IConfiguration _configuration;
    private readonly StaffPermissionService _staffPermissions;

    public NoticesController(RiseFlowDbContext db, ITenantContext tenant, IConfiguration configuration, StaffPermissionService staffPermissions)
    {
        _db = db;
        _tenant = tenant;
        _configuration = configuration;
        _staffPermissions = staffPermissions;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<SchoolNotice>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SchoolNotice>>> List([FromQuery] int? limit, CancellationToken ct)
    {
        if (!IsFeatureEnabled())
            return NotFound();

        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var now = DateTime.UtcNow;
        var role = GetCurrentAppRole();

        var query = _db.SchoolNotices
            .AsNoTracking()
            .Where(n => n.SchoolId == schoolId && n.IsActive)
            .Where(n => !n.ExpiresAtUtc.HasValue || n.ExpiresAtUtc > now)
            .OrderByDescending(n => n.PublishedAtUtc)
            .AsQueryable();

        if (!User.IsInRole(Roles.SchoolAdmin))
            query = query.Where(n =>
                string.IsNullOrEmpty(n.TargetRolesCsv)
                || EF.Functions.Like(n.TargetRolesCsv, "%All%")
                || EF.Functions.Like(n.TargetRolesCsv, $"%{role}%"));

        if (limit.HasValue && limit.Value > 0)
            query = query.Take(Math.Min(limit.Value, 50));

        return Ok(await query.ToListAsync(ct));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    public async Task<ActionResult<SchoolNotice>> Create([FromBody] CreateSchoolNoticeRequest request, CancellationToken ct)
    {
        if (!IsFeatureEnabled())
            return NotFound();

        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanSendParentBroadcasts, "SchoolNotice", "Create", null, ct))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Title and body are required." });

        var entity = new SchoolNotice
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            TargetRolesCsv = NormalizeRoles(request.TargetRolesCsv),
            ExpiresAtUtc = request.ExpiresAtUtc,
            IsActive = request.IsActive,
            PublishedByUserId = TryGetCurrentUserId(),
            PublishedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.SchoolNotices.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    public async Task<ActionResult<SchoolNotice>> Update(Guid id, [FromBody] UpdateSchoolNoticeRequest request, CancellationToken ct)
    {
        if (!IsFeatureEnabled())
            return NotFound();

        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanSendParentBroadcasts, "SchoolNotice", "Update", id.ToString(), ct))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Title and body are required." });

        var entity = await _db.SchoolNotices.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (entity == null)
            return NotFound();

        entity.Title = request.Title.Trim();
        entity.Body = request.Body.Trim();
        entity.TargetRolesCsv = NormalizeRoles(request.TargetRolesCsv);
        entity.ExpiresAtUtc = request.ExpiresAtUtc;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!IsFeatureEnabled())
            return NotFound();

        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanSendParentBroadcasts, "SchoolNotice", "Delete", id.ToString(), ct))
            return Forbid();

        var entity = await _db.SchoolNotices.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (entity == null)
            return NotFound();

        _db.SchoolNotices.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string NormalizeRoles(string roles)
    {
        if (string.IsNullOrWhiteSpace(roles)) return "All";
        var split = roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return split.Length == 0 ? "All" : string.Join(',', split.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private string GetCurrentAppRole()
    {
        if (User.IsInRole(Roles.SchoolAdmin)) return Roles.SchoolAdmin;
        if (User.IsInRole(Roles.Teacher)) return Roles.Teacher;
        if (User.IsInRole(Roles.Parent)) return Roles.Parent;
        if (User.IsInRole(Roles.Student)) return Roles.Student;
        return string.Empty;
    }

    private Guid? TryGetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private bool IsFeatureEnabled() => _configuration.GetValue<bool>("Features:EnableNoticesEventsV1");
}
