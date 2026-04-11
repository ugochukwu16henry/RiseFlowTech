using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParentsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ParentsController> _logger;

    public ParentsController(RiseFlowDbContext db, ITenantContext tenant, UserManager<ApplicationUser> userManager, ILogger<ParentsController> logger)
    {
        _db = db;
        _tenant = tenant;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(typeof(List<Parent>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Parent>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var parents = await _db.Parents
            .AsNoTracking()
            .Where(p => p.SchoolId == schoolId)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(ct);

        return Ok(parents);
    }

    /// <summary>
    /// Parent signup via school gateway. AllowAnonymous. Creates ApplicationUser + Parent for the given school and assigns Parent role.
    /// </summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParentSignupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParentSignupResult>> Signup([FromBody] ParentSignupRequest request, CancellationToken ct)
    {
        if (request == null || request.SchoolId == Guid.Empty || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("SchoolId and Email are required.");
        var school = await _db.Schools.FindAsync(new object[] { request.SchoolId }, ct);
        if (school == null || !school.IsActive)
            return NotFound("School not found or inactive.");

        var email = request.Email.Trim();
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
            return BadRequest("An account with this email already exists. Please sign in and use 'Claim your child' with your access code.");

        var firstName = (request.FirstName ?? string.Empty).Trim();
        var lastName = (request.LastName ?? string.Empty).Trim();
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

        var createResult = await _userManager.CreateAsync(user, request.Password ?? string.Empty);
        if (!createResult.Succeeded)
            return BadRequest(string.Join(" ", createResult.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, Roles.Parent);
        await _userManager.AddClaimAsync(user, new Claim("SchoolId", request.SchoolId.ToString()));

        var parent = new Parent
        {
            Id = Guid.NewGuid(),
            SchoolId = request.SchoolId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone!.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Parents.Add(parent);
        await _db.SaveChangesAsync(ct);

        return Ok(new ParentSignupResult(true, "Account created. Sign in and enter your access code to link your child."));
    }

    /// <summary>
    /// Link the current parent to a student using the student's Parent Access Code.
    /// When a parent enters the code on the web/app, they are instantly linked to their child's profile.
    /// </summary>
    [HttpPost("link-by-code")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(LinkByCodeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LinkByCodeResult>> LinkByCode([FromBody] LinkByCodeRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue || string.IsNullOrWhiteSpace(request?.Code))
            return BadRequest("Code is required.");
        var schoolId = _tenant.CurrentSchoolId.Value;
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var parent = await _db.Parents.FirstOrDefaultAsync(p => p.SchoolId == schoolId && p.Email == email, ct);
        if (parent == null)
            return NotFound("Parent profile not found for this school.");

        var code = request.Code.Trim().ToUpperInvariant();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.SchoolId == schoolId && s.ParentAccessCode == code, ct);
        if (student == null)
            return NotFound("Invalid or expired access code.");

        var alreadyLinked = await _db.StudentParents.AnyAsync(sp => sp.StudentId == student.Id && sp.ParentId == parent.Id, ct);
        if (!alreadyLinked)
        {
            _db.StudentParents.Add(new StudentParent
            {
                StudentId = student.Id,
                ParentId = parent.Id,
                IsPrimaryContact = false,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        var portalAccess = await EnsureStudentPortalAccessForResponseAsync(student, ct);
        var linkedNames = await GetLinkedChildNamesAsync(parent.Id, ct);
        var message = alreadyLinked
            ? "Already linked. Student sign-in details are available from your Parent dashboard."
            : "Linked successfully. Student sign-in details have been generated on your Parent dashboard.";

        return Ok(new LinkByCodeResult(
            true,
            student.Id,
            $"{student.FirstName} {student.LastName}".Trim(),
            message,
            linkedNames,
            portalAccess));
    }

    /// <summary>
    /// List children linked to the current parent (Family View). Returns student id, name, class, and current term average.
    /// </summary>
    [HttpGet("my-children")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(List<MyChildDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MyChildDto>>> MyChildren(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var parent = await _db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.SchoolId == schoolId && p.Email == email, ct);
        if (parent == null)
            return Ok(new List<MyChildDto>());

        var linkedIds = await _db.StudentParents
            .Where(sp => sp.ParentId == parent.Id)
            .Select(sp => sp.StudentId)
            .ToListAsync(ct);
        if (linkedIds.Count == 0)
            return Ok(new List<MyChildDto>());

        var students = await _db.Students
            .AsNoTracking()
            .Include(s => s.Class)
            .Where(s => linkedIds.Contains(s.Id))
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .ToListAsync(ct);

        var currentTerm = await _db.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.IsCurrent, ct);
        var termAverages = new Dictionary<Guid, decimal>();
        if (currentTerm != null)
        {
            var results = await _db.StudentResults
                .AsNoTracking()
                .Where(r => r.TermId == currentTerm.Id && linkedIds.Contains(r.StudentId))
                .ToListAsync(ct);
            var byStudent = results.GroupBy(r => r.StudentId);
            foreach (var g in byStudent)
            {
                var totalScore = g.Sum(r => r.Score);
                var maxTotal = g.Sum(r => r.MaxScore);
                termAverages[g.Key] = maxTotal > 0 ? Math.Round((totalScore / maxTotal) * 100, 1) : 0;
            }
        }

        var list = students.Select(s => new MyChildDto(
            s.Id,
            s.FirstName,
            s.LastName,
            s.MiddleName,
            s.Class?.Name ?? "—",
            termAverages.TryGetValue(s.Id, out var avg) ? avg : (decimal?)null
        )).ToList();
        return Ok(list);
    }

    /// <summary>
    /// Parent dashboard helper: returns each linked child's generated student portal login and visibility settings.
    /// If the student login was missing, it is recreated automatically here.
    /// </summary>
    [HttpGet("student-portal-access")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(List<StudentPortalAccessSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StudentPortalAccessSummaryDto>>> GetStudentPortalAccesses(CancellationToken ct)
    {
        var parent = await GetCurrentParentAsync(ct);
        if (parent == null)
            return Ok(new List<StudentPortalAccessSummaryDto>());

        try
        {
            var linkedStudents = await _db.StudentParents
                .AsNoTracking()
                .Where(sp => sp.ParentId == parent.Id)
                .Select(sp => sp.Student)
                .Include(s => s.Class)
                .OrderBy(s => s.FirstName)
                .ThenBy(s => s.LastName)
                .ToListAsync(ct);

            var list = new List<StudentPortalAccessSummaryDto>();
            foreach (var student in linkedStudents)
            {
                try
                {
                    list.Add(await EnsureStudentPortalAccessForResponseAsync(student, ct));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Student portal access repair failed for parent {ParentId} and student {StudentId}. Returning a safe fallback row.", parent.Id, student.Id);
                    list.Add(CreateFallbackStudentPortalAccessSummary(student));
                }
            }

            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Student portal access list could not be loaded for parent {ParentId}. Returning an empty list instead.", parent.Id);
            return Ok(new List<StudentPortalAccessSummaryDto>());
        }
    }

    [HttpPut("student-portal-access/{studentId:guid}")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(StudentPortalAccessSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StudentPortalAccessSummaryDto>> UpdateStudentPortalAccess(Guid studentId, [FromBody] UpdateStudentPortalAccessRequest request, CancellationToken ct)
    {
        var student = await GetAuthorizedParentStudentAsync(studentId, ct);
        if (student == null)
            return Forbid();

        var (portalAccess, _) = await GetOrCreateStudentPortalAccessAsync(student, ct);
        portalAccess.IsEnabled = request.IsEnabled;
        portalAccess.ShowDateOfBirth = request.ShowDateOfBirth;
        portalAccess.ShowLocationDetails = request.ShowLocationDetails;
        portalAccess.ShowHealthDetails = request.ShowHealthDetails;
        portalAccess.ShowEmergencyContacts = request.ShowEmergencyContacts;
        portalAccess.ShowParentContactDetails = request.ShowParentContactDetails;
        portalAccess.ShowPreviousSchoolDetails = request.ShowPreviousSchoolDetails;
        portalAccess.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(MapStudentPortalAccessSummary(student, portalAccess));
    }

    [HttpPost("student-portal-access/{studentId:guid}/reset-password")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(ResetStudentPortalPasswordResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ResetStudentPortalPasswordResult>> ResetStudentPortalPassword(Guid studentId, CancellationToken ct)
    {
        var student = await GetAuthorizedParentStudentAsync(studentId, ct);
        if (student == null)
            return Forbid();

        var (portalAccess, _) = await GetOrCreateStudentPortalAccessAsync(student, ct);
        var user = await _userManager.FindByIdAsync(portalAccess.UserId.ToString());
        if (user == null)
            return NotFound("Student sign-in could not be found.");

        var temporaryPassword = GenerateTemporaryPassword();
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, temporaryPassword);
        if (!resetResult.Succeeded)
            return BadRequest(string.Join(" ", resetResult.Errors.Select(e => e.Description)));

        portalAccess.IsEnabled = true;
        portalAccess.CredentialsSharedAtUtc = DateTime.UtcNow;
        portalAccess.LastPasswordResetAtUtc = DateTime.UtcNow;
        portalAccess.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new ResetStudentPortalPasswordResult(
            true,
            portalAccess.LoginId,
            temporaryPassword,
            "Student sign-in password reset. Share it only with your child."));
    }

    private async Task<Student?> GetAuthorizedParentStudentAsync(Guid studentId, CancellationToken ct)
    {
        var parent = await GetCurrentParentAsync(ct);
        if (parent == null)
            return null;

        var isLinked = await _db.StudentParents.AnyAsync(sp => sp.ParentId == parent.Id && sp.StudentId == studentId, ct);
        if (!isLinked)
            return null;

        return await _db.Students
            .AsNoTracking()
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.SchoolId == parent.SchoolId, ct);
    }

    private async Task<StudentPortalAccessSummaryDto> EnsureStudentPortalAccessForResponseAsync(Student student, CancellationToken ct)
    {
        var (portalAccess, temporaryPassword) = await GetOrCreateStudentPortalAccessAsync(student, ct);
        return MapStudentPortalAccessSummary(student, portalAccess, temporaryPassword);
    }

    private async Task<(StudentPortalAccess PortalAccess, string? TemporaryPassword)> GetOrCreateStudentPortalAccessAsync(Student student, CancellationToken ct)
    {
        var portalAccess = await _db.StudentPortalAccesses
            .FirstOrDefaultAsync(spa => spa.StudentId == student.Id && spa.SchoolId == student.SchoolId, ct);

        if (portalAccess != null)
        {
            var existingUser = await _userManager.FindByIdAsync(portalAccess.UserId.ToString());
            if (existingUser != null)
            {
                await EnsureStudentUserRoleAndClaimAsync(existingUser, student.SchoolId);

                if (string.IsNullOrWhiteSpace(portalAccess.LoginId))
                {
                    portalAccess.LoginId = await GenerateUniqueStudentLoginIdAsync(student, ct);
                    portalAccess.UpdatedAtUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }

                return (portalAccess, null);
            }

            var repairedLoginId = await GenerateUniqueStudentLoginIdAsync(student, ct, portalAccess.LoginId);
            var repairedPassword = GenerateTemporaryPassword();
            var repairedUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = repairedLoginId,
                Email = repairedLoginId,
                EmailConfirmed = true,
                SchoolId = student.SchoolId,
                FullName = $"{student.FirstName} {student.LastName}".Trim(),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var repairedCreateResult = await _userManager.CreateAsync(repairedUser, repairedPassword);
            if (!repairedCreateResult.Succeeded)
                throw new InvalidOperationException(string.Join(" ", repairedCreateResult.Errors.Select(e => e.Description)));

            await EnsureStudentUserRoleAndClaimAsync(repairedUser, student.SchoolId);

            portalAccess.UserId = repairedUser.Id;
            portalAccess.LoginId = repairedLoginId;
            portalAccess.IsEnabled = true;
            portalAccess.CredentialsSharedAtUtc ??= DateTime.UtcNow;
            portalAccess.LastPasswordResetAtUtc = DateTime.UtcNow;
            portalAccess.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return (portalAccess, repairedPassword);
        }

        var loginId = await GenerateUniqueStudentLoginIdAsync(student, ct);
        var temporaryPassword = GenerateTemporaryPassword();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = loginId,
            Email = loginId,
            EmailConfirmed = true,
            SchoolId = student.SchoolId,
            FullName = $"{student.FirstName} {student.LastName}".Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, temporaryPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException(string.Join(" ", createResult.Errors.Select(e => e.Description)));

        await EnsureStudentUserRoleAndClaimAsync(user, student.SchoolId);

        portalAccess = new StudentPortalAccess
        {
            Id = Guid.NewGuid(),
            SchoolId = student.SchoolId,
            StudentId = student.Id,
            UserId = user.Id,
            LoginId = loginId,
            IsEnabled = true,
            ShowDateOfBirth = true,
            ShowLocationDetails = true,
            ShowHealthDetails = false,
            ShowEmergencyContacts = false,
            ShowParentContactDetails = false,
            ShowPreviousSchoolDetails = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CredentialsSharedAtUtc = DateTime.UtcNow,
            LastPasswordResetAtUtc = DateTime.UtcNow
        };

        _db.StudentPortalAccesses.Add(portalAccess);
        await _db.SaveChangesAsync(ct);
        return (portalAccess, temporaryPassword);
    }

    private async Task EnsureStudentUserRoleAndClaimAsync(ApplicationUser user, Guid schoolId)
    {
        if (!await _userManager.IsInRoleAsync(user, Roles.Student))
            await _userManager.AddToRoleAsync(user, Roles.Student);

        var schoolIdText = schoolId.ToString();
        var claims = await _userManager.GetClaimsAsync(user);
        if (!claims.Any(c => c.Type == "SchoolId" && string.Equals(c.Value, schoolIdText, StringComparison.OrdinalIgnoreCase)))
            await _userManager.AddClaimAsync(user, new Claim("SchoolId", schoolIdText));

        if (user.SchoolId != schoolId)
        {
            user.SchoolId = schoolId;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }
    }

    private async Task<string> GenerateUniqueStudentLoginIdAsync(Student student, CancellationToken ct, string? preferredLoginId = null)
    {
        var schoolTag = student.SchoolId.ToString("N")[..6].ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(preferredLoginId))
        {
            var normalizedPreferred = preferredLoginId.Trim().ToLowerInvariant();
            var preferredExists = await _db.StudentPortalAccesses
                .AsNoTracking()
                .AnyAsync(spa => spa.LoginId == normalizedPreferred, ct);
            var preferredUserExists = await _userManager.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserName == normalizedPreferred || u.Email == normalizedPreferred, ct);
            if (!preferredExists && !preferredUserExists)
                return normalizedPreferred;
        }

        var first = CleanLoginPart(student.FirstName);
        var last = CleanLoginPart(student.LastName);
        var admission = CleanLoginPart(student.AdmissionNumber);

        var pieces = new List<string> { first, last, admission }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        var stem = pieces.Count > 0
            ? string.Join('.', pieces)
            : $"student.{student.Id.ToString("N")[..6].ToLowerInvariant()}";
        if (stem.Length > 40)
            stem = stem[..40].Trim('.');

        for (var suffix = 0; suffix < 100; suffix++)
        {
            var candidateStem = suffix == 0 ? stem : $"{stem}{suffix}";
            var candidate = $"{candidateStem}.{schoolTag}@student.riseflow.app";
            var exists = await _db.StudentPortalAccesses
                .AsNoTracking()
                .AnyAsync(spa => spa.LoginId == candidate, ct);
            var userExists = await _userManager.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserName == candidate || u.Email == candidate, ct);
            if (!exists && !userExists)
                return candidate;
        }

        return $"student.{student.Id.ToString("N")[..8].ToLowerInvariant()}@student.riseflow.app";
    }

    private static string CleanLoginPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var cleaned = new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (cleaned.Length > 16)
            cleaned = cleaned[..16];
        return cleaned;
    }

    private static string GenerateTemporaryPassword()
        => $"RiseFlow@{Random.Shared.Next(100000, 999999)}Aa";

    private static StudentPortalAccessSummaryDto CreateFallbackStudentPortalAccessSummary(Student student)
        => new(
            student.Id,
            $"{student.FirstName} {student.LastName}".Trim(),
            student.Class?.Name,
            false,
            "Pending sync",
            true,
            true,
            false,
            false,
            false,
            false,
            null,
            null,
            null);

    private static StudentPortalAccessSummaryDto MapStudentPortalAccessSummary(Student student, StudentPortalAccess portalAccess, string? temporaryPassword = null)
        => new(
            student.Id,
            $"{student.FirstName} {student.LastName}".Trim(),
            student.Class?.Name,
            portalAccess.IsEnabled,
            portalAccess.LoginId,
            portalAccess.ShowDateOfBirth,
            portalAccess.ShowLocationDetails,
            portalAccess.ShowHealthDetails,
            portalAccess.ShowEmergencyContacts,
            portalAccess.ShowParentContactDetails,
            portalAccess.ShowPreviousSchoolDetails,
            portalAccess.CredentialsSharedAtUtc,
            portalAccess.LastPasswordResetAtUtc,
            temporaryPassword);

    private async Task<List<string>> GetLinkedChildNamesAsync(Guid parentId, CancellationToken ct)
    {
        var linkedIds = await _db.StudentParents.Where(sp => sp.ParentId == parentId).Select(sp => sp.StudentId).ToListAsync(ct);
        if (linkedIds.Count == 0) return new List<string>();
        var students = await _db.Students.AsNoTracking().Where(s => linkedIds.Contains(s.Id)).OrderBy(s => s.FirstName).ThenBy(s => s.LastName).ToListAsync(ct);
        return students.Select(s => $"{s.FirstName} {s.LastName}".Trim()).ToList();
    }

    private async Task<Parent?> GetCurrentParentAsync(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue || string.IsNullOrEmpty(_tenant.CurrentUserEmail))
            return null;
        return await _db.Parents
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SchoolId == _tenant.CurrentSchoolId && p.Email == _tenant.CurrentUserEmail, ct);
    }
}

public record MyChildDto(Guid StudentId, string FirstName, string LastName, string? MiddleName, string ClassName, decimal? TermAverage);

public record ParentSignupRequest(Guid SchoolId, string Email, string? Password, string? FirstName, string? LastName, string? Phone);
public record ParentSignupResult(bool Success, string Message);

public record LinkByCodeRequest(string Code);
public record LinkByCodeResult(bool Success, Guid StudentId, string StudentName, string Message, List<string>? LinkedChildNames = null, StudentPortalAccessSummaryDto? StudentPortalAccess = null);

public record StudentPortalAccessSummaryDto(
    Guid StudentId,
    string StudentName,
    string? ClassName,
    bool IsEnabled,
    string LoginId,
    bool ShowDateOfBirth,
    bool ShowLocationDetails,
    bool ShowHealthDetails,
    bool ShowEmergencyContacts,
    bool ShowParentContactDetails,
    bool ShowPreviousSchoolDetails,
    DateTime? CredentialsSharedAtUtc,
    DateTime? LastPasswordResetAtUtc,
    string? TemporaryPassword = null);

public record UpdateStudentPortalAccessRequest(
    bool IsEnabled,
    bool ShowDateOfBirth,
    bool ShowLocationDetails,
    bool ShowHealthDetails,
    bool ShowEmergencyContacts,
    bool ShowParentContactDetails,
    bool ShowPreviousSchoolDetails);

public record ResetStudentPortalPasswordResult(bool Success, string LoginId, string TemporaryPassword, string Message);
