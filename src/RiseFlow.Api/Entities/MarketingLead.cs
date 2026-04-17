namespace RiseFlow.Api.Entities;

/// <summary>
/// Public marketing lead (e.g. homepage guide download). Not tenant-scoped.
/// </summary>
public class MarketingLead
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    /// <summary>Origin tag, e.g. homepage_digital_guide.</summary>
    public string Source { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
