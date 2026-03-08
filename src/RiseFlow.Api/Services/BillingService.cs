using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

/// <summary>
/// Calculates monthly billing per tenant (school). First 50 students free; thereafter charge per student in the school's currency.
/// Supports conversion to USD and other African currencies via provided exchange rates.
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

    /// <summary>Compute amount due in the given currency: first 50 free, then rate per student.</summary>
    public static decimal ComputeAmountDue(int studentCount, string currencyCode)
    {
        if (studentCount <= CountryBillingConfig.FreeTierStudentCount) return 0;
        var rate = CountryBillingConfig.GetRatePerStudent(currencyCode);
        return (studentCount - CountryBillingConfig.FreeTierStudentCount) * rate;
    }

    /// <summary>Calculate monthly billing for a school. Uses school's country/currency and creates a BillingRecord.</summary>
    public async Task<BillingRecord> CreateBillingRecordAsync(Guid schoolId, string periodLabel, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default)
    {
        var school = await _db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct)
            ?? throw new InvalidOperationException("School not found.");
        var currencyCode = string.IsNullOrWhiteSpace(school.CurrencyCode) ? "NGN" : school.CurrencyCode.Trim().ToUpperInvariant();
        var studentCount = await _db.Students.CountAsync(s => s.SchoolId == schoolId && s.IsActive, ct);
        var amountDue = ComputeAmountDue(studentCount, currencyCode);

        var record = new BillingRecord
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            PeriodLabel = periodLabel,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            StudentCount = studentCount,
            AmountDue = amountDue,
            CurrencyCode = currencyCode,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.BillingRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        return record;
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
