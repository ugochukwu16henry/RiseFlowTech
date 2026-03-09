using Microsoft.AspNetCore.Identity;

namespace RiseFlow.Infrastructure.Identity;

/// <summary>
/// Application user for ASP.NET Core Identity using Guid keys.
/// This lives in Infrastructure to keep the Domain project free of Identity dependencies.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Optional link to the tenant SchoolId. When present, this user is scoped to a specific school.
    /// Super Admin or platform-level users may have this unset.
    /// </summary>
    public Guid? SchoolId { get; set; }
}

