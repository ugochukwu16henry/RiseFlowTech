using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

public class AuditLogService : IAuditLogService
{
    private readonly RiseFlowDbContext _db;

    public AuditLogService(RiseFlowDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        Guid? schoolId,
        string action,
        string entityType,
        string? entityId,
        string? userEmail,
        string? userName,
        string? details,
        CancellationToken ct = default)
    {
        var entry = new AuditLog
        {
            SchoolId = schoolId,
            Action = action.Length > 32 ? action[..32] : action,
            EntityType = entityType.Length > 64 ? entityType[..64] : entityType,
            EntityId = entityId != null && entityId.Length > 36 ? entityId[..36] : entityId,
            UserEmail = userEmail != null && userEmail.Length > 256 ? userEmail[..256] : userEmail,
            UserName = userName != null && userName.Length > 256 ? userName[..256] : userName,
            Details = details != null && details.Length > 1024 ? details[..1024] : details,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }
}
