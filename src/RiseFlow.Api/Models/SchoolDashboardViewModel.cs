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

/// <summary>
/// Lightweight metrics for staff-facing dashboard widgets.
/// </summary>
public record StaffDashboardMetricsDto(
    int TasksCount,
    int PendingApprovalsCount,
    int OfficeQueueCount,
    int PersonalAssignmentsCount,
    int PendingPromotionRequestsCount,
    int PendingFeeVerificationsCount,
    int PendingResultEntriesCount,
    int RecentDeniedAttemptsCount,
    bool HasTeacherProfile);

