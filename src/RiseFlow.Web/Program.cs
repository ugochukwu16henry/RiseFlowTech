using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

var builder = WebApplication.CreateBuilder(args);

// Railway (and similar hosts) provide a dynamic PORT that the app must bind to.
var platformPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(platformPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{platformPort}");
}

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// In container hosting behind a proxy (e.g., Railway), TLS is terminated upstream.
// Redirecting to HTTPS inside the container can break external health checks.
if (string.IsNullOrWhiteSpace(platformPort))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

// Fast liveness endpoint for platform health checks.
app.MapGet("/healthz", () => Results.Ok("ok"));

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

