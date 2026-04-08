namespace RiseFlow.Api.Models;

public record CreateStudentRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? StateOfOrigin,
    string? LGA,
    string? NIN,
    string? NationalIdType,
    string? NationalIdNumber,
    string? AdmissionNumber,
    DateTime? DateOfAdmission,
    Guid? ClassId,
    Guid? GradeId,
    string? PreviousSchool,
    string? PreviousClass,
    string? BloodGroup,
    string? Genotype,
    string? Allergies,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

public record UpdateStudentRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? StateOfOrigin,
    string? LGA,
    string? NIN,
    string? NationalIdType,
    string? NationalIdNumber,
    string? AdmissionNumber,
    DateTime? DateOfAdmission,
    Guid? ClassId,
    Guid? GradeId,
    string? PreviousSchool,
    string? PreviousClass,
    string? BloodGroup,
    string? Genotype,
    string? Allergies,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    bool IsActive);

public record ParentStudentCorrectionRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? StateOfOrigin,
    string? LGA,
    string? PreviousSchool,
    string? PreviousClass,
    string? BloodGroup,
    string? Genotype,
    string? Allergies,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

public record UpdateStudentProfileVisibilitySettingsRequest(
    bool ShowDateOfBirthToTeachers,
    bool ShowLocationDetailsToTeachers,
    bool ShowHealthDetailsToTeachers,
    bool ShowParentContactsToTeachers,
    bool ShowAcademicHistoryToTeachers,
    bool ShowPreviousRecordToTeachers);

public record ParentStudentCorrectionResult(bool Success, string Message, DateTime? NextEditAvailableAtUtc);
