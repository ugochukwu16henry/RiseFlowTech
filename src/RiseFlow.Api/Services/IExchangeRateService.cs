namespace RiseFlow.Api.Services;

/// <summary>
/// Provides exchange rates for converting billing amounts to USD and other African currencies.
/// Rates are typically provided by SuperAdmin or an external feed.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>Gets the rate to convert one unit of the given currency to USD (e.g. 1 NGN = 0.0006 USD).</summary>
    decimal GetRateToUsd(string currencyCode);

    /// <summary>Gets all configured rates (currency code -> rate to USD).</summary>
    IReadOnlyDictionary<string, decimal> GetAllRatesToUsd();

    /// <summary>Converts amount from one currency to another using USD as pivot. Returns converted amount.</summary>
    decimal Convert(decimal amount, string fromCurrencyCode, string toCurrencyCode);
}
