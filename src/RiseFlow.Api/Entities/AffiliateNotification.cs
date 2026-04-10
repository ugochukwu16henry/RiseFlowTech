namespace RiseFlow.Api.Entities;

public class AffiliateNotification
{
    public Guid Id { get; set; }
    public Guid AffiliateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public Affiliate Affiliate { get; set; } = null!;
}
