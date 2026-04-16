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
            .Where(r => r.StudentId == studentId && r.WorkflowStatus == ResultWorkflowStatus.ApprovedLocked);
        if (termIds != null)
        {
            var ids = termIds.ToList();
            if (ids.Count > 0) resultsQuery = resultsQuery.Where(r => ids.Contains(r.TermId));
        }
        var results = await resultsQuery.OrderBy(r => r.Term!.StartDate).ThenBy(r => r.Subject!.Name).ToListAsync(ct);
        var termPositions = await ComputeTermPositionsAsync(studentId, student.ClassId, results.Select(r => r.TermId).Distinct().ToList(), ct);

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
        var pdfBytes = BuildPdf(student, school, results, termPositions, verifyUrl, contentHash);
        return (verification, pdfBytes);
    }

    private async Task<Dictionary<Guid, int?>> ComputeTermPositionsAsync(
        Guid studentId,
        Guid? classId,
        IReadOnlyCollection<Guid> termIds,
        CancellationToken ct)
    {
        var positions = new Dictionary<Guid, int?>();
        if (!classId.HasValue || termIds.Count == 0)
            return positions;

        var rows = await _db.StudentResults
            .AsNoTracking()
            .Include(r => r.Student)
            .Where(r => termIds.Contains(r.TermId)
                        && r.WorkflowStatus == ResultWorkflowStatus.ApprovedLocked
                        && r.Student.ClassId == classId.Value)
            .Select(r => new { r.TermId, r.StudentId, r.Score })
            .ToListAsync(ct);

        foreach (var termGroup in rows.GroupBy(r => r.TermId))
        {
            var totals = termGroup
                .GroupBy(r => r.StudentId)
                .Select(g => new { StudentId = g.Key, TotalScore = g.Sum(x => x.Score) })
                .OrderByDescending(x => x.TotalScore)
                .ToList();

            int position = 0;
            decimal? lastScore = null;
            foreach (var total in totals)
            {
                if (lastScore == null || total.TotalScore < lastScore.Value)
                    position++;
                if (total.StudentId == studentId)
                {
                    positions[termGroup.Key] = position;
                    break;
                }
                lastScore = total.TotalScore;
            }
        }

        return positions;
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

    private static byte[] BuildPdf(
        Student student,
        School school,
        List<StudentResult> results,
        IReadOnlyDictionary<Guid, int?> termPositions,
        string verifyUrl,
        string? contentHash)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var qrBytes = GenerateQrPng(verifyUrl);
        var termSummaries = results
            .GroupBy(r => r.TermId)
            .Select(g => new
            {
                TermId = g.Key,
                TermName = g.First().Term?.Name ?? "—",
                Rows = g.GroupBy(x => x.SubjectId)
                    .Select(sg => new
                    {
                        SubjectName = sg.First().Subject?.Name ?? "—",
                        Score = sg.Sum(x => x.Score),
                        MaxScore = sg.Sum(x => x.MaxScore),
                        GradeLetter = sg.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.GradeLetter))?.GradeLetter
                    })
                    .OrderBy(x => x.SubjectName)
                    .ToList()
            })
            .ToList();

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
                    c.Item().Text($"School ID: {school.Id}").FontSize(9);
                    c.Item().Text($"Email: {school.Email ?? "—"}  |  Phone: {school.Phone ?? "—"}").FontSize(9);
                    c.Item().Text($"Issued: {DateTime.UtcNow:yyyy-MM-dd}").FontSize(9);
                });

                page.Content().Column(c =>
                {
                    c.Spacing(10);
                    c.Item().Text($"Student: {student.FirstName} {student.MiddleName} {student.LastName}".Replace("  ", " ").Trim()).Bold();
                    c.Item().Text($"Admission: {student.AdmissionNumber ?? "—"}  |  Class: {student.Class?.Name ?? "—"}  |  Grade: {student.Grade?.Name ?? "—"}");
                    c.Spacing(10);

                    foreach (var term in termSummaries)
                    {
                        c.Item().Text($"TERM: {term.TermName}").Bold();
                        c.Item().Table(t =>
                        {
                            t.ColumnsDefinition(d =>
                            {
                                d.RelativeColumn(2);
                                d.ConstantColumn(70);
                                d.ConstantColumn(70);
                                d.ConstantColumn(80);
                            });
                            t.Header(h =>
                            {
                                h.Cell().Element(CellStyle).Text("Subject");
                                h.Cell().Element(CellStyle).Text("Score");
                                h.Cell().Element(CellStyle).Text("Percent");
                                h.Cell().Element(CellStyle).Text("Grade");
                            });
                            foreach (var row in term.Rows)
                            {
                                var percent = row.MaxScore > 0 ? Math.Round((row.Score / row.MaxScore) * 100m, 1) : 0m;
                                t.Cell().Text(row.SubjectName);
                                t.Cell().Text($"{row.Score}/{row.MaxScore}");
                                t.Cell().Text($"{percent}%");
                                t.Cell().Text(row.GradeLetter ?? "—");
                            }
                        });
                        var termScore = term.Rows.Sum(x => x.Score);
                        var termMax = term.Rows.Sum(x => x.MaxScore);
                        var termPercent = termMax > 0 ? Math.Round((termScore / termMax) * 100m, 1) : 0m;
                        var position = termPositions.TryGetValue(term.TermId, out var p) ? p : null;
                        c.Item().Text($"Term total: {termScore}/{termMax} ({termPercent}%)  |  Position in class: {(position.HasValue ? position.Value.ToString() : "—")}").FontSize(9);
                        c.Item().PaddingBottom(8);
                    }

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
