namespace RiseFlow.Api.Entities;

public class GradingSystem : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ClassId { get; set; }
    public Guid? TermId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Class? Class { get; set; }
    public AcademicTerm? Term { get; set; }
    public ICollection<GradeRule> Rules { get; set; } = new List<GradeRule>();
}
