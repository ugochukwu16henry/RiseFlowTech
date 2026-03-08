namespace RiseFlow.Api.Entities;

public class BillingRecord
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int StudentCount { get; set; }
    public decimal AmountDueNaira { get; set; }
    public decimal? AmountPaidNaira { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public School School { get; set; } = null!;
}
