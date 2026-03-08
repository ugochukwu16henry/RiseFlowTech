namespace RiseFlow.Api.Entities;

/// <summary>
/// Parent/guardian profile. Tenant-scoped. A parent can be linked to multiple students (many-to-many).
/// </summary>
public class Parent : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? Relationship { get; set; } // e.g. "Father", "Mother", "Guardian"
    public string? ResidentialAddress { get; set; }
    public string? Occupation { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public ICollection<StudentParent> StudentParents { get; set; } = new List<StudentParent>();
}
