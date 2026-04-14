namespace RiseFlow.Api.Entities;

public class AffiliateTrainingCompletion
{
    public Guid Id { get; set; }
    public Guid AffiliateId { get; set; }
    public Guid TrainingVideoId { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Affiliate Affiliate { get; set; } = null!;
    public AffiliateTrainingVideo TrainingVideo { get; set; } = null!;
}
