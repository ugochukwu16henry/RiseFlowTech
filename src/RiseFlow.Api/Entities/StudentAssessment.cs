namespace RiseFlow.Api.Entities;

/// <summary>
/// Competency/behaviour assessment value for a student on a specific item and term (e.g. A/B/C or 1–5). Tenant-scoped.
/// </summary>
public class StudentAssessment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid TermId { get; set; }
    public Guid AssessmentItemId { get; set; }
    public string? Value { get; set; } // e.g. A, B, C, 1–5
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Student Student { get; set; } = null!;
    public AcademicTerm Term { get; set; } = null!;
    public AssessmentItem Item { get; set; } = null!;
}

