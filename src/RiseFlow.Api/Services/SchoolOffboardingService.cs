using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Models;

namespace RiseFlow.Api.Services;

public sealed class SchoolOffboardingService(
    RiseFlowDbContext db,
    UserManager<ApplicationUser> userManager,
    IWebHostEnvironment env,
    IConfiguration configuration)
{
    public async Task<OffboardSchoolResult?> OffboardAsync(Guid schoolId, string? reason, string? recipientEmail, CancellationToken ct)
    {
        var school = await db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        if (school is null)
            return null;

        var exportInfo = await BuildExportPackageAsync(school.Id, school.Name, reason, ct);

        var emailTarget = !string.IsNullOrWhiteSpace(recipientEmail)
            ? recipientEmail.Trim()
            : school.Email;

        var notificationSent = await TrySendNotificationAsync(emailTarget, school.Name, exportInfo.ExportUrl, ct);

        await DeleteSchoolDataAsync(school.Id, ct);

        return new OffboardSchoolResult(
            SchoolId: school.Id,
            SchoolName: school.Name,
            ExportFile: exportInfo.ExportFile,
            ExportUrl: exportInfo.ExportUrl,
            NotificationSent: notificationSent,
            CompletedAtUtc: DateTime.UtcNow);
    }

    private async Task<(string ExportFile, string ExportUrl)> BuildExportPackageAsync(Guid schoolId, string schoolName, string? reason, CancellationToken ct)
    {
        var students = await db.Students.AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.MiddleName,
                x.LastName,
                x.AdmissionNumber,
                x.CreatedAtUtc,
                x.IsActive
            })
            .ToListAsync(ct);

        var teachers = await db.Teachers.AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.MiddleName,
                x.LastName,
                x.Email,
                x.StaffId,
                x.CreatedAtUtc,
                x.IsActive
            })
            .ToListAsync(ct);

        var parents = await db.Parents.AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.MiddleName,
                x.LastName,
                x.Email,
                x.Relationship,
                x.CreatedAtUtc,
                x.IsActive
            })
            .ToListAsync(ct);

        var classes = await db.Classes.AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new { x.Id, x.Name, x.AcademicYear, x.IsActive })
            .ToListAsync(ct);

        var subjects = await db.Subjects.AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new { x.Id, x.Name, x.Code, x.IsActive })
            .ToListAsync(ct);

        var billing = await db.BillingRecords.AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .Select(x => new { x.Id, x.PeriodLabel, x.AmountDue, x.AmountPaid, x.CurrencyCode, x.PaidAtUtc })
            .ToListAsync(ct);

        var exportRoot = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "exports", "offboarding");
        Directory.CreateDirectory(exportRoot);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var safeName = string.Concat(schoolName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "school";
        var baseName = $"offboarding-{safeName}-{timestamp}";
        var tempDir = Path.Combine(exportRoot, baseName);
        Directory.CreateDirectory(tempDir);

        var manifest = new
        {
            schoolId,
            schoolName,
            exportedAtUtc = DateTime.UtcNow,
            reason,
            counts = new
            {
                students = students.Count,
                teachers = teachers.Count,
                parents = parents.Count,
                classes = classes.Count,
                subjects = subjects.Count,
                billingRecords = billing.Count
            }
        };

        await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), ct);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "students.json"), JsonSerializer.Serialize(students, new JsonSerializerOptions { WriteIndented = true }), ct);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "teachers.json"), JsonSerializer.Serialize(teachers, new JsonSerializerOptions { WriteIndented = true }), ct);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "parents.json"), JsonSerializer.Serialize(parents, new JsonSerializerOptions { WriteIndented = true }), ct);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "classes.json"), JsonSerializer.Serialize(classes, new JsonSerializerOptions { WriteIndented = true }), ct);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "subjects.json"), JsonSerializer.Serialize(subjects, new JsonSerializerOptions { WriteIndented = true }), ct);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "billing.json"), JsonSerializer.Serialize(billing, new JsonSerializerOptions { WriteIndented = true }), ct);

        var zipFile = Path.Combine(exportRoot, $"{baseName}.zip");
        if (File.Exists(zipFile))
            File.Delete(zipFile);

        ZipFile.CreateFromDirectory(tempDir, zipFile);
        Directory.Delete(tempDir, true);

        return ($"{baseName}.zip", $"/exports/offboarding/{baseName}.zip");
    }

    private async Task DeleteSchoolDataAsync(Guid schoolId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.AttendanceRecords.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.StudentAssessments.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.StudentResults.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.TranscriptVerifications.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.FileAssets.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.BillingRecords.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.AcademicTerms.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.AssessmentCategories.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.Subjects.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.Classes.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.Grades.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.Parents.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.Teachers.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);
        await db.Students.Where(x => x.SchoolId == schoolId).ExecuteDeleteAsync(ct);

        var schoolUsers = await db.Users.Where(u => u.SchoolId == schoolId).ToListAsync(ct);
        foreach (var user in schoolUsers)
        {
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException("Unable to remove one or more user accounts during school offboarding.");
        }

        await db.Schools.Where(x => x.Id == schoolId).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private async Task<bool> TrySendNotificationAsync(string? recipientEmail, string schoolName, string exportUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            return false;

        var host = configuration["Smtp:Host"];
        var username = configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];
        var from = configuration["Smtp:From"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            return false;

        var port = int.TryParse(configuration["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var enableSsl = !string.Equals(configuration["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

        using var mail = new MailMessage(from, recipientEmail)
        {
            Subject = $"RiseFlow offboarding export for {schoolName}",
            Body = $"Your school offboarding export is ready: {exportUrl}\n\nIf you did not request this, contact RiseFlow support immediately."
        };

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password ?? string.Empty);

        await client.SendMailAsync(mail, ct);
        return true;
    }
}