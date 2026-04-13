namespace RiseFlow.Api.Entities;

public class ClassRoutine : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? TeacherId { get; set; }
    public int Weekday { get; set; } // 0=Sunday ... 6=Saturday
    public string StartTime { get; set; } = string.Empty; // HH:mm
    public string EndTime { get; set; } = string.Empty;   // HH:mm
    public string? Room { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Class Class { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public Teacher? Teacher { get; set; }
}
