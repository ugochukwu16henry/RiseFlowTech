namespace RiseFlow.Api.Models;

public record SuperAdminSchoolRowDto(
    Guid Id,
    string Name,
    string? CountryCode,
    string? CountryName,
    string? CurrencyCode,
    bool IsActive,
    int StudentCount,
    int TeacherCount,
    int ParentCount,
    DateTime CreatedAtUtc,
    string? OwnerEmail,
    string? OwnerName,
    string? Phone,
    string? WhatsAppNumber,
    string? Address,
    string? SchoolEmail,
    string? PrincipalName,
    string? LogoPath,
    string? CacNumber,
    string? RegistrationDocumentPath);

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