namespace RiseFlow.Api.Models;

public record CreateAssignmentRequest(
    Guid ClassId,
    Guid SubjectId,
    Guid TermId,
    string Title,
    string? Description,
    Guid FileAssetId,
    DateTime? DueDateUtc);

public record AssignmentListItemDto(
    Guid Id,
    Guid ClassId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    Guid TermId,
    string TermName,
    Guid TeacherId,
    string TeacherName,
    string Title,
    string? Description,
    Guid FileAssetId,
    string OriginalFileName,
    DateTime? DueDateUtc,
    DateTime CreatedAtUtc);
