using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = Roles.SuperAdmin)]
public class SuperAdminAffiliatesController : ControllerBase
{
    private readonly AffiliateService _affiliateService;

    public SuperAdminAffiliatesController(AffiliateService affiliateService)
    {
        _affiliateService = affiliateService;
    }

    [HttpGet("affiliate-requests")]
    [ProducesResponseType(typeof(IReadOnlyList<AffiliateLeadRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AffiliateLeadRequestDto>>> GetAffiliateRequests(CancellationToken ct)
    {
        return Ok(await _affiliateService.GetLeadRequestsAsync(ct));
    }

    [HttpPost("affiliate-requests/{id:guid}/send-invite")]
    [ProducesResponseType(typeof(SendAffiliateInviteResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SendAffiliateInviteResult>> SendInvite(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _affiliateService.SendInviteAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("affiliates")]
    [ProducesResponseType(typeof(IReadOnlyList<AffiliateSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AffiliateSummaryDto>>> GetAffiliates(CancellationToken ct)
    {
        return Ok(await _affiliateService.GetAffiliateSummariesAsync(ct));
    }

    [HttpGet("affiliates/{id:guid}")]
    [ProducesResponseType(typeof(AffiliateAdminDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AffiliateAdminDetailDto>> GetAffiliate(Guid id, CancellationToken ct)
    {
        var detail = await _affiliateService.GetAffiliateAdminDetailAsync(id, ct);
        if (detail == null)
            return NotFound();
        return Ok(detail);
    }

    [HttpGet("affiliates/{id:guid}/schools")]
    [ProducesResponseType(typeof(IReadOnlyList<AffiliateSchoolSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AffiliateSchoolSummaryDto>>> GetAffiliateSchools(Guid id, CancellationToken ct)
    {
        var detail = await _affiliateService.GetAffiliateAdminDetailAsync(id, ct);
        if (detail == null)
            return NotFound();
        return Ok(detail.Schools);
    }

    [HttpGet("affiliate-payouts")]
    [ProducesResponseType(typeof(IReadOnlyList<AffiliatePayoutDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AffiliatePayoutDto>>> GetAffiliatePayouts(CancellationToken ct)
    {
        return Ok(await _affiliateService.GetPayoutsForSuperAdminAsync(ct));
    }

    [HttpPost("affiliate-payouts/{id:guid}/pay")]
    [ProducesResponseType(typeof(AffiliatePayoutDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AffiliatePayoutDto>> PayAffiliate(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _affiliateService.PayPayoutAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("affiliate-training-videos")]
    [ProducesResponseType(typeof(IReadOnlyList<AffiliateTrainingVideoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AffiliateTrainingVideoDto>>> GetTrainingVideos(CancellationToken ct)
    {
        return Ok(await _affiliateService.ListTrainingVideoDtosAsync(includeUnpublished: true, ct));
    }

    [HttpPost("affiliate-training-videos")]
    [ProducesResponseType(typeof(AffiliateTrainingVideoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AffiliateTrainingVideoDto>> CreateTrainingVideo([FromBody] SaveAffiliateTrainingVideoRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _affiliateService.SaveTrainingVideoAsync(null, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("affiliate-training-videos/{id:guid}")]
    [ProducesResponseType(typeof(AffiliateTrainingVideoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AffiliateTrainingVideoDto>> UpdateTrainingVideo(Guid id, [FromBody] SaveAffiliateTrainingVideoRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _affiliateService.SaveTrainingVideoAsync(id, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("affiliate-training-videos/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteTrainingVideo(Guid id, CancellationToken ct)
    {
        var deleted = await _affiliateService.DeleteTrainingVideoAsync(id, ct);
        if (!deleted)
            return NotFound();
        return NoContent();
    }
}
