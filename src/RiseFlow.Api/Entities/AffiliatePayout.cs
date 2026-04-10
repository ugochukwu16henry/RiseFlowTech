namespace RiseFlow.Api.Entities;

public class AffiliatePayout
{
    public Guid Id { get; set; }
    public Guid AffiliateId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "NGN";
    public string PayoutType { get; set; } = "Commission";
    public string? PaystackTransferReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? FailureReason { get; set; }

    public Affiliate Affiliate { get; set; } = null!;
    public ICollection<AffiliateCommissionLedger> CommissionLedgers { get; set; } = new List<AffiliateCommissionLedger>();
}
