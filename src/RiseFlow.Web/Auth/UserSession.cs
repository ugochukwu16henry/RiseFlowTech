namespace RiseFlow.Web.Auth;

/// <summary>Minimal in-memory session for a logged-in user (lives per Blazor Server circuit).</summary>
public sealed class UserSession
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public Guid? SchoolId { get; init; }

    public UserSession() { }

    public UserSession(string email, string role, Guid? schoolId)
    {
        Email = email;
        Role = role;
        SchoolId = schoolId;
    }

    /// <summary>The home dashboard route for this role.</summary>
    public string HomePath => Role switch
    {
        "SuperAdmin" => "/super-admin/dashboard",
        "SchoolAdmin" => "/admin/dashboard",
        "Teacher" => "/teacher/dashboard",
        "Parent" => "/parent/dashboard",
        "Student" => "/student/dashboard",
        _ => "/login",
    };
}
