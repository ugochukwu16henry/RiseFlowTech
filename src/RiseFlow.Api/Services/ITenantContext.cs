namespace RiseFlow.Api.Services;

/// <summary>
/// Provides the current tenant (School) ID for the request. Used for multi-tenant data isolation.
/// </summary>
public interface ITenantContext
{
    Guid? CurrentSchoolId { get; }
    bool IsSuperAdmin { get; }
}
