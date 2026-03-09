using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Constants;

namespace RiseFlow.Api.Services;

/// <summary>
/// School onboarding: create a new tenant (school) and optionally its first admin user and logo.
/// </summary>
public class SchoolOnboardingService
{
    private readonly RiseFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public SchoolOnboardingService(RiseFlowDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
    }

    public async Task<SchoolOnboardingResult> OnboardSchoolAsync(OnboardSchoolRequest request, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var school = new School
            {
                Id = Guid.NewGuid(),
                Name = request.SchoolName,
                Address = request.Address,
                SchoolType = request.SchoolType,
                PrincipalName = request.PrincipalName,
                Phone = request.Phone,
                Email = request.Email,
                CacNumber = request.CacNumber,
                CountryCode = request.CountryCode?.Trim().ToUpperInvariant(),
                CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "NGN" : request.CurrencyCode.Trim().ToUpperInvariant(),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                TermsAndDpaAgreedAt = request.AgreedToTermsAndDpa ? DateTime.UtcNow : (DateTime?)null
            };

            _db.Schools.Add(school);

            if (!string.IsNullOrWhiteSpace(request.AdminEmail))
            {
                if (string.IsNullOrWhiteSpace(request.AdminPassword))
                    throw new ArgumentException("Admin password required when admin email is provided.");

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

                var createResult = await _userManager.CreateAsync(user, request.AdminPassword);
                if (!createResult.Succeeded)
                {
                    await transaction.RollbackAsync(ct);
                    return SchoolOnboardingResult.CreateFailed(createResult.Errors.Select(e => e.Description).ToList());
                }

                await _userManager.AddToRoleAsync(user, Roles.SchoolAdmin);
                await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("SchoolId", school.Id.ToString()));
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return SchoolOnboardingResult.CreateSuccess(school.Id, school.Name);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Onboard a school with optional logo and CAC document upload. Use from multipart/form-data endpoint.</summary>
    public async Task<SchoolOnboardingResult> OnboardSchoolWithLogoAsync(OnboardSchoolRequest request, IFormFile? logo, IFormFile? cacDocument, CancellationToken ct = default)
    {
        var result = await OnboardSchoolAsync(request, ct);
        if (!result.Success || !result.SchoolId.HasValue)
            return result;

        var schoolId = result.SchoolId.Value;
        var logoPath = await SaveUploadedFileAsync(logo, schoolId, "logos", new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" }, ".png", ct);
        var cacDocumentPath = await SaveUploadedFileAsync(cacDocument, schoolId, "cac", new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp" }, ".pdf", ct);

        var school = await _db.Schools.FirstOrDefaultAsync(s => s.Id == result.SchoolId.Value, ct);
        if (school != null && !string.IsNullOrWhiteSpace(logoPath))
        {
            school.LogoFileName = logoPath;
            await _db.SaveChangesAsync(ct);
        }

        return result with { LogoPath = logoPath, CacDocumentPath = cacDocumentPath };
    }

    private async Task<string?> SaveUploadedFileAsync(
        IFormFile? file,
        Guid schoolId,
        string folderName,
        IReadOnlyCollection<string> allowedExtensions,
        string defaultExtension,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return null;

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = defaultExtension;

        if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return null;

        var root = _env.WebRootPath ?? _env.ContentRootPath;
        var dir = Path.Combine(root, folderName);
        Directory.CreateDirectory(dir);

        var fileName = $"{schoolId:N}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, ct);

        return $"{folderName}/{fileName}";
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
    string? SchoolType,
    string? PrincipalName,
    string? Phone,
    string? Email,
    string? CacNumber,
    string? CountryCode,
    string? CurrencyCode,
    string? AdminEmail,
    string? AdminPassword,
    string? AdminFullName,
    /// <summary>Required when creating an admin account. Must be true to comply with ToS and Data Processing Agreement.</summary>
    bool AgreedToTermsAndDpa = false);

public record SchoolOnboardingResult(bool Success, Guid? SchoolId, string? SchoolName, IReadOnlyList<string> Errors, string? LogoPath = null, string? CacDocumentPath = null)
{
    public static SchoolOnboardingResult CreateSuccess(Guid schoolId, string schoolName) =>
        new(true, schoolId, schoolName, Array.Empty<string>());

    public static SchoolOnboardingResult CreateFailed(IReadOnlyList<string> errors) =>
        new(false, null, null, errors);
}
