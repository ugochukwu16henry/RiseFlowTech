namespace RiseFlow.Api.Entities;

public class AffiliateTrainingVideo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? Description { get; set; }
    public string YoutubeUrl { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
