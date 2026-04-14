namespace RiseFlow.Api.Models;

public record AffiliateProgramInfoDto(
    int FreeTierStudentCount,
    decimal ActivationFeePerStudent,
    decimal MonthlyFeePerStudent,
    decimal AffiliateActivationShare,
    decimal AffiliateMonthlyShare,
    string CurrencyCode,
    string Summary);

public record SubmitAffiliateLeadRequest(
    string FullName,
    string Email,
    string? PhoneNumber,
    string? CountryCode,
    string? Note);

public record CompleteAffiliateInviteRequest(
    string Email,
    string Password,
    string? FullName,
    string? PhoneNumber,
    string? CountryCode);

public record AffiliateInviteValidationDto(
    bool IsValid,
    string? Email,
    DateTime? ExpiresAtUtc,
    string Message);

public record AffiliateLeadRequestDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? CountryCode,
    string? Note,
    string Status,
    DateTime? InviteSentAtUtc,
    DateTime CreatedAtUtc);

public record SendAffiliateInviteResult(
    Guid LeadRequestId,
    string Email,
    string InviteUrl,
    bool EmailSent,
    DateTime ExpiresAtUtc);

public record AffiliateReferralLinkDto(Guid AffiliateId, string UniqueCode, string ReferralUrl);

public record AffiliateSchoolSummaryDto(
    Guid SchoolId,
    string SchoolName,
    int TotalStudents,
    int BillableStudents,
    DateTime CreatedAtUtc,
    DateTime? LatestPaidAtUtc,
    decimal LifetimeCommission,
    decimal PendingCommission,
    decimal PaidCommission);

public record AffiliatePayoutSettingsDto(
    string? BankName,
    string? AccountNumber,
    string? AccountName,
    string? CountryCode,
    string? PhoneNumber,
    string? HeadshotPath);

public record UpdateAffiliatePayoutSettingsRequest(
    string? BankName,
    string? AccountNumber,
    string? AccountName,
    string? CountryCode,
    string? PhoneNumber);

public record SendAffiliateQuestionRequest(string Message);

public record SendSuperAdminAffiliateMessageRequest(string Message);

public record AffiliateTrainingVideoDto(
    Guid Id,
    string Title,
    string? Topic,
    string? Description,
    string YoutubeUrl,
    bool IsPublished,
    int SortOrder,
    DateTime CreatedAtUtc);

public record SaveAffiliateTrainingVideoRequest(
    string Title,
    string? Topic,
    string? Description,
    string YoutubeUrl,
    bool IsPublished,
    int SortOrder);

public record AffiliatePayoutDto(
    Guid Id,
    Guid AffiliateId,
    string AffiliateName,
    decimal Amount,
    string CurrencyCode,
    string PayoutType,
    string Status,
    string? PaystackTransferReference,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    DateTime? PaidAtUtc,
    DateTime CreatedAtUtc,
    string? FailureReason);

public record AffiliateNotificationDto(
    Guid Id,
    string Title,
    string Message,
    string Type,
    bool IsRead,
    DateTime CreatedAtUtc);

public record AffiliateDashboardDto(
    string FullName,
    string Email,
    string UniqueCode,
    string ReferralUrl,
    string? HeadshotPath,
    int TotalReferredSchools,
    int TotalStudents,
    int TotalBillableStudents,
    decimal CurrentMonthEarnings,
    decimal PendingPayoutAmount,
    decimal PaidToDate,
    AffiliatePayoutSettingsDto PayoutSettings,
    IReadOnlyList<AffiliateSchoolSummaryDto> ReferredSchools,
    IReadOnlyList<AffiliatePayoutDto> PayoutHistory,
    IReadOnlyList<AffiliateTrainingVideoDto> TrainingVideos,
    IReadOnlyList<AffiliateNotificationDto> Notifications);

public record AffiliateSummaryDto(
    Guid AffiliateId,
    string FullName,
    string Email,
    string UniqueCode,
    bool IsActive,
    string? CountryCode,
    string? PhoneNumber,
    string? HeadshotPath,
    int ReferredSchoolCount,
    int TotalBillableStudents,
    decimal PendingPayoutAmount,
    decimal PaidToDate,
    DateTime? ApprovedAtUtc);

public record AffiliateAdminDetailDto(
    AffiliateSummaryDto Affiliate,
    AffiliateContactDetailsDto Contact,
    AffiliatePayoutSettingsDto PayoutSettings,
    IReadOnlyList<AffiliateSchoolSummaryDto> Schools,
    IReadOnlyList<AffiliatePayoutDto> Payouts,
    IReadOnlyList<AffiliateNotificationDto> Notifications);

public record AffiliateContactDetailsDto(
    string FullName,
    string Email,
    string? PhoneNumber,
    string? WhatsappNumber,
    string? HeadshotPath,
    string? LatestQuestion);
