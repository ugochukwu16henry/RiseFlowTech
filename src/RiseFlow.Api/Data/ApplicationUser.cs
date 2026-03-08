using Microsoft.AspNetCore.Identity;

namespace RiseFlow.Api.Data;

/// <summary>
/// Identity user with optional SchoolId for tenant-scoped users. SuperAdmin has no SchoolId.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? SchoolId { get; set; }
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
