namespace RiseFlow.Api.Entities;

/// <summary>
/// Individual behavioural/skill item under a category (e.g. "Handwriting", "Punctuality"). Tenant-scoped via category -> school.
/// </summary>
public class AssessmentItem
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public AssessmentCategory Category { get; set; } = null!;
    public ICollection<StudentAssessment> StudentAssessments { get; set; } = new List<StudentAssessment>();
}

