namespace RiseFlow.Api.Models;

public record CreateStudentRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? AdmissionNumber,
    Guid? ClassId,
    Guid? GradeId);

public record UpdateStudentRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? AdmissionNumber,
    Guid? ClassId,
    Guid? GradeId,
    bool IsActive);
