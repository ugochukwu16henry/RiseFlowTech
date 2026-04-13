using System.Security.Claims;
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
[Route("api/events")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IConfiguration _configuration;

    public EventsController(RiseFlowDbContext db, ITenantContext tenant, IConfiguration configuration)
    {
        _db = db;
        _tenant = tenant;
        _configuration = configuration;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<SchoolEvent>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SchoolEvent>>> List([FromQuery] int? limit, CancellationToken ct)
    {
        if (!IsFeatureEnabled())
            return NotFound();

        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var schoolId = _tenant.CurrentSchoolId.Value;
        var now = DateTime.UtcNow;
        var query = _db.SchoolEvents
            .AsNoTracking()
            .Where(e => e.SchoolId == schoolId && e.EndAtUtc >= now.AddDays(-1))
            .OrderBy(e => e.StartAtUtc)
            .AsQueryable();

        if (limit.HasValue && limit.Value > 0)
            query = query.Take(Math.Min(limit.Value, 100));

        return Ok(await query.ToListAsync(ct));
    }

    [HttpPost]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<SchoolEvent>> Create([FromBody] CreateSchoolEventRequest request, CancellationToken ct)
    {
        if (!IsFeatureEnabled())
            return NotFound();

        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Title is required." });
        if (request.EndAtUtc <= request.StartAtUtc)
            return BadRequest(new { message = "End time must be after start time." });

        var entity = new SchoolEvent
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            ColorHex = request.ColorHex,
            CreatedByUserId = TryGetCurrentUserId(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.SchoolEvents.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<SchoolEvent>> Update(Guid id, [FromBody] UpdateSchoolEventRequest request, CancellationToken ct)
    {
        if (!IsFeatureEnabled())
            return NotFound();

        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var entity = await _db.SchoolEvents.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Title is required." });

        if (request.EndAtUtc <= request.StartAtUtc)
            return BadRequest(new { message = "End time must be after start time." });

        entity.Title = request.Title.Trim();
        entity.Description = request.Description?.Trim();
        entity.StartAtUtc = request.StartAtUtc;
        entity.EndAtUtc = request.EndAtUtc;
        entity.ColorHex = request.ColorHex;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(entity);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!IsFeatureEnabled())
            return NotFound();

        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var entity = await _db.SchoolEvents.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity == null)
            return NotFound();

        _db.SchoolEvents.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Guid? TryGetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private bool IsFeatureEnabled() => _configuration.GetValue<bool>("Features:EnableNoticesEventsV1");
}
