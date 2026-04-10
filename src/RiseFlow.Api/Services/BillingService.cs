using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

/// <summary>
/// Calculates billing per tenant (school) using the "First 50 Free" model:
/// - Students 1–50: lifetime free tier.
/// - From the 51st student onward:
///   - One-time activation fee (e.g. ₦500 per student, once when they become billable).
///   - Recurring monthly subscription (e.g. ₦100/month per billable student).
/// Also supports conversion to USD and other African currencies via provided exchange rates.
/// </summary>
public class BillingService
{
    private readonly RiseFlowDbContext _db;
    private readonly IExchangeRateService _exchangeRates;

    public BillingService(RiseFlowDbContext db, IExchangeRateService exchangeRates)
    {
        _db = db;
        _exchangeRates = exchangeRates;
    }

    /// <summary>
    /// Compute total amount due in the given currency for a simple "all at once" estimate.
    /// First 50 students are free; all additional students are treated as billable and
    /// charged both activation and monthly subscription in this calculation.
    /// This is primarily used for rough previews (e.g. Excel import messaging).
    /// </summary>
    public static decimal ComputeAmountDue(int studentCount, string currencyCode)
    {
        if (studentCount <= CountryBillingConfig.FreeTierStudentCount) return 0;
        var billable = studentCount - CountryBillingConfig.FreeTierStudentCount;
        var activationFee = CountryBillingConfig.GetActivationFeePerStudent(currencyCode);
        var monthlyRate = CountryBillingConfig.GetMonthlyRatePerStudent(currencyCode);
        return (billable * activationFee) + (billable * monthlyRate);
    }

    /// <summary>
    /// Compute recurring monthly subscription only (no activation) for a given student count.
    /// First 50 students are free; from 51st student onward apply monthly rate.
    /// Mirrors the monthly side of the homepage pricing calculator.
    /// </summary>
    public static decimal ComputeMonthlyAmount(int studentCount, string currencyCode)
    {
        if (studentCount <= CountryBillingConfig.FreeTierStudentCount) return 0;
        var billable = studentCount - CountryBillingConfig.FreeTierStudentCount;
        var monthlyRate = CountryBillingConfig.GetMonthlyRatePerStudent(currencyCode);
        return billable * monthlyRate;
    }

    /// <summary>
    /// Compute one-time activation fees for newly billable students in this period.
    /// previousBillableStudents is the number of students that were already billable
    /// (i.e. max(0, previousTotalStudents - FreeTierStudentCount)) in the previous period.
    /// </summary>
    public static decimal ComputeActivationAmount(int currentStudentCount, int previousBillableStudents, string currencyCode)
    {
        if (currentStudentCount <= CountryBillingConfig.FreeTierStudentCount) return 0;
        var billableNow = currentStudentCount - CountryBillingConfig.FreeTierStudentCount;
        var newBillable = Math.Max(0, billableNow - Math.Max(0, previousBillableStudents));
        if (newBillable <= 0) return 0;
        var activationFee = CountryBillingConfig.GetActivationFeePerStudent(currencyCode);
        return newBillable * activationFee;
    }

    /// <summary>Calculate monthly billing for a school. Uses school's country/currency and creates a BillingRecord.</summary>
    public async Task<BillingRecord> CreateBillingRecordAsync(Guid schoolId, string periodLabel, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default)
    {
        var school = await _db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct)
            ?? throw new InvalidOperationException("School not found.");
        var currencyCode = string.IsNullOrWhiteSpace(school.CurrencyCode) ? "NGN" : school.CurrencyCode.Trim().ToUpperInvariant();
        var studentCount = await _db.Students.CountAsync(s => s.SchoolId == schoolId && s.IsActive, ct);

        // Look at previous billing record to know how many students were already billable.
        var lastRecord = await _db.BillingRecords
            .Where(b => b.SchoolId == schoolId)
            .OrderByDescending(b => b.PeriodEnd)
            .FirstOrDefaultAsync(ct);

        var previousBillable = 0;
        if (lastRecord != null)
        {
            var previousTotal = lastRecord.StudentCount;
            previousBillable = Math.Max(0, previousTotal - CountryBillingConfig.FreeTierStudentCount);
        }

        var monthlyAmount = ComputeMonthlyAmount(studentCount, currencyCode);
        var activationAmount = ComputeActivationAmount(studentCount, previousBillable, currencyCode);
        var amountDue = monthlyAmount + activationAmount;

        var record = new BillingRecord
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            PeriodLabel = periodLabel,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            StudentCount = studentCount,
            AmountDue = amountDue,
            MonthlyAmountDue = monthlyAmount,
            ActivationAmountDue = activationAmount,
            CurrencyCode = currencyCode,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.BillingRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        return record;
    }

    /// <summary>
    /// Check if a school's subscription is currently active.
    /// A subscription is considered active when the latest billing record for the school:
    /// - Covers today's date (PeriodEnd >= today), and
    /// - Has AmountPaid &gt;= AmountDue (fully paid).
    /// </summary>
    public async Task<bool> IsSubscriptionActiveAsync(Guid schoolId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var record = await _db.BillingRecords
            .Where(b => b.SchoolId == schoolId)
            .OrderByDescending(b => b.PeriodEnd)
            .FirstOrDefaultAsync(ct);

        if (record == null)
            return false;

        if (record.PeriodEnd < today)
            return false;

        if (!record.AmountPaid.HasValue)
            return false;

        return record.AmountPaid.Value >= record.AmountDue;
    }

    /// <summary>Total revenue in USD (converts all paid amounts using current exchange rates).</summary>
    public async Task<decimal> GetTotalRevenueUsdAsync(CancellationToken ct = default)
    {
        var records = await _db.BillingRecords.Where(b => b.AmountPaid != null).ToListAsync(ct);
        decimal totalUsd = 0;
        foreach (var b in records)
        {
            var paid = b.AmountPaid ?? 0;
            if (paid <= 0) continue;
            totalUsd += _exchangeRates.Convert(paid, b.CurrencyCode, "USD");
        }
        return totalUsd;
    }

    /// <summary>Convert an amount from local currency to USD using provided rates.</summary>
    public decimal ConvertToUsd(decimal amount, string fromCurrencyCode)
    {
        return _exchangeRates.Convert(amount, fromCurrencyCode ?? "NGN", "USD");
    }

    /// <summary>Convert an amount to multiple African currencies (and USD). Keys: currency code, Values: converted amount.</summary>
    public IReadOnlyDictionary<string, decimal> ConvertToOtherCurrencies(decimal amount, string fromCurrencyCode)
    {
        var rates = _exchangeRates.GetAllRatesToUsd();
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var amountUsd = _exchangeRates.Convert(amount, fromCurrencyCode, "USD");
        foreach (var kv in rates)
        {
            var toCode = kv.Key;
            var toUsdRate = kv.Value;
            if (toUsdRate == 0) continue;
            result[toCode] = amountUsd / toUsdRate;
        }
        return result;
    }
}
