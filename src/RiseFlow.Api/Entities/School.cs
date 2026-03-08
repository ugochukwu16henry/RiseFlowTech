namespace RiseFlow.Api.Entities;

/// <summary>
/// Tenant entity. Each school is a tenant with a unique ID; all other data is scoped by SchoolId.
/// </summary>
public class School
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();
    public ICollection<Parent> Parents { get; set; } = new List<Parent>();
    public ICollection<Class> Classes { get; set; } = new List<Class>();
    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}
