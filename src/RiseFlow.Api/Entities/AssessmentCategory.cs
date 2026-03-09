namespace RiseFlow.Api.Entities;

/// <summary>
/// Primary/competency-based assessment category (e.g. Social Habits, Psychomotor). Tenant-scoped.
/// </summary>
public class AssessmentCategory : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public ICollection<AssessmentItem> Items { get; set; } = new List<AssessmentItem>();
}

