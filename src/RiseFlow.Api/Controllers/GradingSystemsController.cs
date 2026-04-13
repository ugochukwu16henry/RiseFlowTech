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
[Route("api/grading-systems")]
[Authorize(Roles = Roles.SchoolAdmin)]
public class GradingSystemsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public GradingSystemsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<List<GradingSystem>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var data = await _db.GradingSystems
            .AsNoTracking()
            .Include(x => x.Rules.OrderByDescending(r => r.MinPercent))
            .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(ct);

        return Ok(data);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GradingSystem>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var data = await _db.GradingSystems
            .AsNoTracking()
            .Include(x => x.Rules.OrderByDescending(r => r.MinPercent))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (data == null)
            return NotFound();

        return Ok(data);
    }

    [HttpPost]
    public async Task<ActionResult<GradingSystem>> Create([FromBody] CreateGradingSystemRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var item = new GradingSystem
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            Name = request.Name.Trim(),
            ClassId = request.ClassId,
            TermId = request.TermId,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.GradingSystems.Add(item);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GradingSystem>> Update(Guid id, [FromBody] UpdateGradingSystemRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var item = await _db.GradingSystems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        item.Name = request.Name.Trim();
        item.ClassId = request.ClassId;
        item.TermId = request.TermId;
        item.IsActive = request.IsActive;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var item = await _db.GradingSystems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item == null)
            return NotFound();

        _db.GradingSystems.Remove(item);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("{gradingSystemId:guid}/rules")]
    public async Task<ActionResult<GradeRule>> CreateRule(Guid gradingSystemId, [FromBody] CreateGradeRuleRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var system = await _db.GradingSystems.FirstOrDefaultAsync(x => x.Id == gradingSystemId, ct);
        if (system == null)
            return NotFound(new { message = "Grading system not found." });

        var validation = await ValidateRuleRangeAsync(gradingSystemId, request.MinPercent, request.MaxPercent, null, ct);
        if (!string.IsNullOrEmpty(validation))
            return BadRequest(new { message = validation });

        var rule = new GradeRule
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            GradingSystemId = gradingSystemId,
            GradeLetter = request.GradeLetter.Trim(),
            MinPercent = request.MinPercent,
            MaxPercent = request.MaxPercent,
            GradePoint = request.GradePoint,
            Remarks = request.Remarks,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.GradeRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return Ok(rule);
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<ActionResult<GradeRule>> UpdateRule(Guid id, [FromBody] UpdateGradeRuleRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var rule = await _db.GradeRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (rule == null)
            return NotFound();

        var validation = await ValidateRuleRangeAsync(rule.GradingSystemId, request.MinPercent, request.MaxPercent, rule.Id, ct);
        if (!string.IsNullOrEmpty(validation))
            return BadRequest(new { message = validation });

        rule.GradeLetter = request.GradeLetter.Trim();
        rule.MinPercent = request.MinPercent;
        rule.MaxPercent = request.MaxPercent;
        rule.GradePoint = request.GradePoint;
        rule.Remarks = request.Remarks;
        rule.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(rule);
    }

    [HttpDelete("rules/{id:guid}")]
    public async Task<ActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();

        var rule = await _db.GradeRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (rule == null)
            return NotFound();

        _db.GradeRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<string?> ValidateRuleRangeAsync(Guid gradingSystemId, decimal minPercent, decimal maxPercent, Guid? editingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(minPercent.ToString()) || string.IsNullOrWhiteSpace(maxPercent.ToString()))
            return "MinPercent and MaxPercent are required.";

        if (minPercent < 0 || maxPercent > 100 || minPercent > maxPercent)
            return "Provide a valid percent range between 0 and 100.";

        var existingRules = await _db.GradeRules
            .AsNoTracking()
            .Where(x => x.GradingSystemId == gradingSystemId && (!editingId.HasValue || x.Id != editingId.Value))
            .ToListAsync(ct);

        var overlaps = existingRules.Any(x => minPercent <= x.MaxPercent && maxPercent >= x.MinPercent);
        if (overlaps)
            return "Rule range overlaps with an existing rule.";

        return null;
    }
}
