namespace RiseFlow.Api.Entities;

public class TeacherAssignment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TermId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid FileAssetId { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public Class Class { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public AcademicTerm Term { get; set; } = null!;
    public FileAsset FileAsset { get; set; } = null!;
}
