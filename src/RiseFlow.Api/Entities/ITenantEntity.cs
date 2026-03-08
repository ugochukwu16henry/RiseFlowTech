namespace RiseFlow.Api.Entities;

/// <summary>
/// Marks an entity as belonging to a tenant (School). Every tenant-scoped table has SchoolId for data isolation.
/// </summary>
public interface ITenantEntity
{
    Guid SchoolId { get; set; }
}
