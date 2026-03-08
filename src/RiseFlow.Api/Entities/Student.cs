namespace RiseFlow.Api.Entities;

/// <summary>
/// Student profile. Tenant-scoped. A student can have multiple parents (many-to-many).
/// </summary>
public class Student : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? AdmissionNumber { get; set; }
    public Guid? ClassId { get; set; }
    public Guid? GradeId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Class? Class { get; set; }
    public Grade? Grade { get; set; }
    public ICollection<StudentParent> StudentParents { get; set; } = new List<StudentParent>();
    public ICollection<StudentResult> Results { get; set; } = new List<StudentResult>();
}
