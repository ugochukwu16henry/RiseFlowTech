namespace RiseFlow.Api.Services;

/// <summary>
/// Provides the current tenant (School) ID and user identity for the request.
/// </summary>
public interface ITenantContext
{
    Guid? CurrentSchoolId { get; }
    bool IsSuperAdmin { get; }
    string? CurrentUserEmail { get; }
}
