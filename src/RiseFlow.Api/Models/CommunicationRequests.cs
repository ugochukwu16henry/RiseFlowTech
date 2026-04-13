namespace RiseFlow.Api.Models;

public record CreateSchoolNoticeRequest(
    string Title,
    string Body,
    string TargetRolesCsv,
    DateTime? ExpiresAtUtc,
    bool IsActive);

public record UpdateSchoolNoticeRequest(
    string Title,
    string Body,
    string TargetRolesCsv,
    DateTime? ExpiresAtUtc,
    bool IsActive);

public record CreateSchoolEventRequest(
    string Title,
    string? Description,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string? ColorHex);

public record UpdateSchoolEventRequest(
    string Title,
    string? Description,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string? ColorHex);
