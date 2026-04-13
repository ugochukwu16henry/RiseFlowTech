namespace RiseFlow.Api.Entities;

/// <summary>
/// Tracks parent-initiated student profile edits and enforces cooldown windows between edits.
/// </summary>
public class StudentParentEditWindow : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ParentId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime LastEditedAtUtc { get; set; }
    public DateTime NextEditableAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Parent Parent { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
