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

    public ParentsController(RiseFlowDbContext db, ITenantContext tenant, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _tenant = tenant;
        _userManager = userManager;
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
            return Ok(new LinkByCodeResult(true, student.Id, $"{student.FirstName} {student.LastName}", "Already linked."));

        _db.StudentParents.Add(new StudentParent
        {
            StudentId = student.Id,
            ParentId = parent.Id,
            IsPrimaryContact = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new LinkByCodeResult(true, student.Id, $"{student.FirstName} {student.LastName}", "Linked successfully."));
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
public record LinkByCodeResult(bool Success, Guid StudentId, string StudentName, string Message);
