using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Models;

/// <summary>
/// Aggregated view model for the "digital file" student profile screen.
/// Combines bio, parent info, academic history, and access code in a single payload.
/// </summary>
public record StudentProfileViewModel(
    Guid Id,
    Guid SchoolId,
    string FullName,
    string? AdmissionNumber,
    string? ClassName,
    string? GradeName,
    string? ProfilePhotoFileName,
    bool IsActive,
    string? ParentAccessCode,
    string? NinMasked,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Nationality,
    string? StateOfOrigin,
    string? Lga,
    string? EmergencyContactName,
    string? EmergencyContactPhoneMasked,
    decimal CurrentAveragePercentage,
    decimal? AttendancePercentage,
    string FeeStatus,
    IReadOnlyList<StudentAcademicHistoryItem> AcademicHistory,
    IReadOnlyList<ParentContactDto> Parents,
    IReadOnlyList<PerformanceTrendPoint> PerformanceTrend);

public record StudentAcademicHistoryItem(
    Guid ResultId,
    string Term,
    string Subject,
    string AssessmentType,
    decimal Score,
    decimal MaxScore,
    decimal Percentage,
    string? GradeLetter);

public record ParentContactDto(
    Guid ParentId,
    string FullName,
    string? Relationship,
    string? Phone,
    string? WhatsAppNumber,
    string? Email);

public record PerformanceTrendPoint(
    Guid TermId,
    string Term,
    decimal AveragePercentage);

