using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AcademicTermsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public AcademicTermsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AcademicTerm>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AcademicTerm>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var list = await _db.AcademicTerms
            .AsNoTracking()
            .OrderByDescending(t => t.AcademicYear)
            .ThenBy(t => t.StartDate)
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(AcademicTerm), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AcademicTerm>> GetCurrent(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var term = await _db.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsCurrent, ct);
        if (term == null)
            return NotFound();
        return Ok(term);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AcademicTerm), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AcademicTerm>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var term = await _db.AcademicTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (term == null)
            return NotFound();
        return Ok(term);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AcademicTerm), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AcademicTerm>> Create([FromBody] CreateAcademicTermRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        if (request.SetAsCurrent)
        {
            await _db.AcademicTerms.Where(t => t.SchoolId == _tenant.CurrentSchoolId).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsCurrent, false), ct);
        }
        var term = new AcademicTerm
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            Name = request.Name,
            AcademicYear = request.AcademicYear,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsCurrent = request.SetAsCurrent,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.AcademicTerms.Add(term);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = term.Id }, term);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AcademicTerm), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AcademicTerm>> Update(Guid id, [FromBody] UpdateAcademicTermRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var term = await _db.AcademicTerms.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (term == null)
            return NotFound();
        term.Name = request.Name;
        term.AcademicYear = request.AcademicYear;
        term.StartDate = request.StartDate;
        term.EndDate = request.EndDate;
        if (request.SetAsCurrent)
        {
            await _db.AcademicTerms.Where(t => t.SchoolId == term.SchoolId).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsCurrent, false), ct);
            term.IsCurrent = true;
        }
        term.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(term);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var term = await _db.AcademicTerms.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (term == null)
            return NotFound();
        _db.AcademicTerms.Remove(term);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
