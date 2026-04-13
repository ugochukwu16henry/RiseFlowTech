namespace RiseFlow.Api.Models;

public record CreateClassRoutineRequest(
    Guid ClassId,
    Guid SubjectId,
    Guid? TeacherId,
    int Weekday,
    string StartTime,
    string EndTime,
    string? Room);

public record UpdateClassRoutineRequest(
    Guid ClassId,
    Guid SubjectId,
    Guid? TeacherId,
    int Weekday,
    string StartTime,
    string EndTime,
    string? Room);
