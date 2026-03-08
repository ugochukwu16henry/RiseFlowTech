using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace RiseFlow.Api.Services;

public class TenantContext : ITenantContext
{
    private readonly ITenantService _tenantService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(ITenantService tenantService, IHttpContextAccessor httpContextAccessor)
    {
        _tenantService = tenantService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Current school (tenant) ID from X-Tenant-Id header or user's SchoolId claim.</summary>
    public Guid? CurrentSchoolId => _tenantService.TenantId;

    public bool IsSuperAdmin =>
        _httpContextAccessor.HttpContext?.User?.IsInRole(Constants.Roles.SuperAdmin) ?? false;

    public string? CurrentUserEmail =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
        ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name;
}
