namespace RiseFlow.Api.Models;

public record BulkPromoteStudentsRequest(
    Guid FromClassId,
    Guid ToClassId,
    Guid? FromTermId,
    string? PromotionSessionLabel,
    List<Guid> StudentIds,
    string? Notes);

public record StudentPromotionHistoryDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid FromClassId,
    string FromClassName,
    Guid ToClassId,
    string ToClassName,
    Guid? FromTermId,
    string? PromotionSessionLabel,
    DateTime PromotedAtUtc,
    string? Notes);

public record SubmitPromotionRequest(
    Guid FromClassId,
    Guid ToClassId,
    Guid? FromTermId,
    string? PromotionSessionLabel,
    List<Guid> StudentIds,
    string? Notes);

public record PromotionRequestRowDto(
    Guid Id,
    Guid TeacherId,
    string TeacherName,
    Guid FromClassId,
    string FromClassName,
    Guid ToClassId,
    string ToClassName,
    Guid? FromTermId,
    string? PromotionSessionLabel,
    string Status,
    int StudentCount,
    DateTime RequestedAtUtc,
    string? Notes,
    IReadOnlyList<PromotionRequestStudentDto> Students);

public record PromotionRequestStudentDto(Guid StudentId, string FullName, string? AdmissionNumber);
