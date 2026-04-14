namespace RiseFlow.Api.Entities;

public class ClassPromotionRequest : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid FromClassId { get; set; }
    public Guid ToClassId { get; set; }
    public Guid? FromTermId { get; set; }
    public string? PromotionSessionLabel { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public School School { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public Class FromClass { get; set; } = null!;
    public Class ToClass { get; set; } = null!;
    public AcademicTerm? FromTerm { get; set; }
    public ICollection<ClassPromotionRequestItem> Items { get; set; } = new List<ClassPromotionRequestItem>();
}
