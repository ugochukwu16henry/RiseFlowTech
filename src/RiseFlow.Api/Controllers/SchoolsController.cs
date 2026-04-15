using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
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
    private static readonly IReadOnlyList<OnboardingSchoolModelOption> OnboardingSchoolModels = new List<OnboardingSchoolModelOption>
    {
        new(
            "PUBLIC",
            "Government (Public) School",
            "Strict adherence to national curriculum and exam policy.",
            "Often constrained by larger class sizes and tighter infrastructure budgets.",
            "Official/national language with mother-tongue support in early years where applicable.",
            "Low or subsidized tuition."),
        new(
            "PRIVATE",
            "Private School",
            "National curriculum plus optional international blend (British/IGCSE or American).",
            "Typically stronger facilities and extracurricular offerings.",
            "Usually English or French from early nursery years.",
            "Higher tuition supporting facilities and staffing.")
    };

    private static readonly IReadOnlyList<OnboardingCountryOption> OnboardingCountryOptions = new List<OnboardingCountryOption>
    {
        new(
            "NG",
            "Nigeria",
            "NGN",
            "NG_6334",
            "Anglophone",
            "6-3-3-4",
            new List<PrePrimaryStageOption>
            {
                new("Creche / Daycare", "3 months - 2 years", "Basic childcare and social play."),
                new("Pre-Nursery / Playgroup", "2 - 3 years", "Intro to social interaction and basic motor skills."),
                new("Nursery 1 / KG 1", "3 - 4 years", "Pre-literacy (alphabet) and pre-numeracy (numbers 1-10)."),
                new("Nursery 2 / Reception", "4 - 5 years", "Preparation for Primary 1; basic reading and writing.")
            },
            new List<string>
            {
                "Creche / Daycare", "Pre-Nursery / Playgroup", "Nursery 1", "Nursery 2", "Primary 1", "Primary 2", "Primary 3", "Primary 4", "Primary 5", "Primary 6",
                "JSS 1", "JSS 2", "JSS 3", "SS 1", "SS 2", "SS 3"
            },
            new List<string>
            {
                "English Language", "Mathematics", "Basic Science", "Social Studies", "Civic Education", "Computer Studies",
                "Agricultural Science", "Business Studies", "Literature in English", "Economics"
            },
            new List<string>
            {
                "English Language", "Mathematics", "Basic Science", "Social Studies", "Physical and Health Education", "Yoruba/Igbo/Hausa (Local Language)"
            },
            new List<string>
            {
                "English Language", "Mathematics", "Integrated Science", "ICT", "Business Studies", "Home Economics", "Agricultural Science"
            },
            new List<string>
            {
                "English Language", "Mathematics", "Physics", "Chemistry", "Biology", "Government", "Literature", "Economics", "Accounting", "Food and Nutrition"
            },
            "Senior secondary adds tracks in Sciences, Arts, and Commercial studies."),
        new(
            "GH",
            "Ghana",
            "GHS",
            "GH_633",
            "Anglophone",
            "6-3-3",
            new List<PrePrimaryStageOption>
            {
                new("Creche / Daycare", "3 months - 2 years", "Basic childcare and social play."),
                new("Playgroup", "2 - 3 years", "Social interaction and language development."),
                new("KG 1", "3 - 4 years", "Early literacy and counting foundations."),
                new("KG 2", "4 - 5 years", "Preparation for Primary 1 and routine learning habits.")
            },
            new List<string>
            {
                "Creche / Daycare", "Playgroup", "KG 1", "KG 2", "Primary 1", "Primary 2", "Primary 3", "Primary 4", "Primary 5", "Primary 6",
                "JHS 1", "JHS 2", "JHS 3", "SHS 1", "SHS 2", "SHS 3"
            },
            new List<string>
            {
                "English Language", "Mathematics", "Integrated Science", "Social Studies", "Creative Arts", "Religious and Moral Education",
                "Computing", "Career Technology", "Economics", "Literature"
            },
            new List<string>
            {
                "English Language", "Mathematics", "Integrated Science", "Social Studies", "Creative Arts", "Ghanaian Language"
            },
            new List<string>
            {
                "English Language", "Mathematics", "Integrated Science", "Computing", "Career Technology", "Social Studies"
            },
            new List<string>
            {
                "English Language", "Core Mathematics", "Integrated Science", "Elective Mathematics", "Economics", "Literature"
            },
            "Schools follow national curriculum with flexibility in elective bundles at SHS."),
        new(
            "KE",
            "Kenya",
            "KES",
            "KE_844",
            "Anglophone",
            "CBC (2-6-3-3 transition from 8-4-4)",
            new List<PrePrimaryStageOption>
            {
                new("Daycare", "3 months - 2 years", "Care, play, and social bonding."),
                new("Playgroup", "2 - 3 years", "Early communication and motor development."),
                new("PP1", "3 - 4 years", "Pre-literacy and foundational numeracy."),
                new("PP2", "4 - 5 years", "School readiness under competency-based learning.")
            },
            new List<string>
            {
                "Daycare", "Playgroup", "PP1", "PP2", "Grade 1", "Grade 2", "Grade 3", "Grade 4", "Grade 5", "Grade 6",
                "Junior Secondary 1", "Junior Secondary 2", "Junior Secondary 3", "Senior Secondary 1", "Senior Secondary 2", "Senior Secondary 3"
            },
            new List<string>
            {
                "English", "Kiswahili", "Mathematics", "Integrated Science", "Social Studies", "Agriculture",
                "Creative Arts", "Computer Science", "Business Studies", "Life Skills"
            },
            new List<string>
            {
                "English", "Kiswahili", "Mathematics", "Integrated Science", "Social Studies", "Creative Arts", "Physical Education"
            },
            new List<string>
            {
                "English", "Kiswahili", "Mathematics", "Integrated Science", "Agriculture", "Business Studies", "Computer Science"
            },
            new List<string>
            {
                "English", "Kiswahili", "Mathematics", "Physics", "Chemistry", "Biology", "Business Studies", "Technical Drawing"
            },
            "Kenya has moved to CBC with stronger skill-based continuous assessment."),
        new(
            "SN",
            "Senegal",
            "XOF",
            "FR_643",
            "Francophone",
            "6-4-3",
            new List<PrePrimaryStageOption>
            {
                new("Creche", "3 months - 2 years", "Care, motor play, and social adaptation."),
                new("Pre-maternelle", "2 - 3 years", "Language and social readiness."),
                new("Petite / Moyenne Section", "3 - 4 years", "French phonics and number sense."),
                new("Grande Section", "4 - 5 years", "Preparation for Cours Preparatoire.")
            },
            new List<string>
            {
                "Creche", "Pre-maternelle", "Petite Section", "Moyenne Section", "Grande Section",
                "CP1", "CP2", "CE1", "CE2", "CM1", "CM2",
                "College 1", "College 2", "College 3", "College 4",
                "Lycee 1", "Lycee 2", "Lycee 3"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Geographie", "Education civique", "Sciences", "Technologie"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Sciences", "Geographie", "Education civique", "Arts"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Sciences", "Informatique", "Economie familiale"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Physique", "Chimie", "SVT", "Philosophie", "Economie"
            },
            "Francophone pathway typically culminates in Baccalaureat entry requirements."),
        new(
            "CI",
            "Cote d'Ivoire",
            "XOF",
            "FR_643",
            "Francophone",
            "6-4-3",
            new List<PrePrimaryStageOption>
            {
                new("Creche", "3 months - 2 years", "Basic childcare and social play."),
                new("Pre-maternelle", "2 - 3 years", "Early communication and social skills."),
                new("Maternelle 1", "3 - 4 years", "French language readiness and numeracy."),
                new("Maternelle 2", "4 - 5 years", "Preparation for primary school entry.")
            },
            new List<string>
            {
                "Creche", "Pre-maternelle", "Maternelle 1", "Maternelle 2",
                "CP1", "CP2", "CE1", "CE2", "CM1", "CM2",
                "College 1", "College 2", "College 3", "College 4",
                "Lycee 1", "Lycee 2", "Lycee 3"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Geographie", "Education civique", "Sciences"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Sciences", "Technologie", "Education civique"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Sciences", "Informatique", "Arts"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Physique", "Chimie", "SVT", "Histoire-Geographie", "Philosophie"
            },
            "Supports Francophone national examinations aligned to regional standards."),
        new(
            "MA",
            "Morocco",
            "MAD",
            "FR_643",
            "Francophone",
            "6-4-3",
            new List<PrePrimaryStageOption>
            {
                new("Creche", "3 months - 2 years", "Early childcare and social development."),
                new("Pre-maternelle", "2 - 3 years", "Communication and motor skills."),
                new("Maternelle 1", "3 - 4 years", "French pre-literacy and number readiness."),
                new("Maternelle 2", "4 - 5 years", "Preparation for CP1.")
            },
            new List<string>
            {
                "Creche", "Pre-maternelle", "Maternelle 1", "Maternelle 2",
                "CP1", "CP2", "CE1", "CE2", "CM1", "CM2",
                "College 1", "College 2", "College 3", "College 4",
                "Lycee 1", "Lycee 2", "Lycee 3"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Geographie", "Education civique", "Sciences"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Sciences", "Technologie", "Informatique"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Sciences", "Informatique", "Education civique"
            },
            new List<string>
            {
                "Francais", "Mathematiques", "Physique", "Chimie", "SVT", "Philosophie", "Economie"
            },
            "Francophone track remains common in private and urban school systems.")
    };
    private const long MaxSchoolLogoBytes = 5 * 1024 * 1024; // 5 MB
    private const long MaxRegistrationDocumentBytes = 10 * 1024 * 1024; // 10 MB
    private const string StaffStructureConfigCategory = "school-staff-structure-config";
    private const string StaffStructureConfigRelativePath = "school-config/staff-structure-config.json";
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

    /// <summary>
    /// Staff dashboard metrics (tasks, pending approvals, and office queue health).
    /// </summary>
    [HttpGet("staff/dashboard-metrics")]
    [Authorize(Roles = $"{Roles.Staff},{Roles.Teacher},{Roles.SchoolAdmin}")]
    [ProducesResponseType(typeof(StaffDashboardMetricsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffDashboardMetricsDto>> GetStaffDashboardMetrics(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var nowUtc = DateTime.UtcNow;
        var currentUserEmail = (_tenant.CurrentUserEmail ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty).Trim();

        Guid? teacherProfileId = null;
        if (!string.IsNullOrWhiteSpace(currentUserEmail))
        {
            teacherProfileId = await _db.Teachers
                .AsNoTracking()
                .Where(t => t.SchoolId == schoolId && t.Email != null && t.Email.ToUpper() == currentUserEmail.ToUpper())
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(ct);
        }

        var personalAssignmentsCount = teacherProfileId.HasValue
            ? await _db.TeacherAssignments
                .AsNoTracking()
                .CountAsync(a => a.SchoolId == schoolId
                    && a.TeacherId == teacherProfileId.Value
                    && (!a.DueDateUtc.HasValue || a.DueDateUtc.Value >= nowUtc.AddDays(-1)), ct)
            : 0;

        var pendingPromotionRequestsCount = await _db.ClassPromotionRequests
            .AsNoTracking()
            .CountAsync(r => r.SchoolId == schoolId && r.Status == "Pending", ct);

        var pendingFeeVerificationsCount = await _db.FeePaymentRecords
            .AsNoTracking()
            .CountAsync(r => r.SchoolId == schoolId
                && (r.Status == FeePaymentStatus.ReceiptUploaded || r.Status == FeePaymentStatus.InPersonPending), ct);

        var pendingResultEntriesCount = await _db.StudentResults
            .AsNoTracking()
            .CountAsync(r => r.SchoolId == schoolId && (r.GradeLetter == null || r.GradeLetter == ""), ct);

        var recentDeniedAttemptsCount = await _db.AuditLogs
            .AsNoTracking()
            .CountAsync(a => a.SchoolId == schoolId
                && a.Action == "Denied"
                && a.CreatedAtUtc >= nowUtc.AddDays(-7), ct);

        var pendingApprovalsCount = pendingPromotionRequestsCount + pendingFeeVerificationsCount + pendingResultEntriesCount;
        var tasksCount = personalAssignmentsCount + pendingPromotionRequestsCount + pendingFeeVerificationsCount;
        var officeQueueCount = pendingFeeVerificationsCount + pendingPromotionRequestsCount + recentDeniedAttemptsCount;

        return Ok(new StaffDashboardMetricsDto(
            TasksCount: tasksCount,
            PendingApprovalsCount: pendingApprovalsCount,
            OfficeQueueCount: officeQueueCount,
            PersonalAssignmentsCount: personalAssignmentsCount,
            PendingPromotionRequestsCount: pendingPromotionRequestsCount,
            PendingFeeVerificationsCount: pendingFeeVerificationsCount,
            PendingResultEntriesCount: pendingResultEntriesCount,
            RecentDeniedAttemptsCount: recentDeniedAttemptsCount,
            HasTeacherProfile: teacherProfileId.HasValue));
    }

    /// <summary>Audit view for denied teacher actions in the current school.</summary>
    [HttpGet("audit/denied-attempts")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(DeniedAttemptsPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeniedAttemptsPageDto>> GetDeniedAttempts(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? entityType,
        [FromQuery] string? userEmail,
        [FromQuery] int limit = 200,
        [FromQuery] long? beforeId = null,
        CancellationToken ct = default)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var cap = Math.Clamp(limit, 1, 1000);

        IQueryable<AuditLog> query = _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId && a.Action == "Denied");

        if (fromUtc.HasValue)
            query = query.Where(a => a.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(a => a.CreatedAtUtc <= toUtc.Value);
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType.Trim());
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            var normalizedEmail = userEmail.Trim();
            query = query.Where(a => a.UserEmail != null && a.UserEmail == normalizedEmail);
        }
        if (beforeId.HasValue)
            query = query.Where(a => a.Id < beforeId.Value);

        var pageWithProbe = await query
            .OrderByDescending(a => a.Id)
            .Take(cap + 1)
            .Select(a => new AuditLogDto(a.Id, a.SchoolId, a.Action, a.EntityType, a.EntityId, a.UserEmail, a.UserName, a.Details, a.CreatedAtUtc))
            .ToListAsync(ct);

        var hasMore = pageWithProbe.Count > cap;
        var items = hasMore ? pageWithProbe.Take(cap).ToList() : pageWithProbe;
        long? nextCursor = hasMore && items.Count > 0 ? items[^1].Id : null;

        return Ok(new DeniedAttemptsPageDto(items, nextCursor, hasMore));
    }

    /// <summary>Branding payload for current tenant (school name + logo + registration document URLs).</summary>
    [HttpGet("branding")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher},{Roles.Staff},{Roles.Parent},{Roles.Student}")]
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
        var school = await _db.Schools
            .AsNoTracking()
            .Include(s => s.AcademicSystemProfile)
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        return Ok(ToSchoolProfileDto(school));
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

        if (request.TermsPerYear.HasValue)
        {
            if (request.TermsPerYear.Value < 1 || request.TermsPerYear.Value > 6)
                return BadRequest("TermsPerYear must be between 1 and 6.");
            school.TermsPerYear = request.TermsPerYear.Value;
        }
        else
        {
            school.TermsPerYear = null;
        }

        school.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(ToSchoolProfileDto(school));
    }

    /// <summary>Set school-level terms-per-year preference for planning and setup guidance.</summary>
    [HttpPut("profile/terms-per-year")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolProfileDto>> UpdateTermsPerYear([FromBody] UpdateTermsPerYearRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (request == null)
            return BadRequest("TermsPerYear payload is required.");

        if (request.TermsPerYear.HasValue && (request.TermsPerYear.Value < 1 || request.TermsPerYear.Value > 6))
            return BadRequest("TermsPerYear must be between 1 and 6.");

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools
            .Include(s => s.AcademicSystemProfile)
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        school.TermsPerYear = request.TermsPerYear;
        school.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToSchoolProfileDto(school));
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
        school.PromotionTransitionOverrideJson = null;
        school.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToSchoolProfileDto(school, profile));
    }

    /// <summary>Set or clear school-specific promotion transition rules (JSON map from source grade to allowed target grades).</summary>
    [HttpPut("profile/promotion-transition")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolProfileDto>> UpdatePromotionTransitions([FromBody] UpdatePromotionTransitionRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (request == null)
            return BadRequest("Promotion transition payload is required.");

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools
            .Include(s => s.AcademicSystemProfile)
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        if (request.UseProfileDefault)
        {
            school.PromotionTransitionOverrideJson = null;
            school.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return Ok(ToSchoolProfileDto(school));
        }

        if (!TryNormalizeTransitionJson(request.PromotionTransitionJson, out var normalizedJson, out var error))
            return BadRequest(error);

        school.PromotionTransitionOverrideJson = normalizedJson;
        school.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ToSchoolProfileDto(school));
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
    /// Provide country-aware onboarding defaults for classes and subjects.
    /// </summary>
    [HttpGet("onboarding-options")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetOnboardingOptions()
    {
        return Ok(new { countries = OnboardingCountryOptions, schoolModels = OnboardingSchoolModels });
    }

    /// <summary>
    /// Return country-aware school staff hierarchy defaults for School Admin setup.
    /// </summary>
    [HttpGet("staff-structure-options")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolStaffStructureOptionsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolStaffStructureOptionsDto>> GetStaffStructureOptions(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        var countryCode = (school.CountryCode ?? "NG").Trim().ToUpperInvariant();
        var countryMeta = OnboardingCountryOptions.FirstOrDefault(c => c.CountryCode == countryCode);

        var stageScopes = countryMeta == null
            ? new List<string> { "Pre-Nursery", "Nursery", "Primary", "Secondary", "Whole School" }
            : BuildStageScopes(countryMeta);

        return Ok(new SchoolStaffStructureOptionsDto(
            countryCode,
            countryMeta?.CountryName ?? "Unknown",
            GetDefaultHierarchyRoles(countryCode),
            GetDefaultClassAssignmentRoles(countryCode),
            stageScopes,
            "Use country defaults first, then add custom titles where your school needs local variation."
        ));
    }

    [HttpGet("staff-structure-config")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolStaffStructureConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolStaffStructureConfigDto>> GetStaffStructureConfig(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        var countryCode = (school.CountryCode ?? "NG").Trim().ToUpperInvariant();
        var countryMeta = OnboardingCountryOptions.FirstOrDefault(c => c.CountryCode == countryCode);
        var stageScopes = countryMeta == null
            ? new List<string> { "Pre-Nursery", "Nursery", "Primary", "Secondary", "Whole School" }
            : BuildStageScopes(countryMeta);
        var defaultRoles = GetDefaultHierarchyRoles(countryCode);
        var defaultClassAssignmentRoles = GetDefaultClassAssignmentRoles(countryCode);

        var stored = await LoadStaffStructureConfigAsync(schoolId, ct);
        var roleCatalog = BuildRoleCatalog(defaultRoles, stored?.CustomRoleCatalog, stageScopes);
        var permissionMatrix = BuildPermissionMatrix(roleCatalog, stored?.PermissionMatrix);

        return Ok(new SchoolStaffStructureConfigDto(
            countryCode,
            countryMeta?.CountryName ?? "Unknown",
            roleCatalog,
            defaultClassAssignmentRoles,
            stageScopes,
            permissionMatrix,
            stored?.UpdatedAtUtc,
            stored?.UpdatedBy,
            "Phase1B: school-level hierarchy catalog and governance matrix."
        ));
    }

    [HttpPut("staff-structure-config")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(SchoolStaffStructureConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolStaffStructureConfigDto>> SaveStaffStructureConfig([FromBody] UpdateSchoolStaffStructureConfigRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (request == null)
            return BadRequest("Request body is required.");

        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();

        var countryCode = (school.CountryCode ?? "NG").Trim().ToUpperInvariant();
        var countryMeta = OnboardingCountryOptions.FirstOrDefault(c => c.CountryCode == countryCode);
        var stageScopes = countryMeta == null
            ? new List<string> { "Pre-Nursery", "Nursery", "Primary", "Secondary", "Whole School" }
            : BuildStageScopes(countryMeta);
        var defaultRoles = GetDefaultHierarchyRoles(countryCode);
        var defaultClassAssignmentRoles = GetDefaultClassAssignmentRoles(countryCode);

        var roleCatalog = BuildRoleCatalog(defaultRoles, request.RoleCatalog, stageScopes);
        if (roleCatalog.Count == 0)
            return BadRequest("RoleCatalog must contain at least one role.");
        if (roleCatalog.Count > 120)
            return BadRequest("RoleCatalog cannot exceed 120 entries.");

        var permissionMatrix = BuildPermissionMatrix(roleCatalog, request.PermissionMatrix);
        var customRoleCatalog = roleCatalog.Where(x => !x.IsSystemDefault).ToList();

        var payload = new StoredSchoolStaffStructureConfigDto(
            customRoleCatalog,
            permissionMatrix,
            DateTime.UtcNow,
            _tenant.CurrentUserEmail
        );

        var serialized = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(serialized);

        var existingAsset = await _db.FileAssets
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId
                && x.Category == StaffStructureConfigCategory
                && x.RelativePath == StaffStructureConfigRelativePath, ct);

        if (existingAsset == null)
        {
            existingAsset = new FileAsset
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                OriginalFileName = "staff-structure-config.json",
                StoredFileName = "staff-structure-config.json",
                RelativePath = StaffStructureConfigRelativePath,
                ContentType = "application/json",
                Category = StaffStructureConfigCategory,
                UploadedBy = _tenant.CurrentUserEmail,
                UploadedAtUtc = DateTime.UtcNow,
            };
            _db.FileAssets.Add(existingAsset);
        }

        existingAsset.FileBytes = bytes;
        existingAsset.SizeBytes = bytes.LongLength;
        existingAsset.ContentType = "application/json";
        existingAsset.UploadedBy = _tenant.CurrentUserEmail;
        existingAsset.UploadedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new SchoolStaffStructureConfigDto(
            countryCode,
            countryMeta?.CountryName ?? "Unknown",
            roleCatalog,
            defaultClassAssignmentRoles,
            stageScopes,
            permissionMatrix,
            payload.UpdatedAtUtc,
            payload.UpdatedBy,
            "Phase1B: school-level hierarchy catalog and governance matrix."
        ));
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

    private static SchoolProfileDto ToSchoolProfileDto(School school, AcademicSystemProfile? profile = null)
    {
        var selectedProfile = profile ?? school.AcademicSystemProfile;
        var effectivePromotionTransitionJson = string.IsNullOrWhiteSpace(school.PromotionTransitionOverrideJson)
            ? selectedProfile?.PromotionTransitionJson
            : school.PromotionTransitionOverrideJson;

        return new SchoolProfileDto(
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
            school.TermsPerYear,
            string.IsNullOrWhiteSpace(school.LogoFileName) ? null : BuildSchoolLogoPath(school.Id),
            string.IsNullOrWhiteSpace(school.RegistrationDocumentPath) ? null : BuildRegistrationDocumentPath(school.Id),
            school.AcademicSystemProfileId,
            selectedProfile?.Code,
            selectedProfile?.Name,
            selectedProfile?.PromotionTransitionJson,
            school.PromotionTransitionOverrideJson,
            effectivePromotionTransitionJson,
            school.UpdatedAtUtc);
    }

    private static bool TryNormalizeTransitionJson(string? rawJson, out string? normalizedJson, out string? error)
    {
        normalizedJson = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            error = "PromotionTransitionJson is required unless UseProfileDefault is true.";
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(rawJson);
            if (parsed == null || parsed.Count == 0)
            {
                error = "PromotionTransitionJson must be a non-empty object mapping source grades to target grade arrays.";
                return false;
            }

            var normalized = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in parsed)
            {
                var source = (entry.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(source))
                {
                    error = "PromotionTransitionJson contains an empty source grade key.";
                    return false;
                }

                var targets = (entry.Value ?? new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (targets.Count == 0)
                {
                    error = $"PromotionTransitionJson must include at least one target grade for source '{source}'.";
                    return false;
                }

                normalized[source] = targets;
            }

            normalizedJson = JsonSerializer.Serialize(normalized);
            return true;
        }
        catch (JsonException)
        {
            error = "PromotionTransitionJson must be valid JSON (e.g. {\"Primary 1\":[\"Primary 2\"]}).";
            return false;
        }
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

    private static IReadOnlyList<string> BuildStageScopes(OnboardingCountryOption option)
    {
        var scopes = new List<string>();

        if (option.PrePrimaryStages.Count > 0)
        {
            scopes.Add("Pre-Nursery");
            scopes.Add("Nursery");
        }

        scopes.Add("Primary");
        scopes.Add("Secondary");
        scopes.Add("Whole School");

        return scopes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<StaffHierarchyRoleOptionDto> GetDefaultHierarchyRoles(string countryCode)
    {
        return countryCode switch
        {
            "GH" =>
            [
                new("head_teacher", "Head Teacher", "Whole School", 10),
                new("assistant_head_teacher", "Assistant Head Teacher", "Whole School", 20),
                new("primary_head", "Primary Head", "Primary", 30),
                new("jhs_head", "JHS Lead", "Secondary", 40),
                new("shs_head", "SHS Lead", "Secondary", 50),
                new("class_teacher", "Class Teacher", "Primary", 60),
                new("subject_teacher", "Subject Teacher", "Secondary", 70),
                new("assistant_class_teacher", "Assistant Class Teacher", "Primary", 80),
            ],
            "KE" =>
            [
                new("head_teacher", "Head Teacher", "Whole School", 10),
                new("deputy_head_teacher", "Deputy Head Teacher", "Whole School", 20),
                new("preprimary_lead", "Pre-Primary Lead", "Pre-Nursery", 30),
                new("primary_section_head", "Primary Section Head", "Primary", 40),
                new("junior_secondary_lead", "Junior Secondary Lead", "Secondary", 50),
                new("senior_secondary_lead", "Senior Secondary Lead", "Secondary", 60),
                new("class_teacher", "Class Teacher", "Primary", 70),
                new("assistant_class_teacher", "Assistant Class Teacher", "Primary", 80),
                new("subject_teacher", "Subject Teacher", "Secondary", 90),
            ],
            "SN" or "CI" or "MA" =>
            [
                new("directeur", "Directeur", "Whole School", 10),
                new("directeur_adjoint", "Directeur Adjoint", "Whole School", 20),
                new("responsable_maternelle", "Responsable Maternelle", "Pre-Nursery", 30),
                new("responsable_primaire", "Responsable Primaire", "Primary", 40),
                new("responsable_college", "Responsable College", "Secondary", 50),
                new("professeur_principal", "Professeur Principal", "Secondary", 60),
                new("enseignant", "Enseignant", "Primary", 70),
                new("assistant_enseignant", "Assistant Enseignant", "Primary", 80),
            ],
            _ =>
            [
                new("head_teacher", "Head Teacher", "Whole School", 10),
                new("assistant_head_teacher", "Assistant Head Teacher", "Whole School", 20),
                new("nursery_head", "Nursery Head", "Nursery", 30),
                new("primary_head", "Primary Head", "Primary", 40),
                new("secondary_head", "Secondary Head", "Secondary", 50),
                new("class_teacher", "Class Teacher", "Primary", 60),
                new("assistant_class_teacher", "Assistant Class Teacher", "Primary", 70),
                new("subject_teacher", "Subject Teacher", "Secondary", 80),
            ],
        };
    }

    private static IReadOnlyList<string> GetDefaultClassAssignmentRoles(string countryCode)
    {
        return countryCode switch
        {
            "GH" => ["Class Teacher", "Assistant Class Teacher", "Form Tutor", "Subject Teacher"],
            "KE" => ["Class Teacher", "Assistant Class Teacher", "Form Tutor", "Subject Teacher"],
            "SN" or "CI" or "MA" => ["Professeur Principal", "Assistant de Classe", "Professeur de Matiere"],
            _ => ["Class Teacher", "Assistant Class Teacher", "Form Teacher", "Subject Teacher"],
        };
    }

    private async Task<StoredSchoolStaffStructureConfigDto?> LoadStaffStructureConfigAsync(Guid schoolId, CancellationToken ct)
    {
        var payload = await _db.FileAssets
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId
                && x.Category == StaffStructureConfigCategory
                && x.RelativePath == StaffStructureConfigRelativePath
                && x.FileBytes != null)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => x.FileBytes)
            .FirstOrDefaultAsync(ct);

        if (payload == null || payload.Length == 0)
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(payload);
            return JsonSerializer.Deserialize<StoredSchoolStaffStructureConfigDto>(json);
        }
        catch
        {
            return null;
        }
    }

    private static List<SchoolStaffHierarchyRoleDto> BuildRoleCatalog(
        IReadOnlyList<StaffHierarchyRoleOptionDto> defaultRoles,
        IReadOnlyList<SchoolStaffHierarchyRoleDto>? customRoles,
        IReadOnlyList<string> stageScopes)
    {
        var validScopes = new HashSet<string>(stageScopes, StringComparer.OrdinalIgnoreCase);
        var catalog = defaultRoles
            .OrderBy(x => x.HierarchyOrder)
            .Select(x => new SchoolStaffHierarchyRoleDto(
                x.RoleCode,
                x.RoleTitle,
                validScopes.Contains(x.DefaultStageScope) ? x.DefaultStageScope : "Whole School",
                x.HierarchyOrder,
                true))
            .ToList();

        foreach (var role in customRoles ?? [])
        {
            var title = (role.RoleTitle ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title) || title.Length > 128)
                continue;

            var scope = string.IsNullOrWhiteSpace(role.StageScope) ? "Whole School" : role.StageScope.Trim();
            if (!validScopes.Contains(scope))
                scope = "Whole School";

            var order = role.HierarchyOrder <= 0 ? 1000 : role.HierarchyOrder;
            var exists = catalog.Any(x => string.Equals(x.RoleTitle, title, StringComparison.OrdinalIgnoreCase));
            if (exists)
                continue;

            catalog.Add(new SchoolStaffHierarchyRoleDto(
                string.IsNullOrWhiteSpace(role.RoleCode) ? BuildRoleCodeFromTitle(title) : role.RoleCode.Trim(),
                title,
                scope,
                order,
                false));
        }

        return catalog
            .OrderBy(x => x.HierarchyOrder)
            .ThenBy(x => x.RoleTitle)
            .ToList();
    }

    private static List<StaffPermissionMatrixItemDto> BuildPermissionMatrix(
        IReadOnlyList<SchoolStaffHierarchyRoleDto> roleCatalog,
        IReadOnlyList<StaffPermissionMatrixItemDto>? requested)
    {
        var requestedMap = (requested ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.RoleTitle))
            .GroupBy(x => x.RoleTitle.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var matrix = new List<StaffPermissionMatrixItemDto>();
        foreach (var role in roleCatalog)
        {
            if (requestedMap.TryGetValue(role.RoleTitle, out var configured))
            {
                matrix.Add(configured with { RoleTitle = role.RoleTitle });
                continue;
            }

            var lower = role.RoleTitle.ToLowerInvariant();
            var isLeadership = lower.Contains("head")
                || lower.Contains("deputy")
                || lower.Contains("directeur")
                || lower.Contains("principal");
            var isClassLead = lower.Contains("class teacher")
                || lower.Contains("form tutor")
                || lower.Contains("professeur principal");

            matrix.Add(new StaffPermissionMatrixItemDto(
                role.RoleTitle,
                CanManageTeachers: isLeadership,
                CanAssignClasses: isLeadership || isClassLead,
                CanApproveResults: isLeadership || isClassLead,
                CanSendParentBroadcasts: isLeadership || isClassLead,
                CanManageFees: isLeadership));
        }

        return matrix;
    }

    private static string BuildRoleCodeFromTitle(string title)
    {
        var chars = title
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        var normalized = new string(chars).Trim('_');
        while (normalized.Contains("__", StringComparison.Ordinal))
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(normalized) ? "custom_role" : normalized;
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
    string? CacNumber,
    int? TermsPerYear);

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
    int? TermsPerYear,
    string? LogoPath,
    string? RegistrationDocumentPath,
    Guid? AcademicSystemProfileId,
    string? AcademicSystemProfileCode,
    string? AcademicSystemProfileName,
    string? ProfilePromotionTransitionJson,
    string? PromotionTransitionOverrideJson,
    string? EffectivePromotionTransitionJson,
    DateTime? UpdatedAtUtc);

public record UpdateTermsPerYearRequest(int? TermsPerYear);

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

public record OnboardingCountryOption(
    string CountryCode,
    string CountryName,
    string CurrencyCode,
    string AcademicProfileCode,
    string RegionalSystem,
    string SystemStructure,
    IReadOnlyList<PrePrimaryStageOption> PrePrimaryStages,
    IReadOnlyList<string> DefaultClassLevels,
    IReadOnlyList<string> DefaultSubjects,
    IReadOnlyList<string> PrimarySubjectSamples,
    IReadOnlyList<string> JuniorSubjectSamples,
    IReadOnlyList<string> SeniorSubjectSamples,
    string Notes);

public record PrePrimaryStageOption(
    string LevelName,
    string AgeRange,
    string TypicalFocus);

public record OnboardingSchoolModelOption(
    string ModelCode,
    string ModelName,
    string CurriculumApproach,
    string ResourceProfile,
    string LanguageApproach,
    string CostProfile);

public record UpdateAcademicSystemProfileRequest(Guid AcademicSystemProfileId);

public record UpdatePromotionTransitionRequest(string? PromotionTransitionJson, bool UseProfileDefault = false);

public record SchoolGradeTemplateItemDto(
    string Label,
    string Name,
    int LevelOrder);

public record SchoolGradeTemplateCatalogDto(
    string ProfileCode,
    string ProfileName,
    string? Description,
    IReadOnlyList<SchoolGradeTemplateItemDto> Templates);

public record StaffHierarchyRoleOptionDto(
    string RoleCode,
    string RoleTitle,
    string DefaultStageScope,
    int HierarchyOrder);

public record SchoolStaffStructureOptionsDto(
    string CountryCode,
    string CountryName,
    IReadOnlyList<StaffHierarchyRoleOptionDto> RoleOptions,
    IReadOnlyList<string> ClassAssignmentRoles,
    IReadOnlyList<string> StageScopes,
    string Notes);

public record SchoolStaffHierarchyRoleDto(
    string RoleCode,
    string RoleTitle,
    string StageScope,
    int HierarchyOrder,
    bool IsSystemDefault);

public record StaffPermissionMatrixItemDto(
    string RoleTitle,
    bool CanManageTeachers,
    bool CanAssignClasses,
    bool CanApproveResults,
    bool CanSendParentBroadcasts,
    bool CanManageFees);

public record SchoolStaffStructureConfigDto(
    string CountryCode,
    string CountryName,
    IReadOnlyList<SchoolStaffHierarchyRoleDto> RoleCatalog,
    IReadOnlyList<string> ClassAssignmentRoles,
    IReadOnlyList<string> StageScopes,
    IReadOnlyList<StaffPermissionMatrixItemDto> PermissionMatrix,
    DateTime? UpdatedAtUtc,
    string? UpdatedBy,
    string Notes);

public record UpdateSchoolStaffStructureConfigRequest(
    IReadOnlyList<SchoolStaffHierarchyRoleDto>? RoleCatalog,
    IReadOnlyList<StaffPermissionMatrixItemDto>? PermissionMatrix);

public record StoredSchoolStaffStructureConfigDto(
    IReadOnlyList<SchoolStaffHierarchyRoleDto> CustomRoleCatalog,
    IReadOnlyList<StaffPermissionMatrixItemDto> PermissionMatrix,
    DateTime UpdatedAtUtc,
    string? UpdatedBy);

public record DeniedAttemptsPageDto(
    IReadOnlyList<AuditLogDto> Items,
    long? NextCursor,
    bool HasMore);
