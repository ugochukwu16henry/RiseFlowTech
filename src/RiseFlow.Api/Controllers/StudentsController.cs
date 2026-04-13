using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IWebHostEnvironment _env;
    private readonly StudentBulkUploadService _bulkUpload;
    private readonly ExcelService _excelService;
    private readonly ParentWelcomeLetterPdfService _parentLetterPdf;
    private readonly BillingService _billing;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(RiseFlowDbContext db, ITenantContext tenant, IWebHostEnvironment env, StudentBulkUploadService bulkUpload, ExcelService excelService, ParentWelcomeLetterPdfService parentLetterPdf, BillingService billing, ILogger<StudentsController> logger)
    {
        _db = db;
        _tenant = tenant;
        _env = env;
        _bulkUpload = bulkUpload;
        _excelService = excelService;
        _parentLetterPdf = parentLetterPdf;
        _billing = billing;
        _logger = logger;
    }

    /// <summary>
    /// Get a rich "digital file" profile for a student: bio, parents, academic history, and access code.
    /// SchoolAdmin/Teacher only; tenant filter ensures isolation.
    /// </summary>
    [HttpGet("{id:guid}/profile")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    [ProducesResponseType(typeof(StudentProfileViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentProfileViewModel>> GetProfile(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        var student = await _db.Students
            .Include(s => s.Class)
            .Include(s => s.Grade)
            .Include(s => s.StudentParents)
                .ThenInclude(sp => sp.Parent)
            .Include(s => s.Results)
                .ThenInclude(r => r.Term)
            .Include(s => s.Results)
                .ThenInclude(r => r.Subject)
            .FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == schoolId, ct);

        if (student == null)
            return NotFound();

        var fullName = $"{student.FirstName} {student.LastName}".Trim();

        // Mask NIN by default (e.g. ******4321); do not expose full value in this DTO.
        string? ninMasked = null;
        if (!string.IsNullOrWhiteSpace(student.NIN) && student.NIN!.Length > 4)
        {
            var last4 = student.NIN[^4..];
            ninMasked = new string('*', student.NIN.Length - 4) + last4;
        }

        // Mask emergency contact phone similarly.
        string? emergencyPhoneMasked = null;
        if (!string.IsNullOrWhiteSpace(student.EmergencyContactPhone) && student.EmergencyContactPhone!.Length > 4)
        {
            var last4 = student.EmergencyContactPhone[^4..];
            emergencyPhoneMasked = new string('*', student.EmergencyContactPhone.Length - 4) + last4;
        }

        var parents = student.StudentParents
            .Select(sp => sp.Parent)
            .Distinct()
            .Select(p => new ParentContactDto(
                p.Id,
                $"{p.FirstName} {p.LastName}".Trim(),
                p.Relationship,
                p.Phone,
                p.WhatsAppNumber,
                p.Email))
            .ToList();

        var hasResults = student.Results.Any();
        decimal currentAveragePercentage = 0;
        if (hasResults)
        {
            currentAveragePercentage = student.Results.Average(r =>
                r.MaxScore > 0 ? (r.Score / r.MaxScore) * 100m : 0m);
        }

        // Academic history: all individual results ordered by term then subject.
        var history = student.Results
            .OrderByDescending(r => r.Term.StartDate)
            .ThenBy(r => r.Subject.Name)
            .Select(r =>
            {
                var percentage = r.MaxScore > 0 ? (r.Score / r.MaxScore) * 100m : 0m;
                var termLabel = $"{r.Term.Name} {r.Term.AcademicYear}";
                return new StudentAcademicHistoryItem(
                    r.Id,
                    termLabel,
                    r.Subject.Name,
                    r.AssessmentType,
                    r.Score,
                    r.MaxScore,
                    decimal.Round(percentage, 1),
                    r.GradeLetter);
            })
            .ToList();

        // Performance trend: average percentage per term (last 3 terms).
        var trend = student.Results
            .GroupBy(r => r.TermId)
            .Select(g =>
            {
                var first = g.First();
                var avgPct = g.Average(r => r.MaxScore > 0 ? (r.Score / r.MaxScore) * 100m : 0m);
                var label = $"{first.Term.Name} {first.Term.AcademicYear}";
                return new PerformanceTrendPoint(first.TermId, label, decimal.Round(avgPct, 1));
            })
            .OrderBy(p => p.Term)
            .TakeLast(3)
            .ToList();

        // Fee status: simple school-wide check using BillingService.
        var hasActiveSubscription = await _billing.IsSubscriptionActiveAsync(schoolId, ct);
        var feeStatus = hasActiveSubscription ? "Up to date" : "Action required";

        var vm = new StudentProfileViewModel(
            Id: student.Id,
            SchoolId: student.SchoolId,
            FullName: fullName,
            AdmissionNumber: student.AdmissionNumber,
            ClassName: student.Class?.Name,
            GradeName: student.Grade?.Name,
            ProfilePhotoFileName: student.ProfilePhotoFileName,
            IsActive: student.IsActive,
            ParentAccessCode: student.ParentAccessCode,
            NinMasked: ninMasked,
            DateOfBirth: student.DateOfBirth,
            Gender: student.Gender,
            Nationality: student.Nationality,
            StateOfOrigin: student.StateOfOrigin,
            Lga: student.LGA,
            EmergencyContactName: student.EmergencyContactName,
            EmergencyContactPhoneMasked: emergencyPhoneMasked,
            CurrentAveragePercentage: decimal.Round(currentAveragePercentage, 1),
            AttendancePercentage: null, // ready to be wired to attendance data source
            FeeStatus: feeStatus,
            AcademicHistory: history,
            Parents: parents,
            PerformanceTrend: trend);

        return Ok(vm);
    }

    [HttpGet("me/dashboard")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType(typeof(StudentSelfDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StudentSelfDashboardDto>> GetMyDashboard(CancellationToken ct)
    {
        var portalAccess = await GetCurrentStudentPortalAccessAsync(ct);
        if (portalAccess == null || !portalAccess.IsEnabled)
            return Forbid();

        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.School)
            .Include(s => s.Class)
                .ThenInclude(c => c!.Grade)
            .Include(s => s.Grade)
            .Include(s => s.StudentParents)
                .ThenInclude(sp => sp.Parent)
            .FirstOrDefaultAsync(s => s.Id == portalAccess.StudentId && s.SchoolId == portalAccess.SchoolId, ct);

        if (student == null)
            return NotFound();

        var fullName = $"{student.FirstName} {student.LastName}".Trim();

        List<StudentDashboardTeacherDto> teachers;
        if (student.ClassId.HasValue)
        {
            var subjectTeachers = await _db.Set<TeacherClassSubject>()
                .AsNoTracking()
                .Include(tcs => tcs.Teacher)
                .Include(tcs => tcs.Subject)
                .Where(tcs => tcs.ClassId == student.ClassId.Value && tcs.Teacher.IsActive)
                .Select(tcs => new StudentDashboardTeacherDto(
                    tcs.TeacherId,
                    $"{tcs.Teacher.FirstName} {tcs.Teacher.LastName}".Trim(),
                    tcs.Subject.Name,
                    tcs.Teacher.Email,
                    tcs.Teacher.Phone,
                    tcs.Teacher.WhatsAppNumber ?? tcs.Teacher.Phone))
                .ToListAsync(ct);

            if (subjectTeachers.Count > 0)
            {
                teachers = subjectTeachers
                    .GroupBy(t => new { t.TeacherId, t.FullName, t.RoleOrSubject, t.Email, t.Phone, t.WhatsAppNumber })
                    .Select(g => g.First())
                    .OrderBy(t => t.FullName)
                    .ToList();
            }
            else
            {
                teachers = await _db.TeacherClasses
                    .AsNoTracking()
                    .Include(tc => tc.Teacher)
                    .Where(tc => tc.ClassId == student.ClassId.Value && tc.Teacher.IsActive)
                    .OrderBy(tc => tc.Teacher.FirstName)
                    .ThenBy(tc => tc.Teacher.LastName)
                    .Select(tc => new StudentDashboardTeacherDto(
                        tc.TeacherId,
                        $"{tc.Teacher.FirstName} {tc.Teacher.LastName}".Trim(),
                        tc.RoleInClass,
                        tc.Teacher.Email,
                        tc.Teacher.Phone,
                        tc.Teacher.WhatsAppNumber ?? tc.Teacher.Phone))
                    .ToListAsync(ct);
            }
        }
        else
        {
            teachers = new List<StudentDashboardTeacherDto>();
        }

        var parents = student.StudentParents
            .OrderByDescending(sp => sp.IsPrimaryContact)
            .ThenBy(sp => sp.Parent.FirstName)
            .Select(sp => new StudentDashboardParentDto(
                sp.ParentId,
                $"{sp.Parent.FirstName} {sp.Parent.LastName}".Trim(),
                sp.RelationshipToStudent ?? sp.Parent.Relationship,
                portalAccess.ShowParentContactDetails ? sp.Parent.Phone : null,
                portalAccess.ShowParentContactDetails ? sp.Parent.WhatsAppNumber : null,
                portalAccess.ShowParentContactDetails ? sp.Parent.Email : null))
            .ToList();

        var classmates = student.ClassId.HasValue
            ? await _db.Students
                .AsNoTracking()
                .Where(s => s.SchoolId == student.SchoolId && s.ClassId == student.ClassId && s.Id != student.Id && s.IsActive)
                .OrderBy(s => s.FirstName)
                .ThenBy(s => s.LastName)
                .Select(s => new StudentDashboardClassmateDto(
                    s.Id,
                    $"{s.FirstName} {s.LastName}".Trim(),
                    s.ProfilePhotoFileName))
                .ToListAsync(ct)
            : new List<StudentDashboardClassmateDto>();

        var dto = new StudentSelfDashboardDto(
            student.Id,
            fullName,
            student.School.Name,
            student.Class?.Name,
            student.Grade?.Name,
            student.AdmissionNumber,
            student.ProfilePhotoFileName,
            portalAccess.ShowDateOfBirth ? student.DateOfBirth : null,
            student.Gender,
            portalAccess.ShowLocationDetails ? student.Nationality : null,
            portalAccess.ShowLocationDetails ? student.StateOfOrigin : null,
            portalAccess.ShowLocationDetails ? student.LGA : null,
            portalAccess.ShowPreviousSchoolDetails ? student.PreviousSchool : null,
            portalAccess.ShowHealthDetails ? student.BloodGroup : null,
            portalAccess.ShowHealthDetails ? student.Genotype : null,
            portalAccess.ShowHealthDetails ? student.Allergies : null,
            portalAccess.ShowEmergencyContacts ? student.EmergencyContactName : null,
            portalAccess.ShowEmergencyContacts ? student.EmergencyContactPhone : null,
            parents,
            teachers,
            classmates);

        return Ok(dto);
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    [ProducesResponseType(typeof(List<StudentDirectoryItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StudentDirectoryItemDto>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        try
        {
            var list = await _db.Students
                .AsNoTracking()
                .Where(s => s.SchoolId == schoolId)
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .Select(s => new StudentDirectoryItemDto(
                    s.Id,
                    s.FirstName,
                    s.LastName,
                    s.MiddleName,
                    s.AdmissionNumber,
                    s.IsActive,
                    s.ProfilePhotoFileName,
                    s.ClassId == null
                        ? null
                        : new StudentDirectoryClassDto(
                            s.ClassId.Value,
                            s.Class != null ? s.Class.Name : null,
                            s.Class != null && s.Class.Grade != null
                                ? s.Class.Grade.Name
                                : (s.Grade != null ? s.Grade.Name : null)),
                    s.GradeId == null
                        ? null
                        : new StudentDirectoryGradeDto(
                            s.GradeId.Value,
                            s.Grade != null
                                ? s.Grade.Name
                                : (s.Class != null && s.Class.Grade != null ? s.Class.Grade.Name : null))
                ))
                .ToListAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Student list could not be loaded for school {SchoolId}. Returning minimal fallback rows.", schoolId);

            var fallback = await _db.Students
                .AsNoTracking()
                .Where(s => s.SchoolId == schoolId)
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .Select(s => new StudentDirectoryItemDto(
                    s.Id,
                    s.FirstName,
                    s.LastName,
                    null,
                    null,
                    s.IsActive,
                    null,
                    null,
                    null))
                .ToListAsync(ct);

            return Ok(fallback);
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher},{Roles.Parent}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        var isParent = User.IsInRole(Roles.Parent);
        var isTeacher = User.IsInRole(Roles.Teacher);
        var isSchoolAdmin = User.IsInRole(Roles.SchoolAdmin);
        Parent? currentParent = null;
        var canParentEdit = false;
        DateTime? parentEditLockedUntilUtc = null;
        string? parentEditMessage = null;
        StudentProfileVisibilitySetting? visibilitySetting = null;

        if (isParent)
        {
            var email = _tenant.CurrentUserEmail;
            if (string.IsNullOrWhiteSpace(email))
                return Unauthorized();

            currentParent = await _db.Parents
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.SchoolId == schoolId && p.Email == email, ct);
            if (currentParent == null)
                return Forbid();
        }

        if (isTeacher || isSchoolAdmin)
            visibilitySetting = await GetOrCreateStudentVisibilitySettingAsync(schoolId, ct);

        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.Class)
                .ThenInclude(c => c!.Grade)
            .Include(s => s.Grade)
            .Include(s => s.StudentParents)
                .ThenInclude(sp => sp.Parent)
            .Include(s => s.Results)
                .ThenInclude(r => r.Term)
            .Include(s => s.Results)
                .ThenInclude(r => r.Subject)
            .FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == schoolId, ct);
        if (student == null)
            return NotFound();

        if (isParent)
        {
            var currentParentId = currentParent!.Id;
            var linked = await _db.StudentParents
                .AsNoTracking()
                .AnyAsync(sp => sp.StudentId == id && sp.ParentId == currentParentId, ct);
            if (!linked)
                return Forbid();

            var lockWindow = await _db.StudentParentEditWindows
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.StudentId == id && w.ParentId == currentParentId, ct);
            var now = DateTime.UtcNow;
            canParentEdit = lockWindow == null || lockWindow.NextEditableAtUtc <= now;
            parentEditLockedUntilUtc = lockWindow?.NextEditableAtUtc;
            parentEditMessage = canParentEdit
                ? "You can update your child\'s full details now."
                : $"Parent edits are locked until {lockWindow!.NextEditableAtUtc:dd MMM yyyy}.";
        }

        var assignedTeachers = await GetAssignedTeachersAsync(student, ct);
        var termResults = BuildTermResults(student);

        var teacherCanSeeDateOfBirth = visibilitySetting?.ShowDateOfBirthToTeachers ?? true;
        var teacherCanSeeLocation = visibilitySetting?.ShowLocationDetailsToTeachers ?? false;
        var teacherCanSeeHealth = visibilitySetting?.ShowHealthDetailsToTeachers ?? true;
        var teacherCanSeeParents = visibilitySetting?.ShowParentContactsToTeachers ?? false;
        var teacherCanSeeAcademic = visibilitySetting?.ShowAcademicHistoryToTeachers ?? true;
        var teacherCanSeePrevious = visibilitySetting?.ShowPreviousRecordToTeachers ?? false;

        var teacherView = isTeacher && !isSchoolAdmin;

        var parentsView = (teacherView && !teacherCanSeeParents)
            ? new List<object>()
            : student.StudentParents
                .OrderByDescending(sp => sp.IsPrimaryContact)
                .Select(sp => (object)new
                {
                    parentId = sp.ParentId,
                    firstName = sp.Parent.FirstName,
                    lastName = sp.Parent.LastName,
                    relationshipToStudent = sp.RelationshipToStudent,
                    phone = sp.Parent.Phone,
                    email = sp.Parent.Email
                })
                .ToList();

        var visibleTermResults = (teacherView && !teacherCanSeeAcademic)
            ? new List<StudentTermResultGroupDto>()
            : termResults;

        return Ok(new
        {
            student.Id,
            student.FirstName,
            student.LastName,
            student.MiddleName,
            DateOfBirth = teacherView && !teacherCanSeeDateOfBirth ? null : student.DateOfBirth,
            student.Gender,
            Nationality = teacherView && !teacherCanSeeLocation ? null : student.Nationality,
            stateOfOrigin = teacherView && !teacherCanSeeLocation ? null : student.StateOfOrigin,
            lga = teacherView && !teacherCanSeeLocation ? null : student.LGA,
            nin = student.NIN,
            student.NationalIdType,
            student.NationalIdNumber,
            student.AdmissionNumber,
            student.DateOfAdmission,
            PreviousSchool = teacherView && !teacherCanSeePrevious ? null : student.PreviousSchool,
            BloodGroup = teacherView && !teacherCanSeeHealth ? null : student.BloodGroup,
            Genotype = teacherView && !teacherCanSeeHealth ? null : student.Genotype,
            Allergies = teacherView && !teacherCanSeeHealth ? null : student.Allergies,
            EmergencyContactName = teacherView && !teacherCanSeeHealth ? null : student.EmergencyContactName,
            EmergencyContactPhone = teacherView && !teacherCanSeeHealth ? null : student.EmergencyContactPhone,
            student.ParentAccessCode,
            student.ProfilePhotoFileName,
            student.IsActive,
            Class = student.Class == null ? null : new
            {
                student.Class.Id,
                student.Class.Name,
                Grade = student.Class.Grade == null ? null : new { student.Class.Grade.Id, student.Class.Grade.Name }
            },
            Grade = student.Grade == null ? null : new { student.Grade.Id, student.Grade.Name },
            studentParents = parentsView,
            assignedTeachers,
            termResults = visibleTermResults,
            currentAveragePercentage = visibleTermResults.Count == 0 ? (decimal?)null : Math.Round(visibleTermResults.Average(t => t.AveragePercentage), 1),
            canParentEdit,
            parentEditLockedUntilUtc,
            parentEditMessage,
            canManageTeacherVisibility = isSchoolAdmin,
            teacherVisibilitySettings = isSchoolAdmin ? visibilitySetting : null
        });
    }

    [HttpGet("profile-visibility-settings")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(StudentProfileVisibilitySetting), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentProfileVisibilitySetting>> GetStudentProfileVisibilitySettings(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var setting = await GetOrCreateStudentVisibilitySettingAsync(schoolId, ct);
        return Ok(setting);
    }

    [HttpPut("profile-visibility-settings")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(StudentProfileVisibilitySetting), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentProfileVisibilitySetting>> UpdateStudentProfileVisibilitySettings([FromBody] UpdateStudentProfileVisibilityRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var setting = await GetOrCreateStudentVisibilitySettingAsync(schoolId, ct);

        setting.ShowDateOfBirthToTeachers = request.ShowDateOfBirthToTeachers;
        setting.ShowLocationDetailsToTeachers = request.ShowLocationDetailsToTeachers;
        setting.ShowHealthDetailsToTeachers = request.ShowHealthDetailsToTeachers;
        setting.ShowParentContactsToTeachers = request.ShowParentContactsToTeachers;
        setting.ShowAcademicHistoryToTeachers = request.ShowAcademicHistoryToTeachers;
        setting.ShowPreviousRecordToTeachers = request.ShowPreviousRecordToTeachers;
        setting.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(setting);
    }

    /// <summary>Register a single student. SchoolAdmin only. Use this to add new students one-by-one; use bulk upload for many at once.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(Student), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Student>> Create([FromBody] CreateStudentRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        // "First 50 Free" guardrail: after the free tier, require an active subscription.
        var activeCount = await _db.Students.CountAsync(s => s.SchoolId == schoolId && s.IsActive, ct);
        if (activeCount >= CountryBillingConfig.FreeTierStudentCount)
        {
            var hasActiveSubscription = await _billing.IsSubscriptionActiveAsync(schoolId, ct);
            if (!hasActiveSubscription)
            {
                return BadRequest($"Free tier limit ({CountryBillingConfig.FreeTierStudentCount} students) reached. Please upgrade to add more students.");
            }
        }
        var student = new Student
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Nationality = request.Nationality,
            StateOfOrigin = request.StateOfOrigin,
            LGA = request.LGA,
            NIN = request.NIN,
            NationalIdType = request.NationalIdType,
            NationalIdNumber = request.NationalIdNumber,
            AdmissionNumber = request.AdmissionNumber,
            DateOfAdmission = request.DateOfAdmission,
            ClassId = request.ClassId,
            GradeId = request.GradeId,
            PreviousSchool = request.PreviousSchool,
            BloodGroup = request.BloodGroup,
            Genotype = request.Genotype,
            Allergies = request.Allergies,
            EmergencyContactName = request.EmergencyContactName,
            EmergencyContactPhone = request.EmergencyContactPhone,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Students.Add(student);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = student.Id }, student);
    }

    /// <summary>Download Excel template for bulk student upload. Aligned with African ministry requirements (NIN, Class, Parent, etc.).</summary>
    [HttpGet("bulk-upload-template")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public ActionResult DownloadBulkUploadTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Students");
        ws.Cell(1, 1).Value = "FirstName";
        ws.Cell(1, 2).Value = "LastName";
        ws.Cell(1, 3).Value = "MiddleName";
        ws.Cell(1, 4).Value = "Gender";
        ws.Cell(1, 5).Value = "DateOfBirth";
        ws.Cell(1, 6).Value = "NIN";
        ws.Cell(1, 7).Value = "NationalIdType";
        ws.Cell(1, 8).Value = "NationalIdNumber";
        ws.Cell(1, 9).Value = "Class";
        ws.Cell(1, 10).Value = "AdmissionNumber";
        ws.Cell(1, 11).Value = "StateOfOrigin";
        ws.Cell(1, 12).Value = "LGA";
        ws.Cell(1, 13).Value = "Nationality";
        ws.Cell(1, 14).Value = "ParentName";
        ws.Cell(1, 15).Value = "ParentPhone";
        ws.Cell(1, 16).Value = "BloodGroup";
        ws.Cell(1, 17).Value = "Genotype";
        ws.Cell(1, 18).Value = "EmergencyContactName";
        ws.Cell(1, 19).Value = "EmergencyContactPhone";
        ws.Row(1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "John";
        ws.Cell(2, 2).Value = "Doe";
        ws.Cell(2, 4).Value = "Male";
        ws.Cell(2, 5).Value = "2015-09-01";
        ws.Cell(2, 9).Value = "Grade 1A";
        ws.Cell(2, 14).Value = "Jane Doe";
        ws.Cell(2, 15).Value = "+2348012345678";
        var countrySheet = workbook.Worksheets.Add("Country_Columns");
        countrySheet.Cell(1, 1).Value = "Country";
        countrySheet.Cell(1, 2).Value = "Required / Recommended columns";
        countrySheet.Row(1).Style.Font.Bold = true;
        countrySheet.Cell(2, 1).Value = "Nigeria";
        countrySheet.Cell(2, 2).Value = "NIN (National ID), StateOfOrigin, LGA required for ministry alignment.";
        countrySheet.Cell(3, 1).Value = "Ghana";
        countrySheet.Cell(3, 2).Value = "NationalIdType=GHANA_CARD, NationalIdNumber.";
        countrySheet.Cell(4, 1).Value = "Kenya";
        countrySheet.Cell(4, 2).Value = "NationalIdType=KENYA_ID, NationalIdNumber.";
        countrySheet.Cell(5, 1).Value = "All";
        countrySheet.Cell(5, 2).Value = "FirstName, LastName required. Class = class name (create class in RiseFlow first). ParentName, ParentPhone for guardian.";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream, false);
        stream.Position = 0;
        const string fileName = "RiseFlow-Students-Template.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>Preview Excel import: first 5 rows and validation errors. Does not save.</summary>
    [HttpPost("bulk-upload-preview")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(ExcelPreviewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExcelPreviewResult>> BulkUploadPreview(IFormFile file, [FromQuery] int previewRows = 5, CancellationToken ct = default)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        if (Path.GetExtension(file.FileName)?.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) != true)
            return BadRequest("Only .xlsx files are supported.");
        await using var stream = file.OpenReadStream();
        var result = await _excelService.GetPreviewAsync(stream, _tenant.CurrentSchoolId.Value, previewRows, ct);
        return Ok(result);
    }

    /// <summary>Bulk import students from Excel. Returns imported count, billing message, and error rows for download.</summary>
    [HttpPost("bulk-upload")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(ExcelImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExcelImportResult>> BulkUpload(IFormFile file, CancellationToken ct = default)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        if (Path.GetExtension(file.FileName)?.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) != true)
            return BadRequest("Only .xlsx files are supported.");
        await using var stream = file.OpenReadStream();
        var result = await _excelService.ImportAsync(stream, _tenant.CurrentSchoolId.Value, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    [ProducesResponseType(typeof(Student), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Student>> Update(Guid id, [FromBody] UpdateStudentRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
            return NotFound();
        if (student.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();
        student.FirstName = request.FirstName;
        student.LastName = request.LastName;
        student.MiddleName = request.MiddleName;
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender;
        student.Nationality = request.Nationality;
        student.StateOfOrigin = request.StateOfOrigin;
        student.LGA = request.LGA;
        student.NIN = request.NIN;
        student.NationalIdType = request.NationalIdType;
        student.NationalIdNumber = request.NationalIdNumber;
        student.AdmissionNumber = request.AdmissionNumber;
        student.DateOfAdmission = request.DateOfAdmission;
        student.ClassId = request.ClassId;
        student.GradeId = request.GradeId;
        student.PreviousSchool = request.PreviousSchool;
        student.BloodGroup = request.BloodGroup;
        student.Genotype = request.Genotype;
        student.Allergies = request.Allergies;
        student.EmergencyContactName = request.EmergencyContactName;
        student.EmergencyContactPhone = request.EmergencyContactPhone;
        student.IsActive = request.IsActive;
        student.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(student);
    }

    [HttpPut("{id:guid}/parent-corrections")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> ParentCorrections(Guid id, [FromBody] ParentStudentCorrectionRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        var parent = await _db.Parents
            .FirstOrDefaultAsync(p => p.SchoolId == schoolId && p.Email == email, ct);
        if (parent == null)
            return Forbid();

        var linked = await _db.StudentParents
            .AnyAsync(sp => sp.StudentId == id && sp.ParentId == parent.Id, ct);
        if (!linked)
            return Forbid();

        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == schoolId, ct);
        if (student == null)
            return NotFound();

        var now = DateTime.UtcNow;
        var editWindow = await _db.StudentParentEditWindows
            .FirstOrDefaultAsync(w => w.ParentId == parent.Id && w.StudentId == student.Id, ct);

        if (editWindow != null && editWindow.NextEditableAtUtc > now)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = $"Parent edits are locked until {editWindow.NextEditableAtUtc:dd MMM yyyy}.",
                lockedUntilUtc = editWindow.NextEditableAtUtc
            });
        }

        student.FirstName = request.FirstName;
        student.LastName = request.LastName;
        student.MiddleName = request.MiddleName;
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender;
        student.Nationality = request.Nationality;
        student.StateOfOrigin = request.StateOfOrigin;
        student.LGA = request.LGA;
        student.NIN = request.NIN;
        student.NationalIdType = request.NationalIdType;
        student.NationalIdNumber = request.NationalIdNumber;
        student.AdmissionNumber = request.AdmissionNumber;
        student.DateOfAdmission = request.DateOfAdmission;
        student.ClassId = request.ClassId;
        student.GradeId = request.GradeId;
        student.PreviousSchool = request.PreviousSchool;
        student.BloodGroup = request.BloodGroup;
        student.Genotype = request.Genotype;
        student.Allergies = request.Allergies;
        student.EmergencyContactName = request.EmergencyContactName;
        student.EmergencyContactPhone = request.EmergencyContactPhone;
        student.UpdatedAtUtc = now;

        if (editWindow == null)
        {
            editWindow = new StudentParentEditWindow
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                ParentId = parent.Id,
                StudentId = student.Id,
            };
            _db.StudentParentEditWindows.Add(editWindow);
        }

        editWindow.LastEditedAtUtc = now;
        editWindow.NextEditableAtUtc = now.AddMonths(3);

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = $"Child details updated. Next parent edit window opens on {editWindow.NextEditableAtUtc:dd MMM yyyy}.",
            lockedUntilUtc = editWindow.NextEditableAtUtc
        });
    }

    /// <summary>List students with their Parent Access Codes (for school to give to parents). SchoolAdmin/Teacher.</summary>
    [HttpGet("with-access-codes")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    [ProducesResponseType(typeof(List<StudentWithAccessCodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StudentWithAccessCodeDto>>> ListWithAccessCodes(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var list = await _db.Students
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Select(s => new StudentWithAccessCodeDto(
                s.Id,
                s.FirstName,
                s.LastName,
                s.MiddleName,
                s.AdmissionNumber,
                s.Class != null ? s.Class.Name : null,
                s.ParentAccessCode))
            .ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>Get or generate Parent Access Code for a student. Parent enters this code to link to the student. SchoolAdmin/Teacher.</summary>
    [HttpGet("{id:guid}/access-code")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    [ProducesResponseType(typeof(AccessCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccessCodeDto>> GetOrCreateAccessCode(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == _tenant.CurrentSchoolId.Value, ct);
        if (student == null)
            return NotFound();
        if (string.IsNullOrEmpty(student.ParentAccessCode))
        {
            student.ParentAccessCode = await GenerateUniqueAccessCodeAsync(_tenant.CurrentSchoolId.Value, ct);
            await _db.SaveChangesAsync(ct);
        }
        return Ok(new AccessCodeDto(student.ParentAccessCode!));
    }

    /// <summary>Generate parent access codes for all students in the school that don't have one. SchoolAdmin.</summary>
    [HttpPost("generate-access-codes")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(GenerateAccessCodesResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GenerateAccessCodesResult>> GenerateAccessCodes(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var studentsWithoutCode = await _db.Students.Where(s => s.SchoolId == schoolId && (s.ParentAccessCode == null || s.ParentAccessCode == "")).ToListAsync(ct);
        var generated = 0;
        foreach (var s in studentsWithoutCode)
        {
            s.ParentAccessCode = await GenerateUniqueAccessCodeAsync(schoolId, ct);
            generated++;
        }
        await _db.SaveChangesAsync(ct);
        var totalStudents = await _db.Students.CountAsync(s => s.SchoolId == schoolId, ct);
        var withCode = await _db.Students.CountAsync(s => s.SchoolId == schoolId && !string.IsNullOrEmpty(s.ParentAccessCode), ct);
        return Ok(new GenerateAccessCodesResult(generated, totalStudents, withCode));
    }

    /// <summary>Generate Parent Welcome Letters (one page per student) for printing. SchoolAdmin only. Optionally filter by classId. Students without a code get one generated.</summary>
    [HttpGet("parent-welcome-letters")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParentWelcomeLettersPdf([FromQuery] Guid? classId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var school = await _db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school == null)
            return NotFound();
        var query = _db.Students.Include(s => s.Class).Where(s => s.SchoolId == schoolId);
        if (classId.HasValue)
            query = query.Where(s => s.ClassId == classId.Value);
        var students = await query.ToListAsync(ct);
        for (var i = 0; i < students.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(students[i].ParentAccessCode))
            {
                students[i].ParentAccessCode = await GenerateUniqueAccessCodeAsync(schoolId, ct);
            }
        }
        await _db.SaveChangesAsync(ct);
        var list = students
            .Select(s => (
                StudentFullName: $"{s.FirstName} {s.LastName}".Trim(),
                AccessCode: s.ParentAccessCode ?? ""
            ))
            .Where(x => !string.IsNullOrEmpty(x.AccessCode))
            .ToList();
        if (list.Count == 0)
            return NotFound("No students to generate letters for.");
        byte[]? logoBytes = null;
        if (!string.IsNullOrEmpty(school.LogoFileName))
        {
            var root = _env.WebRootPath ?? _env.ContentRootPath;
            var path = Path.Combine(root, school.LogoFileName.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(path))
            {
                try { logoBytes = await System.IO.File.ReadAllBytesAsync(path, ct); } catch { /* ignore */ }
            }
        }
        var pdfBytes = _parentLetterPdf.GeneratePdf(school.Name, logoBytes, list, DateTime.UtcNow);
        return File(pdfBytes, "application/pdf", "RiseFlow-Parent-Welcome-Letters.pdf");
    }

    /// <summary>Generate a unique parent access code (e.g. RF-7G2B) for the school. 6-char format: RF- plus 4 from safe charset (no 0,O,1,I) so parents can type it easily. Parent enters this in the app to claim their child.</summary>
    private async Task<string> GenerateUniqueAccessCodeAsync(Guid schoolId, CancellationToken ct)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excludes 0, O, 1, I to avoid confusion
        var rng = Random.Shared;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var suffix = new string(Enumerable.Range(0, 4).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
            var code = "RF-" + suffix;
            var exists = await _db.Students.AnyAsync(s => s.SchoolId == schoolId && s.ParentAccessCode == code, ct);
            if (!exists) return code;
        }
        return "RF-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
    }

    /// <summary>Get student passport-size profile photo. Authorized: same school (SchoolAdmin/Teacher) or parent of this student.</summary>
    [HttpGet("{id:guid}/photo")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhoto(Guid id, CancellationToken ct)
    {
        var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null || string.IsNullOrEmpty(student.ProfilePhotoFileName))
            return NotFound();
        if (!await CanViewStudentAsync(id, ct))
            return Forbid();
        var root = _env.WebRootPath ?? _env.ContentRootPath;
        var path = Path.Combine(root, student.ProfilePhotoFileName.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(path))
            return NotFound();
        var contentType = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif"
            : path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
            : "image/jpeg";
        return PhysicalFile(path, contentType, enableRangeProcessing: false);
    }

    /// <summary>Upload passport-size profile photo for a student. SchoolAdmin only. Accepts .jpg, .jpeg, .png, .gif, .webp.</summary>
    [HttpPost("{id:guid}/photo")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UploadPhoto(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == _tenant.CurrentSchoolId.Value, ct);
        if (student == null)
            return NotFound();
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Allowed formats: .jpg, .jpeg, .png, .gif, .webp");
        var root = _env.WebRootPath ?? _env.ContentRootPath;
        var studentsDir = Path.Combine(root, "students", student.SchoolId.ToString("N"));
        Directory.CreateDirectory(studentsDir);
        var fileName = $"{student.Id:N}{ext}";
        var relativePath = $"students/{student.SchoolId:N}/{fileName}";
        var fullPath = Path.Combine(studentsDir, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream, ct);
        student.ProfilePhotoFileName = relativePath;
        student.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Photo uploaded.", profilePhotoFileName = relativePath });
    }

    private async Task<List<object>> GetAssignedTeachersAsync(Student student, CancellationToken ct)
    {
        if (!student.ClassId.HasValue)
            return new List<object>();

        var subjectTeachers = await _db.TeacherClassSubjects
            .AsNoTracking()
            .Include(tcs => tcs.Teacher)
            .Include(tcs => tcs.Subject)
            .Where(tcs => tcs.ClassId == student.ClassId.Value && tcs.Teacher.IsActive)
            .Select(tcs => new
            {
                teacherId = tcs.TeacherId,
                fullName = $"{tcs.Teacher.FirstName} {tcs.Teacher.LastName}".Trim(),
                roleOrSubject = tcs.Subject.Name,
                phone = tcs.Teacher.Phone
            })
            .ToListAsync(ct);

        if (subjectTeachers.Count > 0)
        {
            return subjectTeachers
                .GroupBy(t => new { t.teacherId, t.fullName, t.roleOrSubject, t.phone })
                .Select(g => (object)g.Key)
                .ToList();
        }

        var classTeachers = await _db.TeacherClasses
            .AsNoTracking()
            .Include(tc => tc.Teacher)
            .Where(tc => tc.ClassId == student.ClassId.Value && tc.Teacher.IsActive)
            .Select(tc => new
            {
                teacherId = tc.TeacherId,
                fullName = $"{tc.Teacher.FirstName} {tc.Teacher.LastName}".Trim(),
                roleOrSubject = tc.RoleInClass,
                phone = tc.Teacher.Phone
            })
            .ToListAsync(ct);

        return classTeachers.Select(t => (object)t).ToList();
    }

    private static List<StudentTermResultGroupDto> BuildTermResults(Student student)
    {
        return student.Results
            .GroupBy(r => r.TermId)
            .Select(group => new StudentTermResultGroupDto(
                Term: $"{group.First().Term.Name} {group.First().Term.AcademicYear}".Trim(),
                AveragePercentage: Math.Round(group.Average(r => r.MaxScore > 0 ? (r.Score / r.MaxScore) * 100m : 0m), 1),
                Results: group
                    .OrderBy(r => r.Subject.Name)
                    .Select(r => new StudentTermResultItemDto(
                        ResultId: r.Id,
                        Subject: r.Subject.Name,
                        Percentage: Math.Round(r.MaxScore > 0 ? (r.Score / r.MaxScore) * 100m : 0m, 1),
                        GradeLetter: r.GradeLetter))
                    .ToList()))
            .OrderByDescending(t => t.Term)
            .ToList();
    }

    private async Task<StudentProfileVisibilitySetting> GetOrCreateStudentVisibilitySettingAsync(Guid schoolId, CancellationToken ct)
    {
        var setting = await _db.StudentProfileVisibilitySettings
            .FirstOrDefaultAsync(s => s.SchoolId == schoolId, ct);

        if (setting != null)
            return setting;

        setting = new StudentProfileVisibilitySetting
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            ShowDateOfBirthToTeachers = true,
            ShowLocationDetailsToTeachers = false,
            ShowHealthDetailsToTeachers = true,
            ShowParentContactsToTeachers = false,
            ShowAcademicHistoryToTeachers = true,
            ShowPreviousRecordToTeachers = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _db.StudentProfileVisibilitySettings.Add(setting);
        await _db.SaveChangesAsync(ct);
        return setting;
    }

    private async Task<StudentPortalAccess?> GetCurrentStudentPortalAccessAsync(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return null;

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return null;

        return await _db.StudentPortalAccesses
            .AsNoTracking()
            .FirstOrDefaultAsync(spa => spa.SchoolId == _tenant.CurrentSchoolId.Value && spa.UserId == userId, ct);
    }

    private async Task<bool> CanViewStudentAsync(Guid studentId, CancellationToken ct)
    {
        if (_tenant.CurrentSchoolId.HasValue)
        {
            var inSchool = await _db.Students.AnyAsync(s => s.Id == studentId && s.SchoolId == _tenant.CurrentSchoolId.Value, ct);
            if (inSchool) return true;
        }
        var email = User.FindFirstValue(ClaimTypes.Email) ?? _tenant.CurrentUserEmail;
        if (string.IsNullOrEmpty(email) || !_tenant.CurrentSchoolId.HasValue) return false;
        var parent = await _db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.SchoolId == _tenant.CurrentSchoolId && p.Email == email, ct);
        if (parent == null) return false;
        return await _db.StudentParents.AnyAsync(sp => sp.StudentId == studentId && sp.ParentId == parent.Id, ct);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
            return NotFound();
        if (student.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();
        _db.Students.Remove(student);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record AccessCodeDto(string Code);
public record GenerateAccessCodesResult(int GeneratedCount, int TotalStudents, int StudentsWithCode);
public record StudentWithAccessCodeDto(Guid Id, string FirstName, string LastName, string? MiddleName, string? AdmissionNumber, string? ClassName, string? ParentAccessCode);
public record StudentDirectoryItemDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? AdmissionNumber,
    bool IsActive,
    string? ProfilePhotoFileName,
    StudentDirectoryClassDto? Class,
    StudentDirectoryGradeDto? Grade);
public record StudentDirectoryClassDto(Guid Id, string? Name, string? GradeName);
public record StudentDirectoryGradeDto(Guid Id, string? Name);
public record StudentSelfDashboardDto(
    Guid StudentId,
    string FullName,
    string SchoolName,
    string? ClassName,
    string? GradeName,
    string? AdmissionNumber,
    string? ProfilePhotoFileName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? StateOfOrigin,
    string? Lga,
    string? PreviousSchool,
    string? BloodGroup,
    string? Genotype,
    string? Allergies,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    IReadOnlyList<StudentDashboardParentDto> Parents,
    IReadOnlyList<StudentDashboardTeacherDto> Teachers,
    IReadOnlyList<StudentDashboardClassmateDto> Classmates);
public record StudentDashboardParentDto(Guid ParentId, string FullName, string? Relationship, string? Phone, string? WhatsAppNumber, string? Email);
public record StudentDashboardTeacherDto(Guid TeacherId, string FullName, string? RoleOrSubject, string? Email, string? Phone, string? WhatsAppNumber);
public record StudentDashboardClassmateDto(Guid StudentId, string FullName, string? ProfilePhotoFileName);
public record StudentTermResultGroupDto(string Term, decimal AveragePercentage, List<StudentTermResultItemDto> Results);
public record StudentTermResultItemDto(Guid ResultId, string Subject, decimal Percentage, string? GradeLetter);
public record UpdateStudentProfileVisibilityRequest(
    bool ShowDateOfBirthToTeachers,
    bool ShowLocationDetailsToTeachers,
    bool ShowHealthDetailsToTeachers,
    bool ShowParentContactsToTeachers,
    bool ShowAcademicHistoryToTeachers,
    bool ShowPreviousRecordToTeachers);
