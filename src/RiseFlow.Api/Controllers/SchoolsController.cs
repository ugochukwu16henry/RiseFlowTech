using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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
    private const long MaxSchoolLogoBytes = 5 * 1024 * 1024; // 5 MB
    private const long MaxRegistrationDocumentBytes = 10 * 1024 * 1024; // 10 MB
    private readonly SchoolOnboardingService _onboarding;
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly SchoolDashboardService _dashboard;
    private readonly FileStorageService _fileStorage;
    private readonly ILogger<SchoolsController> _logger;

    public SchoolsController(SchoolOnboardingService onboarding, RiseFlowDbContext db, ITenantContext tenant, SchoolDashboardService dashboard, FileStorageService fileStorage, ILogger<SchoolsController> logger)
    {
        _onboarding = onboarding;
        _db = db;
        _tenant = tenant;
        _dashboard = dashboard;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    private static string BuildSchoolLogoPath(Guid schoolId) => $"api/schools/{schoolId}/logo";

    private static string BuildRegistrationDocumentPath(Guid schoolId) => $"api/schools/{schoolId}/registration-document";

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

    /// <summary>Branding payload for current tenant (school name + logo + registration document URLs).</summary>
    [HttpGet("branding")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher},{Roles.Parent},{Roles.Student}")]
    [ProducesResponseType(typeof(SchoolBrandingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolBrandingDto>> GetBranding(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools
            .AsNoTracking()
            .Include(s => s.AcademicSystemProfile)
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        return Ok(new SchoolBrandingDto(
            school.Id,
            school.Name,
            string.IsNullOrWhiteSpace(school.LogoFileName) ? null : BuildSchoolLogoPath(school.Id),
            string.IsNullOrWhiteSpace(school.RegistrationDocumentPath) ? null : BuildRegistrationDocumentPath(school.Id)));
    }

    /// <summary>Get school profile for the current SchoolAdmin tenant.</summary>
    [HttpGet("profile")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolProfileDto>> GetProfile(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        return Ok(new SchoolProfileDto(
            school.Id,
            school.Name,
            school.OwnerName,
            school.SchoolAdminName,
            school.PrincipalName,
            school.Address,
            school.CountryCode,
            school.Email,
            school.Phone,
            school.WhatsAppNumber,
            school.CacNumber,
            string.IsNullOrWhiteSpace(school.LogoFileName) ? null : BuildSchoolLogoPath(school.Id),
            string.IsNullOrWhiteSpace(school.RegistrationDocumentPath) ? null : BuildRegistrationDocumentPath(school.Id),
            school.AcademicSystemProfileId,
            school.AcademicSystemProfile?.Code,
            school.AcademicSystemProfile?.Name,
            school.UpdatedAtUtc));
    }

    /// <summary>List active academic system profiles for school setup.</summary>
    [HttpGet("academic-system-profiles")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(List<AcademicSystemProfileOptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AcademicSystemProfileOptionDto>>> GetAcademicSystemProfiles(CancellationToken ct)
    {
        var profiles = await _db.AcademicSystemProfiles
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new AcademicSystemProfileOptionDto(
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.SuggestedTermsPerYear,
                p.DefaultGradingScaleCode))
            .ToListAsync(ct);

        return Ok(profiles);
    }

    /// <summary>Update school profile information for current SchoolAdmin tenant.</summary>
    [HttpPut("profile")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolProfileDto>> UpdateProfile([FromBody] UpdateSchoolProfileRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (request == null)
            return BadRequest("Profile payload is required.");

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools
            .Include(s => s.AcademicSystemProfile)
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("School name is required.");
        if (name.Length > 256)
            return BadRequest("School name must be 256 characters or fewer.");

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var normalizedCountry = request.CountryCode.Trim().ToUpperInvariant();
            if (normalizedCountry.Length != 2)
                return BadRequest("Country code must be a 2-letter ISO code (e.g. NG).");
            school.CountryCode = normalizedCountry;
        }
        else
        {
            school.CountryCode = null;
        }

        school.Name = name;
        school.OwnerName = TrimOrNull(request.OwnerName, 128);
        school.SchoolAdminName = TrimOrNull(request.SchoolAdminName, 128);
        school.PrincipalName = TrimOrNull(request.PrincipalName, 128);
        school.Address = TrimOrNull(request.Address, 512);
        school.Email = TrimOrNull(request.Email, 256);
        school.Phone = TrimOrNull(request.Phone, 128);
        school.WhatsAppNumber = TrimOrNull(request.WhatsAppNumber, 128);
        school.CacNumber = TrimOrNull(request.CacNumber, 64);
        school.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new SchoolProfileDto(
            school.Id,
            school.Name,
            school.OwnerName,
            school.SchoolAdminName,
            school.PrincipalName,
            school.Address,
            school.CountryCode,
            school.Email,
            school.Phone,
            school.WhatsAppNumber,
            school.CacNumber,
            string.IsNullOrWhiteSpace(school.LogoFileName) ? null : BuildSchoolLogoPath(school.Id),
            string.IsNullOrWhiteSpace(school.RegistrationDocumentPath) ? null : BuildRegistrationDocumentPath(school.Id),
            school.AcademicSystemProfileId,
            school.AcademicSystemProfile?.Code,
            school.AcademicSystemProfile?.Name,
            school.UpdatedAtUtc));
    }

    /// <summary>Set academic system profile for current school.</summary>
    [HttpPut("profile/academic-system")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolProfileDto>> UpdateAcademicSystemProfile([FromBody] UpdateAcademicSystemProfileRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (request == null || request.AcademicSystemProfileId == Guid.Empty)
            return BadRequest("AcademicSystemProfileId is required.");

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools.FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        var profile = await _db.AcademicSystemProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.AcademicSystemProfileId && p.IsActive, ct);
        if (profile == null)
            return BadRequest("Selected academic system profile was not found.");

        school.AcademicSystemProfileId = profile.Id;
        school.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new SchoolProfileDto(
            school.Id,
            school.Name,
            school.OwnerName,
            school.SchoolAdminName,
            school.PrincipalName,
            school.Address,
            school.CountryCode,
            school.Email,
            school.Phone,
            school.WhatsAppNumber,
            school.CacNumber,
            string.IsNullOrWhiteSpace(school.LogoFileName) ? null : BuildSchoolLogoPath(school.Id),
            string.IsNullOrWhiteSpace(school.RegistrationDocumentPath) ? null : BuildRegistrationDocumentPath(school.Id),
            profile.Id,
            profile.Code,
            profile.Name,
            school.UpdatedAtUtc));
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

        try
        {
            var list = await _db.Classes
                .AsNoTracking()
                .Where(c => c.SchoolId == schoolId)
                .OrderBy(c => c.Grade.LevelOrder)
                .ThenBy(c => c.Name)
                .Select(c => new SchoolClassDto(c.Id, c.Name, c.GradeId, c.Grade.Name, c.AcademicYear))
                .ToListAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Class list could not be loaded for school {SchoolId}. Returning minimal fallback rows.", schoolId);

            var fallback = await _db.Classes
                .AsNoTracking()
                .Where(c => c.SchoolId == schoolId)
                .OrderBy(c => c.Name)
                .Select(c => new SchoolClassDto(c.Id, c.Name, c.GradeId, string.Empty, null))
                .ToListAsync(ct);

            return Ok(fallback);
        }
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

    /// <summary>Get recommended quick grade templates based on the school's academic profile/country.</summary>
    [HttpGet("grade-templates")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolGradeTemplateCatalogDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolGradeTemplateCatalogDto>> GetGradeTemplates(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools
            .AsNoTracking()
            .Include(s => s.AcademicSystemProfile)
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);

        if (school == null)
            return NotFound();

        var profile = school.AcademicSystemProfile;
        if (profile == null)
        {
            var inferredCode = InferProfileCodeFromCountry(school.CountryCode);
            if (!string.IsNullOrWhiteSpace(inferredCode))
            {
                profile = await _db.AcademicSystemProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Code == inferredCode && x.IsActive, ct);
            }
        }

        if (profile != null)
        {
            var parsed = ParseGradeTemplates(profile.GradeTemplatesJson);
            if (parsed.Count > 0)
            {
                return Ok(new SchoolGradeTemplateCatalogDto(
                    profile.Code,
                    profile.Name,
                    profile.Description,
                    parsed));
            }
        }

        var fallbackCode = InferProfileCodeFromCountry(school.CountryCode) ?? "NG_6334";
        var fallback = GetDefaultGradeTemplates(fallbackCode);
        return Ok(new SchoolGradeTemplateCatalogDto(
            fallbackCode,
            fallbackCode switch
            {
                "GH_633" => "Ghana 6-3-3",
                "KE_844" => "Kenya 8-4-4",
                _ => "Nigeria 6-3-3-4"
            },
            "System-recommended quick grade templates.",
            fallback));
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

    /// <summary>Upload or replace current school logo. SchoolAdmin only.</summary>
    [HttpPost("logo")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UploadLogo([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools.FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        if (file.Length > MaxSchoolLogoBytes)
            return BadRequest("School logo is too large. Maximum allowed size is 5 MB.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".png";

        var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Allowed formats: .jpg, .jpeg, .png, .gif, .webp");

        var fileName = $"{schoolId:N}{ext}";
        var relativePath = $"logos/{fileName}";

        byte[] fileBytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
            ms.Position = 0;
            try
            {
                await _fileStorage.UploadAsync(relativePath, ms, file.ContentType, ct);
            }
            catch (Exception ex)
            {
                // Keep school setup unblocked during transient storage outages.
                _logger.LogWarning(ex, "Storage upload failed for school logo {SchoolId}; falling back to DB blob.", schoolId);
            }
        }

        _db.FileAssets.Add(new FileAsset
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            OriginalFileName = file.FileName,
            StoredFileName = fileName,
            RelativePath = relativePath,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            FileBytes = fileBytes,
            Category = "school-logo",
            UploadedBy = _tenant.CurrentUserEmail,
            UploadedAtUtc = DateTime.UtcNow
        });

        school.LogoFileName = relativePath;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Logo uploaded.",
            logoFileName = relativePath, // backward compatibility
            logoPath = BuildSchoolLogoPath(schoolId)
        });
    }

    /// <summary>Get a school's logo file. SuperAdmin can access any; SchoolAdmin can access own school.</summary>
    [HttpGet("{id:guid}/logo")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.SchoolAdmin},{Roles.Teacher},{Roles.Parent},{Roles.Student}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(Guid id, CancellationToken ct)
    {
        var school = await _db.Schools.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (school == null || string.IsNullOrWhiteSpace(school.LogoFileName))
            return NotFound();

        if (!User.IsInRole(Roles.SuperAdmin) && (!_tenant.CurrentSchoolId.HasValue || _tenant.CurrentSchoolId.Value != id))
            return Forbid();

        var path = school.LogoFileName!;
        var bytes = await _fileStorage.TryReadBytesAsync(path, ct);
        if (bytes is { Length: > 0 })
            return File(bytes, DetectImageContentType(path));

        var blob = await _db.FileAssets
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.SchoolId == id
                && a.Category == "school-logo"
                && a.RelativePath == path
                && a.FileBytes != null)
            .OrderByDescending(a => a.UploadedAtUtc)
            .Select(a => new { a.FileBytes, a.ContentType })
            .FirstOrDefaultAsync(ct);
        if (blob?.FileBytes is { Length: > 0 })
            return File(blob.FileBytes, string.IsNullOrWhiteSpace(blob.ContentType) ? DetectImageContentType(path) : blob.ContentType);

        return NotFound();
    }

    /// <summary>Upload or replace school registration/CAC document. SchoolAdmin only.</summary>
    [HttpPost("registration-document")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UploadRegistrationDocument([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools.FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        if (file.Length > MaxRegistrationDocumentBytes)
            return BadRequest("Registration document is too large. Maximum allowed size is 10 MB.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".pdf";

        var allowed = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp" };
        if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Allowed formats: .pdf, .png, .jpg, .jpeg, .webp");

        var fileName = $"{schoolId:N}{ext}";
        var relativePath = $"cac/{fileName}";

        byte[] fileBytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            fileBytes = ms.ToArray();
            ms.Position = 0;
            try
            {
                await _fileStorage.UploadAsync(relativePath, ms, file.ContentType, ct);
            }
            catch (Exception ex)
            {
                // Keep school setup unblocked during transient storage outages.
                _logger.LogWarning(ex, "Storage upload failed for school registration document {SchoolId}; falling back to DB blob.", schoolId);
            }
        }

        _db.FileAssets.Add(new FileAsset
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            OriginalFileName = file.FileName,
            StoredFileName = fileName,
            RelativePath = relativePath,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            FileBytes = fileBytes,
            Category = "school-registration-document",
            UploadedBy = _tenant.CurrentUserEmail,
            UploadedAtUtc = DateTime.UtcNow
        });

        school.RegistrationDocumentPath = relativePath;
        school.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Registration document uploaded.",
            registrationDocumentPath = BuildRegistrationDocumentPath(schoolId)
        });
    }

    /// <summary>Get a school's registration/CAC document. SuperAdmin can access any; SchoolAdmin can access own school.</summary>
    [HttpGet("{id:guid}/registration-document")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.SchoolAdmin},{Roles.Teacher},{Roles.Parent},{Roles.Student}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRegistrationDocument(Guid id, CancellationToken ct)
    {
        var school = await _db.Schools.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (school == null || string.IsNullOrWhiteSpace(school.RegistrationDocumentPath))
            return NotFound();

        if (!User.IsInRole(Roles.SuperAdmin) && (!_tenant.CurrentSchoolId.HasValue || _tenant.CurrentSchoolId.Value != id))
            return Forbid();

        var path = school.RegistrationDocumentPath!;
        var bytes = await _fileStorage.TryReadBytesAsync(path, ct);
        if (bytes is { Length: > 0 })
            return File(bytes, DetectDocumentContentType(path));

        var blob = await _db.FileAssets
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.SchoolId == id
                && a.Category == "school-registration-document"
                && a.RelativePath == path
                && a.FileBytes != null)
            .OrderByDescending(a => a.UploadedAtUtc)
            .Select(a => new { a.FileBytes, a.ContentType })
            .FirstOrDefaultAsync(ct);
        if (blob?.FileBytes is { Length: > 0 })
            return File(blob.FileBytes, string.IsNullOrWhiteSpace(blob.ContentType) ? DetectDocumentContentType(path) : blob.ContentType);

        return NotFound();
    }

    private static string DetectImageContentType(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    private static string DetectDocumentContentType(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
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
        if (!User.IsInRole(Roles.SuperAdmin) && (!_tenant.CurrentSchoolId.HasValue || _tenant.CurrentSchoolId.Value != id))
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

    private static string? TrimOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? InferProfileCodeFromCountry(string? countryCode)
    {
        return (countryCode ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "GH" => "GH_633",
            "KE" => "KE_844",
            "NG" => "NG_6334",
            _ => null,
        };
    }

    private static List<SchoolGradeTemplateItemDto> ParseGradeTemplates(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<SchoolGradeTemplateItemDto>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<SchoolGradeTemplateItemDto>>(json);
            return parsed?
                .Where(x => !string.IsNullOrWhiteSpace(x.Name) && x.LevelOrder > 0)
                .DistinctBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.LevelOrder)
                .ToList()
                ?? new List<SchoolGradeTemplateItemDto>();
        }
        catch
        {
            return new List<SchoolGradeTemplateItemDto>();
        }
    }

    private static List<SchoolGradeTemplateItemDto> GetDefaultGradeTemplates(string code)
    {
        return code switch
        {
            "GH_633" =>
            [
                new("Kindergarten 1", "Kindergarten 1", 5),
                new("Kindergarten 2", "Kindergarten 2", 6),
                new("Primary 1", "Primary 1", 10),
                new("Primary 6", "Primary 6", 15),
                new("JHS 1", "JHS 1", 30),
                new("JHS 3", "JHS 3", 32),
                new("SHS 1", "SHS 1", 40),
                new("SHS 3", "SHS 3", 42),
            ],
            "KE_844" =>
            [
                new("Grade 1", "Grade 1", 10),
                new("Grade 8", "Grade 8", 18),
                new("Form 1", "Form 1", 30),
                new("Form 4", "Form 4", 34),
            ],
            _ =>
            [
                new("Nursery", "Nursery", 5),
                new("Primary 1", "Primary 1", 10),
                new("Primary 6", "Primary 6", 15),
                new("JSS 1", "JSS 1", 30),
                new("JSS 3", "JSS 3", 32),
                new("SS1", "SS1", 40),
                new("SS3", "SS3", 42),
            ],
        };
    }
}

public record SchoolClassDto(Guid Id, string Name, Guid GradeId, string GradeName, string? AcademicYear);

public record SchoolGradeDto(Guid Id, string Name, int LevelOrder);

public record CreateSchoolGradeRequest(string Name, int LevelOrder = 0);

public record CreateSchoolClassRequest(string Name, Guid GradeId, string? AcademicYear);

public record UpdateSchoolProfileRequest(
    string Name,
    string? OwnerName,
    string? SchoolAdminName,
    string? PrincipalName,
    string? Address,
    string? CountryCode,
    string? Email,
    string? Phone,
    string? WhatsAppNumber,
    string? CacNumber);

public record SchoolProfileDto(
    Guid Id,
    string Name,
    string? OwnerName,
    string? SchoolAdminName,
    string? PrincipalName,
    string? Address,
    string? CountryCode,
    string? Email,
    string? Phone,
    string? WhatsAppNumber,
    string? CacNumber,
    string? LogoPath,
    string? RegistrationDocumentPath,
    Guid? AcademicSystemProfileId,
    string? AcademicSystemProfileCode,
    string? AcademicSystemProfileName,
    DateTime? UpdatedAtUtc);

public record SchoolBrandingDto(
    Guid Id,
    string Name,
    string? LogoPath,
    string? RegistrationDocumentPath);

public record AcademicSystemProfileOptionDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int? SuggestedTermsPerYear,
    string? DefaultGradingScaleCode);

public record UpdateAcademicSystemProfileRequest(Guid AcademicSystemProfileId);

public record SchoolGradeTemplateItemDto(
    string Label,
    string Name,
    int LevelOrder);

public record SchoolGradeTemplateCatalogDto(
    string ProfileCode,
    string ProfileName,
    string? Description,
    IReadOnlyList<SchoolGradeTemplateItemDto> Templates);
