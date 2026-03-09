namespace RiseFlow.Api.Entities;

/// <summary>
/// Academic term/semester (e.g. Term 1, Semester 1). Used for results and reporting.
/// </summary>
public class AcademicTerm : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public ICollection<StudentResult> StudentResults { get; set; } = new List<StudentResult>();
    public ICollection<StudentAssessment> StudentAssessments { get; set; } = new List<StudentAssessment>();
}
