namespace RiseFlow.Api.Entities;

/// <summary>
/// Join table: Teacher can be assigned to multiple Classes, Class can have multiple Teachers.
/// </summary>
public class TeacherClass
{
    public Guid TeacherId { get; set; }
    public Guid ClassId { get; set; }
    public string? RoleInClass { get; set; } // e.g. "Class Teacher", "Subject Teacher"
    public DateTime AssignedAtUtc { get; set; }

    public Teacher Teacher { get; set; } = null!;
    public Class Class { get; set; } = null!;
}
