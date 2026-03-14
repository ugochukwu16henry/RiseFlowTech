using System.ComponentModel.DataAnnotations;

namespace RiseFlow.Api.Entities;

/// <summary>
/// Daily attendance for a student in a given school.
/// This is the central, server-side source of truth that offline clients sync into.
/// </summary>
public class AttendanceRecord : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Tenant / school this attendance belongs to.</summary>
    public Guid SchoolId { get; set; }

    /// <summary>The student this attendance entry is for.</summary>
    public Guid StudentId { get; set; }

    /// <summary>UTC date for which attendance was recorded (date-only semantics).</summary>
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; }

    /// <summary>
    /// Present, Absent, Late, Excused.
    /// We keep it as a short string so that mobile / desktop offline clients can map easily.
    /// </summary>
    [MaxLength(16)]
    public string Status { get; set; } = "Present";

    /// <summary>Optional period or session label (e.g. "AM", "PM", "FullDay").</summary>
    [MaxLength(16)]
    public string? Period { get; set; }

    /// <summary>Free-form note from the teacher (e.g. reason, comment).</summary>
    [MaxLength(256)]
    public string? Note { get; set; }

    /// <summary>Client-side identifier for the device or app instance (for troubleshooting sync issues).</summary>
    [MaxLength(64)]
    public string? SourceDeviceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

