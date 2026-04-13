namespace RiseFlow.Api.Entities;

public class MarkSubmissionWindow : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid TermId { get; set; }
    public bool IsOpen { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public AcademicTerm Term { get; set; } = null!;
}
