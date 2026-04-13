namespace RiseFlow.Api.Entities;

public class StudentPromotion : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid FromClassId { get; set; }
    public Guid ToClassId { get; set; }
    public Guid? FromTermId { get; set; }
    public string? PromotionSessionLabel { get; set; }
    public Guid? PromotedByUserId { get; set; }
    public DateTime PromotedAtUtc { get; set; }
    public string? Notes { get; set; }

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public Class FromClass { get; set; } = null!;
    public Class ToClass { get; set; } = null!;
    public AcademicTerm? FromTerm { get; set; }
}
