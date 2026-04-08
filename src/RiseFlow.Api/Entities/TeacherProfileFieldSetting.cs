namespace RiseFlow.Api.Entities;

/// <summary>
/// School-admin controls for teacher-visible profile fields.
/// Used for admin-only fields like salary/allowances and for custom teacher profile fields.
/// </summary>
public class TeacherProfileFieldSetting : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    public bool IsVisibleToTeacher { get; set; } = true;
    public bool IsEditableByTeacher { get; set; } = true;
    public bool IsAdminOnly { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
}
