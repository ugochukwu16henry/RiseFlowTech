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
}

public class StaffPermissionService
{
    private const string StaffStructureConfigCategory = "school-staff-structure-config";
    private const string StaffStructureConfigRelativePath = "school-config/staff-structure-config.json";

    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public StaffPermissionService(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
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
            _ => false,
        };
    }
}
