namespace RiseFlow.Domain.Entities;

/// <summary>
/// Support ticket raised by a school. Tenant-scoped by SchoolId so all queries are
/// automatically filtered at the DbContext level.
/// </summary>
public class SupportTicket : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Tenant key: which school is asking?</summary>
    public Guid SchoolId { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>Open, In-Progress, Resolved.</summary>
    public string Status { get; set; } = "Open";

    /// <summary>Low, Medium, High, Urgent.</summary>
    public string Priority { get; set; } = "Medium";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}

/// <summary>
/// Individual message in a support ticket conversation.
/// </summary>
public class TicketMessage
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }
    public SupportTicket Ticket { get; set; } = null!;

    /// <summary>Identity user ID (Principal / SchoolAdmin / SuperAdmin).</summary>
    public Guid SenderId { get; set; }

    public string MessageBody { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True when sent from a school-side user (SchoolAdmin/principal), false for SuperAdmin or support staff.
    /// </summary>
    public bool FromSchoolAdmin { get; set; }
}

