namespace RiseFlow.Api.Models;

public record CreateSubjectRequest(string Name, string? Code);
public record UpdateSubjectRequest(string Name, string? Code, bool IsActive);
