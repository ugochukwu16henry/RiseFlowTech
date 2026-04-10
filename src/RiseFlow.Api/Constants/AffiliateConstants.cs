namespace RiseFlow.Api.Constants;

public static class AffiliateConstants
{
    public const decimal ActivationCommissionPerStudentNgn = 60m;
    public const decimal MonthlyCommissionPerStudentNgn = 20m;
    public const int InviteExpiryDays = 14;

    public const string LeadStatusPending = "Pending";
    public const string LeadStatusInvited = "Invited";
    public const string LeadStatusApproved = "Approved";
    public const string LeadStatusRejected = "Rejected";

    public const string CommissionStatusPending = "Pending";
    public const string CommissionStatusReadyForPayout = "ReadyForPayout";
    public const string CommissionStatusPaid = "Paid";

    public const string PayoutStatusPending = "Pending";
    public const string PayoutStatusProcessing = "Processing";
    public const string PayoutStatusPaid = "Paid";
    public const string PayoutStatusFailed = "Failed";
}
