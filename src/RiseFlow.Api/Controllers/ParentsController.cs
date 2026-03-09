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
}

public record ParentSignupRequest(Guid SchoolId, string Email, string? Password, string? FirstName, string? LastName, string? Phone);
public record ParentSignupResult(bool Success, string Message);

public record LinkByCodeRequest(string Code);
public record LinkByCodeResult(bool Success, Guid StudentId, string StudentName, string Message);
