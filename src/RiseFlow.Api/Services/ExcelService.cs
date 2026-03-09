using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

/// <summary>
/// Excel import for students: template aligned with African ministry requirements (NIN, NationalIdType, Class, Parent, etc.).
/// Supports preview, validation (highlight missing name/NIN per country), and "First 50 Free" billing message.
/// </summary>
public class ExcelService
{
    private readonly RiseFlowDbContext _db;

    // Column indices for template: FirstName, LastName, MiddleName, Gender, DateOfBirth, NIN, NationalIdType, NationalIdNumber, Class, AdmissionNumber, StateOfOrigin, LGA, Nationality, ParentName, ParentPhone, BloodGroup, Genotype, EmergencyContactName, EmergencyContactPhone
    private const int ColFirstName = 1, ColLastName = 2, ColMiddleName = 3, ColGender = 4, ColDateOfBirth = 5;
    private const int ColNIN = 6, ColNationalIdType = 7, ColNationalIdNumber = 8, ColClass = 9, ColAdmissionNumber = 10;
    private const int ColStateOfOrigin = 11, ColLGA = 12, ColNationality = 13, ColParentName = 14, ColParentPhone = 15;
    private const int ColBloodGroup = 16, ColGenotype = 17, ColEmergencyContactName = 18, ColEmergencyContactPhone = 19;

    public ExcelService(RiseFlowDbContext db)
    {
        _db = db;
    }

    /// <summary>Parse and validate Excel; return preview rows and per-row errors. Does not save.</summary>
    public async Task<ExcelPreviewResult> GetPreviewAsync(Stream excelStream, Guid schoolId, int previewMaxRows = 5, CancellationToken ct = default)
    {
        var previewRows = new List<ExcelPreviewRow>();
        var validationErrors = new List<ExcelRowValidation>();
        int totalRows = 0;

        using var workbook = new XLWorkbook(excelStream);
        var ws = workbook.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
            return new ExcelPreviewResult(previewRows, validationErrors, 0);

        var classes = await _db.Classes
            .AsNoTracking()
            .Where(c => c.SchoolId == schoolId)
            .ToDictionaryAsync(c => c.Name.Trim().ToUpperInvariant(), c => c.Id, StringComparer.OrdinalIgnoreCase, ct);

        for (var row = 2; row <= lastRow; row++)
        {
            if (ct.IsCancellationRequested) break;
            totalRows++;
            var (firstName, lastName, middleName, gender, dob, nin, nationalIdType, nationalIdNumber, className, admissionNumber,
                 stateOfOrigin, lga, nationality, parentName, parentPhone, bloodGroup, genotype, emergencyName, emergencyPhone) = ReadRow(ws, row);

            var errors = ValidateRow(row, firstName, lastName, nin, nationalIdNumber, nationalIdType, className, classes);
            foreach (var e in errors)
                validationErrors.Add(e);

            var classId = ResolveClassId(className, classes);
            var preview = new ExcelPreviewRow(
                row,
                firstName ?? "",
                lastName ?? "",
                middleName,
                gender,
                dob,
                nin,
                nationalIdType,
                nationalIdNumber,
                className,
                admissionNumber,
                stateOfOrigin,
                lga,
                nationality,
                parentName,
                parentPhone,
                bloodGroup,
                genotype,
                emergencyName,
                emergencyPhone,
                classId,
                errors.Count > 0
            );
            if (row <= 1 + previewMaxRows)
                previewRows.Add(preview);
        }

        return new ExcelPreviewResult(previewRows, validationErrors, totalRows);
    }

    /// <summary>Import valid rows; return created count, error rows for download, and billing message.</summary>
    public async Task<ExcelImportResult> ImportAsync(Stream excelStream, Guid schoolId, CancellationToken ct = default)
    {
        var (validStudents, errorRows, validationErrors) = await ParseAndValidateAllAsync(excelStream, schoolId, ct);
        if (validStudents.Count == 0 && errorRows.Count == 0)
            return new ExcelImportResult(0, 0, errorRows, validationErrors, "No valid rows to import.");

        var existingCount = await _db.Students.CountAsync(s => s.SchoolId == schoolId && s.IsActive, ct);
        var school = await _db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        var currencyCode = school?.CurrencyCode?.Trim() ?? "NGN";
        var classes = await _db.Classes.Where(c => c.SchoolId == schoolId).ToDictionaryAsync(c => c.Name.Trim().ToUpperInvariant(), c => c, StringComparer.OrdinalIgnoreCase, ct);
        var parentCache = new Dictionary<string, Parent>(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in validStudents)
        {
            if (ct.IsCancellationRequested) break;
            Guid? classId = ResolveClassId(dto.ClassName, classes.ToDictionary(c => c.Key, c => c.Value.Id));

            var student = new Student
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName.Trim(),
                Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim(),
                DateOfBirth = dto.DateOfBirth,
                NIN = string.IsNullOrWhiteSpace(dto.NIN) ? null : dto.NIN.Trim(),
                NationalIdType = string.IsNullOrWhiteSpace(dto.NationalIdType) ? null : dto.NationalIdType.Trim(),
                NationalIdNumber = string.IsNullOrWhiteSpace(dto.NationalIdNumber) ? null : dto.NationalIdNumber.Trim(),
                AdmissionNumber = string.IsNullOrWhiteSpace(dto.AdmissionNumber) ? null : dto.AdmissionNumber.Trim(),
                StateOfOrigin = string.IsNullOrWhiteSpace(dto.StateOfOrigin) ? null : dto.StateOfOrigin.Trim(),
                LGA = string.IsNullOrWhiteSpace(dto.LGA) ? null : dto.LGA.Trim(),
                Nationality = string.IsNullOrWhiteSpace(dto.Nationality) ? null : dto.Nationality.Trim(),
                BloodGroup = string.IsNullOrWhiteSpace(dto.BloodGroup) ? null : dto.BloodGroup.Trim(),
                Genotype = string.IsNullOrWhiteSpace(dto.Genotype) ? null : dto.Genotype.Trim(),
                EmergencyContactName = string.IsNullOrWhiteSpace(dto.EmergencyContactName) ? null : dto.EmergencyContactName.Trim(),
                EmergencyContactPhone = string.IsNullOrWhiteSpace(dto.EmergencyContactPhone) ? null : dto.EmergencyContactPhone.Trim(),
                ClassId = classId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Students.Add(student);

            if (!string.IsNullOrWhiteSpace(dto.ParentName) || !string.IsNullOrWhiteSpace(dto.ParentPhone))
            {
                var parent = GetOrCreateParentInBatch(schoolId, dto.ParentName, dto.ParentPhone, parentCache, _db);
                _db.StudentParents.Add(new StudentParent
                {
                    StudentId = student.Id,
                    ParentId = parent.Id,
                    IsPrimaryContact = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        if (validStudents.Count > 0)
            await _db.SaveChangesAsync(ct);

        var newTotal = existingCount + validStudents.Count;
        var billingMessage = BillingService.ComputeAmountDue(newTotal, currencyCode) == 0
            ? $"Successfully imported {validStudents.Count} student(s). Your first {CountryBillingConfig.FreeTierStudentCount} students are free."
            : $"Successfully imported {validStudents.Count} student(s). Your first {CountryBillingConfig.FreeTierStudentCount} students are free; your billing will now reflect {Math.Max(0, newTotal - CountryBillingConfig.FreeTierStudentCount)} paid students.";

        return new ExcelImportResult(validStudents.Count, existingCount + validStudents.Count, errorRows, validationErrors, billingMessage);
    }

    private async Task<(List<StudentExcelRow> valid, List<ExcelErrorRow> errorRows, List<ExcelRowValidation> errors)> ParseAndValidateAllAsync(Stream excelStream, Guid schoolId, CancellationToken ct)
    {
        var valid = new List<StudentExcelRow>();
        var errorRows = new List<ExcelErrorRow>();
        var errors = new List<ExcelRowValidation>();

        using var workbook = new XLWorkbook(excelStream);
        var ws = workbook.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
            return (valid, errorRows, errors);

        var classes = await _db.Classes.AsNoTracking().Where(c => c.SchoolId == schoolId).ToDictionaryAsync(c => c.Name.Trim().ToUpperInvariant(), c => c.Id, StringComparer.OrdinalIgnoreCase, ct);

        for (var row = 2; row <= lastRow; row++)
        {
            if (ct.IsCancellationRequested) break;
            var (firstName, lastName, middleName, gender, dob, nin, nationalIdType, nationalIdNumber, className, admissionNumber,
                 stateOfOrigin, lga, nationality, parentName, parentPhone, bloodGroup, genotype, emergencyName, emergencyPhone) = ReadRow(ws, row);

            var rowErrors = ValidateRow(row, firstName, lastName, nin, nationalIdNumber, nationalIdType, className, classes);
            foreach (var e in rowErrors)
                errors.Add(e);

            if (rowErrors.Count > 0)
            {
                errorRows.Add(new ExcelErrorRow(row, firstName ?? "", lastName ?? "", string.Join("; ", rowErrors.Select(x => x.Error))));
                continue;
            }
            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                continue;

            valid.Add(new StudentExcelRow(
                firstName?.Trim() ?? "—",
                lastName?.Trim() ?? "—",
                middleName?.Trim(),
                gender?.Trim(),
                dob,
                nin?.Trim(),
                nationalIdType?.Trim(),
                nationalIdNumber?.Trim(),
                className?.Trim(),
                admissionNumber?.Trim(),
                stateOfOrigin?.Trim(),
                lga?.Trim(),
                nationality?.Trim(),
                parentName?.Trim(),
                parentPhone?.Trim(),
                bloodGroup?.Trim(),
                genotype?.Trim(),
                emergencyName?.Trim(),
                emergencyPhone?.Trim()
            ));
        }

        return (valid, errorRows, errors);
    }

    private static (string? fn, string? ln, string? mn, string? gender, DateOnly? dob, string? nin, string? idType, string? idNum, string? cls, string? adm, string? state, string? lga, string? nat, string? pName, string? pPhone, string? bg, string? geno, string? ecName, string? ecPhone) ReadRow(IXLWorksheet ws, int row)
    {
        string? Get(int col) => string.IsNullOrWhiteSpace(ws.Cell(row, col).GetString()) ? null : ws.Cell(row, col).GetString();
        DateOnly? dob = null;
        var dobVal = ws.Cell(row, ColDateOfBirth).GetString();
        if (!string.IsNullOrWhiteSpace(dobVal))
        {
            if (DateOnly.TryParse(dobVal, out var d)) dob = d;
            else if (double.TryParse(dobVal, out var oa)) try { dob = DateOnly.FromDateTime(DateTime.FromOADate(oa)); } catch { /* ignore */ }
        }
        return (Get(ColFirstName), Get(ColLastName), Get(ColMiddleName), Get(ColGender), dob, Get(ColNIN), Get(ColNationalIdType), Get(ColNationalIdNumber), Get(ColClass), Get(ColAdmissionNumber), Get(ColStateOfOrigin), Get(ColLGA), Get(ColNationality), Get(ColParentName), Get(ColParentPhone), Get(ColBloodGroup), Get(ColGenotype), Get(ColEmergencyContactName), Get(ColEmergencyContactPhone));
    }

    private static List<ExcelRowValidation> ValidateRow(int rowIndex, string? firstName, string? lastName, string? nin, string? nationalIdNumber, string? nationalIdType, string? className, IReadOnlyDictionary<string, Guid> classes)
    {
        var list = new List<ExcelRowValidation>();
        if (string.IsNullOrWhiteSpace(firstName))
            list.Add(new ExcelRowValidation(rowIndex, "FirstName is required."));
        if (string.IsNullOrWhiteSpace(lastName))
            list.Add(new ExcelRowValidation(rowIndex, "LastName is required."));
        // At least one national ID (NIN or NationalIdNumber) recommended for ministry alignment; not blocking
        if (!string.IsNullOrWhiteSpace(className) && !classes.ContainsKey(className.Trim().ToUpperInvariant()))
            list.Add(new ExcelRowValidation(rowIndex, $"Class '{className}' not found in this school. Create the class first or leave blank."));
        return list;
    }

    private static Guid? ResolveClassId(string? className, IReadOnlyDictionary<string, Guid> classes)
    {
        if (string.IsNullOrWhiteSpace(className)) return null;
        return classes.TryGetValue(className.Trim().ToUpperInvariant(), out var id) ? id : null;
    }

    private static Parent GetOrCreateParentInBatch(Guid schoolId, string? parentName, string? parentPhone, Dictionary<string, Parent> cache, RiseFlowDbContext db)
    {
        var name = (parentName ?? "").Trim();
        var first = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Parent";
        var last = name.Length > first.Length ? name.Substring(first.Length).Trim() : "";
        var phone = string.IsNullOrWhiteSpace(parentPhone) ? "" : parentPhone.Trim();
        var key = $"{schoolId}|{first}|{last}|{phone}";
        if (cache.TryGetValue(key, out var existing)) return existing;
        var parent = new Parent
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            FirstName = first,
            LastName = last,
            Phone = string.IsNullOrWhiteSpace(parentPhone) ? null : parentPhone.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Parents.Add(parent);
        cache[key] = parent;
        return parent;
    }
}

public record ExcelPreviewResult(IReadOnlyList<ExcelPreviewRow> PreviewRows, IReadOnlyList<ExcelRowValidation> ValidationErrors, int TotalRows);
public record ExcelPreviewRow(int RowIndex, string FirstName, string LastName, string? MiddleName, string? Gender, DateOnly? DateOfBirth, string? NIN, string? NationalIdType, string? NationalIdNumber, string? ClassName, string? AdmissionNumber, string? StateOfOrigin, string? LGA, string? Nationality, string? ParentName, string? ParentPhone, string? BloodGroup, string? Genotype, string? EmergencyContactName, string? EmergencyContactPhone, Guid? ClassId, bool HasErrors);
public record ExcelRowValidation(int RowIndex, string Error);
public record ExcelErrorRow(int RowIndex, string FirstName, string LastName, string Errors);
public record ExcelImportResult(int ImportedCount, int TotalStudentsAfter, IReadOnlyList<ExcelErrorRow> ErrorRows, IReadOnlyList<ExcelRowValidation> ValidationErrors, string BillingMessage);

internal record StudentExcelRow(string FirstName, string LastName, string? MiddleName, string? Gender, DateOnly? DateOfBirth, string? NIN, string? NationalIdType, string? NationalIdNumber, string? ClassName, string? AdmissionNumber, string? StateOfOrigin, string? LGA, string? Nationality, string? ParentName, string? ParentPhone, string? BloodGroup, string? Genotype, string? EmergencyContactName, string? EmergencyContactPhone);
