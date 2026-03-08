namespace RiseFlow.Api.Constants;

/// <summary>
/// Billing: first 50 students free, then 500 Naira per additional student per school.
/// </summary>
public static class BillingConstants
{
    public const int FreeTierStudentCount = 50;
    public const decimal RatePerStudentNaira = 500m;
    public const string CurrencyCode = "NGN";
}
