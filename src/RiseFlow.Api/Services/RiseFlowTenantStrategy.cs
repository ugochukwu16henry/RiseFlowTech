using System.Security.Claims;
using Finbuckle.MultiTenant.Abstractions;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Middleware;

namespace RiseFlow.Api.Services;

/// <summary>
/// Resolves tenant identifier from header or authenticated user claim.
/// Header switching is restricted to SuperAdmin users.
/// </summary>
public class RiseFlowTenantStrategy : IMultiTenantStrategy
{
    public Task<string?> GetIdentifierAsync(object context)
    {
        if (context is not HttpContext httpContext)
            return Task.FromResult<string?>(null);

        var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated ?? false;
        var isSuperAdmin = httpContext.User?.IsInRole(Roles.SuperAdmin) ?? false;
        var claimTenantId = httpContext.User?.FindFirst("SchoolId")?.Value
            ?? httpContext.User?.FindFirst(ClaimTypes.GroupSid)?.Value;

        if (httpContext.Request.Headers.TryGetValue(TenantMiddleware.TenantIdHeaderName, out var headerValues))
        {
            var headerTenantId = headerValues.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(headerTenantId))
            {
                if (isAuthenticated && !isSuperAdmin)
                    return Task.FromResult(claimTenantId);

                return Task.FromResult<string?>(headerTenantId);
            }
        }

        return Task.FromResult(claimTenantId);
    }
}
