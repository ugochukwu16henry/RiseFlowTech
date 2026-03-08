namespace RiseFlow.Api.Models;

public record SuperAdminDashboardDto(
    int TotalSchools,
    int ActiveSchools,
    int TotalStudents,
    int ActiveStudents,
    decimal TotalRevenueUsd,
    decimal MonthlyRevenueUsd,
    int BillingRecordsCount,
    IReadOnlyList<SchoolsByCountryDto> SchoolsByCountry);

public record SchoolsByCountryDto(string CountryCode, string CountryName, int SchoolCount);
