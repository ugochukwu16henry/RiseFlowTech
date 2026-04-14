using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/affiliate-media")]
[Authorize(Roles = Roles.Affiliate + "," + Roles.SuperAdmin)]
public class AffiliateMediaController : ControllerBase
{
    private readonly AffiliateService _affiliateService;

    public AffiliateMediaController(AffiliateService affiliateService)
    {
        _affiliateService = affiliateService;
    }

    [HttpGet("headshot/{affiliateId:guid}")]
    public async Task<IActionResult> GetHeadshot(Guid affiliateId, CancellationToken ct)
    {
        // Affiliates can only load their own headshot, while SuperAdmin can load any affiliate headshot.
        if (User.IsInRole(Roles.Affiliate) && !User.IsInRole(Roles.SuperAdmin))
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
                return Forbid();

            var currentAffiliateId = await _affiliateService.GetAffiliateIdForUserAsync(currentUserId.Value, ct);
            if (!currentAffiliateId.HasValue || currentAffiliateId.Value != affiliateId)
                return Forbid();
        }

        var headshot = await _affiliateService.GetHeadshotContentAsync(affiliateId, ct);
        if (!headshot.HasValue)
            return NotFound();

        return File(headshot.Value.Bytes, headshot.Value.ContentType);
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }
}
