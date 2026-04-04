using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Models;

/// <summary>
/// Aggregated dashboard metrics for a single school (SchoolAdmin view).
/// </summary>
public record SchoolDashboardViewModel(
    Guid SchoolId,
    int StudentCount,
    int TeacherCount,
    int PendingResultsCount,
    decimal MonthlySubscriptionFee,
    string CurrencyCode,
    decimal UnpaidFeesTotal,
    IReadOnlyList<AuditLog> RecentActivities);

