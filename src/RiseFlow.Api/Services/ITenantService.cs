namespace RiseFlow.Api.Services;

/// <summary>
/// Holds the current tenant (School) ID for the request lifecycle.
/// Set by TenantMiddleware from the X-Tenant-Id header, or from the authenticated user's SchoolId claim.
/// Used by EF global query filters to automatically filter data by School.
/// </summary>
public interface ITenantService
{
    /// <summary>Current tenant/school ID for this request (from header or user claim).</summary>
    Guid? TenantId { get; }
}
