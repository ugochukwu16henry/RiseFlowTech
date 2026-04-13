namespace RiseFlow.Api.Models;

public record CreateAcademicTermRequest(
    string Name,
    string AcademicYear,
    DateOnly StartDate,
    DateOnly EndDate,
    bool SetAsCurrent,
    DateOnly? MidtermBreakStart = null,
    DateOnly? MidtermBreakEnd = null,
    string? Description = null,
    int SortOrder = 0);

public record UpdateAcademicTermRequest(
    string Name,
    string AcademicYear,
    DateOnly StartDate,
    DateOnly EndDate,
    bool SetAsCurrent,
    DateOnly? MidtermBreakStart = null,
    DateOnly? MidtermBreakEnd = null,
    string? Description = null,
    int SortOrder = 0);
