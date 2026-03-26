using Microsoft.AspNetCore.Components.Authorization;
using RiseFlow.Web;
using RiseFlow.Web.Auth;
using RiseFlow.Web.Services;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Railway/platform dynamic PORT binding.
var platformPort = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{platformPort}");

// ── Blazor Web App (.NET 10) ────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Auth ───────────────────────────────────────────────────────────────────
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<RiseFlowAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<RiseFlowAuthStateProvider>());

// ── Scoped HttpClient with per-circuit CookieContainer ─────────────────────
// Each Blazor Server circuit gets its own cookie jar so one user's
// API session never leaks to another user's circuit.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5221";
builder.Services.AddScoped<CookieContainer>();
builder.Services.AddScoped(sp =>
{
    var cookies = sp.GetRequiredService<CookieContainer>();
    var handler = new HttpClientHandler { CookieContainer = cookies, UseCookies = true };
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

// ── App services ────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// In container hosting behind a proxy (Railway), TLS is terminated upstream.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT")))
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStarted.Register(() =>
    Console.WriteLine($"RiseFlow.Web started on PORT={platformPort}"));

app.Run();

