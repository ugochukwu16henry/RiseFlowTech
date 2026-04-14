namespace RiseFlow.Api.Entities;

public class ClassPromotionRequestItem : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid RequestId { get; set; }
    public Guid StudentId { get; set; }

    public ClassPromotionRequest Request { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
