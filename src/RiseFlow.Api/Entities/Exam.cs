namespace RiseFlow.Api.Entities;

public class Exam : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TermId { get; set; }
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public Guid? CreatedByTeacherId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Class Class { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public AcademicTerm Term { get; set; } = null!;
    public Teacher? CreatedByTeacher { get; set; }
    public ICollection<StudentResult> Results { get; set; } = new List<StudentResult>();
}
