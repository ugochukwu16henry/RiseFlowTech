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

    public StudentsController(RiseFlowDbContext db, ITenantContext tenant, IWebHostEnvironment env, StudentBulkUploadService bulkUpload, ExcelService excelService, ParentWelcomeLetterPdfService parentLetterPdf, BillingService billing)
    {
        _db = db;
        _tenant = tenant;
        _env = env;
        _bulkUpload = bulkUpload;
        _excelService = excelService;
        _parentLetterPdf = parentLetterPdf;
        _billing = billing;
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
            .OrderBy(p => p.Label)
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
    [ProducesResponseType(typeof(List<Student>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Student>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var list = await _db.Students
            .AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.Grade)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Student), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Student>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.Grade)
            .Include(s => s.StudentParents)
            .ThenInclude(sp => sp.Parent)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
            return NotFound();
        return Ok(student);
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
