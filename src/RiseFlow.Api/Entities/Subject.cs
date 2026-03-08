namespace RiseFlow.Api.Entities;

/// <summary>
/// Subject taught at the school (e.g. Mathematics, English). Tenant-scoped.
/// </summary>
public class Subject : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<ClassSubject> ClassSubjects { get; set; } = new List<ClassSubject>();
    public ICollection<TeacherClassSubject> TeacherClassSubjects { get; set; } = new List<TeacherClassSubject>();
    public ICollection<StudentResult> StudentResults { get; set; } = new List<StudentResult>();
}
