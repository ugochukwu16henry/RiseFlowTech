using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RiseFlow.Application;
using RiseFlow.Application.Support;

namespace RiseFlow.WebAPI.Hubs;

/// <summary>
/// SignalR hub for real-time RiseFlow support chat between schools and SuperAdmin.
/// </summary>
[Authorize]
public class SupportHub : Hub
{
    private readonly ISchoolContext _schoolContext;
    private readonly ISupportService _support;

    public SupportHub(ISchoolContext schoolContext, ISupportService support)
    {
        _schoolContext = schoolContext;
        _support = support;
    }

    /// <summary>
    /// School admins join their private group based on SchoolId so messages are isolated per tenant.
    /// </summary>
    public async Task JoinSupportGroup()
    {
        var schoolId = _schoolContext.SchoolId;
        if (schoolId == Guid.Empty)
            throw new HubException("School context is not available.");

        await Groups.AddToGroupAsync(Context.ConnectionId, schoolId.ToString());
    }

    /// <summary>
    /// SuperAdmins join a shared "war room" group where they see all incoming tickets/messages.
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    public async Task JoinSuperAdminWarRoom()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "SuperAdmins");
    }

    /// <summary>
    /// School admin creates a new ticket with an initial message.
    /// </summary>
    public async Task<Guid> CreateTicket(string subject, string message)
    {
        var userId = GetUserIdOrThrow();
        var ticket = await _support.CreateTicketAsync(subject, userId, message, Context.ConnectionAborted);

        // Notify this school's group
        await Clients.Group(ticket.SchoolId.ToString())
            .SendAsync("ReceiveSupportMessage", ticket.Id, message, fromSchoolAdmin: true);

        // Notify SuperAdmin inbox
        await Clients.Group("SuperAdmins")
            .SendAsync("TicketCreated", ticket.Id, ticket.SchoolId.ToString(), ticket.Subject, ticket.Priority);

        return ticket.Id;
    }

    /// <summary>
    /// Append a message to an existing ticket. fromSchoolAdmin indicates direction
    /// so the client can style messages differently.
    /// </summary>
    public async Task SendMessage(Guid ticketId, string messageBody, bool fromSchoolAdmin)
    {
        var userId = GetUserIdOrThrow();
        var (message, schoolId) = await _support.AddMessageAsync(ticketId, userId, messageBody, fromSchoolAdmin, Context.ConnectionAborted);

        // Deliver to the school's group
        await Clients.Group(schoolId.ToString())
            .SendAsync("ReceiveSupportMessage", ticketId, message.MessageBody, fromSchoolAdmin);

        // Deliver to SuperAdmin "war room" as well
        await Clients.Group("SuperAdmins")
            .SendAsync("ReceiveSupportMessageAdminView", ticketId, schoolId.ToString(), message.MessageBody, fromSchoolAdmin);
    }

    private Guid GetUserIdOrThrow()
    {
        var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var guid))
            throw new HubException("User id is missing or invalid.");
        return guid;
    }
}

