namespace RiseFlow.Api.Models;

public record CreateAcademicTermRequest(string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate, bool SetAsCurrent);
public record UpdateAcademicTermRequest(string Name, string AcademicYear, DateOnly StartDate, DateOnly EndDate, bool SetAsCurrent);
