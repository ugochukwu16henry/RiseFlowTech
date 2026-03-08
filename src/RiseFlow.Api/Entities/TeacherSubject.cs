namespace RiseFlow.Api.Entities;

/// <summary>
/// Links a teacher to subjects they can teach (many-to-many).
/// </summary>
public class TeacherSubject
{
    public Guid TeacherId { get; set; }
    public Guid SubjectId { get; set; }
    public DateTime AssignedAtUtc { get; set; }

    public Teacher Teacher { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
}
