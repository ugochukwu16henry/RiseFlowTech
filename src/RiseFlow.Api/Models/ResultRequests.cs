namespace RiseFlow.Api.Models;

public record CreateResultRequest(
    Guid StudentId,
    Guid SubjectId,
    Guid TermId,
    string AssessmentType,
    decimal Score,
    decimal MaxScore,
    string? GradeLetter,
    string? Comment,
    Guid? ExamId);

public record UpdateResultRequest(
    string AssessmentType,
    decimal Score,
    decimal MaxScore,
    string? GradeLetter,
    string? Comment,
    Guid? ExamId);

public record ReviewResultRequest(string? Comment);
