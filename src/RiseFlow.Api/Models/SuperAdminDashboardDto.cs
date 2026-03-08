namespace RiseFlow.Api.Models;

public record SuperAdminDashboardDto(
    int TotalSchools,
    int ActiveSchools,
    int TotalStudents,
    int ActiveStudents,
    decimal TotalRevenueUsd,
    int BillingRecordsCount);
