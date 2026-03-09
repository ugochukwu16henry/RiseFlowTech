namespace RiseFlow.Domain.Entities;

/// <summary>
/// Teacher profile. Tenant-scoped by SchoolId; all reads and writes are automatically constrained to the current school.
/// </summary>
public class Teacher : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Tenant key. Identifies which school this teacher belongs to.</summary>
    public Guid SchoolId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
}

