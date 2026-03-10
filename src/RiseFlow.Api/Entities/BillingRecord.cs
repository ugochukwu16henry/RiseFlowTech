namespace RiseFlow.Api.Entities;

public class BillingRecord
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int StudentCount { get; set; }

    /// <summary>
    /// Total amount due for this billing period in the school's currency.
    /// This includes both the one-time activation fees (for newly billable students)
    /// and the recurring monthly subscription.
    /// </summary>
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Portion of <see cref="AmountDue"/> that is recurring monthly subscription
    /// (billable students × monthly rate).
    /// </summary>
    public decimal MonthlyAmountDue { get; set; }

    /// <summary>
    /// Portion of <see cref="AmountDue"/> that is one-time activation fees
    /// for newly billable students in this period.
    /// </summary>
    public decimal ActivationAmountDue { get; set; }

    public decimal? AmountPaid { get; set; }
    /// <summary>ISO 4217. Amounts are in this currency.</summary>
    public string CurrencyCode { get; set; } = "NGN";
    public DateTime? PaidAtUtc { get; set; }
    /// <summary>Gateway payment reference (e.g. Paystack reference) for this period, if initialized.</summary>
    public string? PaymentReference { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public School School { get; set; } = null!;
}
