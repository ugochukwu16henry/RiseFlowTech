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

    /// <summary>Control room dashboard: total schools, active students, revenue metrics.</summary>
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

        return Ok(new SuperAdminDashboardDto(
            TotalSchools: totalSchools,
            ActiveSchools: activeSchools,
            TotalStudents: totalStudents,
            ActiveStudents: activeStudents,
            TotalRevenueUsd: totalRevenue,
            BillingRecordsCount: billingRecordsCount));
    }
}
