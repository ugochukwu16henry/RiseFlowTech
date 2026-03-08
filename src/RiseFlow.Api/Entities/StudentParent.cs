namespace RiseFlow.Api.Entities;

/// <summary>
/// Join table: Student can have multiple Parents, Parent can have multiple Students.
/// </summary>
public class StudentParent
{
    public Guid StudentId { get; set; }
    public Guid ParentId { get; set; }
    public string? RelationshipToStudent { get; set; } // e.g. "Father", "Mother"
    public bool IsPrimaryContact { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Student Student { get; set; } = null!;
    public Parent Parent { get; set; } = null!;
}
