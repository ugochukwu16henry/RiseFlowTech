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

    /// <summary>
    /// Revenue hub view: split global revenue into one‑time activation fees vs recurring monthly subscriptions,
    /// and surface top revenue‑generating schools.
    /// </summary>
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(SuperAdminRevenueViewModel), StatusCodes.Status200OK)]
    public async Task<ActionResult<SuperAdminRevenueViewModel>> GetRevenue(CancellationToken ct)
    {
        // Only consider paid billing records to avoid counting unpaid invoices as revenue.
        var paid = await _db.BillingRecords
            .AsNoTracking()
            .Where(b => b.AmountPaid != null && b.AmountPaid > 0)
            .ToListAsync(ct);

        // Global totals in original currency (SuperAdmin UI can show per‑currency or convert to NGN/USD if needed).
        var totalOneTime = paid.Sum(b => b.ActivationAmountDue);
        var totalMonthly = paid.Sum(b => b.MonthlyAmountDue);

        // Billable students: students above the 50‑student free tier across all active schools.
        var billableBySchool = await _db.Students
            .Where(s => s.IsActive)
            .GroupBy(s => s.SchoolId)
            .Select(g => new
            {
                SchoolId = g.Key,
                Total = g.Count(),
                Billable = g.Count() > CountryBillingConfig.FreeTierStudentCount
                    ? g.Count() - CountryBillingConfig.FreeTierStudentCount
                    : 0
            })
            .ToListAsync(ct);

        var totalBillableStudents = billableBySchool.Sum(x => x.Billable);
        var totalSchools = await _db.Schools.CountAsync(ct);

        // Top revenue schools: based on total AmountPaid to date.
        var paidBySchool = paid
            .GroupBy(b => b.SchoolId)
            .Select(g => new
            {
                SchoolId = g.Key,
                TotalPaid = g.Sum(x => x.AmountPaid ?? 0),
                MonthlyIncome = g.Sum(x => x.MonthlyAmountDue)
            })
            .ToList();

        var schoolIds = paidBySchool.Select(x => x.SchoolId).ToHashSet();
        var schoolMeta = await _db.Schools
            .AsNoTracking()
            .Where(s => schoolIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);
        var studentCounts = billableBySchool.ToDictionary(x => x.SchoolId, x => x.Total);

        var topRevenueSchools = paidBySchool
            .OrderByDescending(x => x.TotalPaid)
            .Take(10)
            .Join(
                schoolMeta,
                x => x.SchoolId,
                s => s.Id,
                (x, s) => new SchoolRevenueBreakdown
                {
                    SchoolId = s.Id,
                    SchoolName = s.Name,
                    StudentCount = studentCounts.GetValueOrDefault(s.Id, 0),
                    MonthlyIncome = x.MonthlyIncome,
                    TotalPaidToDate = x.TotalPaid
                })
            .ToList();

        var vm = new SuperAdminRevenueViewModel
        {
            TotalOneTimeFees = totalOneTime,
            TotalMonthlySubscriptions = totalMonthly,
            TotalSchools = totalSchools,
            TotalBillableStudents = totalBillableStudents,
            TopRevenueSchools = topRevenueSchools
        };

        return Ok(vm);
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
    
    /// <summary>
    /// Platform-wide compliance settings for NDPC / data protection. SuperAdmin can set DPO details and DPIA URL.
    /// </summary>
    [HttpGet("compliance-settings")]
    [ProducesResponseType(typeof(PlatformComplianceSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformComplianceSettingsDto>> GetComplianceSettings(CancellationToken ct)
    {
        var settings = await _db.PlatformComplianceSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct);
        if (settings == null)
        {
            return Ok(new PlatformComplianceSettingsDto(null, null, null, null));
        }

        return Ok(new PlatformComplianceSettingsDto(
            settings.DataProtectionOfficerName,
            settings.DataProtectionOfficerEmail,
            settings.DpiaDocumentUrl,
            settings.LastUpdatedUtc));
    }

    /// <summary>
    /// Update or create platform compliance settings (DPO and DPIA). SuperAdmin only.
    /// </summary>
    [HttpPut("compliance-settings")]
    [ProducesResponseType(typeof(PlatformComplianceSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformComplianceSettingsDto>> UpsertComplianceSettings(
        [FromBody] UpdatePlatformComplianceSettingsRequest request,
        CancellationToken ct)
    {
        var settings = await _db.PlatformComplianceSettings.FirstOrDefaultAsync(s => s.Id == 1, ct);
        if (settings == null)
        {
            settings = new PlatformComplianceSettings { Id = 1 };
            _db.PlatformComplianceSettings.Add(settings);
        }

        settings.DataProtectionOfficerName = request.DataProtectionOfficerName;
        settings.DataProtectionOfficerEmail = request.DataProtectionOfficerEmail;
        settings.DpiaDocumentUrl = request.DpiaDocumentUrl;
        settings.LastUpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new PlatformComplianceSettingsDto(
            settings.DataProtectionOfficerName,
            settings.DataProtectionOfficerEmail,
            settings.DpiaDocumentUrl,
            settings.LastUpdatedUtc));
    }
}

public record AuditLogDto(long Id, Guid? SchoolId, string Action, string EntityType, string? EntityId, string? UserEmail, string? UserName, string? Details, DateTime CreatedAtUtc);

public record PlatformComplianceSettingsDto(
    string? DataProtectionOfficerName,
    string? DataProtectionOfficerEmail,
    string? DpiaDocumentUrl,
    DateTime? LastUpdatedUtc);

public record UpdatePlatformComplianceSettingsRequest(
    string? DataProtectionOfficerName,
    string? DataProtectionOfficerEmail,
    string? DpiaDocumentUrl);
