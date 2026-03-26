using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace RiseFlow.Web.Auth;

/// <summary>
/// Circuit-scoped authentication state provider for RiseFlow Blazor Web App.
/// Each SignalR circuit (i.e. browser tab) gets its own isolated instance so one
/// user's session cannot bleed into another's.
/// </summary>
public sealed class RiseFlowAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private AuthenticationState _current = AnonymousState;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_current);

    public UserSession? CurrentSession { get; private set; }

    /// <summary>Sets the authenticated user and notifies Blazor to re-render auth-guarded components.</summary>
    public void SignIn(UserSession session)
    {
        CurrentSession = session;

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name,  session.Email),
            new Claim(ClaimTypes.Email, session.Email),
            new Claim(ClaimTypes.Role,  session.Role),
            new Claim("SchoolId",       session.SchoolId?.ToString() ?? string.Empty),
        ], "RiseFlowAuth");

        _current = new AuthenticationState(new ClaimsPrincipal(identity));
        NotifyAuthenticationStateChanged(Task.FromResult(_current));
    }

    /// <summary>Clears the session and notifies Blazor components.</summary>
    public void SignOut()
    {
        CurrentSession = null;
        _current = AnonymousState;
        NotifyAuthenticationStateChanged(Task.FromResult(_current));
    }
}
