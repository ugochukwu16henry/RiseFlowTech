using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
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

        return Ok(new SuperAdminDashboardDto(
            TotalSchools: totalSchools,
            ActiveSchools: activeSchools,
            TotalStudents: totalStudents,
            ActiveStudents: activeStudents,
            TotalRevenueUsd: totalRevenue,
            MonthlyRevenueUsd: monthlyRevenueUsd,
            BillingRecordsCount: billingRecordsCount,
            TotalResultsProcessed: totalResultsProcessed,
            SchoolsByCountry: schoolsByCountry));
    }
}
