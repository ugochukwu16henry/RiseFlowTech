using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Middleware;

namespace RiseFlow.Api.Services;

/// <summary>
/// Holds the tenant ID during the request lifecycle. Reads from the X-Tenant-Id header (set by TenantMiddleware)
/// first, then falls back to the authenticated user's SchoolId claim.
/// </summary>
public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
                return null;

            var isAuthenticated = context.User?.Identity?.IsAuthenticated ?? false;
            var isSuperAdmin = context.User?.IsInRole(Roles.SuperAdmin) ?? false;

            Guid? claimTenantId = null;
            var schoolIdClaim = context.User?.FindFirst("SchoolId")?.Value
                ?? context.User?.FindFirst(ClaimTypes.GroupSid)?.Value;
            if (!string.IsNullOrEmpty(schoolIdClaim) && Guid.TryParse(schoolIdClaim, out var parsedClaimTenantId))
                claimTenantId = parsedClaimTenantId;

            // 1. Header set by TenantMiddleware (X-Tenant-Id)
            if (context.Items.TryGetValue(TenantMiddleware.TenantIdItemKey, out var item) && item is Guid headerTenantId)
            {
                // Authenticated tenant-scoped users should prefer their own claim.
                // Backward compatibility: if older accounts are missing the claim, fall back to the saved tenant header.
                if (isAuthenticated && !isSuperAdmin)
                    return claimTenantId ?? headerTenantId;

                return headerTenantId;
            }

            // 2. Authenticated user's SchoolId claim
            if (claimTenantId.HasValue)
                return claimTenantId;

            return null;
        }
    }
}
