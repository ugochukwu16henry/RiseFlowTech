namespace RiseFlow.Api.Models;

public record SuperAdminSchoolRowDto(
    Guid Id,
    string Name,
    string? CountryCode,
    string? CurrencyCode,
    bool IsActive,
    int StudentCount,
    int TeacherCount,
    int ParentCount,
    DateTime CreatedAtUtc,
    string? OwnerEmail);

public record OffboardSchoolRequest(
    string? Reason,
    string? ExportRecipientEmail);

public record OffboardSchoolResult(
    Guid SchoolId,
    string SchoolName,
    string ExportFile,
    string ExportUrl,
    bool NotificationSent,
    DateTime CompletedAtUtc);