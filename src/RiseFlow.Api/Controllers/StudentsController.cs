using ClosedXML.Excel;
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
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    private readonly StudentBulkUploadService _bulkUpload;

    public StudentsController(RiseFlowDbContext db, ITenantContext tenant, StudentBulkUploadService bulkUpload)
    {
        _db = db;
        _tenant = tenant;
        _bulkUpload = bulkUpload;
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

    [HttpPost]
    [ProducesResponseType(typeof(Student), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Student>> Create([FromBody] CreateStudentRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = new Student
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
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

    /// <summary>Download Excel template for bulk student upload. Headers: FirstName, LastName, MiddleName, AdmissionNumber, Gender, DateOfBirth.</summary>
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
        ws.Cell(1, 4).Value = "AdmissionNumber";
        ws.Cell(1, 5).Value = "Gender";
        ws.Cell(1, 6).Value = "DateOfBirth";
        ws.Row(1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "John";
        ws.Cell(2, 2).Value = "Doe";
        ws.Cell(2, 6).Value = "2015-09-01";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream, false);
        stream.Position = 0;
        const string fileName = "RiseFlow-Students-Template.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>Bulk upload students from Excel. SchoolAdmin only. Template: Row 1 = headers (FirstName, LastName, MiddleName, AdmissionNumber, Gender, DateOfBirth).</summary>
    [HttpPost("bulk-upload")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(BulkUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkUploadResult>> BulkUpload(IFormFile file, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");
        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .xlsx files are supported.");
        await using var stream = file.OpenReadStream();
        var result = await _bulkUpload.UploadFromExcelAsync(stream, _tenant.CurrentSchoolId.Value, ct);
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

    private async Task<string> GenerateUniqueAccessCodeAsync(Guid schoolId, CancellationToken ct)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = Random.Shared;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var code = new string(Enumerable.Range(0, 8).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
            var exists = await _db.Students.AnyAsync(s => s.SchoolId == schoolId && s.ParentAccessCode == code, ct);
            if (!exists) return code;
        }
        return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
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
        _db.Students.Remove(student);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record AccessCodeDto(string Code);
public record GenerateAccessCodesResult(int GeneratedCount, int TotalStudents, int StudentsWithCode);
