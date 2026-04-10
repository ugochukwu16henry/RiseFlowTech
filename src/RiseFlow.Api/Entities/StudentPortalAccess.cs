using RiseFlow.Api.Data;

namespace RiseFlow.Api.Entities;

/// <summary>
/// Parent-managed access and privacy controls for a student's view-only portal.
/// Credentials are generated only after a parent claims the child.
/// </summary>
public class StudentPortalAccess : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid UserId { get; set; }

    public string LoginId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public bool ShowDateOfBirth { get; set; } = true;
    public bool ShowLocationDetails { get; set; } = true;
    public bool ShowHealthDetails { get; set; } = false;
    public bool ShowEmergencyContacts { get; set; } = false;
    public bool ShowParentContactDetails { get; set; } = false;
    public bool ShowPreviousSchoolDetails { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? CredentialsSharedAtUtc { get; set; }
    public DateTime? LastPasswordResetAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
