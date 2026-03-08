using Microsoft.AspNetCore.Http;
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

            // 1. Header set by TenantMiddleware (X-Tenant-Id)
            if (context.Items.TryGetValue(TenantMiddleware.TenantIdItemKey, out var item) && item is Guid headerTenantId)
                return headerTenantId;

            // 2. Authenticated user's SchoolId claim
            var schoolIdClaim = context.User?.FindFirst("SchoolId")?.Value;
            if (!string.IsNullOrEmpty(schoolIdClaim) && Guid.TryParse(schoolIdClaim, out var claimTenantId))
                return claimTenantId;

            return null;
        }
    }
}
