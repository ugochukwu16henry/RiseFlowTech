namespace RiseFlow.Api.Models;

public record SuperAdminDashboardDto(
    int TotalSchools,
    int ActiveSchools,
    int TotalStudents,
    int ActiveStudents,
    decimal TotalRevenueUsd,
    decimal MonthlyRevenueUsd,
    int BillingRecordsCount,
    long TotalResultsProcessed,
    IReadOnlyList<SchoolsByCountryDto> SchoolsByCountry,
    /// <summary>Schools with &gt;50 students that have unpaid/overdue billing (payment delinquency).</summary>
    IReadOnlyList<PaymentDelinquencyDto> PaymentDelinquency,
    /// <summary>Schools that have completed term results (at least one result in current or recent term).</summary>
    int SchoolsWithTermResultsCount,
    /// <summary>Schools that have not yet had their signed Data Consent forms recorded.</summary>
    IReadOnlyList<ComplianceSchoolDto> CompliancePending);

public record SchoolsByCountryDto(string CountryCode, string CountryName, int SchoolCount);

public record PaymentDelinquencyDto(Guid SchoolId, string SchoolName, int StudentCount, decimal AmountDue, string CurrencyCode);

public record ComplianceSchoolDto(Guid SchoolId, string SchoolName);
