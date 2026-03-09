using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeachersController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly Services.ITenantContext _tenant;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public TeachersController(RiseFlowDbContext db, Services.ITenantContext tenant, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _db = db;
        _tenant = tenant;
        _userManager = userManager;
        _env = env;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Teacher>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Teacher>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var list = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses)
            .ThenInclude(tc => tc.Class)
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Teacher), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Teacher>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var teacher = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses)
            .ThenInclude(tc => tc.Class)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
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
        _db.Teachers.Remove(teacher);
        await _db.SaveChangesAsync(ct);
        return NoContent();
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

        await _userManager.AddToRoleAsync(user, Constants.Roles.Teacher);
        await _userManager.AddClaimAsync(user, new Claim("SchoolId", request.SchoolId.ToString()));

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
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Teachers.Add(teacher);
        await _db.SaveChangesAsync(ct);

        return Ok(new TeacherSignupResult(true, "Account created. Sign in as a teacher. Your school admin will assign your classes and subjects."));
    }

    /// <summary>Current teacher profile (by email + school). Teacher only.</summary>
    [HttpGet("me")]
    [Authorize(Roles = Constants.Roles.Teacher)]
    [ProducesResponseType(typeof(Teacher), StatusCodes.Status200OK)]
    public async Task<ActionResult<Teacher>> Me(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrEmpty(email))
            return Forbid();
        var teacher = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses).ThenInclude(tc => tc.Class)
            .FirstOrDefaultAsync(t => t.SchoolId == _tenant.CurrentSchoolId.Value && t.Email == email, ct);
        if (teacher == null)
            return Ok(null);
        return Ok(teacher);
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AssignToClass(Guid teacherId, Guid classId, [FromBody] AssignTeacherToClassRequest? request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var exists = await _db.TeacherClasses.AnyAsync(tc => tc.TeacherId == teacherId && tc.ClassId == classId, ct);
        if (exists)
            return NoContent();
        var link = new TeacherClass
        {
            TeacherId = teacherId,
            ClassId = classId,
            RoleInClass = request?.RoleInClass,
            AssignedAtUtc = DateTime.UtcNow
        };
        _db.TeacherClasses.Add(link);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{teacherId:guid}/classes/{classId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UnassignFromClass(Guid teacherId, Guid classId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var link = await _db.TeacherClasses.FirstOrDefaultAsync(tc => tc.TeacherId == teacherId && tc.ClassId == classId, ct);
        if (link != null)
        {
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
    [Authorize(Roles = $"{Constants.Roles.SchoolAdmin},{Constants.Roles.Teacher}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UploadPhoto(Guid id, IFormFile? file, CancellationToken ct)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null)
            return NotFound();
        if (_tenant.CurrentSchoolId.HasValue && teacher.SchoolId != _tenant.CurrentSchoolId.Value)
            return Forbid();

        // If uploading as Teacher, ensure this is their own profile
        if (User.IsInRole(Constants.Roles.Teacher))
        {
            var email = _tenant.CurrentUserEmail ?? User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email) || !string.Equals(email, teacher.Email, StringComparison.OrdinalIgnoreCase))
                return Forbid();
        }

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var allowed = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Allowed formats: .jpg, .jpeg, .png, .gif, .webp");

        var root = _env.WebRootPath ?? _env.ContentRootPath;
        var dir = Path.Combine(root, "teachers", teacher.SchoolId.ToString("N"));
        Directory.CreateDirectory(dir);
        var fileName = $"{teacher.Id:N}{ext}";
        var relativePath = $"teachers/{teacher.SchoolId:N}/{fileName}";
        var fullPath = Path.Combine(dir, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream, ct);

        teacher.ProfilePhotoFileName = relativePath;
        teacher.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Photo uploaded.", profilePhotoFileName = relativePath });
    }
}

public record MyStudentDto(Guid StudentId, string FirstName, string LastName, string? MiddleName, string? AdmissionNumber, string? ClassName, string? Gender);
