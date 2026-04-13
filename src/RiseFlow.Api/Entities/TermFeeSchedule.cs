namespace RiseFlow.Api.Entities;

/// <summary>
/// A fee schedule set by the school admin: how much each class/grade owes per academic term.
/// GradeId is optional — a null GradeId means the schedule applies to the entire school.
/// ClassId is optional — a null ClassId means it applies to all classes in the grade.
/// </summary>
public class TermFeeSchedule : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }

    /// <summary>Academic term name (e.g. "Term 1 2025/2026").</summary>
    public string TermLabel { get; set; } = string.Empty;
    /// <summary>Academic year (e.g. "2025/2026").</summary>
    public string AcademicYear { get; set; } = string.Empty;

    /// <summary>Optional: scope schedule to a specific grade level.</summary>
    public Guid? GradeId { get; set; }
    /// <summary>Optional: scope schedule to a specific class.</summary>
    public Guid? ClassId { get; set; }

    /// <summary>Fee amount in the school's currency.</summary>
    public decimal Amount { get; set; }
    /// <summary>Optional description of what the fee covers.</summary>
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Grade? Grade { get; set; }
    public Class? Class { get; set; }
    public ICollection<FeePaymentRecord> Payments { get; set; } = new List<FeePaymentRecord>();
}
