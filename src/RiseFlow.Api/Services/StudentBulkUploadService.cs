using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

/// <summary>
/// Bulk upload students from Excel (.xlsx). Template: Row 1 = headers (FirstName, LastName, MiddleName, AdmissionNumber, Gender, DateOfBirth).
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
        var result = new BulkUploadResult(0, new List<string>());
        using var workbook = new XLWorkbook(excelStream);
        var ws = workbook.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2) return result;

        var created = 0;
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
        return new BulkUploadResult(created, result.Errors);
    }

    private static string GetCellString(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        var v = cell.GetString();
        return v ?? string.Empty;
    }
}

public record BulkUploadResult(int CreatedCount, IReadOnlyList<string> Errors);
