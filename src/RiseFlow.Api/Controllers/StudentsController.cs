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
    private readonly FileStorageService _fileStorage;
    private readonly StudentBulkUploadService _bulkUpload;
    private readonly ExcelService _excelService;
    private readonly ParentWelcomeLetterPdfService _parentLetterPdf;
    private readonly BillingService _billing;
    private readonly StudentAdmissionNumberService _admissionNumbers;
    private readonly IAuditLogService _audit;

    public StudentsController(RiseFlowDbContext db, ITenantContext tenant, IWebHostEnvironment env, FileStorageService fileStorage, StudentBulkUploadService bulkUpload, ExcelService excelService, ParentWelcomeLetterPdfService parentLetterPdf, BillingService billing, StudentAdmissionNumberService admissionNumbers, IAuditLogService audit)
    {
        _db = db;
        _tenant = tenant;
        _env = env;
        _fileStorage = fileStorage;
        _bulkUpload = bulkUpload;
        _excelService = excelService;
        _parentLetterPdf = parentLetterPdf;
        _billing = billing;
        _admissionNumbers = admissionNumbers;
        _audit = audit;
    }

    /// <summary>
    /// Get a rich "digital file" profile for a student: bio, parents, academic history, and access code.
    /// SchoolAdmin/Teacher only; tenant filter ensures isolation.
    /// </summary>
    [HttpGet("{id:guid}/profile")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher},{Roles.Parent}")]
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

        if (!await CanAccessStudentRecordAsync(student, ct))
            return Forbid();

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

    [HttpGet]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(List<StudentListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StudentListItemDto>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var list = await _db.Students
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Select(s => new StudentListItemDto(
                s.Id,
                s.FirstName,
                s.LastName,
                s.MiddleName,
                s.AdmissionNumber,
                s.Gender,
                s.IsActive,
                s.ProfilePhotoFileName,
                s.Class == null
                    ? null
                    : new StudentClassSummaryDto(
                        s.Class.Id,
                        s.Class.Name,
                        s.Class.AcademicYear,
                        s.Class.Grade == null ? null : new StudentGradeSummaryDto(s.Class.Grade.Id, s.Class.Grade.Name, s.Class.Grade.LevelOrder)),
                s.Grade == null ? null : new StudentGradeSummaryDto(s.Grade.Id, s.Grade.Name, s.Grade.LevelOrder)))
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher},{Roles.Parent}")]
    [ProducesResponseType(typeof(StudentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var student = await _db.Students
            .Include(s => s.Class)
                .ThenInclude(c => c!.Grade)
            .Include(s => s.Grade)
            .Include(s => s.StudentParents)
                .ThenInclude(sp => sp.Parent)
            .Include(s => s.Results)
                .ThenInclude(r => r.Subject)
            .Include(s => s.Results)
                .ThenInclude(r => r.Term)
            .FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == schoolId, ct);

        if (student == null)
            return NotFound();

        if (!await CanAccessStudentRecordAsync(student, ct))
            return Forbid();

        var settings = await GetStudentProfileVisibilitySettingsAsync(schoolId, ct);
        var detail = await BuildStudentDetailDtoAsync(student, settings, ct);
        return Ok(detail);
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
                return BadRequest($"Free tier limit ({CountryBillingConfig.FreeTierStudentCount} students) reached. Go to Billing & Fees to pay the activation and monthly subscription with Paystack before adding more students.");
            }
        }
        Class? schoolClass = null;
        if (request.ClassId.HasValue)
        {
            schoolClass = await _db.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClassId.Value && c.SchoolId == schoolId, ct);
            if (schoolClass == null)
                return BadRequest("Selected class was not found for this school.");
        }

        var admissionNumber = await _admissionNumbers.GetUniqueAdmissionNumberAsync(schoolId, request.AdmissionNumber, ct);

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
            AdmissionNumber = admissionNumber,
            DateOfAdmission = request.DateOfAdmission ?? DateTime.UtcNow,
            ClassId = request.ClassId,
            GradeId = request.GradeId ?? schoolClass?.GradeId,
            PreviousSchool = request.PreviousSchool,
            PreviousClass = request.PreviousClass,
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

    /// <summary>Download a simple Excel template for bulk student upload. Only core onboarding fields are requested; admission numbers are generated automatically.</summary>
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
        ws.Row(1).Style.Font.Bold = true;

        ws.Cell(2, 1).Value = "John";
        ws.Cell(2, 2).Value = "Doe";
        ws.Cell(2, 3).Value = "Michael";
        ws.Cell(2, 4).Value = "Male";
        ws.Cell(2, 5).Value = "2015-09-01";

        var notesSheet = workbook.Worksheets.Add("Notes");
        notesSheet.Cell(1, 1).Value = "Field";
        notesSheet.Cell(1, 2).Value = "Guidance";
        notesSheet.Row(1).Style.Font.Bold = true;
        notesSheet.Cell(2, 1).Value = "FirstName";
        notesSheet.Cell(2, 2).Value = "Required";
        notesSheet.Cell(3, 1).Value = "LastName";
        notesSheet.Cell(3, 2).Value = "Required";
        notesSheet.Cell(4, 1).Value = "MiddleName / Gender / DateOfBirth";
        notesSheet.Cell(4, 2).Value = "Optional during import";
        notesSheet.Cell(5, 1).Value = "AdmissionNumber";
        notesSheet.Cell(5, 2).Value = "Not needed in the sheet. RiseFlow generates it automatically during import.";
        notesSheet.Cell(6, 1).Value = "Other student details";
        notesSheet.Cell(6, 2).Value = "Can be updated later by the School Admin or parents.";

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
    [Authorize(Roles = Roles.SchoolAdmin)]
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
        Class? schoolClass = null;
        if (request.ClassId.HasValue)
        {
            schoolClass = await _db.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClassId.Value && c.SchoolId == student.SchoolId, ct);
            if (schoolClass == null)
                return BadRequest("Selected class was not found for this school.");
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
        student.AdmissionNumber = await _admissionNumbers.GetUniqueAdmissionNumberAsync(
            student.SchoolId,
            string.IsNullOrWhiteSpace(request.AdmissionNumber) ? student.AdmissionNumber : request.AdmissionNumber,
            ct,
            excludeStudentId: student.Id);
        student.DateOfAdmission = request.DateOfAdmission ?? student.DateOfAdmission ?? DateTime.UtcNow;
        student.ClassId = request.ClassId;
        student.GradeId = request.GradeId ?? schoolClass?.GradeId ?? student.GradeId;
        student.PreviousSchool = request.PreviousSchool;
        student.PreviousClass = request.PreviousClass;
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

    [HttpPut("{id:guid}/class-assignment")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(Student), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Student>> UpdateClassAssignment(Guid id, [FromBody] UpdateStudentClassAssignmentRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
            return NotFound();
        if (student.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();

        Class? schoolClass = null;
        if (request.ClassId.HasValue)
        {
            schoolClass = await _db.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClassId.Value && c.SchoolId == student.SchoolId, ct);
            if (schoolClass == null)
                return BadRequest("Selected class was not found for this school.");
        }

        student.ClassId = request.ClassId;
        student.GradeId = schoolClass?.GradeId;
        student.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(student);
    }

    [HttpPut("{id:guid}/parent-corrections")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(ParentStudentCorrectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParentStudentCorrectionResult>> UpdateParentCorrections(Guid id, [FromBody] ParentStudentCorrectionRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == schoolId, ct);
        if (student == null)
            return NotFound();

        var parent = await GetCurrentParentAsync(ct);
        if (parent == null)
            return Forbid();

        var linkedToStudent = await _db.StudentParents.AnyAsync(sp => sp.StudentId == student.Id && sp.ParentId == parent.Id, ct);
        if (!linkedToStudent)
            return Forbid();

        var nextEditAvailableAtUtc = student.ParentProfileLastUpdatedAtUtc?.AddMonths(3);
        if (nextEditAvailableAtUtc.HasValue && nextEditAvailableAtUtc.Value > DateTime.UtcNow)
        {
            return BadRequest(new ParentStudentCorrectionResult(
                false,
                $"This child profile is locked until {nextEditAvailableAtUtc.Value:dd MMM yyyy}. School Admin can still make urgent changes for you.",
                nextEditAvailableAtUtc));
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName)) student.FirstName = request.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(request.LastName)) student.LastName = request.LastName.Trim();
        student.MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        student.Nationality = string.IsNullOrWhiteSpace(request.Nationality) ? null : request.Nationality.Trim();
        student.StateOfOrigin = string.IsNullOrWhiteSpace(request.StateOfOrigin) ? null : request.StateOfOrigin.Trim();
        student.LGA = string.IsNullOrWhiteSpace(request.LGA) ? null : request.LGA.Trim();
        student.PreviousSchool = string.IsNullOrWhiteSpace(request.PreviousSchool) ? null : request.PreviousSchool.Trim();
        student.PreviousClass = string.IsNullOrWhiteSpace(request.PreviousClass) ? null : request.PreviousClass.Trim();
        student.BloodGroup = string.IsNullOrWhiteSpace(request.BloodGroup) ? null : request.BloodGroup.Trim();
        student.Genotype = string.IsNullOrWhiteSpace(request.Genotype) ? null : request.Genotype.Trim();
        student.Allergies = string.IsNullOrWhiteSpace(request.Allergies) ? null : request.Allergies.Trim();
        student.EmergencyContactName = string.IsNullOrWhiteSpace(request.EmergencyContactName) ? null : request.EmergencyContactName.Trim();
        student.EmergencyContactPhone = string.IsNullOrWhiteSpace(request.EmergencyContactPhone) ? null : request.EmergencyContactPhone.Trim();
        student.ParentProfileLastUpdatedAtUtc = DateTime.UtcNow;
        student.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var unlockAt = student.ParentProfileLastUpdatedAtUtc.Value.AddMonths(3);
        await _audit.LogAsync(
            schoolId,
            "ParentCorrection",
            nameof(Student),
            student.Id.ToString(),
            parent.Email,
            $"{parent.FirstName} {parent.LastName}".Trim(),
            $"Parent corrected student information. Next edit opens {unlockAt:yyyy-MM-dd} UTC.",
            ct);

        return Ok(new ParentStudentCorrectionResult(
            true,
            $"Child information updated successfully. The next parent correction window opens on {unlockAt:dd MMM yyyy}.",
            unlockAt));
    }

    [HttpGet("profile-visibility-settings")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    [ProducesResponseType(typeof(StudentProfileVisibilitySettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentProfileVisibilitySettingsDto>> GetProfileVisibilitySettings(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var settings = await GetStudentProfileVisibilitySettingsAsync(_tenant.CurrentSchoolId.Value, ct);
        return Ok(MapVisibilitySettings(settings));
    }

    [HttpPut("profile-visibility-settings")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(StudentProfileVisibilitySettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentProfileVisibilitySettingsDto>> UpdateProfileVisibilitySettings([FromBody] UpdateStudentProfileVisibilitySettingsRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var settings = await _db.StudentProfileVisibilitySettings
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId, ct);

        if (settings == null)
        {
            settings = new StudentProfileVisibilitySetting
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.StudentProfileVisibilitySettings.Add(settings);
        }

        settings.ShowDateOfBirthToTeachers = request.ShowDateOfBirthToTeachers;
        settings.ShowLocationDetailsToTeachers = request.ShowLocationDetailsToTeachers;
        settings.ShowHealthDetailsToTeachers = request.ShowHealthDetailsToTeachers;
        settings.ShowParentContactsToTeachers = request.ShowParentContactsToTeachers;
        settings.ShowAcademicHistoryToTeachers = request.ShowAcademicHistoryToTeachers;
        settings.ShowPreviousRecordToTeachers = request.ShowPreviousRecordToTeachers;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(
            schoolId,
            "TeacherViewRules",
            nameof(StudentProfileVisibilitySetting),
            settings.Id.ToString(),
            _tenant.CurrentUserEmail,
            User.Identity?.Name,
            "School admin updated teacher visibility controls for student records.",
            ct);

        return Ok(MapVisibilitySettings(settings));
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
            var path = _fileStorage.ResolveReadPath(school.LogoFileName);
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
        var path = _fileStorage.ResolveReadPath(student.ProfilePhotoFileName);
        if (!System.IO.File.Exists(path))
            return NotFound();
        var contentType = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif"
            : path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
            : "image/jpeg";
        return PhysicalFile(path, contentType, enableRangeProcessing: false);
    }

    /// <summary>Upload passport-size profile photo for a student. SchoolAdmin and linked parents can update it. Accepts .jpg, .jpeg, .png, .gif, .webp.</summary>
    [HttpPost("{id:guid}/photo")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Parent}")]
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

        if (User.IsInRole(Roles.Parent))
        {
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? _tenant.CurrentUserEmail;
            if (string.IsNullOrWhiteSpace(email))
                return Forbid();

            var parent = await _db.Parents
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.SchoolId == student.SchoolId && p.Email == email, ct);
            if (parent == null)
                return Forbid();

            var linkedToChild = await _db.StudentParents.AnyAsync(sp => sp.StudentId == student.Id && sp.ParentId == parent.Id, ct);
            if (!linkedToChild)
                return Forbid();
        }

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Allowed formats: .jpg, .jpeg, .png, .gif, .webp");
        var fileName = $"{student.Id:N}{ext}";
        var relativePath = $"students/{student.SchoolId:N}/{fileName}";
        var fullPath = _fileStorage.EnsureWritePath(relativePath);
        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream, ct);
        student.ProfilePhotoFileName = relativePath;
        student.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Photo uploaded.", profilePhotoFileName = relativePath });
    }

    private async Task<bool> CanViewStudentAsync(Guid studentId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return false;

        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId && s.SchoolId == _tenant.CurrentSchoolId.Value, ct);

        if (student == null)
            return false;

        return await CanAccessStudentRecordAsync(student, ct);
    }

    private async Task<bool> CanAccessStudentRecordAsync(Student student, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue || student.SchoolId != _tenant.CurrentSchoolId.Value)
            return false;

        if (User.IsInRole(Roles.SchoolAdmin))
            return true;

        if (User.IsInRole(Roles.Teacher))
            return await CanTeacherAccessStudentAsync(student, ct);

        if (User.IsInRole(Roles.Parent))
        {
            var parent = await GetCurrentParentAsync(ct);
            if (parent == null)
                return false;

            return await _db.StudentParents.AnyAsync(sp => sp.StudentId == student.Id && sp.ParentId == parent.Id, ct);
        }

        return false;
    }

    private async Task<bool> CanTeacherAccessStudentAsync(Student student, CancellationToken ct)
    {
        if (!student.ClassId.HasValue || !_tenant.CurrentSchoolId.HasValue)
            return false;

        var email = _tenant.CurrentUserEmail ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var teacher = await _db.Teachers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SchoolId == _tenant.CurrentSchoolId.Value && t.Email == email, ct);

        if (teacher == null)
            return false;

        var classId = student.ClassId.Value;
        var hasDirectClass = await _db.TeacherClasses.AnyAsync(tc => tc.TeacherId == teacher.Id && tc.ClassId == classId, ct);
        if (hasDirectClass)
            return true;

        return await _db.TeacherClassSubjects.AnyAsync(tcs => tcs.TeacherId == teacher.Id && tcs.ClassId == classId, ct);
    }

    private async Task<Parent?> GetCurrentParentAsync(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return null;

        var email = _tenant.CurrentUserEmail ?? User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _db.Parents
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SchoolId == _tenant.CurrentSchoolId.Value && p.Email == email, ct);
    }

    private async Task<StudentProfileVisibilitySetting> GetStudentProfileVisibilitySettingsAsync(Guid schoolId, CancellationToken ct)
    {
        return await _db.StudentProfileVisibilitySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId, ct)
            ?? new StudentProfileVisibilitySetting
            {
                Id = Guid.Empty,
                SchoolId = schoolId,
                CreatedAtUtc = DateTime.UtcNow,
                ShowDateOfBirthToTeachers = true,
                ShowLocationDetailsToTeachers = false,
                ShowHealthDetailsToTeachers = true,
                ShowParentContactsToTeachers = false,
                ShowAcademicHistoryToTeachers = true,
                ShowPreviousRecordToTeachers = false
            };
    }

    private StudentProfileVisibilitySettingsDto MapVisibilitySettings(StudentProfileVisibilitySetting settings)
        => new(
            settings.ShowDateOfBirthToTeachers,
            settings.ShowLocationDetailsToTeachers,
            settings.ShowHealthDetailsToTeachers,
            settings.ShowParentContactsToTeachers,
            settings.ShowAcademicHistoryToTeachers,
            settings.ShowPreviousRecordToTeachers);

    private async Task<List<StudentAssignedTeacherDto>> GetAssignedTeachersAsync(Student student, CancellationToken ct)
    {
        if (!student.ClassId.HasValue)
            return new List<StudentAssignedTeacherDto>();

        var classId = student.ClassId.Value;

        var subjectAssignments = await _db.TeacherClassSubjects
            .AsNoTracking()
            .Include(tcs => tcs.Teacher)
            .Include(tcs => tcs.Subject)
            .Where(tcs => tcs.ClassId == classId && tcs.Teacher.IsActive)
            .Select(tcs => new StudentAssignedTeacherDto(
                tcs.TeacherId,
                $"{tcs.Teacher.FirstName} {tcs.Teacher.LastName}".Trim(),
                tcs.Subject.Name,
                tcs.Teacher.Email,
                tcs.Teacher.Phone,
                tcs.Teacher.WhatsAppNumber ?? tcs.Teacher.Phone))
            .ToListAsync(ct);

        var classAssignments = await _db.TeacherClasses
            .AsNoTracking()
            .Include(tc => tc.Teacher)
            .Where(tc => tc.ClassId == classId && tc.Teacher.IsActive)
            .Select(tc => new StudentAssignedTeacherDto(
                tc.TeacherId,
                $"{tc.Teacher.FirstName} {tc.Teacher.LastName}".Trim(),
                tc.RoleInClass ?? "Class Teacher",
                tc.Teacher.Email,
                tc.Teacher.Phone,
                tc.Teacher.WhatsAppNumber ?? tc.Teacher.Phone))
            .ToListAsync(ct);

        return subjectAssignments
            .Concat(classAssignments)
            .GroupBy(t => new { t.TeacherId, t.RoleOrSubject, t.Email, t.Phone, t.WhatsAppNumber })
            .Select(g => g.First())
            .OrderBy(t => t.FullName)
            .ThenBy(t => t.RoleOrSubject)
            .ToList();
    }

    private async Task<StudentDetailDto> BuildStudentDetailDtoAsync(Student student, StudentProfileVisibilitySetting settings, CancellationToken ct)
    {
        var isSchoolAdmin = User.IsInRole(Roles.SchoolAdmin);
        var isParent = User.IsInRole(Roles.Parent);
        var isTeacher = User.IsInRole(Roles.Teacher);

        var parents = student.StudentParents
            .OrderBy(sp => sp.Parent.LastName)
            .ThenBy(sp => sp.Parent.FirstName)
            .Select(sp => new StudentParentSummaryDto(
                sp.ParentId,
                sp.Parent.FirstName,
                sp.Parent.LastName,
                sp.Parent.Email,
                sp.Parent.Phone,
                sp.RelationshipToStudent,
                sp.IsPrimaryContact))
            .ToList();

        var academicHistory = student.Results
            .OrderByDescending(r => r.Term.StartDate)
            .ThenBy(r => r.Subject.Name)
            .Select(r =>
            {
                var percentage = r.MaxScore > 0 ? (r.Score / r.MaxScore) * 100m : 0m;
                return new StudentAcademicHistoryItem(
                    r.Id,
                    $"{r.Term.Name} {r.Term.AcademicYear}",
                    r.Subject.Name,
                    r.AssessmentType,
                    r.Score,
                    r.MaxScore,
                    decimal.Round(percentage, 1),
                    r.GradeLetter);
            })
            .ToList();

        if (isTeacher && !settings.ShowParentContactsToTeachers)
            parents = new List<StudentParentSummaryDto>();

        if (isTeacher && !settings.ShowAcademicHistoryToTeachers)
            academicHistory = new List<StudentAcademicHistoryItem>();

        var termResults = academicHistory
            .GroupBy(r => r.Term)
            .Select(g => new StudentTermSummaryDto(
                g.Key,
                decimal.Round(g.Average(x => x.Percentage), 1),
                g.ToList()))
            .ToList();

        var currentAverage = academicHistory.Count > 0
            ? decimal.Round(academicHistory.Average(x => x.Percentage), 1)
            : 0m;

        var dateOfBirth = student.DateOfBirth;
        var nationality = student.Nationality;
        var stateOfOrigin = student.StateOfOrigin;
        var lga = student.LGA;
        var previousSchool = student.PreviousSchool;
        var previousClass = student.PreviousClass;
        var bloodGroup = student.BloodGroup;
        var genotype = student.Genotype;
        var allergies = student.Allergies;
        var emergencyContactName = student.EmergencyContactName;
        var emergencyContactPhone = student.EmergencyContactPhone;
        var nin = student.NIN;
        var nationalIdNumber = student.NationalIdNumber;
        var parentAccessCode = isSchoolAdmin ? student.ParentAccessCode : null;

        if (isTeacher)
        {
            nin = null;
            nationalIdNumber = null;
            parentAccessCode = null;

            if (!settings.ShowDateOfBirthToTeachers)
                dateOfBirth = null;

            if (!settings.ShowLocationDetailsToTeachers)
            {
                nationality = null;
                stateOfOrigin = null;
                lga = null;
            }

            if (!settings.ShowHealthDetailsToTeachers)
            {
                bloodGroup = null;
                genotype = null;
                allergies = null;
                emergencyContactName = null;
                emergencyContactPhone = null;
            }

            if (!settings.ShowPreviousRecordToTeachers)
            {
                previousSchool = null;
                previousClass = null;
            }
        }

        var parentEditLockedUntilUtc = student.ParentProfileLastUpdatedAtUtc?.AddMonths(3);
        var canParentEdit = isParent && (!parentEditLockedUntilUtc.HasValue || parentEditLockedUntilUtc.Value <= DateTime.UtcNow);
        string? parentEditMessage = null;
        if (isParent)
        {
            parentEditMessage = canParentEdit
                ? "You can correct this child profile now. After saving, the form locks for 3 months."
                : $"This form is locked until {parentEditLockedUntilUtc:dd MMM yyyy}. School Admin can still help with urgent changes.";
        }

        return new StudentDetailDto(
            student.Id,
            student.SchoolId,
            student.FirstName,
            student.LastName,
            student.MiddleName,
            dateOfBirth,
            student.Gender,
            nationality,
            stateOfOrigin,
            lga,
            nin,
            student.NationalIdType,
            nationalIdNumber,
            student.AdmissionNumber,
            student.DateOfAdmission,
            previousSchool,
            previousClass,
            bloodGroup,
            genotype,
            allergies,
            emergencyContactName,
            emergencyContactPhone,
            parentAccessCode,
            student.ProfilePhotoFileName,
            student.IsActive,
            student.CreatedAtUtc,
            student.UpdatedAtUtc,
            student.Class == null
                ? null
                : new StudentClassSummaryDto(
                    student.Class.Id,
                    student.Class.Name,
                    student.Class.AcademicYear,
                    student.Class.Grade == null ? null : new StudentGradeSummaryDto(student.Class.Grade.Id, student.Class.Grade.Name, student.Class.Grade.LevelOrder)),
            student.Grade == null ? null : new StudentGradeSummaryDto(student.Grade.Id, student.Grade.Name, student.Grade.LevelOrder),
            parents,
            await GetAssignedTeachersAsync(student, ct),
            academicHistory,
            termResults,
            currentAverage,
            MapVisibilitySettings(settings),
            isSchoolAdmin || canParentEdit,
            canParentEdit,
            isSchoolAdmin,
            parentEditLockedUntilUtc,
            parentEditMessage);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.SchoolAdmin)]
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

public record UpdateStudentClassAssignmentRequest(Guid? ClassId);
public record StudentGradeSummaryDto(Guid Id, string Name, int LevelOrder);
public record StudentClassSummaryDto(Guid Id, string Name, string? AcademicYear, StudentGradeSummaryDto? Grade);
public record StudentListItemDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? AdmissionNumber,
    string? Gender,
    bool IsActive,
    string? ProfilePhotoFileName,
    StudentClassSummaryDto? Class,
    StudentGradeSummaryDto? Grade);
public record StudentParentSummaryDto(
    Guid ParentId,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? RelationshipToStudent,
    bool IsPrimaryContact);
public record StudentDetailDto(
    Guid Id,
    Guid SchoolId,
    string FirstName,
    string LastName,
    string? MiddleName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? StateOfOrigin,
    string? LGA,
    string? NIN,
    string? NationalIdType,
    string? NationalIdNumber,
    string? AdmissionNumber,
    DateTime? DateOfAdmission,
    string? PreviousSchool,
    string? PreviousClass,
    string? BloodGroup,
    string? Genotype,
    string? Allergies,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? ParentAccessCode,
    string? ProfilePhotoFileName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    StudentClassSummaryDto? Class,
    StudentGradeSummaryDto? Grade,
    List<StudentParentSummaryDto> StudentParents,
    List<StudentAssignedTeacherDto> AssignedTeachers,
    List<StudentAcademicHistoryItem> AcademicHistory,
    List<StudentTermSummaryDto> TermResults,
    decimal CurrentAveragePercentage,
    StudentProfileVisibilitySettingsDto TeacherVisibilitySettings,
    bool CanEdit,
    bool CanParentEdit,
    bool CanManageTeacherVisibility,
    DateTime? ParentEditLockedUntilUtc,
    string? ParentEditMessage);
public record StudentAssignedTeacherDto(Guid TeacherId, string FullName, string? RoleOrSubject, string? Email, string? Phone, string? WhatsAppNumber);
public record StudentTermSummaryDto(string Term, decimal AveragePercentage, List<StudentAcademicHistoryItem> Results);
public record StudentProfileVisibilitySettingsDto(
    bool ShowDateOfBirthToTeachers,
    bool ShowLocationDetailsToTeachers,
    bool ShowHealthDetailsToTeachers,
    bool ShowParentContactsToTeachers,
    bool ShowAcademicHistoryToTeachers,
    bool ShowPreviousRecordToTeachers);
public record AccessCodeDto(string Code);
public record GenerateAccessCodesResult(int GeneratedCount, int TotalStudents, int StudentsWithCode);
public record StudentWithAccessCodeDto(Guid Id, string FirstName, string LastName, string? MiddleName, string? AdmissionNumber, string? ClassName, string? ParentAccessCode);
