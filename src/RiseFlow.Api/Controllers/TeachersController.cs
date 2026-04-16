using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeachersController : ControllerBase
{
    private const long MaxTeacherPhotoBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly IReadOnlyList<(string FieldKey, string DisplayName, bool IsAdminOnly, bool IsVisibleToTeacher, bool IsEditableByTeacher, int SortOrder)> DefaultProfileFields =
    [
        ("firstName", "First Name", false, true, true, 10),
        ("middleName", "Middle Name", false, true, true, 20),
        ("lastName", "Last Name", false, true, true, 30),
        ("phone", "Phone", false, true, true, 40),
        ("whatsAppNumber", "WhatsApp Number", false, true, true, 50),
        ("dateOfBirth", "Date of Birth", false, true, true, 60),
        ("gender", "Gender", false, true, true, 70),
        ("nationality", "Nationality", false, true, true, 80),
        ("stateOfOrigin", "State", false, true, true, 90),
        ("lga", "LGA", false, true, true, 100),
        ("religion", "Religion", false, true, true, 110),
        ("residentialAddress", "Residential Address", false, true, true, 120),
        ("subjectSpecialization", "Subject Specialization", false, true, true, 130),
        ("highestQualification", "Highest Qualification", false, true, true, 140),
        ("fieldOfStudy", "Field Of Study", false, true, true, 150),
        ("yearsOfExperience", "Years Of Experience", false, true, true, 160),
        ("previousSchools", "Previous Schools", false, true, true, 170),
        ("professionalBodies", "Professional Bodies", false, true, true, 180),
        ("baseSalaryAmount", "Base Salary", true, false, false, 400),
        ("allowancesNote", "Allowances", true, false, false, 410),
        ("recognitions", "Recognitions", true, false, false, 420)
    ];

    private readonly RiseFlowDbContext _db;
    private readonly Services.ITenantContext _tenant;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly Services.FileStorageService _fileStorage;
    private readonly StaffPermissionService _staffPermissions;
    private readonly ILogger<TeachersController> _logger;

    public TeachersController(RiseFlowDbContext db, Services.ITenantContext tenant, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, Services.FileStorageService fileStorage, StaffPermissionService staffPermissions, ILogger<TeachersController> logger)
    {
        _db = db;
        _tenant = tenant;
        _userManager = userManager;
        _env = env;
        _fileStorage = fileStorage;
        _staffPermissions = staffPermissions;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Teacher>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Teacher>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        try
        {
            var list = await _db.Teachers
                .AsNoTracking()
                .Include(t => t.TeacherClasses)
                .ThenInclude(tc => tc.Class)
                .Include(t => t.TeacherClassSubjects)
                .ThenInclude(tcs => tcs.Class)
                .Where(t => t.SchoolId == schoolId)
                .OrderBy(t => t.LastName)
                .ThenBy(t => t.FirstName)
                .ToListAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Teacher list could not be loaded for school {SchoolId}. Returning minimal fallback rows.", schoolId);

            var fallback = await _db.Teachers
                .AsNoTracking()
                .Where(t => t.SchoolId == schoolId)
                .OrderBy(t => t.LastName)
                .ThenBy(t => t.FirstName)
                .Select(t => new
                {
                    t.Id,
                    t.FirstName,
                    t.LastName,
                    MiddleName = (string?)null,
                    Email = (string?)null,
                    TeacherClasses = Array.Empty<object>(),
                    TeacherClassSubjects = Array.Empty<object>(),
                    t.IsActive
                })
                .ToListAsync(ct);

            return Ok(fallback);
        }
    }

    [HttpGet("people")]
    [Authorize(Roles = Constants.Roles.SchoolAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListPeople(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var people = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses)
            .ThenInclude(tc => tc.Class)
            .Include(t => t.TeacherClassSubjects)
            .ThenInclude(tcs => tcs.Class)
            .Where(t => t.SchoolId == schoolId)
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .ToListAsync(ct);

        var normalizedEmails = people
            .Select(t => t.Email?.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.ToUpperInvariant())
            .Distinct()
            .ToList();

        var roleRows = new List<(string Email, string Role)>();
        if (normalizedEmails.Count > 0)
        {
            var rawRoleRows = await (from u in _db.Users.AsNoTracking()
                                     join ur in _db.UserRoles.AsNoTracking() on u.Id equals ur.UserId
                                     join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                     where u.Email != null && normalizedEmails.Contains(u.Email.ToUpper())
                                     select new { Email = u.Email!, Role = r.Name! })
                .ToListAsync(ct);

            roleRows = rawRoleRows
                .Select(x => (x.Email.Trim().ToUpperInvariant(), x.Role))
                .ToList();
        }

        var emailRoleMap = roleRows
            .GroupBy(x => x.Email)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Role).Where(r => !string.IsNullOrWhiteSpace(r)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        static string ResolveFallbackPersonRole(string? roleTitle)
        {
            var role = (roleTitle ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(role)) return Constants.Roles.Teacher;

            var looksStaff = role.Contains("staff")
                || role.Contains("bursar")
                || role.Contains("clerk")
                || role.Contains("secretary")
                || role.Contains("front desk")
                || role.Contains("account")
                || role.Contains("support")
                || role.Contains("office");

            return looksStaff ? Constants.Roles.Staff : Constants.Roles.Teacher;
        }

        var result = people.Select(t =>
        {
            var normalizedEmail = t.Email?.Trim().ToUpperInvariant();
            emailRoleMap.TryGetValue(normalizedEmail ?? string.Empty, out var roles);
            var personRole = roles != null && roles.Contains(Constants.Roles.Staff)
                ? Constants.Roles.Staff
                : roles != null && roles.Contains(Constants.Roles.Teacher)
                    ? Constants.Roles.Teacher
                    : ResolveFallbackPersonRole(t.RoleTitle);

            return new
            {
                t.Id,
                t.FirstName,
                t.LastName,
                t.MiddleName,
                t.Email,
                t.Phone,
                t.WhatsAppNumber,
                t.StaffId,
                t.SubjectSpecialization,
                t.DateOfBirth,
                t.Gender,
                t.Nationality,
                t.StateOfOrigin,
                t.LGA,
                t.Religion,
                t.ResidentialAddress,
                t.HighestQualification,
                t.FieldOfStudy,
                t.YearsOfExperience,
                t.PreviousSchools,
                t.ProfessionalBodies,
                t.RoleTitle,
                t.Department,
                t.ProfilePhotoFileName,
                t.IsActive,
                personRole,
                teacherClasses = (t.TeacherClasses ?? Array.Empty<TeacherClass>())
                    .Select(tc => new { tc.ClassId, tc.RoleInClass, @class = tc.Class == null ? null : new { tc.Class.Id, tc.Class.Name } })
                    .ToList(),
                teacherClassSubjects = (t.TeacherClassSubjects ?? Array.Empty<TeacherClassSubject>())
                    .Select(tcs => new { tcs.ClassId, @class = tcs.Class == null ? null : new { tcs.Class.Id, tcs.Class.Name } })
                    .ToList()
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Teacher), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Teacher>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var teacher = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses)
            .ThenInclude(tc => tc.Class)
            .Include(t => t.TeacherClassSubjects)
            .ThenInclude(tcs => tcs.Class)
            .FirstOrDefaultAsync(t => t.Id == id && t.SchoolId == schoolId, ct);
        if (teacher == null)
            return NotFound();
        return Ok(teacher);
    }

    [HttpPost]
    [Authorize(Roles = Constants.Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(Teacher), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Teacher>> Create([FromBody] CreateTeacherRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            Email = request.Email,
            Phone = request.Phone,
            WhatsAppNumber = request.WhatsAppNumber,
            StaffId = request.StaffId,
            SubjectSpecialization = request.SubjectSpecialization,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Nationality = request.Nationality,
            StateOfOrigin = request.StateOfOrigin,
            LGA = request.LGA,
            Religion = request.Religion,
            NIN = request.NIN,
            NationalIdType = request.NationalIdType,
            NationalIdNumber = request.NationalIdNumber,
            TrcnNumber = request.TrcnNumber,
            ResidentialAddress = request.ResidentialAddress,
            HighestQualification = request.HighestQualification,
            FieldOfStudy = request.FieldOfStudy,
            YearsOfExperience = request.YearsOfExperience,
            PreviousSchools = request.PreviousSchools,
            ProfessionalBodies = request.ProfessionalBodies,
            DateEmployed = request.DateEmployed,
            EmploymentType = request.EmploymentType,
            RoleTitle = request.RoleTitle,
            Department = request.Department,
            BaseSalaryAmount = request.BaseSalaryAmount,
            BaseSalaryCurrency = request.BaseSalaryCurrency,
            AllowancesNote = request.AllowancesNote,
            PromotionHistory = request.PromotionHistory,
            Recognitions = request.Recognitions,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Teachers.Add(teacher);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = teacher.Id }, teacher);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Constants.Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(Teacher), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Teacher>> Update(Guid id, [FromBody] UpdateTeacherRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null)
            return NotFound();
        if (teacher.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();
        teacher.FirstName = request.FirstName;
        teacher.LastName = request.LastName;
        teacher.MiddleName = request.MiddleName;
        teacher.Email = request.Email;
        teacher.Phone = request.Phone;
        teacher.WhatsAppNumber = request.WhatsAppNumber;
        teacher.StaffId = request.StaffId;
        teacher.SubjectSpecialization = request.SubjectSpecialization;
        teacher.DateOfBirth = request.DateOfBirth;
        teacher.Gender = request.Gender;
        teacher.Nationality = request.Nationality;
        teacher.StateOfOrigin = request.StateOfOrigin;
        teacher.LGA = request.LGA;
        teacher.Religion = request.Religion;
        teacher.NIN = request.NIN;
        teacher.NationalIdType = request.NationalIdType;
        teacher.NationalIdNumber = request.NationalIdNumber;
        teacher.TrcnNumber = request.TrcnNumber;
        teacher.ResidentialAddress = request.ResidentialAddress;
        teacher.HighestQualification = request.HighestQualification;
        teacher.FieldOfStudy = request.FieldOfStudy;
        teacher.YearsOfExperience = request.YearsOfExperience;
        teacher.PreviousSchools = request.PreviousSchools;
        teacher.ProfessionalBodies = request.ProfessionalBodies;
        teacher.DateEmployed = request.DateEmployed;
        teacher.EmploymentType = request.EmploymentType;
        teacher.RoleTitle = request.RoleTitle;
        teacher.Department = request.Department;
        teacher.BaseSalaryAmount = request.BaseSalaryAmount;
        teacher.BaseSalaryCurrency = request.BaseSalaryCurrency;
        teacher.AllowancesNote = request.AllowancesNote;
        teacher.PromotionHistory = request.PromotionHistory;
        teacher.Recognitions = request.Recognitions;
        teacher.IsActive = request.IsActive;
        teacher.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(teacher);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null)
            return NotFound();
        if (teacher.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();
        _db.Teachers.Remove(teacher);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/role-profile")]
    [Authorize(Roles = Constants.Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(Teacher), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Teacher>> UpdateRoleProfile(Guid id, [FromBody] UpdateTeacherRoleProfileRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id && t.SchoolId == schoolId, ct);
        if (teacher == null)
            return NotFound();

        var normalizedRoleTitle = string.IsNullOrWhiteSpace(request.RoleTitle)
            ? null
            : request.RoleTitle.Trim();
        var normalizedDepartment = string.IsNullOrWhiteSpace(request.Department)
            ? null
            : request.Department.Trim();

        if (normalizedRoleTitle is { Length: > 128 })
            return BadRequest("Role title must be 128 characters or fewer.");
        if (normalizedDepartment is { Length: > 128 })
            return BadRequest("Department must be 128 characters or fewer.");

        teacher.RoleTitle = normalizedRoleTitle;
        teacher.Department = normalizedDepartment;
        teacher.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(teacher);
    }

    /// <summary>
    /// Teacher signup via school gateway. AllowAnonymous. Creates ApplicationUser + Teacher profile for the given school and assigns Teacher role.
    /// </summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TeacherSignupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeacherSignupResult>> Signup([FromBody] TeacherSignupRequest request, CancellationToken ct)
    {
        if (request == null || request.SchoolId == Guid.Empty || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("SchoolId, Email and Password are required.");

        var school = await _db.Schools.FindAsync(new object[] { request.SchoolId }, ct);
        if (school == null || !school.IsActive)
            return NotFound("School not found or inactive.");

        var email = request.Email.Trim();
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
            return BadRequest("An account with this email already exists. Please sign in and contact your school admin if you should be a teacher.");

        var firstName = (request.FirstName ?? "").Trim();
        var lastName = (request.LastName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(firstName)) firstName = email.Split('@')[0];
        var assignedRole = request.IsStaffAccount ? Constants.Roles.Staff : Constants.Roles.Teacher;
        var defaultRoleTitle = request.IsStaffAccount ? "Support Staff" : "Teacher";
        var roleTitle = string.IsNullOrWhiteSpace(request.RoleTitle) ? defaultRoleTitle : request.RoleTitle.Trim();
        var department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            SchoolId = request.SchoolId,
            FullName = $"{firstName} {lastName}".Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return BadRequest(string.Join(" ", createResult.Errors.Select(e => e.Description)));

        var addRoleResult = await _userManager.AddToRoleAsync(user, assignedRole);
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return BadRequest(string.Join(" ", addRoleResult.Errors.Select(e => e.Description)));
        }

        var addClaimResult = await _userManager.AddClaimAsync(user, new Claim("SchoolId", request.SchoolId.ToString()));
        if (!addClaimResult.Succeeded)
        {
            await _userManager.RemoveFromRoleAsync(user, assignedRole);
            await _userManager.DeleteAsync(user);
            return BadRequest(string.Join(" ", addClaimResult.Errors.Select(e => e.Description)));
        }

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            SchoolId = request.SchoolId,
            FirstName = firstName,
            LastName = lastName,
            MiddleName = request.MiddleName,
            Email = email,
            Phone = request.Phone,
            WhatsAppNumber = request.WhatsAppNumber,
            StaffId = request.StaffId,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Nationality = request.Nationality,
            StateOfOrigin = request.StateOfOrigin,
            LGA = request.LGA,
            Religion = request.Religion,
            NIN = request.NIN,
            NationalIdType = request.NationalIdType,
            NationalIdNumber = request.NationalIdNumber,
            TrcnNumber = request.TrcnNumber,
            ResidentialAddress = request.ResidentialAddress,
            HighestQualification = request.HighestQualification,
            FieldOfStudy = request.FieldOfStudy,
            YearsOfExperience = request.YearsOfExperience,
            PreviousSchools = request.PreviousSchools,
            ProfessionalBodies = request.ProfessionalBodies,
            RoleTitle = roleTitle,
            Department = department,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Teachers.Add(teacher);
        await _db.SaveChangesAsync(ct);

        var successMessage = request.IsStaffAccount
            ? "Account created. Sign in as staff. Your school admin can review your profile and assignments."
            : "Account created. Sign in as a teacher. Your school admin will assign your classes and subjects.";
        return Ok(new TeacherSignupResult(true, successMessage));
    }

    /// <summary>Current teacher profile (filtered by school-admin visibility settings). Teacher only.</summary>
    [HttpGet("me")]
    [Authorize(Roles = $"{Constants.Roles.Teacher},{Constants.Roles.Staff}")]
    [ProducesResponseType(typeof(TeacherProfileConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherProfileConfigDto>> Me(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrEmpty(email))
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var teacher = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses).ThenInclude(tc => tc.Class)
            .Include(t => t.TeacherClassSubjects).ThenInclude(tcs => tcs.Class)
            .FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.Email == email, ct);
        if (teacher == null)
            return Ok(null);

        return Ok(await BuildTeacherProfileConfigDtoAsync(schoolId, teacher, teacherView: true, ct));
    }

    /// <summary>Teacher profile plus field settings for self-edit dashboard.</summary>
    [HttpGet("me/profile-config")]
    [Authorize(Roles = $"{Constants.Roles.Teacher},{Constants.Roles.Staff}")]
    [ProducesResponseType(typeof(TeacherProfileConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherProfileConfigDto>> MeProfileConfig(CancellationToken ct) => await Me(ct);

    /// <summary>School-admin view of one teacher profile + governance settings.</summary>
    [HttpGet("{id:guid}/profile-config")]
    [Authorize(Roles = Constants.Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(TeacherProfileConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeacherProfileConfigDto>> GetProfileConfig(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var teacher = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses).ThenInclude(tc => tc.Class)
            .Include(t => t.TeacherClassSubjects).ThenInclude(tcs => tcs.Class)
            .FirstOrDefaultAsync(t => t.Id == id && t.SchoolId == schoolId, ct);
        if (teacher == null)
            return NotFound();
        return Ok(await BuildTeacherProfileConfigDtoAsync(schoolId, teacher, teacherView: false, ct));
    }

    /// <summary>School-admin managed teacher field visibility/editability settings.</summary>
    [HttpGet("profile-field-settings")]
    [Authorize(Roles = Constants.Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(List<TeacherProfileFieldSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TeacherProfileFieldSettingDto>>> GetProfileFieldSettings(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var settings = await EnsureTeacherProfileFieldSettingsAsync(schoolId, ct);
        return Ok(settings.Select(MapSettingDto).ToList());
    }

    /// <summary>Create or update teacher profile field setting. Supports custom fields.</summary>
    [HttpPost("profile-field-settings")]
    [Authorize(Roles = Constants.Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(TeacherProfileFieldSettingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherProfileFieldSettingDto>> UpsertProfileFieldSetting([FromBody] UpsertTeacherProfileFieldSettingRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var normalizedKey = NormalizeFieldKey(request.FieldKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return BadRequest("FieldKey is required.");

        var settings = await EnsureTeacherProfileFieldSettingsAsync(schoolId, ct);
        var setting = settings.FirstOrDefault(s => s.FieldKey == normalizedKey);
        if (setting == null)
        {
            setting = new TeacherProfileFieldSetting
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                FieldKey = normalizedKey,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.TeacherProfileFieldSettings.Add(setting);
        }

        var isBuiltIn = DefaultProfileFields.Any(f => f.FieldKey == normalizedKey);
        setting.IsCustom = request.IsCustom || !isBuiltIn;
        setting.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? normalizedKey : request.DisplayName.Trim();
        setting.IsAdminOnly = request.IsAdminOnly;
        setting.IsVisibleToTeacher = request.IsVisibleToTeacher;
        setting.IsEditableByTeacher = request.IsEditableByTeacher && !request.IsAdminOnly;
        setting.SortOrder = request.SortOrder;
        setting.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(MapSettingDto(setting));
    }

    /// <summary>Update current teacher profile respecting school-admin field locks and visibility.</summary>
    [HttpPut("me")]
    [Authorize(Roles = $"{Constants.Roles.Teacher},{Constants.Roles.Staff}")]
    [ProducesResponseType(typeof(TeacherProfileConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeacherProfileConfigDto>> UpdateMe([FromBody] UpdateTeacherSelfRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var email = _tenant.CurrentUserEmail ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var settings = (await EnsureTeacherProfileFieldSettingsAsync(schoolId, ct)).ToDictionary(x => x.FieldKey, x => x);

        var teacher = await _db.Teachers
            .FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.Email == email, ct);

        if (teacher == null)
            return NotFound();

        bool CanEdit(string fieldKey)
        {
            if (!settings.TryGetValue(fieldKey, out var setting)) return true;
            return setting.IsVisibleToTeacher && setting.IsEditableByTeacher && !setting.IsAdminOnly;
        }

        if (CanEdit("firstName")) teacher.FirstName = request.FirstName;
        if (CanEdit("lastName")) teacher.LastName = request.LastName;
        if (CanEdit("middleName")) teacher.MiddleName = request.MiddleName;
        if (CanEdit("phone")) teacher.Phone = request.Phone;
        if (CanEdit("whatsAppNumber")) teacher.WhatsAppNumber = request.WhatsAppNumber;
        if (CanEdit("dateOfBirth")) teacher.DateOfBirth = request.DateOfBirth;
        if (CanEdit("gender")) teacher.Gender = request.Gender;
        if (CanEdit("nationality")) teacher.Nationality = request.Nationality;
        if (CanEdit("stateOfOrigin")) teacher.StateOfOrigin = request.StateOfOrigin;
        if (CanEdit("lga")) teacher.LGA = request.LGA;
        if (CanEdit("religion")) teacher.Religion = request.Religion;
        if (CanEdit("residentialAddress")) teacher.ResidentialAddress = request.ResidentialAddress;
        if (CanEdit("subjectSpecialization")) teacher.SubjectSpecialization = request.SubjectSpecialization;
        if (CanEdit("highestQualification")) teacher.HighestQualification = request.HighestQualification;
        if (CanEdit("fieldOfStudy")) teacher.FieldOfStudy = request.FieldOfStudy;
        if (CanEdit("yearsOfExperience")) teacher.YearsOfExperience = request.YearsOfExperience;
        if (CanEdit("previousSchools")) teacher.PreviousSchools = request.PreviousSchools;
        if (CanEdit("professionalBodies")) teacher.ProfessionalBodies = request.ProfessionalBodies;
        teacher.UpdatedAtUtc = DateTime.UtcNow;

        if (request.CustomFields != null && request.CustomFields.Count > 0)
        {
            var editableCustomKeys = settings.Values
                .Where(s => s.IsCustom && s.IsVisibleToTeacher && s.IsEditableByTeacher && !s.IsAdminOnly)
                .Select(s => s.FieldKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingCustomValues = await _db.TeacherCustomFieldValues
                .Where(v => v.SchoolId == schoolId && v.TeacherId == teacher.Id)
                .ToListAsync(ct);

            foreach (var pair in request.CustomFields)
            {
                var key = NormalizeFieldKey(pair.Key);
                if (!editableCustomKeys.Contains(key))
                    continue;

                var existing = existingCustomValues.FirstOrDefault(v => v.FieldKey == key);
                if (existing == null)
                {
                    _db.TeacherCustomFieldValues.Add(new TeacherCustomFieldValue
                    {
                        Id = Guid.NewGuid(),
                        SchoolId = schoolId,
                        TeacherId = teacher.Id,
                        FieldKey = key,
                        Value = pair.Value,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Value = pair.Value;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        var refreshed = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses).ThenInclude(tc => tc.Class)
            .Include(t => t.TeacherClassSubjects).ThenInclude(tcs => tcs.Class)
            .FirstOrDefaultAsync(t => t.Id == teacher.Id, ct);

        return Ok(await BuildTeacherProfileConfigDtoAsync(schoolId, refreshed!, teacherView: true, ct));
    }

    /// <summary>Students in classes assigned to the current teacher. Teacher only. Returns empty list until admin assigns classes/subjects.</summary>
    [HttpGet("my-students")]
    [Authorize(Roles = Constants.Roles.Teacher)]
    [ProducesResponseType(typeof(List<MyStudentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MyStudentDto>>> MyStudents(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrEmpty(email))
            return Ok(new List<MyStudentDto>());

        var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.Email == email, ct);
        if (teacher == null)
            return Ok(new List<MyStudentDto>());

        var classIds = new HashSet<Guid>();
        var directClasses = await _db.TeacherClasses
            .Where(tc => tc.TeacherId == teacher.Id)
            .Select(tc => tc.ClassId)
            .ToListAsync(ct);
        foreach (var cid in directClasses)
            classIds.Add(cid);

        var subjectClasses = await _db.TeacherClassSubjects
            .Where(tcs => tcs.TeacherId == teacher.Id)
            .Select(tcs => tcs.ClassId)
            .ToListAsync(ct);
        foreach (var cid in subjectClasses)
            classIds.Add(cid);

        if (classIds.Count == 0)
            return Ok(new List<MyStudentDto>());

        var students = await _db.Students
            .AsNoTracking()
            .Include(s => s.Class)
            .Where(s => s.SchoolId == schoolId && s.ClassId != null && classIds.Contains(s.ClassId.Value))
            .OrderBy(s => s.Class!.Name)
            .ThenBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync(ct);

        var list = students.Select(s => new MyStudentDto(
            s.Id,
            s.ClassId!.Value,
            s.FirstName,
            s.LastName,
            s.MiddleName,
            s.AdmissionNumber,
            s.Class?.Name,
            s.Gender
        )).ToList();
        return Ok(list);
    }

    [HttpPost("{teacherId:guid}/classes/{classId:guid}")]
    [Authorize(Roles = $"{Constants.Roles.SchoolAdmin},{Constants.Roles.Teacher}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AssignToClass(Guid teacherId, Guid classId, [FromBody] AssignTeacherToClassRequest? request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanManageTeachers, "TeacherClass", "AssignToClass.ManageTeachers", teacherId.ToString(), ct))
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanAssignClasses, "TeacherClass", "AssignToClass", teacherId.ToString(), ct))
            return Forbid();
        var existing = await _db.TeacherClasses.FirstOrDefaultAsync(tc => tc.TeacherId == teacherId && tc.ClassId == classId, ct);
        if (existing != null)
        {
            var normalizedRole = string.IsNullOrWhiteSpace(request?.RoleInClass) ? null : request!.RoleInClass!.Trim();
            if (!string.Equals(existing.RoleInClass, normalizedRole, StringComparison.Ordinal))
            {
                existing.RoleInClass = normalizedRole;
                await _db.SaveChangesAsync(ct);
            }
            return NoContent();
        }
        var link = new TeacherClass
        {
            TeacherId = teacherId,
            ClassId = classId,
            RoleInClass = string.IsNullOrWhiteSpace(request?.RoleInClass) ? null : request!.RoleInClass!.Trim(),
            AssignedAtUtc = DateTime.UtcNow
        };
        _db.TeacherClasses.Add(link);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{teacherId:guid}/classes/{classId:guid}")]
    [Authorize(Roles = $"{Constants.Roles.SchoolAdmin},{Constants.Roles.Teacher}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UnassignFromClass(Guid teacherId, Guid classId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanManageTeachers, "TeacherClass", "UnassignFromClass.ManageTeachers", teacherId.ToString(), ct))
            return Forbid();
        if (!await _staffPermissions.EnsureTeacherPermissionAsync(User, StaffPermissionKeys.CanAssignClasses, "TeacherClass", "UnassignFromClass", teacherId.ToString(), ct))
            return Forbid();
        var link = await _db.TeacherClasses.FirstOrDefaultAsync(tc => tc.TeacherId == teacherId && tc.ClassId == classId, ct);
        if (link != null)
        {
            var teacherInSchool = await _db.Teachers.AnyAsync(t => t.Id == teacherId && t.SchoolId == _tenant.CurrentSchoolId.Value, ct);
            if (!teacherInSchool)
                return Forbid();
            _db.TeacherClasses.Remove(link);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    /// <summary>Get teacher passport-size profile photo. Allowed for same-school users (SchoolAdmin/Teacher/Parent).</summary>
    [HttpGet("{id:guid}/photo")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhoto(Guid id, CancellationToken ct)
    {
        var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null || string.IsNullOrEmpty(teacher.ProfilePhotoFileName))
            return NotFound();
        if (_tenant.CurrentSchoolId.HasValue && teacher.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();

        var bytes = await _fileStorage.TryReadBytesAsync(teacher.ProfilePhotoFileName, ct);
        if (bytes != null && bytes.Length > 0)
        {
            var detectedContentType = teacher.ProfilePhotoFileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
                : teacher.ProfilePhotoFileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif"
                : teacher.ProfilePhotoFileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
                : "image/jpeg";
            return File(bytes, detectedContentType);
        }

        // Fallback for transient storage outages: serve the last persisted DB blob for this photo path.
        var blob = await _db.FileAssets
            .AsNoTracking()
            .Where(a => a.SchoolId == teacher.SchoolId
                && a.Category == "teacher-photo"
                && a.RelativePath == teacher.ProfilePhotoFileName
                && a.FileBytes != null)
            .OrderByDescending(a => a.UploadedAtUtc)
            .Select(a => new { a.FileBytes, a.ContentType })
            .FirstOrDefaultAsync(ct);
        if (blob?.FileBytes is { Length: > 0 })
        {
            return File(blob.FileBytes, string.IsNullOrWhiteSpace(blob.ContentType) ? "image/jpeg" : blob.ContentType);
        }

        var root = _env.WebRootPath ?? _env.ContentRootPath;
        var path = Path.Combine(root, teacher.ProfilePhotoFileName.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(path))
            return NotFound();
        var contentType = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif"
            : path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
            : "image/jpeg";
        return PhysicalFile(path, contentType, enableRangeProcessing: false);
    }

    /// <summary>Upload or update teacher passport photo. SchoolAdmin or the teacher themself.</summary>
    [HttpPost("{id:guid}/photo")]
    [Authorize(Roles = $"{Constants.Roles.SchoolAdmin},{Constants.Roles.Teacher},{Constants.Roles.Staff}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UploadPhoto(Guid id, [FromForm] IFormFile? file, CancellationToken ct)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null)
            return NotFound("Teacher not found.");
        if (_tenant.CurrentSchoolId.HasValue && teacher.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();

        // If uploading as Teacher/Staff, ensure this is their own profile
        if (User.IsInRole(Constants.Roles.Teacher) || User.IsInRole(Constants.Roles.Staff))
        {
            var email = _tenant.CurrentUserEmail ?? User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email) || !string.Equals(email, teacher.Email, StringComparison.OrdinalIgnoreCase))
                return Forbid();
        }

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        if (file.Length > MaxTeacherPhotoBytes)
            return BadRequest("Photo is too large. Maximum allowed size is 5 MB.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Allowed formats: .jpg, .jpeg, .png, .gif, .webp");

        var fileName = $"{teacher.Id:N}{ext}";
        var relativePath = $"teachers/{teacher.SchoolId:N}/{fileName}";

        byte[] photoBytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            photoBytes = ms.ToArray();
            ms.Position = 0;
            try
            {
                await _fileStorage.UploadAsync(relativePath, ms, file.ContentType, ct);
            }
            catch (Exception ex)
            {
                // Keep uploads working when storage is temporarily unavailable by falling back to DB blob.
                _logger.LogWarning(ex, "Storage upload failed for teacher photo {TeacherId} in school {SchoolId}; falling back to DB blob.", teacher.Id, teacher.SchoolId);
            }
        }

        _db.FileAssets.Add(new FileAsset
        {
            Id = Guid.NewGuid(),
            SchoolId = teacher.SchoolId,
            OriginalFileName = file.FileName,
            StoredFileName = fileName,
            RelativePath = relativePath,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            FileBytes = photoBytes,
            Category = "teacher-photo",
            UploadedBy = _tenant.CurrentUserEmail,
            UploadedAtUtc = DateTime.UtcNow
        });

        teacher.ProfilePhotoFileName = relativePath;
        teacher.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Photo uploaded.", profilePhotoFileName = relativePath });
    }

    private async Task<List<TeacherProfileFieldSetting>> EnsureTeacherProfileFieldSettingsAsync(Guid schoolId, CancellationToken ct)
    {
        var existing = await _db.TeacherProfileFieldSettings
            .Where(x => x.SchoolId == schoolId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(ct);

        if (existing.Count == 0)
        {
            var now = DateTime.UtcNow;
            existing = DefaultProfileFields.Select(x => new TeacherProfileFieldSetting
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                FieldKey = x.FieldKey,
                DisplayName = x.DisplayName,
                IsCustom = false,
                IsAdminOnly = x.IsAdminOnly,
                IsVisibleToTeacher = x.IsVisibleToTeacher,
                IsEditableByTeacher = x.IsEditableByTeacher,
                SortOrder = x.SortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }).ToList();

            _db.TeacherProfileFieldSettings.AddRange(existing);
            await _db.SaveChangesAsync(ct);
            return existing.OrderBy(x => x.SortOrder).ThenBy(x => x.DisplayName).ToList();
        }

        var missingDefaults = DefaultProfileFields
            .Where(d => existing.All(e => e.FieldKey != d.FieldKey))
            .ToList();

        if (missingDefaults.Count > 0)
        {
            var now = DateTime.UtcNow;
            var toAdd = missingDefaults.Select(x => new TeacherProfileFieldSetting
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                FieldKey = x.FieldKey,
                DisplayName = x.DisplayName,
                IsCustom = false,
                IsAdminOnly = x.IsAdminOnly,
                IsVisibleToTeacher = x.IsVisibleToTeacher,
                IsEditableByTeacher = x.IsEditableByTeacher,
                SortOrder = x.SortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }).ToList();

            _db.TeacherProfileFieldSettings.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);

            existing.AddRange(toAdd);
            existing = existing.OrderBy(x => x.SortOrder).ThenBy(x => x.DisplayName).ToList();
        }

        return existing;
    }

    private async Task<TeacherProfileConfigDto> BuildTeacherProfileConfigDtoAsync(Guid schoolId, Teacher teacher, bool teacherView, CancellationToken ct)
    {
        var settings = await EnsureTeacherProfileFieldSettingsAsync(schoolId, ct);
        var values = await _db.TeacherCustomFieldValues
            .AsNoTracking()
            .Where(v => v.SchoolId == schoolId && v.TeacherId == teacher.Id)
            .ToListAsync(ct);

        var settingsDto = settings.Select(MapSettingDto).ToList();
        var customMap = values.ToDictionary(x => x.FieldKey, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        if (teacherView)
        {
            settingsDto = settingsDto
                .Where(s => s.isVisibleToTeacher || s.isCustom)
                .ToList();

            // Hide admin-only values from teacher responses.
            if (settings.Any(s => s.FieldKey == "baseSalaryAmount" && !s.IsVisibleToTeacher))
                teacher.BaseSalaryAmount = null;
            if (settings.Any(s => s.FieldKey == "allowancesNote" && !s.IsVisibleToTeacher))
                teacher.AllowancesNote = null;
            if (settings.Any(s => s.FieldKey == "recognitions" && !s.IsVisibleToTeacher))
                teacher.Recognitions = null;

            var visibleCustomKeys = settings
                .Where(s => s.IsCustom && s.IsVisibleToTeacher)
                .Select(s => s.FieldKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            customMap = customMap
                .Where(kv => visibleCustomKeys.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        }

        var permissions = new TeacherRolePermissionsDto(
            canManageTeachers: teacherView && await _staffPermissions.HasTeacherPermissionAsync(User, StaffPermissionKeys.CanManageTeachers, ct),
            canAssignClasses: teacherView && await _staffPermissions.HasTeacherPermissionAsync(User, StaffPermissionKeys.CanAssignClasses, ct),
            canApproveResults: teacherView && await _staffPermissions.HasTeacherPermissionAsync(User, StaffPermissionKeys.CanApproveResults, ct),
            canSendParentBroadcasts: teacherView && await _staffPermissions.HasTeacherPermissionAsync(User, StaffPermissionKeys.CanSendParentBroadcasts, ct),
            canManageFees: teacherView && await _staffPermissions.HasTeacherPermissionAsync(User, StaffPermissionKeys.CanManageFees, ct),
            canManageAttendance: teacherView && await _staffPermissions.HasTeacherPermissionAsync(User, StaffPermissionKeys.CanManageAttendance, ct),
            canManageAssessments: teacherView && await _staffPermissions.HasTeacherPermissionAsync(User, StaffPermissionKeys.CanManageAssessments, ct)
        );

        return new TeacherProfileConfigDto(teacher, settingsDto, customMap, permissions);
    }

    private static TeacherProfileFieldSettingDto MapSettingDto(TeacherProfileFieldSetting setting)
        => new(setting.FieldKey, setting.DisplayName, setting.IsCustom, setting.IsVisibleToTeacher, setting.IsEditableByTeacher, setting.IsAdminOnly, setting.SortOrder);

    private static string NormalizeFieldKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        return new string(key.Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch)).ToArray());
    }
}

public record MyStudentDto(
    Guid StudentId,
    Guid ClassId,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? AdmissionNumber,
    string? ClassName,
    string? Gender);

public record TeacherProfileFieldSettingDto(
    string fieldKey,
    string displayName,
    bool isCustom,
    bool isVisibleToTeacher,
    bool isEditableByTeacher,
    bool isAdminOnly,
    int sortOrder);

public record UpsertTeacherProfileFieldSettingRequest(
    string FieldKey,
    string DisplayName,
    bool IsCustom,
    bool IsVisibleToTeacher,
    bool IsEditableByTeacher,
    bool IsAdminOnly,
    int SortOrder);

public record TeacherProfileConfigDto(
    Teacher teacher,
    List<TeacherProfileFieldSettingDto> fieldSettings,
    Dictionary<string, string> customFields,
    TeacherRolePermissionsDto permissions);

public record TeacherRolePermissionsDto(
    bool canManageTeachers,
    bool canAssignClasses,
    bool canApproveResults,
    bool canSendParentBroadcasts,
    bool canManageFees,
    bool canManageAttendance,
    bool canManageAssessments);
