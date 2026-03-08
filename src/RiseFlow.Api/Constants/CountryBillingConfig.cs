namespace RiseFlow.Api.Constants;

/// <summary>
/// Default billing rate per student (after free tier) by country/currency. Used when school does not override.
/// </summary>
public static class CountryBillingConfig
{
    public const int FreeTierStudentCount = 50;

    /// <summary>Rate per student after the first 50 (in local currency). ISO 4217 currency code -> rate.</summary>
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

    public static decimal GetRatePerStudent(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) return DefaultRatePerStudent;
        return RatePerStudentByCurrency.TryGetValue(currencyCode.Trim(), out var rate) ? rate : DefaultRatePerStudent;
    }
}
