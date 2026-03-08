using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

public class TranscriptPdfService
{
    private readonly RiseFlowDbContext _db;
    private readonly IConfiguration _config;

    public TranscriptPdfService(RiseFlowDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<(TranscriptVerification Verification, byte[] PdfBytes)> GenerateTranscriptAsync(
        Guid studentId,
        Guid schoolId,
        IEnumerable<Guid>? termIds,
        string? issuedToName,
        string verificationBaseUrl,
        CancellationToken ct = default)
    {
        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.School)
            .Include(s => s.Class)
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.SchoolId == schoolId, ct)
            ?? throw new InvalidOperationException("Student not found.");
        var school = student.School;

        var resultsQuery = _db.StudentResults
            .AsNoTracking()
            .Include(r => r.Subject)
            .Include(r => r.Term)
            .Where(r => r.StudentId == studentId);
        if (termIds != null)
        {
            var ids = termIds.ToList();
            if (ids.Count > 0) resultsQuery = resultsQuery.Where(r => ids.Contains(r.TermId));
        }
        var results = await resultsQuery.OrderBy(r => r.Term!.StartDate).ThenBy(r => r.Subject!.Name).ToListAsync(ct);

        var issuedAt = DateTime.UtcNow;
        var canonical = BuildCanonicalContent(student, school, results, issuedAt, issuedToName);
        var contentHash = ComputeSha256Hex(canonical);

        var token = Guid.NewGuid().ToString("N")[..16];
        var verification = new TranscriptVerification
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            SchoolId = schoolId,
            VerificationToken = token,
            ContentHash = contentHash,
            IssuedAtUtc = issuedAt,
            IssuedToName = issuedToName
        };
        _db.TranscriptVerifications.Add(verification);
        await _db.SaveChangesAsync(ct);

        var verifyUrl = $"{verificationBaseUrl.TrimEnd('/')}/verify/transcript/{token}";
        var pdfBytes = BuildPdf(student, school, results, verifyUrl, contentHash);
        return (verification, pdfBytes);
    }

    private static string BuildCanonicalContent(Student student, School school, List<StudentResult> results, DateTime issuedAt, string? issuedToName)
    {
        var sb = new StringBuilder();
        sb.Append(student.Id).Append('|').Append(school.Id).Append('|').Append(issuedAt.ToString("O")).Append('|').Append(issuedToName ?? "");
        foreach (var r in results)
            sb.Append('|').Append(r.Term?.Name).Append('|').Append(r.Subject?.Name).Append('|').Append(r.Score).Append('|').Append(r.MaxScore).Append('|').Append(r.GradeLetter ?? "");
        return sb.ToString();
    }

    private static string ComputeSha256Hex(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static byte[] BuildPdf(Student student, School school, List<StudentResult> results, string verifyUrl, string? contentHash)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var qrBytes = GenerateQrPng(verifyUrl);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(c =>
                {
                    c.Item().Text("ACADEMIC TRANSCRIPT").Bold().FontSize(14);
                    c.Item().Text(school.Name).FontSize(12);
                    c.Item().Text($"Issued: {DateTime.UtcNow:yyyy-MM-dd}").FontSize(9);
                });

                page.Content().Column(c =>
                {
                    c.Spacing(10);
                    c.Item().Text($"Student: {student.FirstName} {student.LastName}").Bold();
                    c.Item().Text($"Admission: {student.AdmissionNumber ?? "—"}  |  Class: {student.Class?.Name ?? "—"}  |  Grade: {student.Grade?.Name ?? "—"}");
                    c.Spacing(10);
                    c.Item().Text("RESULTS").Bold();
                    c.Item().Table(t =>
                    {
                        t.ColumnsDefinition(d =>
                        {
                            d.ConstantColumn(80);
                            d.RelativeColumn(2);
                            d.ConstantColumn(60);
                            d.ConstantColumn(60);
                            d.ConstantColumn(80);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(CellStyle).Text("Term");
                            h.Cell().Element(CellStyle).Text("Subject");
                            h.Cell().Element(CellStyle).Text("Type");
                            h.Cell().Element(CellStyle).Text("Score");
                            h.Cell().Element(CellStyle).Text("Grade");
                        });
                        foreach (var r in results)
                        {
                            t.Cell().Text(r.Term?.Name ?? "—");
                            t.Cell().Text(r.Subject?.Name ?? "—");
                            t.Cell().Text(r.AssessmentType);
                            t.Cell().Text($"{r.Score}/{r.MaxScore}");
                            t.Cell().Text(r.GradeLetter ?? "—");
                        }
                    });
                    c.Spacing(15);
                    c.Item().Row(r =>
                    {
                        r.RelativeItem();
                        r.ConstantItem(120).Height(120).Width(120).Image(qrBytes).FitArea();
                    });
                    if (!string.IsNullOrEmpty(contentHash))
                        c.Item().Text($"Verification hash: {contentHash}").FontSize(7);
                    c.Item().Text("Scan QR code or visit the URL to verify this transcript at riseflow.com/verify.").FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    private static IContainer CellStyle(IContainer c) => c.DefaultTextStyle(x => x.SemiBold()).Padding(4);

    private static byte[] GenerateQrPng(string content)
    {
        using var qr = new QRCodeGenerator();
        using var data = qr.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(4);
    }
}
