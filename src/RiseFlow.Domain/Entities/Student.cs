namespace RiseFlow.Domain.Entities;

/// <summary>
/// Student profile. Tenant-scoped by SchoolId; all queries are filtered by SchoolId at the DbContext level.
/// This class intentionally keeps to core identity/academic fields – additional profile fields can be added later.
/// </summary>
public class Student : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Tenant key. Identifies which school this student belongs to.</summary>
    public Guid SchoolId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
}

