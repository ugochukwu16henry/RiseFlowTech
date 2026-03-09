using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = Roles.SuperAdmin)]
public class SuperAdminController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly BillingService _billing;

    public SuperAdminController(RiseFlowDbContext db, BillingService billing)
    {
        _db = db;
        _billing = billing;
    }

    private static readonly IReadOnlyDictionary<string, string> CountryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NG"] = "Nigeria", ["GH"] = "Ghana", ["KE"] = "Kenya", ["ZA"] = "South Africa",
        ["TZ"] = "Tanzania", ["UG"] = "Uganda", ["SN"] = "Senegal", ["CI"] = "Côte d'Ivoire",
        ["CM"] = "Cameroon", ["ET"] = "Ethiopia", ["RW"] = "Rwanda", ["ZM"] = "Zambia",
    };

    /// <summary>Control room dashboard: schools by country (map data), total and monthly revenue.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(SuperAdminDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SuperAdminDashboardDto>> GetDashboard(CancellationToken ct)
    {
        var totalSchools = await _db.Schools.CountAsync(ct);
        var activeSchools = await _db.Schools.CountAsync(s => s.IsActive, ct);
        var totalStudents = await _db.Students.CountAsync(ct);
        var activeStudents = await _db.Students.CountAsync(s => s.IsActive, ct);
        var totalRevenue = await _billing.GetTotalRevenueUsdAsync(ct);
        var billingRecordsCount = await _db.BillingRecords.CountAsync(ct);
        var totalResultsProcessed = await _db.StudentResults.LongCountAsync(ct);

        var now = DateTime.UtcNow;
        var firstOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthlyRevenue = await _db.BillingRecords
            .Where(b => b.PaidAtUtc >= firstOfMonth && b.AmountPaid != null)
            .ToListAsync(ct);
        var monthlyRevenueUsd = 0m;
        foreach (var b in monthlyRevenue)
            monthlyRevenueUsd += _billing.ConvertToUsd(b.AmountPaid ?? 0, b.CurrencyCode);

        var byCountry = await _db.Schools
            .Where(s => s.IsActive && s.CountryCode != null)
            .GroupBy(s => s.CountryCode!)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var schoolsByCountry = byCountry
            .OrderByDescending(x => x.Count)
            .Select(x => new SchoolsByCountryDto(x.Code, CountryNames.GetValueOrDefault(x.Code, x.Code), x.Count))
            .ToList();

        // Payment delinquency: schools with >50 students and at least one unpaid billing record
        var over50Ids = await _db.Students.Where(st => st.IsActive).GroupBy(st => st.SchoolId)
            .Where(g => g.Count() > CountryBillingConfig.FreeTierStudentCount)
            .Select(g => g.Key).ToListAsync(ct);
        var unpaidRecords = over50Ids.Count > 0
            ? await _db.BillingRecords
                .Where(b => over50Ids.Contains(b.SchoolId) && b.AmountDue > 0 && (b.AmountPaid == null || b.AmountPaid < b.AmountDue))
                .OrderByDescending(b => b.CreatedAtUtc)
                .ToListAsync(ct)
            : new List<BillingRecord>();
        var latestUnpaidBySchool = unpaidRecords.GroupBy(b => b.SchoolId).ToDictionary(g => g.Key, g => g.First());
        var studentCountBySchool = await _db.Students.Where(st => st.IsActive && over50Ids.Contains(st.SchoolId))
            .GroupBy(st => st.SchoolId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var countDict = studentCountBySchool.ToDictionary(x => x.Key, x => x.Count);
        var schoolsOver50 = await _db.Schools.AsNoTracking().Where(s => over50Ids.Contains(s.Id)).Select(s => new { s.Id, s.Name }).ToListAsync(ct);
        var paymentDelinquency = schoolsOver50
            .Where(s => latestUnpaidBySchool.ContainsKey(s.Id))
            .Select(s => new PaymentDelinquencyDto(s.Id, s.Name, countDict.GetValueOrDefault(s.Id, 0), latestUnpaidBySchool[s.Id].AmountDue, latestUnpaidBySchool[s.Id].CurrencyCode))
            .ToList();

        // Data health: schools that have completed term results (at least one StudentResult)
        var schoolsWithTermResultsCount = await _db.Students.Where(s => s.Results.Any()).Select(s => s.SchoolId).Distinct().CountAsync(ct);

        // Compliance: schools that have not yet had signed Data Consent forms recorded
        var compliancePending = await _db.Schools.AsNoTracking()
            .Where(s => s.IsActive && s.DataConsentFormReceivedAt == null)
            .Select(s => new ComplianceSchoolDto(s.Id, s.Name))
            .ToListAsync(ct);

        return Ok(new SuperAdminDashboardDto(
            TotalSchools: totalSchools,
            ActiveSchools: activeSchools,
            TotalStudents: totalStudents,
            ActiveStudents: activeStudents,
            TotalRevenueUsd: totalRevenue,
            MonthlyRevenueUsd: monthlyRevenueUsd,
            BillingRecordsCount: billingRecordsCount,
            TotalResultsProcessed: totalResultsProcessed,
            SchoolsByCountry: schoolsByCountry,
            PaymentDelinquency: paymentDelinquency,
            SchoolsWithTermResultsCount: schoolsWithTermResultsCount,
            CompliancePending: compliancePending));
    }

    /// <summary>Audit log: who did what (e.g. grade changes). Super Admin only.</summary>
    [HttpGet("audit")]
    [ProducesResponseType(typeof(List<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditLogDto>>> GetAuditLog(
        [FromQuery] Guid? schoolId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int limit = 200,
        CancellationToken ct = default)
    {
        var cap = Math.Clamp(limit, 1, 1000);
        IQueryable<AuditLog> query = _db.AuditLogs.AsNoTracking();
        if (schoolId.HasValue)
            query = query.Where(a => a.SchoolId == schoolId.Value);
        if (fromUtc.HasValue)
            query = query.Where(a => a.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(a => a.CreatedAtUtc <= toUtc.Value);
        var list = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(cap)
            .Select(a => new AuditLogDto(a.Id, a.SchoolId, a.Action, a.EntityType, a.EntityId, a.UserEmail, a.UserName, a.Details, a.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(list);
    }
}

public record AuditLogDto(long Id, Guid? SchoolId, string Action, string EntityType, string? EntityId, string? UserEmail, string? UserName, string? Details, DateTime CreatedAtUtc);
