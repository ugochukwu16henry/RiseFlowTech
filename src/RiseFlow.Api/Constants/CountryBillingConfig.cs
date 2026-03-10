namespace RiseFlow.Api.Constants;

public static class CountryBillingConfig
{
    public const int FreeTierStudentCount = 50;

    /// <summary>Legacy: single rate per student after the first 50 (in local currency). Kept for backwards compatibility.</summary>
    public static readonly IReadOnlyDictionary<string, decimal> RatePerStudentByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["NGN"] = 500m,   // Nigeria
        ["GHS"] = 5m,     // Ghana (Cedis)
        ["KES"] = 100m,   // Kenya (Shillings)
        ["ZAR"] = 50m,    // South Africa (Rand)
        ["XOF"] = 500m,   // West African CFA
        ["XAF"] = 500m,   // Central African CFA
        ["TZS"] = 2000m,  // Tanzania
        ["UGX"] = 2000m,  // Uganda
        ["USD"] = 1m,     // US Dollar (default for others)
    };

    /// <summary>Default rate if currency not found (e.g. 1 USD equivalent).</summary>
    public const decimal DefaultRatePerStudent = 1m;

    /// <summary>
    /// One-time activation fee per billable student (51st student onwards) in the school's currency.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, decimal> ActivationFeePerStudentByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["NGN"] = 500m,   // One-time ₦500 activation per student after 50
        ["GHS"] = 5m,
        ["KES"] = 100m,
        ["ZAR"] = 50m,
        ["XOF"] = 500m,
        ["XAF"] = 500m,
        ["TZS"] = 2000m,
        ["UGX"] = 2000m,
        ["USD"] = 1m,
    };

    /// <summary>
    /// Monthly subscription rate per billable student (51st student onwards) in the school's currency.
    /// For Nigeria this is ₦100/month per student after the first 50, matching the homepage pricing calculator.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, decimal> MonthlyRatePerStudentByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["NGN"] = 100m,   // ₦100/month per billable student
        ["GHS"] = 1m,
        ["KES"] = 20m,
        ["ZAR"] = 10m,
        ["XOF"] = 100m,
        ["XAF"] = 100m,
        ["TZS"] = 400m,
        ["UGX"] = 400m,
        ["USD"] = 0.25m,
    };

    public static decimal GetRatePerStudent(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) return DefaultRatePerStudent;
        return RatePerStudentByCurrency.TryGetValue(currencyCode.Trim(), out var rate) ? rate : DefaultRatePerStudent;
    }

    public static decimal GetActivationFeePerStudent(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) return DefaultRatePerStudent;
        return ActivationFeePerStudentByCurrency.TryGetValue(currencyCode.Trim(), out var fee)
            ? fee
            : DefaultRatePerStudent;
    }

    public static decimal GetMonthlyRatePerStudent(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) return DefaultRatePerStudent;
        return MonthlyRatePerStudentByCurrency.TryGetValue(currencyCode.Trim(), out var rate)
            ? rate
            : DefaultRatePerStudent;
    }
}
