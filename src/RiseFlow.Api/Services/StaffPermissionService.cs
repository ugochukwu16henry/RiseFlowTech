using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;

namespace RiseFlow.Api.Services;

public static class StaffPermissionKeys
{
    public const string CanManageTeachers = "manage-teachers";
    public const string CanAssignClasses = "assign-classes";
    public const string CanApproveResults = "approve-results";
    public const string CanSendParentBroadcasts = "send-parent-broadcasts";
    public const string CanManageFees = "manage-fees";
    public const string CanManageAttendance = "manage-attendance";
    public const string CanManageAssessments = "manage-assessments";
}

public class StaffPermissionService
{
    private const string StaffStructureConfigCategory = "school-staff-structure-config";
    private const string StaffStructureConfigRelativePath = "school-config/staff-structure-config.json";

    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogService _audit;

    public StaffPermissionService(RiseFlowDbContext db, ITenantContext tenant, IAuditLogService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<bool> EnsureTeacherPermissionAsync(
        ClaimsPrincipal user,
        string permissionKey,
        string entityType,
        string action,
        string? entityId,
        CancellationToken ct)
    {
        var allowed = await HasTeacherPermissionAsync(user, permissionKey, ct);
        if (allowed)
            return true;

        if (user.IsInRole(Roles.Teacher))
        {
            await _audit.LogAsync(
                _tenant.CurrentSchoolId,
                "Denied",
                entityType,
                entityId,
                _tenant.CurrentUserEmail,
                user.Identity?.Name,
                $"Teacher denied '{action}' because '{permissionKey}' is not granted by staff structure permissions.",
                ct);
        }

        return false;
    }

    public async Task<bool> HasTeacherPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken ct)
    {
        if (user.IsInRole(Roles.SchoolAdmin))
            return true;

        if (!user.IsInRole(Roles.Teacher))
            return false;

        if (!_tenant.CurrentSchoolId.HasValue)
            return false;

        var schoolId = _tenant.CurrentSchoolId.Value;
        var email = _tenant.CurrentUserEmail
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        if (string.IsNullOrWhiteSpace(email))
            return false;

        var teacher = await _db.Teachers
            .AsNoTracking()
            .Where(t => t.SchoolId == schoolId && t.Email == email)
            .Select(t => new { t.RoleTitle })
            .FirstOrDefaultAsync(ct);

        var roleTitle = string.IsNullOrWhiteSpace(teacher?.RoleTitle) ? "Teacher" : teacher!.RoleTitle!.Trim();

        var matrix = await LoadPermissionMatrixAsync(schoolId, ct);
        var roleRule = matrix.FirstOrDefault(x => string.Equals(x.RoleTitle, roleTitle, StringComparison.OrdinalIgnoreCase));
        if (roleRule != null)
            return roleRule.HasPermission(permissionKey);

        return EvaluateFallback(roleTitle, permissionKey);
    }

    private async Task<List<PermissionMatrixEntry>> LoadPermissionMatrixAsync(Guid schoolId, CancellationToken ct)
    {
        var payload = await _db.FileAssets
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId
                && x.Category == StaffStructureConfigCategory
                && x.RelativePath == StaffStructureConfigRelativePath
                && x.FileBytes != null)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => x.FileBytes)
            .FirstOrDefaultAsync(ct);

        if (payload == null || payload.Length == 0)
            return new List<PermissionMatrixEntry>();

        try
        {
            var json = Encoding.UTF8.GetString(payload);
            var parsed = JsonSerializer.Deserialize<StaffConfigPayload>(json);
            return parsed?.PermissionMatrix?.Where(x => !string.IsNullOrWhiteSpace(x.RoleTitle)).ToList()
                ?? new List<PermissionMatrixEntry>();
        }
        catch
        {
            return new List<PermissionMatrixEntry>();
        }
    }

    private static bool EvaluateFallback(string roleTitle, string permissionKey)
    {
        var lower = roleTitle.ToLowerInvariant();
        var isLeadership = lower.Contains("head")
            || lower.Contains("deputy")
            || lower.Contains("director")
            || lower.Contains("directeur")
            || lower.Contains("principal");
        var isClassLead = lower.Contains("class teacher")
            || lower.Contains("form tutor")
            || lower.Contains("professeur principal");

        return permissionKey switch
        {
            StaffPermissionKeys.CanManageTeachers => isLeadership,
            StaffPermissionKeys.CanAssignClasses => isLeadership || isClassLead,
            StaffPermissionKeys.CanApproveResults => true, // Keep teacher result entry functional when no matrix exists yet.
            StaffPermissionKeys.CanSendParentBroadcasts => isLeadership || isClassLead,
            StaffPermissionKeys.CanManageFees => isLeadership,
            StaffPermissionKeys.CanManageAttendance => true, // Keep attendance marking available by default for teacher workflows.
            StaffPermissionKeys.CanManageAssessments => true, // Keep assessments/exams/assignments functional when matrix is not configured.
            _ => false,
        };
    }

    private sealed record StaffConfigPayload(List<PermissionMatrixEntry>? PermissionMatrix);

    private sealed record PermissionMatrixEntry(
        string RoleTitle,
        bool CanManageTeachers,
        bool CanAssignClasses,
        bool CanApproveResults,
        bool CanSendParentBroadcasts,
        bool CanManageFees)
    {
        public bool HasPermission(string permissionKey) => permissionKey switch
        {
            StaffPermissionKeys.CanManageTeachers => CanManageTeachers,
            StaffPermissionKeys.CanAssignClasses => CanAssignClasses,
            StaffPermissionKeys.CanApproveResults => CanApproveResults,
            StaffPermissionKeys.CanSendParentBroadcasts => CanSendParentBroadcasts,
            StaffPermissionKeys.CanManageFees => CanManageFees,
            StaffPermissionKeys.CanManageAttendance => CanAssignClasses,
            StaffPermissionKeys.CanManageAssessments => CanApproveResults,
            _ => false,
        };
    }
}
