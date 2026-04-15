using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Constants;
using System.Text.RegularExpressions;

namespace RiseFlow.Api.Services;

/// <summary>
/// School onboarding: create a new tenant (school) and optionally its first admin user and logo.
/// </summary>
public class SchoolOnboardingService
{
    private readonly RiseFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FileStorageService _fileStorage;
    private readonly AffiliateService _affiliateService;

    public SchoolOnboardingService(RiseFlowDbContext db, UserManager<ApplicationUser> userManager, FileStorageService fileStorage, AffiliateService affiliateService)
    {
        _db = db;
        _userManager = userManager;
        _fileStorage = fileStorage;
        _affiliateService = affiliateService;
    }

    public async Task<SchoolOnboardingResult> OnboardSchoolAsync(OnboardSchoolRequest request, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var affiliate = await _affiliateService.FindByReferralCodeAsync(request.ReferralCode, ct);

            var school = new School
            {
                Id = Guid.NewGuid(),
                Name = request.SchoolName,
                Address = request.Address,
                SchoolType = request.SchoolType,
                PrincipalName = request.PrincipalName,
                OwnerName = request.PrincipalName,
                SchoolAdminName = request.AdminFullName,
                Phone = request.Phone,
                WhatsAppNumber = request.Phone,
                Email = request.Email,
                CacNumber = request.CacNumber,
                AffiliateId = affiliate?.Id,
                AffiliateReferralCodeUsed = affiliate?.UniqueCode,
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

            await ProvisionAcademicSetupAsync(school.Id, request, ct);

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
        if (school != null)
        {
            if (!string.IsNullOrWhiteSpace(logoPath))
                school.LogoFileName = logoPath;
            if (!string.IsNullOrWhiteSpace(cacDocumentPath))
                school.RegistrationDocumentPath = cacDocumentPath;
            school.UpdatedAtUtc = DateTime.UtcNow;
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

        var fileName = $"{schoolId:N}{ext}";
        var relativePath = $"{folderName}/{fileName}";

        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;
            await _fileStorage.UploadAsync(relativePath, ms, file.ContentType, ct);
        }

        _db.FileAssets.Add(new FileAsset
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            OriginalFileName = file.FileName,
            StoredFileName = fileName,
            RelativePath = relativePath,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            FileBytes = null,
            Category = folderName,
            UploadedBy = null,
            UploadedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return relativePath;
    }

    public async Task<School?> GetSchoolByIdAsync(Guid schoolId, CancellationToken ct = default)
    {
        return await _db.Schools.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == schoolId, ct);
    }

    public async Task<List<School>> ListSchoolsAsync(CancellationToken ct = default)
    {
        return await _db.Schools.AsNoTracking().IgnoreQueryFilters().OrderBy(s => s.Name).ToListAsync(ct);
    }

    private async Task ProvisionAcademicSetupAsync(Guid schoolId, OnboardSchoolRequest request, CancellationToken ct)
    {
        var countryCode = (request.CountryCode ?? "NG").Trim().ToUpperInvariant();

        var selectedClassLevels = NormalizeDistinct(request.SelectedClassLevels);
        var customClassLevels = NormalizeDistinct(request.CustomClassLevels);
        var classLevels = selectedClassLevels.Concat(customClassLevels).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (classLevels.Count == 0)
            classLevels = GetDefaultClassLevels(countryCode).ToList();

        var selectedSubjects = NormalizeDistinct(request.SelectedSubjects);
        var customSubjects = NormalizeDistinct(request.CustomSubjects);
        var subjects = selectedSubjects.Concat(customSubjects).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (subjects.Count == 0)
            subjects = GetDefaultSubjects(countryCode).ToList();

        var existingGrades = await _db.Grades
            .Where(g => g.SchoolId == schoolId)
            .ToListAsync(ct);

        var nextLevelOrder = existingGrades.Count == 0 ? 1 : existingGrades.Max(x => x.LevelOrder) + 1;
        var gradeByName = existingGrades.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var newGrades = new List<Grade>();
        foreach (var classLevel in classLevels)
        {
            if (gradeByName.ContainsKey(classLevel))
                continue;

            var grade = new Grade
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                Name = classLevel,
                LevelOrder = nextLevelOrder++,
                CreatedAtUtc = DateTime.UtcNow
            };
            gradeByName[classLevel] = grade;
            newGrades.Add(grade);
        }

        if (newGrades.Count > 0)
            _db.Grades.AddRange(newGrades);

        var existingClasses = await _db.Classes
            .Where(c => c.SchoolId == schoolId)
            .ToListAsync(ct);
        var classKeys = new HashSet<string>(
            existingClasses.Select(c => (c.Name ?? string.Empty).Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var classLevel in classLevels)
        {
            var classKey = classLevel;
            if (classKeys.Contains(classKey))
                continue;

            _db.Classes.Add(new Class
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                GradeId = gradeByName[classLevel].Id,
                Grade = gradeByName[classLevel],
                Name = classLevel,
                CreatedAtUtc = DateTime.UtcNow
            });

            classKeys.Add(classKey);
        }

        var existingSubjects = await _db.Subjects
            .Where(s => s.SchoolId == schoolId)
            .ToListAsync(ct);
        var existingSubjectNames = new HashSet<string>(existingSubjects.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        var existingSubjectCodes = new HashSet<string>(
            existingSubjects
                .Where(s => !string.IsNullOrWhiteSpace(s.Code))
                .Select(s => s.Code!),
            StringComparer.OrdinalIgnoreCase);
        foreach (var subjectName in subjects)
        {
            if (existingSubjectNames.Contains(subjectName))
                continue;

            var subjectCode = GenerateUniqueSubjectCode(subjectName, existingSubjectCodes);
            _db.Subjects.Add(new Subject
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                Name = subjectName,
                Code = subjectCode,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

            existingSubjectCodes.Add(subjectCode);
            existingSubjectNames.Add(subjectName);
        }
    }

    private static List<string> NormalizeDistinct(IEnumerable<string>? values)
    {
        if (values == null)
            return new List<string>();

        return values
            .Select(v => (v ?? string.Empty).Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetDefaultClassLevels(string countryCode)
    {
        return countryCode switch
        {
            "GH" =>
            [
                "Creche / Daycare", "Playgroup", "KG 1", "KG 2", "Primary 1", "Primary 2", "Primary 3", "Primary 4", "Primary 5", "Primary 6",
                "JHS 1", "JHS 2", "JHS 3", "SHS 1", "SHS 2", "SHS 3"
            ],
            "KE" =>
            [
                "Daycare", "Playgroup", "PP1", "PP2", "Grade 1", "Grade 2", "Grade 3", "Grade 4", "Grade 5", "Grade 6",
                "Junior Secondary 1", "Junior Secondary 2", "Junior Secondary 3", "Senior Secondary 1", "Senior Secondary 2", "Senior Secondary 3"
            ],
            "SN" or "CI" or "MA" =>
            [
                "Creche", "Pre-maternelle", "Maternelle 1", "Maternelle 2",
                "CP1", "CP2", "CE1", "CE2", "CM1", "CM2",
                "College 1", "College 2", "College 3", "College 4",
                "Lycee 1", "Lycee 2", "Lycee 3"
            ],
            _ =>
            [
                "Creche / Daycare", "Pre-Nursery / Playgroup", "Nursery 1", "Nursery 2", "Primary 1", "Primary 2", "Primary 3", "Primary 4", "Primary 5", "Primary 6",
                "JSS 1", "JSS 2", "JSS 3", "SS 1", "SS 2", "SS 3"
            ]
        };
    }

    private static IReadOnlyList<string> GetDefaultSubjects(string countryCode)
    {
        return countryCode switch
        {
            "GH" =>
            [
                "English Language", "Mathematics", "Integrated Science", "Social Studies", "Creative Arts", "Religious and Moral Education",
                "Computing", "Career Technology", "Economics", "Literature"
            ],
            "KE" =>
            [
                "English", "Kiswahili", "Mathematics", "Integrated Science", "Social Studies", "Agriculture",
                "Creative Arts", "Computer Science", "Business Studies", "Life Skills"
            ],
            "SN" or "CI" or "MA" =>
            [
                "Francais", "Mathematiques", "Sciences", "Geographie", "Education civique", "Technologie", "Informatique"
            ],
            _ =>
            [
                "English Language", "Mathematics", "Basic Science", "Social Studies", "Civic Education", "Computer Studies",
                "Agricultural Science", "Business Studies", "Literature in English", "Economics"
            ]
        };
    }

    private static string GenerateUniqueSubjectCode(string subjectName, ISet<string> existingCodes)
    {
        var normalized = Regex.Replace(subjectName.ToUpperInvariant(), "[^A-Z0-9]+", string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = "SUBJECT";

        var baseCode = normalized.Length > 10 ? normalized[..10] : normalized;
        var candidate = baseCode;
        var suffix = 2;

        while (existingCodes.Contains(candidate))
        {
            var suffixText = suffix.ToString();
            var prefixLength = Math.Max(1, 10 - suffixText.Length);
            var prefix = baseCode.Length > prefixLength ? baseCode[..prefixLength] : baseCode;
            candidate = $"{prefix}{suffixText}";
            suffix++;
        }

        return candidate;
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
    IReadOnlyList<string>? SelectedClassLevels = null,
    IReadOnlyList<string>? CustomClassLevels = null,
    IReadOnlyList<string>? SelectedSubjects = null,
    IReadOnlyList<string>? CustomSubjects = null,
    string? ReferralCode = null,
    /// <summary>Required when creating an admin account. Must be true to comply with ToS and Data Processing Agreement.</summary>
    bool AgreedToTermsAndDpa = false);

public record SchoolOnboardingResult(bool Success, Guid? SchoolId, string? SchoolName, IReadOnlyList<string> Errors, string? LogoPath = null, string? CacDocumentPath = null)
{
    public static SchoolOnboardingResult CreateSuccess(Guid schoolId, string schoolName) =>
        new(true, schoolId, schoolName, Array.Empty<string>());

    public static SchoolOnboardingResult CreateFailed(IReadOnlyList<string> errors) =>
        new(false, null, null, errors);
}
