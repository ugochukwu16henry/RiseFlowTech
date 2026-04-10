using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/affiliates")]
[Authorize(Roles = Roles.Affiliate)]
public class AffiliatesController : ControllerBase
{
    private readonly AffiliateService _affiliateService;

    public AffiliatesController(AffiliateService affiliateService)
    {
        _affiliateService = affiliateService;
    }

    [HttpGet("me/dashboard")]
    [ProducesResponseType(typeof(AffiliateDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AffiliateDashboardDto>> GetDashboard(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        var dashboard = await _affiliateService.GetAffiliateDashboardAsync(userId.Value, ct);
        if (dashboard == null)
            return NotFound();
        return Ok(dashboard);
    }

    [HttpGet("me/referral-link")]
    [ProducesResponseType(typeof(AffiliateReferralLinkDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AffiliateReferralLinkDto>> GetReferralLink(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        var dashboard = await _affiliateService.GetAffiliateDashboardAsync(userId.Value, ct);
        if (dashboard == null)
            return NotFound();

        return Ok(new AffiliateReferralLinkDto(Guid.Empty, dashboard.UniqueCode, dashboard.ReferralUrl));
    }

    [HttpGet("me/referred-schools")]
    [ProducesResponseType(typeof(IReadOnlyList<AffiliateSchoolSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AffiliateSchoolSummaryDto>>> GetReferredSchools(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        var dashboard = await _affiliateService.GetAffiliateDashboardAsync(userId.Value, ct);
        if (dashboard == null)
            return NotFound();

        return Ok(dashboard.ReferredSchools);
    }

    [HttpPut("me/payout-settings")]
    [ProducesResponseType(typeof(AffiliatePayoutSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AffiliatePayoutSettingsDto>> UpdatePayoutSettings([FromBody] UpdateAffiliatePayoutSettingsRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        var settings = await _affiliateService.UpdatePayoutSettingsAsync(userId.Value, request, ct);
        if (settings == null)
            return NotFound();
        return Ok(settings);
    }

    [HttpPost("me/headshot")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> UploadHeadshot([FromForm] IFormFile file, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        try
        {
            var path = await _affiliateService.SaveHeadshotAsync(userId.Value, file, ct);
            return Ok(new { headshotPath = path });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("me/training-videos")]
    [ProducesResponseType(typeof(IReadOnlyList<AffiliateTrainingVideoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AffiliateTrainingVideoDto>>> GetTrainingVideos(CancellationToken ct)
    {
        return Ok(await _affiliateService.ListTrainingVideoDtosAsync(includeUnpublished: false, ct));
    }

    [HttpGet("me/payout-history")]
    [ProducesResponseType(typeof(IReadOnlyList<AffiliatePayoutDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AffiliatePayoutDto>>> GetPayoutHistory(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Forbid();

        var dashboard = await _affiliateService.GetAffiliateDashboardAsync(userId.Value, ct);
        if (dashboard == null)
            return NotFound();

        return Ok(dashboard.PayoutHistory);
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }
}
