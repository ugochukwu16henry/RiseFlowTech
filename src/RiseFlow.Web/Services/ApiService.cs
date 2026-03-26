using RiseFlow.Web.Auth;

namespace RiseFlow.Web.Services;

/// <summary>
/// Typed wrapper over the scoped HttpClient for all authenticated API calls.
/// Automatically attaches the X-Tenant-Id header using the logged-in user's school.
/// </summary>
public sealed class ApiService(HttpClient http, RiseFlowAuthStateProvider authState)
{
    private void ApplyTenantHeader()
    {
        var schoolId = authState.CurrentSession?.SchoolId;
        if (schoolId.HasValue)
        {
            http.DefaultRequestHeaders.Remove("X-Tenant-Id");
            http.DefaultRequestHeaders.Add("X-Tenant-Id", schoolId.Value.ToString());
        }
    }

    public async Task<T?> GetAsync<T>(string path)
    {
        ApplyTenantHeader();
        var response = await http.GetAsync(path);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public async Task<bool> PostJsonAsync<T>(string path, T body)
    {
        ApplyTenantHeader();
        var response = await http.PostAsJsonAsync(path, body);
        return response.IsSuccessStatusCode;
    }

    public async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(string path, TRequest body)
    {
        ApplyTenantHeader();
        var response = await http.PostAsJsonAsync(path, body);
        if (!response.IsSuccessStatusCode) return default;
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }
}
