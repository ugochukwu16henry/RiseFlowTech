namespace RiseFlow.Api.Entities;

/// <summary>
/// Links a class to subjects taught in that class (many-to-many).
/// </summary>
public class ClassSubject
{
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public int? Order { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Class Class { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
}
