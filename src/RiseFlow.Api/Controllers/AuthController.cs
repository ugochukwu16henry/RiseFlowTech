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
            return Unauthorized(new LoginResponse(false, "Email or login ID and password are required.", null, null));

        var loginId = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(loginId)
            ?? await _userManager.FindByNameAsync(loginId);
        if (user == null || !user.IsActive)
            return Unauthorized(new LoginResponse(false, "Invalid credentials.", null, null));

        var result = await _signInManager.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: false);
        if (!result.Succeeded)
            return Unauthorized(new LoginResponse(false, "Invalid credentials.", null, null));

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(Roles.Student))
        {
            var portalAccess = await _db.StudentPortalAccesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == user.Id);

            if (portalAccess == null || !portalAccess.IsEnabled)
            {
                await _signInManager.SignOutAsync();
                return Unauthorized(new LoginResponse(false, "Your student portal is not active yet. Ask your parent to open and share it from the Parent dashboard.", null, user.SchoolId));
            }
        }

        var primaryRole = roles.FirstOrDefault();
        return Ok(new LoginResponse(true, "Signed in.", primaryRole, user.SchoolId));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
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
