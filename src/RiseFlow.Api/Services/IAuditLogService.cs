namespace RiseFlow.Api.Services;

/// <summary>
/// Records "who did what" for sensitive operations (e.g. grade changes).
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(
        Guid? schoolId,
        string action,
        string entityType,
        string? entityId,
        string? userEmail,
        string? userName,
        string? details,
        CancellationToken ct = default);
}
