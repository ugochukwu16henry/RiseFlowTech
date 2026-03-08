namespace RiseFlow.Api.Middleware;

/// <summary>
/// Multi-tenant middleware for RiseFlow. Extracts TenantId (School ID) from the request header
/// and stores it for the request lifecycle so EF can filter data by School.
/// </summary>
public class TenantMiddleware
{
    public const string TenantIdHeaderName = "X-Tenant-Id";
    public const string TenantIdItemKey = "RiseFlow.TenantId";

    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(TenantIdHeaderName, out var value) &&
            !string.IsNullOrWhiteSpace(value) &&
            Guid.TryParse(value.ToString().Trim(), out var tenantId))
        {
            context.Items[TenantIdItemKey] = tenantId;
        }

        await _next(context);
    }
}
