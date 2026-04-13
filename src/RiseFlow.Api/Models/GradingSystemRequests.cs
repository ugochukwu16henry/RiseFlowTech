namespace RiseFlow.Api.Models;

public record CreateGradingSystemRequest(
    string Name,
    Guid? ClassId,
    Guid? TermId,
    bool IsActive);

public record UpdateGradingSystemRequest(
    string Name,
    Guid? ClassId,
    Guid? TermId,
    bool IsActive);

public record CreateGradeRuleRequest(
    string GradeLetter,
    decimal MinPercent,
    decimal MaxPercent,
    decimal? GradePoint,
    string? Remarks);

public record UpdateGradeRuleRequest(
    string GradeLetter,
    decimal MinPercent,
    decimal MaxPercent,
    decimal? GradePoint,
    string? Remarks);
