namespace RiseFlow.Api.Entities;

/// <summary>
/// School-level controls for what teachers are allowed to view on student detail records.
/// Parents always see their own linked children, while School Admins always see the full record.
/// </summary>
public class StudentProfileVisibilitySetting : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }

    public bool ShowDateOfBirthToTeachers { get; set; } = true;
    public bool ShowLocationDetailsToTeachers { get; set; } = false;
    public bool ShowHealthDetailsToTeachers { get; set; } = true;
    public bool ShowParentContactsToTeachers { get; set; } = false;
    public bool ShowAcademicHistoryToTeachers { get; set; } = true;
    public bool ShowPreviousRecordToTeachers { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
}
