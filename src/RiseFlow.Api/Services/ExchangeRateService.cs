namespace RiseFlow.Api.Services;

/// <summary>
/// Exchange rates from configuration (appsettings or database). Keys like "ExchangeRates:NGN", "ExchangeRates:USD" = rate to USD (e.g. 0.0006 for NGN).
/// </summary>
public class ExchangeRateService : IExchangeRateService
{
    private readonly IConfiguration _config;
    private const string SectionKey = "ExchangeRates";

    public ExchangeRateService(IConfiguration config)
    {
        _config = config;
    }

    public decimal GetRateToUsd(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) return 1m;
        var key = $"{SectionKey}:{currencyCode.Trim().ToUpperInvariant()}";
        var rate = _config.GetValue<decimal?>(key);
        return rate ?? (currencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase) ? 1m : 0m);
    }

    public IReadOnlyDictionary<string, decimal> GetAllRatesToUsd()
    {
        var section = _config.GetSection(SectionKey);
        var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in section.GetChildren())
            if (decimal.TryParse(child.Value, System.Globalization.NumberStyles.Any, null, out var rate))
                dict[child.Key] = rate;
        if (!dict.ContainsKey("USD")) dict["USD"] = 1m;
        return dict;
    }

    public decimal Convert(decimal amount, string fromCurrencyCode, string toCurrencyCode)
    {
        if (string.Equals(fromCurrencyCode, toCurrencyCode, StringComparison.OrdinalIgnoreCase))
            return amount;
        var toUsd = GetRateToUsd(fromCurrencyCode);
        var fromUsd = GetRateToUsd(toCurrencyCode);
        if (fromUsd == 0) return 0;
        var amountUsd = amount * toUsd;
        return amountUsd / fromUsd;
    }
}
