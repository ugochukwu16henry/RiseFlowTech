namespace RiseFlow.Domain;

/// <summary>
/// Marker interface for tenant-scoped entities. Every record that belongs to a school
/// must carry the SchoolId so multi-tenancy filters can be applied automatically.
/// </summary>
public interface ITenantEntity
{
    Guid SchoolId { get; set; }
}

