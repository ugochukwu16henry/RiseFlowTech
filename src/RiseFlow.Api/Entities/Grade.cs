namespace RiseFlow.Api.Entities;

/// <summary>
/// Grade level (e.g. Grade 1, Grade 2). Tenant-scoped.
/// </summary>
public class Grade : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Grade 1", "Form 1"
    public int LevelOrder { get; set; } // for sorting
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public ICollection<Class> Classes { get; set; } = new List<Class>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
