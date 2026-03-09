using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Infrastructure.Data;
using RiseFlow.Infrastructure.Identity;

namespace RiseFlow.WebAPI.Controllers;

/// <summary>
/// Super Admin Control Room: high-level business pulse (schools, students, revenue, alerts).
/// Restricted to SuperAdmin role.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class ControlRoomController : ControllerBase
{
    private readonly RiseFlowDbContext _db;

    public ControlRoomController(RiseFlowDbContext db)
    {
        _db = db;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ControlRoomDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ControlRoomDashboardDto>> GetDashboard(CancellationToken ct)
    {
        var totalStudents = await _db.Students.CountAsync(ct);

        // Approximate number of schools from tenant keys on Students.
        var totalSchools = await _db.Students
            .Select(s => s.SchoolId)
            .Distinct()
            .CountAsync(ct);

        // "First 50 Free" billing estimate: ₦500 per active student beyond 50 per school.
        var studentCountsPerSchool = await _db.Students
            .GroupBy(s => s.SchoolId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        decimal totalMonthlyRevenueNgn = 0;
        foreach (var s in studentCountsPerSchool)
        {
            var billable = Math.Max(0, s.Count - 50);
            totalMonthlyRevenueNgn += billable * 500m;
        }

        // Latest signups from Identity users.
        var latestSignups = await _db.Users
            .OrderByDescending(u => u.Id) // if CreatedAtUtc is not set yet, this still approximates recency
            .Take(10)
            .Select(u => new LatestSignupDto(
                u.Id,
                u.Email ?? "",
                u.FullName ?? u.Email ?? "",
                u.SchoolId,
                u.CreatedAtUtc))
            .ToListAsync(ct);

        // System alerts: here we only surface a placeholder for failed payments; in a full app
        // this would be driven by a BillingRecords table.
        var alerts = new List<SystemAlertDto>();

        return Ok(new ControlRoomDashboardDto(
            TotalSchools: totalSchools,
            TotalStudents: totalStudents,
            TotalMonthlyRevenueNgn: totalMonthlyRevenueNgn,
            LatestSignups: latestSignups,
            SystemAlerts: alerts));
    }
}

public record ControlRoomDashboardDto(
    int TotalSchools,
    int TotalStudents,
    decimal TotalMonthlyRevenueNgn,
    IReadOnlyList<LatestSignupDto> LatestSignups,
    IReadOnlyList<SystemAlertDto> SystemAlerts);

public record LatestSignupDto(
    Guid UserId,
    string Email,
    string FullName,
    Guid? SchoolId,
    DateTime CreatedAtUtc);

public record SystemAlertDto(
    string Id,
    string Message,
    string Severity); // e.g. "info", "warning", "error"

