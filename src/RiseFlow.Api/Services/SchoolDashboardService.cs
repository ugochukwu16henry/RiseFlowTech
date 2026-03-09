using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;

namespace RiseFlow.Api.Services;

/// <summary>
/// Calculates key SchoolAdmin dashboard metrics: enrollment, staff, pending results,
/// current subscription fee estimate, and recent activity.
/// </summary>
public class SchoolDashboardService
{
    private readonly RiseFlowDbContext _db;

    public SchoolDashboardService(RiseFlowDbContext db)
    {
        _db = db;
    }

    public async Task<SchoolDashboardViewModel> GetDashboardStatsAsync(Guid schoolId, CancellationToken ct = default)
    {
        var totalStudents = await _db.Students.CountAsync(s => s.SchoolId == schoolId && s.IsActive, ct);
        var totalTeachers = await _db.Teachers.CountAsync(t => t.SchoolId == schoolId, ct);

        // Pending results: results that don't yet have a grade letter (not fully processed/approved).
        var pendingResults = await _db.StudentResults
            .CountAsync(r => r.SchoolId == schoolId && (r.GradeLetter == null || r.GradeLetter == ""), ct);

        var school = await _db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schoolId, ct);
        var currencyCode = school?.CurrencyCode?.Trim().ToUpperInvariant() ?? "NGN";

        // "First 50 Free" billing logic using shared BillingService helper.
        var monthlyFee = BillingService.ComputeAmountDue(totalStudents, currencyCode);

        var recentActivities = await _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.SchoolId == schoolId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(5)
            .ToListAsync(ct);

        return new SchoolDashboardViewModel(
            StudentCount: totalStudents,
            TeacherCount: totalTeachers,
            PendingResultsCount: pendingResults,
            MonthlySubscriptionFee: monthlyFee,
            RecentActivities: recentActivities);
    }
}

