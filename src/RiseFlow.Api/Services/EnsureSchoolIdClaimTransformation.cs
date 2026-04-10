using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    private readonly RiseFlowDbContext _db;

    public EnsureSchoolIdClaimTransformation(UserManager<ApplicationUser> userManager, RiseFlowDbContext db)
    {
        _userManager = userManager;
        _db = db;
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

        var desiredSchoolId = user.SchoolId?.ToString();
        var desiredStudentId = await _db.StudentPortalAccesses
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.IsEnabled)
            .Select(x => (Guid?)x.StudentId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        var currentSchoolId = principal.FindFirst("SchoolId")?.Value;
        var currentStudentId = principal.FindFirst("StudentId")?.Value;
        var desiredStudentIdValue = desiredStudentId?.ToString();

        if (desiredSchoolId == currentSchoolId && desiredStudentIdValue == currentStudentId)
            return principal;

        var clone = principal.Clone();
        if (clone.Identity is not ClaimsIdentity identity)
            return principal;

        foreach (var c in identity.FindAll("SchoolId").ToList())
            identity.RemoveClaim(c);
        foreach (var c in identity.FindAll("StudentId").ToList())
            identity.RemoveClaim(c);

        if (desiredSchoolId != null)
            identity.AddClaim(new Claim("SchoolId", desiredSchoolId));
        if (desiredStudentIdValue != null)
            identity.AddClaim(new Claim("StudentId", desiredStudentIdValue));

        return clone;
    }
}
