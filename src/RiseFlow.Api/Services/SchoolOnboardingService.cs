using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Constants;

namespace RiseFlow.Api.Services;

/// <summary>
/// School onboarding: create a new tenant (school) and optionally its first admin user.
/// </summary>
public class SchoolOnboardingService
{
    private readonly RiseFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public SchoolOnboardingService(RiseFlowDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<SchoolOnboardingResult> OnboardSchoolAsync(OnboardSchoolRequest request, CancellationToken ct = default)
    {
        var school = new School
        {
            Id = Guid.NewGuid(),
            Name = request.SchoolName,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Schools.Add(school);

        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = request.AdminEmail,
                Email = request.AdminEmail,
                EmailConfirmed = false,
                SchoolId = school.Id,
                FullName = request.AdminFullName ?? request.AdminEmail,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, request.AdminPassword ?? throw new ArgumentException("Admin password required when admin email is provided."));
            if (!createResult.Succeeded)
                return SchoolOnboardingResult.CreateFailed(createResult.Errors.Select(e => e.Description).ToList());

            await _userManager.AddToRoleAsync(user, Roles.SchoolAdmin);
            await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("SchoolId", school.Id.ToString()));
        }

        await _db.SaveChangesAsync(ct);
        return SchoolOnboardingResult.CreateSuccess(school.Id, school.Name);
    }

    public async Task<School?> GetSchoolByIdAsync(Guid schoolId, CancellationToken ct = default)
    {
        return await _db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct);
    }

    public async Task<List<School>> ListSchoolsAsync(CancellationToken ct = default)
    {
        return await _db.Schools.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);
    }
}

public record OnboardSchoolRequest(
    string SchoolName,
    string? Address,
    string? Phone,
    string? Email,
    string? AdminEmail,
    string? AdminPassword,
    string? AdminFullName);

public record SchoolOnboardingResult(bool Success, Guid? SchoolId, string? SchoolName, IReadOnlyList<string> Errors)
{
    public static SchoolOnboardingResult CreateSuccess(Guid schoolId, string schoolName) =>
        new(true, schoolId, schoolName, Array.Empty<string>());

    public static SchoolOnboardingResult CreateFailed(IReadOnlyList<string> errors) =>
        new(false, null, null, errors);
}
