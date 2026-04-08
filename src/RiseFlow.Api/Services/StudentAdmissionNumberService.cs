using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;

namespace RiseFlow.Api.Services;

/// <summary>
/// Generates school-scoped student admission numbers and guarantees uniqueness at the application layer.
/// Format example: RIS-2026-0001.
/// </summary>
public class StudentAdmissionNumberService
{
    private readonly RiseFlowDbContext _db;

    public StudentAdmissionNumberService(RiseFlowDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetUniqueAdmissionNumberAsync(
        Guid schoolId,
        string? requestedAdmissionNumber,
        CancellationToken ct = default,
        ISet<string>? reservedNumbers = null,
        Guid? excludeStudentId = null)
    {
        var normalizedRequested = Normalize(requestedAdmissionNumber);
        if (!string.IsNullOrWhiteSpace(normalizedRequested)
            && !await AdmissionNumberExistsAsync(schoolId, normalizedRequested, ct, reservedNumbers, excludeStudentId))
        {
            reservedNumbers?.Add(normalizedRequested);
            return normalizedRequested;
        }

        return await GenerateNextAsync(schoolId, ct, reservedNumbers, excludeStudentId);
    }

    private async Task<string> GenerateNextAsync(Guid schoolId, CancellationToken ct, ISet<string>? reservedNumbers, Guid? excludeStudentId)
    {
        var schoolName = await _db.Schools
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.Id == schoolId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);

        var prefix = BuildPrefix(schoolName);
        var year = DateTime.UtcNow.Year;
        var stem = $"{prefix}-{year}-";

        var existingAdmissionNumbers = await _db.Students
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.SchoolId == schoolId && s.AdmissionNumber != null)
            .Select(s => s.AdmissionNumber!)
            .ToListAsync(ct);

        var maxSequence = 0;
        foreach (var existing in existingAdmissionNumbers)
        {
            var normalized = Normalize(existing);
            if (!normalized.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
                continue;

            var suffix = normalized[stem.Length..];
            if (int.TryParse(suffix, out var parsed) && parsed > maxSequence)
            {
                maxSequence = parsed;
            }
        }

        var baseSequence = maxSequence + 1;

        for (var offset = 0; offset < 10000; offset++)
        {
            var candidate = $"{stem}{baseSequence + offset:0000}";
            if (await AdmissionNumberExistsAsync(schoolId, candidate, ct, reservedNumbers, excludeStudentId))
                continue;

            reservedNumbers?.Add(candidate);
            return candidate;
        }

        var fallback = $"{prefix}-{year}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        reservedNumbers?.Add(fallback);
        return fallback;
    }

    private async Task<bool> AdmissionNumberExistsAsync(Guid schoolId, string admissionNumber, CancellationToken ct, ISet<string>? reservedNumbers, Guid? excludeStudentId)
    {
        if (reservedNumbers?.Contains(admissionNumber) == true)
            return true;

        var normalizedAdmissionNumber = Normalize(admissionNumber);

        return await _db.Students
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                s => s.SchoolId == schoolId
                    && s.AdmissionNumber != null
                    && s.AdmissionNumber.ToUpper() == normalizedAdmissionNumber
                    && (!excludeStudentId.HasValue || s.Id != excludeStudentId.Value),
                ct);
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string BuildPrefix(string? schoolName)
    {
        var letters = new string((schoolName ?? "School")
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (letters.Length >= 3)
            return letters[..3];
        if (letters.Length == 2)
            return letters + "S";
        if (letters.Length == 1)
            return letters + "CH";

        return "SCH";
    }
}
