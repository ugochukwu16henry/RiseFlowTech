using RiseFlow.Web.Auth;

namespace RiseFlow.Web.Services;

/// <summary>
/// Calls the RiseFlow API authentication endpoints.
/// The injected HttpClient carries a per-circuit CookieContainer so the
/// ASP.NET Core Identity cookie is retained between requests without ever
/// being surfaced to the browser.
/// </summary>
public sealed class AuthService(HttpClient http, RiseFlowAuthStateProvider authState)
{
    public record LoginResult(bool Success, string Message, string? Role = null);

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/auth/login",
                new { Email = email, Password = password });

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                return new(false, "Too many attempts. Please wait a minute.");

            if (!response.IsSuccessStatusCode)
                return new(false, "Incorrect email or password.");

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (body is null || !body.Success || body.PrimaryRole is null)
                return new(false, body?.Message ?? "Login failed.");

            authState.SignIn(new UserSession(
                email.Trim().ToLowerInvariant(),
                body.PrimaryRole,
                body.SchoolId));

            return new(true, "Signed in.", body.PrimaryRole);
        }
        catch (HttpRequestException)
        {
            return new(false, "Network error. Check your connection.");
        }
    }

    public async Task LogoutAsync()
    {
        try { await http.PostAsync("/api/auth/logout", null); } catch { /* best-effort */ }
        authState.SignOut();
    }

    // Mirror of the API response record (only the fields the web needs).
    private sealed record LoginResponse(bool Success, string Message, string? PrimaryRole, Guid? SchoolId);
}
