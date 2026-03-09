using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Application;
using RiseFlow.Domain.Entities;
using RiseFlow.Infrastructure.Data;

namespace RiseFlow.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ISchoolContext _schoolContext;

    public SupportController(RiseFlowDbContext db, ISchoolContext schoolContext)
    {
        _db = db;
        _schoolContext = schoolContext;
    }

    /// <summary>
    /// List support tickets. SuperAdmin sees all; SchoolAdmin sees only their school's tickets
    /// via the global tenant filter.
    /// </summary>
    [HttpGet("tickets")]
    [ProducesResponseType(typeof(List<SupportTicketSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SupportTicketSummaryDto>>> GetTickets(CancellationToken ct)
    {
        var query = _db.SupportTickets.AsNoTracking();

        // Compute student counts per school to flag priority customers (500+ students).
        var studentCounts = await _db.Students
            .GroupBy(s => s.SchoolId)
            .Select(g => new { SchoolId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SchoolId, x => x.Count, ct);

        var list = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new SupportTicketSummaryDto(
                t.Id,
                t.SchoolId,
                t.Subject,
                t.Status,
                t.Priority,
                t.CreatedAtUtc,
                studentCounts.TryGetValue(t.SchoolId, out var c) ? c : 0))
            .ToListAsync(ct);

        return Ok(list);
    }

    /// <summary>
    /// Get all messages for a given ticket. Tenant filter ensures only the owning school
    /// (or SuperAdmin with unrestricted context) can see the conversation.
    /// </summary>
    [HttpGet("tickets/{ticketId:guid}/messages")]
    [ProducesResponseType(typeof(List<TicketMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TicketMessageDto>>> GetMessages(Guid ticketId, CancellationToken ct)
    {
        var ticketExists = await _db.SupportTickets.AnyAsync(t => t.Id == ticketId, ct);
        if (!ticketExists)
            return NotFound();

        var messages = await _db.TicketMessages
            .AsNoTracking()
            .Where(m => m.TicketId == ticketId)
            .OrderBy(m => m.SentAtUtc)
            .Select(m => new TicketMessageDto(m.Id, m.TicketId, m.SenderId, m.MessageBody, m.SentAtUtc, m.FromSchoolAdmin))
            .ToListAsync(ct);

        return Ok(messages);
    }
}

public record SupportTicketSummaryDto(
    Guid Id,
    Guid SchoolId,
    string Subject,
    string Status,
    string Priority,
    DateTime CreatedAtUtc,
    int StudentCount)
{
    public bool IsPriorityCustomer => StudentCount >= 500;
}

public record TicketMessageDto(
    Guid Id,
    Guid TicketId,
    Guid SenderId,
    string MessageBody,
    DateTime SentAtUtc,
    bool FromSchoolAdmin);

