using Microsoft.EntityFrameworkCore;
using RiseFlow.Application;
using RiseFlow.Application.Support;
using RiseFlow.Domain.Entities;
using RiseFlow.Infrastructure.Data;

namespace RiseFlow.Infrastructure.Support;

/// <summary>
/// Concrete implementation of ISupportService backed by RiseFlowDbContext.
/// Responsible for persisting support tickets and messages in a tenant-aware way.
/// </summary>
public class SupportService : ISupportService
{
    private readonly RiseFlowDbContext _db;
    private readonly ISchoolContext _schoolContext;

    public SupportService(RiseFlowDbContext db, ISchoolContext schoolContext)
    {
        _db = db;
        _schoolContext = schoolContext;
    }

    public async Task<SupportTicket> CreateTicketAsync(string subject, Guid userId, string initialMessage, CancellationToken ct = default)
    {
        var schoolId = _schoolContext.SchoolId;
        if (schoolId == Guid.Empty)
            throw new InvalidOperationException("School context is not set for support ticket creation.");

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            Subject = subject,
            Status = "Open",
            Priority = "Medium",
            CreatedAtUtc = DateTime.UtcNow
        };

        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            SenderId = userId,
            MessageBody = initialMessage,
            FromSchoolAdmin = true,
            SentAtUtc = DateTime.UtcNow
        };

        _db.SupportTickets.Add(ticket);
        _db.TicketMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        return ticket;
    }

    public async Task<(TicketMessage Message, Guid SchoolId)> AddMessageAsync(Guid ticketId, Guid userId, string messageBody, bool fromSchoolAdmin, CancellationToken ct = default)
    {
        var ticket = await _db.SupportTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new InvalidOperationException("Support ticket not found.");

        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            SenderId = userId,
            MessageBody = messageBody,
            FromSchoolAdmin = fromSchoolAdmin,
            SentAtUtc = DateTime.UtcNow
        };

        _db.TicketMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        return (message, ticket.SchoolId);
    }
}

