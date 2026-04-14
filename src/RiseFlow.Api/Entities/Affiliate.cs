using RiseFlow.Api.Data;

namespace RiseFlow.Api.Entities;

public class Affiliate
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public string? HeadshotPath { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? PaystackRecipientCode { get; set; }
    public byte[]? HeadshotBytes { get; set; }
    public string? HeadshotContentType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public ICollection<School> ReferredSchools { get; set; } = new List<School>();
    public ICollection<AffiliatePayout> Payouts { get; set; } = new List<AffiliatePayout>();
    public ICollection<AffiliateCommissionLedger> CommissionLedgers { get; set; } = new List<AffiliateCommissionLedger>();
    public ICollection<AffiliateNotification> Notifications { get; set; } = new List<AffiliateNotification>();
}
