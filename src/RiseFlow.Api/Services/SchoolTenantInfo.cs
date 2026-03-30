using Finbuckle.MultiTenant.Abstractions;

namespace RiseFlow.Api.Services;

/// <summary>
/// Finbuckle tenant descriptor mapped from RiseFlow School records.
/// </summary>
public class SchoolTenantInfo : ITenantInfo
{
    public string Id { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ConnectionString { get; set; }
    public string? Items { get; set; }
}
