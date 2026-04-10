namespace RiseFlow.Api.Entities;

public class AffiliateCommissionLedger
{
    public Guid Id { get; set; }
    public Guid AffiliateId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid? BillingRecordId { get; set; }
    public Guid? AffiliatePayoutId { get; set; }
    public int StudentCount { get; set; }
    public int BillableStudentCount { get; set; }
    public decimal ActivationCommissionAmount { get; set; }
    public decimal MonthlyCommissionAmount { get; set; }
    public decimal TotalCommissionAmount { get; set; }
    public string CommissionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public Affiliate Affiliate { get; set; } = null!;
    public School School { get; set; } = null!;
    public BillingRecord? BillingRecord { get; set; }
    public AffiliatePayout? AffiliatePayout { get; set; }
}
