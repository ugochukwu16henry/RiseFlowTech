using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchoolsController : ControllerBase
{
    private readonly SchoolOnboardingService _onboarding;
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public SchoolsController(SchoolOnboardingService onboarding, RiseFlowDbContext db, ITenantContext tenant)
    {
        _onboarding = onboarding;
        _db = db;
        _tenant = tenant;
    }

    /// <summary>School dashboard: high-level view of active students and unpaid fees. SchoolAdmin only.</summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolDashboardDto>> GetDashboard(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var activeStudents = await _db.Students.CountAsync(s => s.SchoolId == schoolId && s.IsActive, ct);
        var school = await _db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        var currencyCode = school?.CurrencyCode ?? "NGN";
        var unpaidRecords = await _db.BillingRecords
            .Where(b => b.SchoolId == schoolId && (b.AmountPaid == null || b.AmountPaid < b.AmountDue))
            .ToListAsync(ct);
        var unpaidFeesTotal = unpaidRecords.Sum(b => b.AmountDue - (b.AmountPaid ?? 0));
        return Ok(new SchoolDashboardDto(activeStudents, unpaidFeesTotal, currencyCode));
    }

    /// <summary>List classes for the current school (for dropdowns e.g. Add student). SchoolAdmin/Teacher.</summary>
    [HttpGet("classes")]
    [Authorize]
    [ProducesResponseType(typeof(List<SchoolClassDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SchoolClassDto>>> GetClasses(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var list = await _db.Classes
            .AsNoTracking()
            .Where(c => c.SchoolId == _tenant.CurrentSchoolId.Value)
            .OrderBy(c => c.Name)
            .Select(c => new SchoolClassDto(c.Id, c.Name))
            .ToListAsync(ct);
        return Ok(list);
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
        if (!string.IsNullOrWhiteSpace(request.AdminEmail) && !request.AgreedToTermsAndDpa)
            return BadRequest("You must agree to the RiseFlow Terms of Service and Data Processing Agreement to register.");

        var result = await _onboarding.OnboardSchoolAsync(request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Onboard a new school with optional logo upload (multipart/form-data). Principal signs up, sets school name, and uploads logo.
    /// </summary>
    [HttpPost("onboard-with-logo")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SchoolOnboardingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SchoolOnboardingResult>> OnboardWithLogo([FromForm] OnboardSchoolRequest request, [FromForm] IFormFile? Logo, [FromForm] IFormFile? CacDocument, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SchoolName))
            return BadRequest("School name is required.");
        if (!string.IsNullOrWhiteSpace(request.AdminEmail) && string.IsNullOrWhiteSpace(request.AdminPassword))
            return BadRequest("Admin password is required when admin email is provided.");
        if (!string.IsNullOrWhiteSpace(request.AdminEmail) && !request.AgreedToTermsAndDpa)
            return BadRequest("You must agree to the RiseFlow Terms of Service and Data Processing Agreement to register.");
        var result = await _onboarding.OnboardSchoolWithLogoAsync(request, Logo, CacDocument, ct);
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

    /// <summary>
    /// Mark that the school's signed Data Consent forms have been received (NDPA compliance). SuperAdmin only.
    /// </summary>
    [HttpPatch("{id:guid}/data-consent-received")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkDataConsentReceived(Guid id, CancellationToken ct)
    {
        var school = await _db.Schools.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (school == null)
            return NotFound();
        school.DataConsentFormReceivedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record SchoolDashboardDto(int ActiveStudentCount, decimal UnpaidFeesTotal, string CurrencyCode);
public record SchoolClassDto(Guid Id, string Name);
