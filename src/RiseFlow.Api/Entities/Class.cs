namespace RiseFlow.Api.Entities;

/// <summary>
/// Class (e.g. "Grade 1A"). Tenant-scoped. A teacher can be assigned to multiple classes.
/// </summary>
public class Class : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid GradeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AcademicYear { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Grade Grade { get; set; } = null!;
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<TeacherClass> TeacherClasses { get; set; } = new List<TeacherClass>();
}
