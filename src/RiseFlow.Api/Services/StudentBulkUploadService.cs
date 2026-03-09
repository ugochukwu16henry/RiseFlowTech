using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

/// <summary>
/// Bulk upload students from Excel (.xlsx). Template: Row 1 = headers (FirstName, LastName, MiddleName, AdmissionNumber, Gender, DateOfBirth).
/// Skips rows that duplicate an existing student (same admission number or same first+last+DOB in school) or duplicate another row in the same file.
/// </summary>
public class StudentBulkUploadService
{
    private readonly RiseFlowDbContext _db;

    public StudentBulkUploadService(RiseFlowDbContext db)
    {
        _db = db;
    }

    public async Task<BulkUploadResult> UploadFromExcelAsync(Stream excelStream, Guid schoolId, CancellationToken ct = default)
    {
        var errors = new List<string>();
        using var workbook = new XLWorkbook(excelStream);
        var ws = workbook.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2) return new BulkUploadResult(0, 0, errors);

        var existingKeys = await GetExistingStudentKeysAsync(schoolId, ct);
        var withinFileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var skipped = 0;

        for (var row = 2; row <= lastRow; row++)
        {
            if (ct.IsCancellationRequested) break;
            var firstName = GetCellString(ws, row, 1);
            var lastName = GetCellString(ws, row, 2);
            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                continue;
            if (string.IsNullOrWhiteSpace(firstName)) firstName = "—";
            if (string.IsNullOrWhiteSpace(lastName)) lastName = "—";

            var middleName = GetCellString(ws, row, 3);
            var admissionNumber = GetCellString(ws, row, 4);
            var gender = GetCellString(ws, row, 5);
            DateOnly? dateOfBirth = null;
            var dobStr = GetCellString(ws, row, 6);
            if (!string.IsNullOrWhiteSpace(dobStr))
            {
                if (DateOnly.TryParse(dobStr, out var dob))
                    dateOfBirth = dob;
                else if (double.TryParse(dobStr, out var oa))
                    try { dateOfBirth = DateOnly.FromDateTime(DateTime.FromOADate(oa)); } catch { /* ignore */ }
            }

            var key = StudentDuplicateKey(admissionNumber, firstName, lastName, dateOfBirth);
            if (existingKeys.Contains(key) || withinFileKeys.Contains(key))
            {
                skipped++;
                continue;
            }
            withinFileKeys.Add(key);

            var student = new Student
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim(),
                AdmissionNumber = string.IsNullOrWhiteSpace(admissionNumber) ? null : admissionNumber.Trim(),
                Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim(),
                DateOfBirth = dateOfBirth,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Students.Add(student);
            created++;
        }

        if (created > 0)
            await _db.SaveChangesAsync(ct);
        return new BulkUploadResult(created, skipped, errors);
    }

    private static string StudentDuplicateKey(string? admissionNumber, string firstName, string lastName, DateOnly? dateOfBirth)
    {
        if (!string.IsNullOrWhiteSpace(admissionNumber))
            return "A:" + admissionNumber.Trim().ToUpperInvariant();
        var first = (firstName ?? "").Trim().ToUpperInvariant();
        var last = (lastName ?? "").Trim().ToUpperInvariant();
        var dob = dateOfBirth?.ToString("O") ?? "";
        return "N:" + first + "|" + last + "|" + dob;
    }

    private async Task<HashSet<string>> GetExistingStudentKeysAsync(Guid schoolId, CancellationToken ct)
    {
        var students = await _db.Students
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId && s.IsActive)
            .Select(s => new { s.AdmissionNumber, s.FirstName, s.LastName, s.DateOfBirth })
            .ToListAsync(ct);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in students)
            set.Add(StudentDuplicateKey(s.AdmissionNumber, s.FirstName, s.LastName, s.DateOfBirth));
        return set;
    }

    private static string GetCellString(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        var v = cell.GetString();
        return v ?? string.Empty;
    }
}

public record BulkUploadResult(int CreatedCount, int SkippedDuplicateCount, IReadOnlyList<string> Errors);
