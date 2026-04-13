namespace RiseFlow.Api.Entities;

/// <summary>
/// Payment status lifecycle.
/// </summary>
public enum FeePaymentStatus
{
    /// <summary>Payment has not been submitted yet.</summary>
    Pending = 0,
    /// <summary>Parent uploaded a receipt for bank transfer.</summary>
    ReceiptUploaded = 1,
    /// <summary>Parent indicated they will pay at the school in person.</summary>
    InPersonPending = 2,
    /// <summary>School confirmed the payment (bank transfer or in-person).</summary>
    Confirmed = 3,
}

/// <summary>
/// A single payment record for a student for a specific term fee schedule.
/// Created when a parent submits payment (receipt upload or in-person declaration).
/// Updated when school confirms payment.
/// </summary>
public class FeePaymentRecord : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }

    public Guid ScheduleId { get; set; }
    public Guid StudentId { get; set; }
    /// <summary>The parent who submitted or declared this payment.</summary>
    public Guid? ParentId { get; set; }

    public FeePaymentStatus Status { get; set; } = FeePaymentStatus.Pending;

    /// <summary>Relative path to uploaded receipt file (stored via FileAsset infrastructure).</summary>
    public string? ReceiptFilePath { get; set; }
    /// <summary>Original file name of the uploaded receipt.</summary>
    public string? ReceiptFileName { get; set; }

    /// <summary>Optional note from the parent (e.g. bank reference).</summary>
    public string? ParentNote { get; set; }
    /// <summary>Optional note from the school admin when confirming.</summary>
    public string? AdminNote { get; set; }

    /// <summary>When the parent submitted the payment claim or receipt.</summary>
    public DateTime? SubmittedAtUtc { get; set; }
    /// <summary>When the school admin confirmed the payment.</summary>
    public DateTime? ConfirmedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public TermFeeSchedule Schedule { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public Parent? Parent { get; set; }
}
