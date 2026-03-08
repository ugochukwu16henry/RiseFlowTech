using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchoolsController : ControllerBase
{
    private readonly SchoolOnboardingService _onboarding;

    public SchoolsController(SchoolOnboardingService onboarding)
    {
        _onboarding = onboarding;
    }

    /// <summary>
    /// Onboard a new school (tenant). SuperAdmin only, or allow anonymous for self-service signup depending on policy.
    /// </summary>
    [HttpPost("onboard")]
    [AllowAnonymous] // Restrict to SuperAdmin in production when you have auth
    [ProducesResponseType(typeof(SchoolOnboardingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SchoolOnboardingResult>> Onboard([FromBody] OnboardSchoolRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SchoolName))
            return BadRequest("School name is required.");

        if (!string.IsNullOrWhiteSpace(request.AdminEmail) && string.IsNullOrWhiteSpace(request.AdminPassword))
            return BadRequest("Admin password is required when admin email is provided.");

        var result = await _onboarding.OnboardSchoolAsync(request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// List all schools. SuperAdmin only.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(List<Entities.School>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Entities.School>>> List(CancellationToken ct)
    {
        var list = await _onboarding.ListSchoolsAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// Get a school by ID. SuperAdmin or SchoolAdmin for their own school.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(Entities.School), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Entities.School>> GetById(Guid id, CancellationToken ct)
    {
        var school = await _onboarding.GetSchoolByIdAsync(id, ct);
        if (school == null)
            return NotFound();
        return Ok(school);
    }
}
