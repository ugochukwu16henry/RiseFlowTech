using RiseFlow.Domain.Entities;

namespace RiseFlow.Application.Support;

/// <summary>
/// Abstraction for creating and updating support tickets and messages.
/// Implemented in Infrastructure; consumed by WebAPI and other application layers.
/// </summary>
public interface ISupportService
{
    Task<SupportTicket> CreateTicketAsync(string subject, Guid userId, string initialMessage, CancellationToken ct = default);

    /// <summary>
    /// Append a message to an existing ticket. Returns the created message and the ticket's SchoolId
    /// so callers (e.g. SignalR hubs) can route notifications to the correct tenant group.
    /// </summary>
    Task<(TicketMessage Message, Guid SchoolId)> AddMessageAsync(Guid ticketId, Guid userId, string messageBody, bool fromSchoolAdmin, CancellationToken ct = default);
}

