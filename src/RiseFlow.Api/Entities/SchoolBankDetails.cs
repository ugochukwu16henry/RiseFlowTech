namespace RiseFlow.Api.Entities;

/// <summary>
/// School's bank account details for fee collection.
/// Each school has one active set of bank details displayed to parents for payment.
/// </summary>
public class SchoolBankDetails : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }

    public string BankName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    /// <summary>Optional: branch name or sort code.</summary>
    public string? BranchOrSortCode { get; set; }
    /// <summary>Optional payment instructions shown to parents (e.g. "Use student name as payment reference").</summary>
    public string? PaymentInstructions { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
}
