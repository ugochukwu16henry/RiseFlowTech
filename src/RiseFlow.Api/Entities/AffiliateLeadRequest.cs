namespace RiseFlow.Api.Entities;

public class AffiliateLeadRequest
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? InviteSentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<AffiliateInvite> Invites { get; set; } = new List<AffiliateInvite>();
}
