namespace RiseFlow.Api.Entities;

/// <summary>
/// Records "who did what" for sensitive operations (e.g. grade changes).
/// Not tenant-scoped so Super Admin can query across schools.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public Guid? SchoolId { get; set; }
    public string Action { get; set; } = null!; // Created, Updated, Deleted
    public string EntityType { get; set; } = null!; // e.g. StudentResult
    public string? EntityId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserName { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
