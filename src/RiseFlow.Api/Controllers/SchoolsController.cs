using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchoolsController : ControllerBase
{
    private readonly SchoolOnboardingService _onboarding;
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly SchoolDashboardService _dashboard;

    public SchoolsController(SchoolOnboardingService onboarding, RiseFlowDbContext db, ITenantContext tenant, SchoolDashboardService dashboard)
    {
        _onboarding = onboarding;
        _db = db;
        _tenant = tenant;
        _dashboard = dashboard;
    }

    /// <summary>
    /// School dashboard: nerve center view for SchoolAdmin.
    /// Aggregates students, teachers, pending results, billing, and recent activity.
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolDashboardViewModel), StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolDashboardViewModel>> GetDashboard(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var vm = await _dashboard.GetDashboardStatsAsync(schoolId, ct);
        return Ok(vm);
    }

    /// <summary>List classes for the current school (for dropdowns e.g. Add student). SchoolAdmin/Teacher.</summary>
    [HttpGet("classes")]
    [Authorize]
    [ProducesResponseType(typeof(List<SchoolClassDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SchoolClassDto>>> GetClasses(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var list = await _db.Classes
            .AsNoTracking()
            .Where(c => c.SchoolId == schoolId)
            .OrderBy(c => c.Grade.LevelOrder)
            .ThenBy(c => c.Name)
            .Select(c => new SchoolClassDto(c.Id, c.Name, c.GradeId, c.Grade.Name, c.AcademicYear))
            .ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>List grade levels for the current school (Nursery, Primary 1, JSS1, SS1, etc.). SchoolAdmin.</summary>
    [HttpGet("grades")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(List<SchoolGradeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SchoolGradeDto>>> GetGrades(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var list = await _db.Grades
            .AsNoTracking()
            .Where(g => g.SchoolId == schoolId)
            .OrderBy(g => g.LevelOrder)
            .ThenBy(g => g.Name)
            .Select(g => new SchoolGradeDto(g.Id, g.Name, g.LevelOrder))
            .ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>Create a grade level (programme / stage). SchoolAdmin. Example names: Nursery, Primary 1, JSS 1, SS 2.</summary>
    [HttpPost("grades")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolGradeDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SchoolGradeDto>> CreateGrade([FromBody] CreateSchoolGradeRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");
        var schoolId = _tenant.CurrentSchoolId.Value;
        var name = request.Name.Trim();
        if (name.Length > 64)
            return BadRequest("Name must be 64 characters or fewer.");

        var exists = await _db.Grades.AnyAsync(g => g.SchoolId == schoolId && g.Name == name, ct);
        if (exists)
            return Conflict($"A grade named '{name}' already exists.");

        var levelOrder = request.LevelOrder;
        if (levelOrder <= 0)
        {
            var max = await _db.Grades.Where(g => g.SchoolId == schoolId).MaxAsync(g => (int?)g.LevelOrder, ct) ?? 0;
            levelOrder = max + 1;
        }

        var grade = new Grade
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            Name = name,
            LevelOrder = levelOrder,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Grades.Add(grade);
        await _db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, new SchoolGradeDto(grade.Id, grade.Name, grade.LevelOrder));
    }

    /// <summary>Create a class under a grade (e.g. JSS 1A, SS2 Science). SchoolAdmin.</summary>
    [HttpPost("classes")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolClassDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SchoolClassDto>> CreateClass([FromBody] CreateSchoolClassRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.GradeId == Guid.Empty)
            return BadRequest("Name and GradeId are required.");
        var schoolId = _tenant.CurrentSchoolId.Value;
        var className = request.Name.Trim();
        if (className.Length > 64)
            return BadRequest("Class name must be 64 characters or fewer.");

        var grade = await _db.Grades.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.GradeId && g.SchoolId == schoolId, ct);
        if (grade == null)
            return BadRequest("Grade not found for this school.");

        var academicYear = string.IsNullOrWhiteSpace(request.AcademicYear) ? null : request.AcademicYear!.Trim();
        if (academicYear != null && academicYear.Length > 16)
            return BadRequest("Academic year must be 16 characters or fewer.");

        var cls = new Class
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            GradeId = request.GradeId,
            Name = className,
            AcademicYear = academicYear,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Classes.Add(cls);
        await _db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, new SchoolClassDto(cls.Id, cls.Name, cls.GradeId, grade.Name, cls.AcademicYear));
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
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        if (!isSuperAdmin && _tenant.CurrentSchoolId.HasValue && _tenant.CurrentSchoolId.Value != id)
            return Forbid();

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

public record SchoolClassDto(Guid Id, string Name, Guid GradeId, string GradeName, string? AcademicYear);

public record SchoolGradeDto(Guid Id, string Name, int LevelOrder);

public record CreateSchoolGradeRequest(string Name, int LevelOrder = 0);

public record CreateSchoolClassRequest(string Name, Guid GradeId, string? AcademicYear);
