namespace RiseFlow.Api.Models;

public record CreateExamRequest(
    string Name,
    Guid ClassId,
    Guid SubjectId,
    Guid TermId,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc);

public record UpdateExamRequest(
    string Name,
    Guid ClassId,
    Guid SubjectId,
    Guid TermId,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc);

public record UpdateMarkSubmissionWindowRequest(
    Guid TermId,
    bool IsOpen);
