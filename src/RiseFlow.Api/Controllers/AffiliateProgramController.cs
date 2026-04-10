using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/affiliate-program")]
public class AffiliateProgramController : ControllerBase
{
    private readonly AffiliateService _affiliateService;

    public AffiliateProgramController(AffiliateService affiliateService)
    {
        _affiliateService = affiliateService;
    }

    [HttpGet("info")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AffiliateProgramInfoDto), StatusCodes.Status200OK)]
    public ActionResult<AffiliateProgramInfoDto> GetInfo()
    {
        return Ok(_affiliateService.GetProgramInfo());
    }

    [HttpPost("requests")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AffiliateLeadRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AffiliateLeadRequestDto>> SubmitRequest([FromBody] SubmitAffiliateLeadRequest request, CancellationToken ct)
    {
        try
        {
            var lead = await _affiliateService.CreateLeadRequestAsync(request, ct);
            return Ok(new AffiliateLeadRequestDto(
                lead.Id,
                lead.FullName,
                lead.Email,
                lead.PhoneNumber,
                lead.CountryCode,
                lead.Note,
                lead.Status,
                lead.InviteSentAtUtc,
                lead.CreatedAtUtc));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("invites/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AffiliateInviteValidationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AffiliateInviteValidationDto>> ValidateInvite(string token, CancellationToken ct)
    {
        return Ok(await _affiliateService.ValidateInviteAsync(token, ct));
    }

    [HttpPost("invites/{token}/complete")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> CompleteInvite(string token, [FromBody] CompleteAffiliateInviteRequest request, CancellationToken ct)
    {
        try
        {
            var (_, affiliate) = await _affiliateService.CompleteInviteAsync(token, request, ct);
            return Ok(new
            {
                success = true,
                affiliateId = affiliate.Id,
                uniqueCode = affiliate.UniqueCode,
                message = "Affiliate account created successfully. You can now sign in."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
