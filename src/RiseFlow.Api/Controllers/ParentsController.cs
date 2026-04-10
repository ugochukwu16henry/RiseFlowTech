using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
public class ParentsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly UserManager<ApplicationUser> _userManager;

    public ParentsController(RiseFlowDbContext db, ITenantContext tenant, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenant = tenant;
        _userManager = userManager;
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

        var createResult = await _userManager.CreateAsync(user, request.Password ?? "");
        if (!createResult.Succeeded)
            return BadRequest(string.Join(" ", createResult.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, Roles.Parent);
        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("SchoolId", request.SchoolId.ToString()));

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
        if (alreadyLinked)
        {
            var linkedNames = await GetLinkedChildNamesAsync(parent.Id, ct);
            var existingPortal = await EnsureStudentPortalAccessAsync(student, ct);
            return Ok(new LinkByCodeResult(true, student.Id, $"{student.FirstName} {student.LastName}", "Already linked.", linkedNames, existingPortal));
        }

        _db.StudentParents.Add(new StudentParent
        {
            StudentId = student.Id,
            ParentId = parent.Id,
            IsPrimaryContact = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        var portal = await EnsureStudentPortalAccessAsync(student, ct);
        var names = await GetLinkedChildNamesAsync(parent.Id, ct);
        return Ok(new LinkByCodeResult(true, student.Id, $"{student.FirstName} {student.LastName}", "Linked successfully.", names, portal));
    }

    private async Task<List<string>> GetLinkedChildNamesAsync(Guid parentId, CancellationToken ct)
    {
        var linkedIds = await _db.StudentParents.Where(sp => sp.ParentId == parentId).Select(sp => sp.StudentId).ToListAsync(ct);
        if (linkedIds.Count == 0) return new List<string>();
        var students = await _db.Students.AsNoTracking().Where(s => linkedIds.Contains(s.Id)).OrderBy(s => s.FirstName).ThenBy(s => s.LastName).ToListAsync(ct);
        return students.Select(s => $"{s.FirstName} {s.LastName}".Trim()).ToList();
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

    [HttpGet("student-portal-access/{studentId:guid}")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(StudentPortalAccessSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentPortalAccessSummaryDto>> GetStudentPortalAccess(Guid studentId, CancellationToken ct)
    {
        var linked = await GetManagedStudentAsync(studentId, ct);
        if (linked == null)
            return Forbid();

        var summary = await EnsureStudentPortalAccessAsync(linked, ct);
        return Ok(summary);
    }

    [HttpPut("student-portal-access/{studentId:guid}")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(StudentPortalAccessSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentPortalAccessSummaryDto>> UpdateStudentPortalAccess(Guid studentId, [FromBody] UpdateStudentPortalAccessRequest request, CancellationToken ct)
    {
        var linked = await GetManagedStudentAsync(studentId, ct);
        if (linked == null)
            return Forbid();

        var access = await _db.StudentPortalAccesses
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.StudentId == linked.Id, ct);

        if (access == null)
        {
            await EnsureStudentPortalAccessAsync(linked, ct);
            access = await _db.StudentPortalAccesses
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.StudentId == linked.Id, ct);
        }

        if (access == null)
            return NotFound("Student portal access could not be created.");

        access.IsEnabled = request.IsEnabled;
        access.ShowDateOfBirth = request.ShowDateOfBirth;
        access.ShowLocationDetails = request.ShowLocationDetails;
        access.ShowHealthDetails = request.ShowHealthDetails;
        access.ShowEmergencyContacts = request.ShowEmergencyContacts;
        access.ShowParentContactDetails = request.ShowParentContactDetails;
        access.ShowPreviousSchoolDetails = request.ShowPreviousSchoolDetails;
        access.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(MapStudentPortalAccessSummary(linked, access));
    }

    [HttpPost("student-portal-access/{studentId:guid}/reset-password")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(StudentPortalAccessSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentPortalAccessSummaryDto>> ResetStudentPortalPassword(Guid studentId, CancellationToken ct)
    {
        var linked = await GetManagedStudentAsync(studentId, ct);
        if (linked == null)
            return Forbid();

        var summary = await EnsureStudentPortalAccessAsync(linked, ct, forcePasswordReset: true);
        return Ok(summary);
    }

    private async Task<Student?> GetManagedStudentAsync(Guid studentId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return null;

        var parent = await GetCurrentParentAsync(ct);
        if (parent == null)
            return null;

        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId && s.SchoolId == _tenant.CurrentSchoolId.Value, ct);
        if (student == null)
            return null;

        var linked = await _db.StudentParents.AnyAsync(sp => sp.StudentId == studentId && sp.ParentId == parent.Id, ct);
        return linked ? student : null;
    }

    private async Task<StudentPortalAccessSummaryDto> EnsureStudentPortalAccessAsync(Student student, CancellationToken ct, bool forcePasswordReset = false)
    {
        var access = await _db.StudentPortalAccesses
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.StudentId == student.Id, ct);

        string? temporaryPassword = null;
        var now = DateTime.UtcNow;

        if (access == null || access.User == null)
        {
            temporaryPassword = GenerateTemporaryPassword();
            var loginId = await GenerateStudentLoginIdAsync(student, ct);
            var pseudoEmail = $"{loginId}@student.riseflow.local";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = loginId,
                Email = pseudoEmail,
                EmailConfirmed = true,
                SchoolId = student.SchoolId,
                FullName = $"{student.FirstName} {student.LastName}".Trim(),
                IsActive = true,
                CreatedAtUtc = now
            };

            var createResult = await _userManager.CreateAsync(user, temporaryPassword);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(string.Join(" ", createResult.Errors.Select(e => e.Description)));

            var roleResult = await _userManager.AddToRoleAsync(user, Roles.Student);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(e => e.Description)));

            access = new StudentPortalAccess
            {
                Id = Guid.NewGuid(),
                SchoolId = student.SchoolId,
                StudentId = student.Id,
                UserId = user.Id,
                User = user,
                LoginId = loginId,
                IsEnabled = true,
                ShowDateOfBirth = true,
                ShowLocationDetails = true,
                ShowHealthDetails = false,
                ShowEmergencyContacts = false,
                ShowParentContactDetails = false,
                ShowPreviousSchoolDetails = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CredentialsSharedAtUtc = now,
                LastPasswordResetAtUtc = now
            };

            _db.StudentPortalAccesses.Add(access);
            await _db.SaveChangesAsync(ct);
        }
        else if (forcePasswordReset)
        {
            temporaryPassword = GenerateTemporaryPassword();
            var token = await _userManager.GeneratePasswordResetTokenAsync(access.User);
            var resetResult = await _userManager.ResetPasswordAsync(access.User, token, temporaryPassword);
            if (!resetResult.Succeeded)
                throw new InvalidOperationException(string.Join(" ", resetResult.Errors.Select(e => e.Description)));

            access.IsEnabled = true;
            access.UpdatedAtUtc = now;
            access.CredentialsSharedAtUtc = now;
            access.LastPasswordResetAtUtc = now;
            await _db.SaveChangesAsync(ct);
        }

        return MapStudentPortalAccessSummary(student, access, temporaryPassword);
    }

    private async Task<string> GenerateStudentLoginIdAsync(Student student, CancellationToken ct)
    {
        var basePart = NormalizeLoginPart(student.AdmissionNumber);
        if (string.IsNullOrWhiteSpace(basePart))
        {
            basePart = NormalizeLoginPart($"{student.FirstName}{student.LastName}");
        }

        var baseLogin = $"stu-{basePart}";
        if (baseLogin.Length > 28)
            baseLogin = baseLogin[..28];

        var candidate = baseLogin;
        var suffix = 1;
        while (await _db.StudentPortalAccesses.AnyAsync(x => x.SchoolId == student.SchoolId && x.LoginId == candidate, ct))
        {
            suffix++;
            candidate = $"{baseLogin}-{suffix}";
        }

        return candidate;
    }

    private static string NormalizeLoginPart(string? value)
    {
        var cleaned = new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return string.IsNullOrWhiteSpace(cleaned)
            ? Guid.NewGuid().ToString("N")[..8]
            : cleaned;
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        var upperChar = upper[Random.Shared.Next(upper.Length)];
        var lowerChar = lower[Random.Shared.Next(lower.Length)];
        var number = Random.Shared.Next(100, 999);
        return $"Rise{number}{upperChar}{lowerChar}9";
    }

    private static StudentPortalAccessSummaryDto MapStudentPortalAccessSummary(Student student, StudentPortalAccess access, string? temporaryPassword = null)
        => new(
            student.Id,
            $"{student.FirstName} {student.LastName}".Trim(),
            access.LoginId,
            "/login",
            access.IsEnabled,
            access.CreatedAtUtc,
            access.CredentialsSharedAtUtc,
            access.LastPasswordResetAtUtc,
            new StudentPortalVisibilityDto(
                access.ShowDateOfBirth,
                access.ShowLocationDetails,
                access.ShowHealthDetails,
                access.ShowEmergencyContacts,
                access.ShowParentContactDetails,
                access.ShowPreviousSchoolDetails),
            temporaryPassword);

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
public record LinkByCodeResult(bool Success, Guid StudentId, string StudentName, string Message, List<string>? LinkedChildNames = null, StudentPortalAccessSummaryDto? StudentPortal = null);
public record StudentPortalVisibilityDto(
    bool ShowDateOfBirth,
    bool ShowLocationDetails,
    bool ShowHealthDetails,
    bool ShowEmergencyContacts,
    bool ShowParentContactDetails,
    bool ShowPreviousSchoolDetails);
public record StudentPortalAccessSummaryDto(
    Guid StudentId,
    string StudentName,
    string LoginId,
    string LoginPath,
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime? CredentialsSharedAtUtc,
    DateTime? LastPasswordResetAtUtc,
    StudentPortalVisibilityDto Visibility,
    string? TemporaryPassword = null);
