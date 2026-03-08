using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace RiseFlow.Api.Services;

public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? CurrentSchoolId
    {
        get
        {
            var schoolIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("SchoolId");
            if (string.IsNullOrEmpty(schoolIdClaim) || !Guid.TryParse(schoolIdClaim, out var id))
                return null;
            return id;
        }
    }

    public bool IsSuperAdmin =>
        _httpContextAccessor.HttpContext?.User?.IsInRole(Constants.Roles.SuperAdmin) ?? false;

    public string? CurrentUserEmail =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
        ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name;
}
