namespace RiseFlow.Api.Entities;

/// <summary>
/// Maps which teacher teaches which subject in which class (teacher-class-subject assignment).
/// </summary>
public class TeacherClassSubject
{
    public Guid TeacherId { get; set; }
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public DateTime AssignedAtUtc { get; set; }

    public Teacher Teacher { get; set; } = null!;
    public Class Class { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
}
