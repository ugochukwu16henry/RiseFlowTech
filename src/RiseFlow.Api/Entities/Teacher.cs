namespace RiseFlow.Api.Entities;

/// <summary>
/// Teacher profile. Tenant-scoped. A teacher can be assigned to multiple classes (many-to-many).
/// </summary>
public class Teacher : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? StaffId { get; set; }
    public string? SubjectSpecialization { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public ICollection<TeacherClass> TeacherClasses { get; set; } = new List<TeacherClass>();
}
