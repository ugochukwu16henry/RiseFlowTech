namespace RiseFlow.Api.Models;

public record CreateTeacherRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? Email,
    string? Phone,
    string? WhatsAppNumber,
    string? StaffId,
    string? SubjectSpecialization);

public record UpdateTeacherRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? Email,
    string? Phone,
    string? WhatsAppNumber,
    string? StaffId,
    string? SubjectSpecialization,
    bool IsActive);

public record AssignTeacherToClassRequest(string? RoleInClass);
