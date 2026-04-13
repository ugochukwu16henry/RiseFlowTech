namespace RiseFlow.Api.Models;

// ─── Bank Details ───────────────────────────────────────────────────────────

public record SaveBankDetailsRequest(
    string BankName,
    string AccountName,
    string AccountNumber,
    string? BranchOrSortCode,
    string? PaymentInstructions);

public record BankDetailsDto(
    Guid Id,
    string BankName,
    string AccountName,
    string AccountNumber,
    string? BranchOrSortCode,
    string? PaymentInstructions);

// ─── Fee Schedules ──────────────────────────────────────────────────────────

public record CreateFeeScheduleRequest(
    string TermLabel,
    string AcademicYear,
    Guid? GradeId,
    Guid? ClassId,
    decimal Amount,
    string? Description);

public record UpdateFeeScheduleRequest(
    string TermLabel,
    string AcademicYear,
    Guid? GradeId,
    Guid? ClassId,
    decimal Amount,
    string? Description,
    bool IsActive);

public record FeeScheduleDto(
    Guid Id,
    string TermLabel,
    string AcademicYear,
    Guid? GradeId,
    string? GradeName,
    Guid? ClassId,
    string? ClassName,
    decimal Amount,
    string? Description,
    bool IsActive,
    int PaymentCount,
    int ConfirmedCount);

// ─── Payment Records ─────────────────────────────────────────────────────────

public record FeePaymentRowDto(
    Guid Id,
    Guid ScheduleId,
    string TermLabel,
    string AcademicYear,
    Guid StudentId,
    string StudentName,
    string? AdmissionNumber,
    Guid? ParentId,
    string? ParentName,
    string Status,
    decimal Amount,
    string? ReceiptFilePath,
    string? ReceiptFileName,
    string? ParentNote,
    string? AdminNote,
    DateTime? SubmittedAtUtc,
    DateTime? ConfirmedAtUtc);

public record SubmitPaymentRequest(
    Guid ScheduleId,
    Guid StudentId,
    bool IsInPerson,
    string? ParentNote);

public record ConfirmPaymentRequest(
    string? AdminNote);

// ─── Roster (school admin sees who paid per schedule) ────────────────────────

public record StudentFeeRosterRow(
    Guid StudentId,
    string StudentName,
    string? AdmissionNumber,
    string? ClassName,
    string? GradeName,
    string PaymentStatus,
    DateTime? ConfirmedAtUtc);

// ─── Parent view ─────────────────────────────────────────────────────────────

public record ParentChildFeeOverviewDto(
    Guid StudentId,
    string StudentName,
    string? AdmissionNumber,
    string? ClassName,
    string? GradeName,
    IReadOnlyList<ParentFeeItemDto> FeeItems);

public record ParentFeeItemDto(
    Guid PaymentId,
    Guid ScheduleId,
    string TermLabel,
    string AcademicYear,
    decimal Amount,
    string Status,
    string? ReceiptFilePath,
    string? ReceiptFileName,
    string? ParentNote,
    string? AdminNote,
    DateTime? SubmittedAtUtc,
    DateTime? ConfirmedAtUtc);
