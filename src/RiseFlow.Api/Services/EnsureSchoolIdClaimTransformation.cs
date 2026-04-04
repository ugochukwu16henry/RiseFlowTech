using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using RiseFlow.Api.Data;

namespace RiseFlow.Api.Services;

/// <summary>
/// Ensures the cookie principal carries a <c>SchoolId</c> claim whenever <see cref="ApplicationUser.SchoolId"/> is set,
/// and removes a stale claim when it is not. <see cref="TenantService"/> and EF tenant filters rely on this claim;
/// without it, school-scoped endpoints return HTTP 403 even though the user row is correct.
/// </summary>
public sealed class EnsureSchoolIdClaimTransformation : IClaimsTransformation
{
    private readonly UserManager<ApplicationUser> _userManager;

    public EnsureSchoolIdClaimTransformation(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return principal;

        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user == null)
            return principal;

        var desired = user.SchoolId?.ToString();
        var current = principal.FindFirst("SchoolId")?.Value;
        if (desired == current)
            return principal;

        var clone = principal.Clone();
        if (clone.Identity is not ClaimsIdentity identity)
            return principal;

        foreach (var c in identity.FindAll("SchoolId").ToList())
            identity.RemoveClaim(c);

        if (desired != null)
            identity.AddClaim(new Claim("SchoolId", desired));

        return clone;
    }
}
