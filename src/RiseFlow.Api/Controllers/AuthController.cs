using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RiseFlowDbContext _db;

    public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, RiseFlowDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("Auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new LoginResponse(false, "Email and password are required.", null, null));

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user == null || !user.IsActive)
            return Unauthorized(new LoginResponse(false, "Invalid credentials.", null, null));

        var result = await _signInManager.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: false);
        if (!result.Succeeded)
            return Unauthorized(new LoginResponse(false, "Invalid credentials.", null, null));

        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault();
        var schoolId = await EnsureSchoolContextAsync(user, roles);
        return Ok(new LoginResponse(true, "Signed in.", primaryRole, schoolId));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    private async Task<Guid?> EnsureSchoolContextAsync(ApplicationUser user, IList<string> roles)
    {
        Guid? schoolId = user.SchoolId;
        var claims = await _userManager.GetClaimsAsync(user);

        if (!schoolId.HasValue)
        {
            var existingClaimValue = claims.FirstOrDefault(c => c.Type == "SchoolId")?.Value
                ?? claims.FirstOrDefault(c => c.Type == ClaimTypes.GroupSid)?.Value;

            if (!string.IsNullOrWhiteSpace(existingClaimValue) && Guid.TryParse(existingClaimValue, out var parsedClaimSchoolId))
                schoolId = parsedClaimSchoolId;
        }

        if (!schoolId.HasValue)
        {
            var email = user.Email?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(email))
            {
                if (roles.Contains(Roles.SchoolAdmin))
                {
                    schoolId = await _db.Schools
                        .AsNoTracking()
                        .Where(s => s.IsActive && s.Email != null && s.Email.ToUpper() == email)
                        .Select(s => (Guid?)s.Id)
                        .FirstOrDefaultAsync();
                }

                if (!schoolId.HasValue && roles.Contains(Roles.Teacher))
                {
                    schoolId = await _db.Teachers
                        .AsNoTracking()
                        .Where(t => t.Email != null && t.Email.ToUpper() == email)
                        .Select(t => (Guid?)t.SchoolId)
                        .FirstOrDefaultAsync();
                }

                if (!schoolId.HasValue && roles.Contains(Roles.Parent))
                {
                    schoolId = await _db.Parents
                        .AsNoTracking()
                        .Where(p => p.Email != null && p.Email.ToUpper() == email)
                        .Select(p => (Guid?)p.SchoolId)
                        .FirstOrDefaultAsync();
                }
            }
        }

        if (!schoolId.HasValue)
            return null;

        var schoolIdText = schoolId.Value.ToString();
        var needsUserUpdate = user.SchoolId != schoolId;
        if (needsUserUpdate)
        {
            user.SchoolId = schoolId;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        var existingSchoolClaims = claims.Where(c => c.Type == "SchoolId").ToList();
        foreach (var claim in existingSchoolClaims.Where(c => !string.Equals(c.Value, schoolIdText, StringComparison.OrdinalIgnoreCase)))
            await _userManager.RemoveClaimAsync(user, claim);

        if (!existingSchoolClaims.Any(c => string.Equals(c.Value, schoolIdText, StringComparison.OrdinalIgnoreCase)))
            await _userManager.AddClaimAsync(user, new Claim("SchoolId", schoolIdText));

        await _signInManager.RefreshSignInAsync(user);
        return schoolId;
    }

    /// <summary>Request password reset (rate limited). When implemented, sends email; for now returns 200 to avoid email enumeration.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("Auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email))
            return Ok(new ForgotPasswordResponse("If an account exists for this email, you will receive reset instructions."));
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user != null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            // TODO: send email with reset link (e.g. /reset-password?token=...&email=...). Use same rate limit as login.
        }
        return Ok(new ForgotPasswordResponse("If an account exists for this email, you will receive reset instructions."));
    }
}

public record LoginRequest(string Email, string Password);
public record LoginResponse(bool Success, string Message, string? PrimaryRole, Guid? SchoolId);
public record ForgotPasswordRequest(string? Email);
public record ForgotPasswordResponse(string Message);
