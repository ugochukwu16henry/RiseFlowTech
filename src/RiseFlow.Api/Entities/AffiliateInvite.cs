namespace RiseFlow.Api.Entities;

public class AffiliateInvite
{
    public Guid Id { get; set; }
    public Guid AffiliateLeadRequestId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string InviteToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public AffiliateLeadRequest AffiliateLeadRequest { get; set; } = null!;
}
