namespace RiseFlow.Api.Entities;

public class BillingRecord
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int StudentCount { get; set; }
    public decimal AmountDue { get; set; }
    public decimal? AmountPaid { get; set; }
    /// <summary>ISO 4217. Amounts are in this currency.</summary>
    public string CurrencyCode { get; set; } = "NGN";
    public DateTime? PaidAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public School School { get; set; } = null!;
}
