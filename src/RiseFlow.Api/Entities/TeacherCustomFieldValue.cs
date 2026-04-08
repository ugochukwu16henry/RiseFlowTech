namespace RiseFlow.Api.Entities;

/// <summary>
/// Per-teacher values for school-defined custom profile fields.
/// </summary>
public class TeacherCustomFieldValue : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid TeacherId { get; set; }
    public string FieldKey { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
}
