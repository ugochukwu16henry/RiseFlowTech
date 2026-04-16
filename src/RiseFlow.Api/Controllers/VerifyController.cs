using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("verify")]
[AllowAnonymous]
public class VerifyController : ControllerBase
{
    private readonly RiseFlowDbContext _db;

    public VerifyController(RiseFlowDbContext db)
    {
        _db = db;
    }

    /// <summary>Public verification for transcript QR code. Returns JSON with validity and details.</summary>
    [HttpGet("transcript/{token}")]
    [ProducesResponseType(typeof(TranscriptVerificationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TranscriptVerificationResult>> Transcript(string token, CancellationToken ct)
    {
        var verification = await _db.TranscriptVerifications
            .AsNoTracking()
            .Include(v => v.Student)
                .ThenInclude(s => s.Class)
            .Include(v => v.School)
            .FirstOrDefaultAsync(v => v.VerificationToken == token, ct);
        if (verification == null)
            return NotFound();

        var resultRows = await _db.StudentResults
            .AsNoTracking()
            .Include(r => r.Subject)
            .Include(r => r.Term)
            .Where(r => r.StudentId == verification.StudentId && r.WorkflowStatus == Entities.ResultWorkflowStatus.ApprovedLocked)
            .OrderBy(r => r.Term!.StartDate)
            .ThenBy(r => r.Subject!.Name)
            .ToListAsync(ct);

        var termSummaries = resultRows
            .GroupBy(r => r.TermId)
            .Select(g => new TranscriptVerificationTermSummary(
                g.Key,
                g.First().Term?.Name ?? "—",
                g.GroupBy(x => x.SubjectId)
                    .Select(sg =>
                    {
                        var score = sg.Sum(x => x.Score);
                        var max = sg.Sum(x => x.MaxScore);
                        var percent = max > 0 ? Math.Round((score / max) * 100m, 1) : 0m;
                        var grade = sg.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.GradeLetter))?.GradeLetter;
                        return new TranscriptVerificationSubjectSummary(sg.First().Subject?.Name ?? "—", score, max, percent, grade);
                    })
                    .OrderBy(x => x.SubjectName)
                    .ToList()))
            .ToList();

        var classHistory = await _db.StudentPromotions
            .AsNoTracking()
            .Include(x => x.FromClass)
            .Include(x => x.ToClass)
            .Where(x => x.StudentId == verification.StudentId)
            .OrderBy(x => x.PromotedAtUtc)
            .Select(x => new StudentClassHistoryDto(
                x.FromClass != null ? x.FromClass.Name : "—",
                x.ToClass != null ? x.ToClass.Name : "—",
                x.PromotedAtUtc))
            .ToListAsync(ct);

        var teacherNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (verification.Student.ClassId.HasValue)
        {
            var classTeacherNames = await _db.TeacherClasses
                .AsNoTracking()
                .Include(tc => tc.Teacher)
                .Where(tc => tc.ClassId == verification.Student.ClassId.Value && tc.Teacher.IsActive)
                .Select(tc => $"{tc.Teacher.FirstName} {tc.Teacher.LastName}".Trim())
                .ToListAsync(ct);
            foreach (var name in classTeacherNames)
                if (!string.IsNullOrWhiteSpace(name))
                    teacherNames.Add(name);

            var subjectTeacherNames = await _db.TeacherClassSubjects
                .AsNoTracking()
                .Include(tc => tc.Teacher)
                .Where(tc => tc.ClassId == verification.Student.ClassId.Value && tc.Teacher.IsActive)
                .Select(tc => $"{tc.Teacher.FirstName} {tc.Teacher.LastName}".Trim())
                .ToListAsync(ct);
            foreach (var name in subjectTeacherNames)
                if (!string.IsNullOrWhiteSpace(name))
                    teacherNames.Add(name);
        }

        var schoolContact = new TranscriptVerificationSchoolContactDto(
            verification.School.Name,
            verification.School.Address,
            verification.School.Email,
            verification.School.Phone,
            verification.School.LogoFileName);

        return Ok(new TranscriptVerificationResult(
            Valid: true,
            StudentName: $"{verification.Student.FirstName} {verification.Student.LastName}",
            SchoolName: verification.School.Name,
            IssuedAtUtc: verification.IssuedAtUtc,
            IssuedToName: verification.IssuedToName,
            ContentHash: verification.ContentHash,
            TermSummaries: termSummaries,
            DateOfAdmission: verification.Student.DateOfAdmission,
            CurrentClassName: verification.Student.Class?.Name,
            EnrollmentStatus: verification.Student.EnrollmentStatus,
            ClassHistory: classHistory,
            Teachers: teacherNames.OrderBy(x => x).ToList(),
            SchoolContact: schoolContact));
    }
}

public record TranscriptVerificationResult(
    bool Valid,
    string StudentName,
    string SchoolName,
    DateTime IssuedAtUtc,
    string? IssuedToName,
    string? ContentHash,
    IReadOnlyList<TranscriptVerificationTermSummary> TermSummaries,
    DateTime? DateOfAdmission,
    string? CurrentClassName,
    string? EnrollmentStatus,
    IReadOnlyList<StudentClassHistoryDto> ClassHistory,
    IReadOnlyList<string> Teachers,
    TranscriptVerificationSchoolContactDto SchoolContact);

public record TranscriptVerificationTermSummary(
    Guid TermId,
    string TermName,
    IReadOnlyList<TranscriptVerificationSubjectSummary> Subjects);

public record TranscriptVerificationSubjectSummary(
    string SubjectName,
    decimal Score,
    decimal MaxScore,
    decimal Percentage,
    string? GradeLetter);

public record StudentClassHistoryDto(
    string FromClass,
    string ToClass,
    DateTime PromotedAtUtc);

public record TranscriptVerificationSchoolContactDto(
    string SchoolName,
    string? Address,
    string? Email,
    string? Phone,
    string? LogoPath);
