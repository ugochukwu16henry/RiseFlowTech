namespace RiseFlow.Api.Entities;

public class GradeRule : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid GradingSystemId { get; set; }
    public string GradeLetter { get; set; } = string.Empty;
    public decimal MinPercent { get; set; }
    public decimal MaxPercent { get; set; }
    public decimal? GradePoint { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public GradingSystem GradingSystem { get; set; } = null!;
}
