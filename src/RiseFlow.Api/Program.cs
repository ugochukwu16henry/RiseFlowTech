using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Middleware;
using RiseFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Sensitive data encryption at rest (NIN, phone numbers). Set Encryption:Key (Base64 256-bit) in config; if unset, values stay plaintext.
SensitiveDataEncryption.Initialize(builder.Configuration["Encryption:Key"]);

// Railway (and similar hosts): listen on PORT when set
if (Environment.GetEnvironmentVariable("PORT") is { } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Database (DATABASE_URL on Railway, or DefaultConnection in config)
builder.Services.AddDbContext<RiseFlowDbContext>(options =>
{
    options.UseNpgsql(DatabaseConnectionHelper.GetConnectionString(builder.Configuration));
});

// Identity with Guid keys
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<RiseFlowDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    // For APIs: return 401/403 instead of redirecting to /Account/Login
    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

// Multi-tenancy: TenantService holds TenantId (from header or claim) for the request; EF filters by School
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<SchoolOnboardingService>();
builder.Services.AddSingleton<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<TranscriptPdfService>();
builder.Services.AddScoped<BillingReceiptPdfService>();
builder.Services.AddScoped<SchoolDashboardService>();
builder.Services.AddSingleton<PitchDeckPdfService>();
builder.Services.AddSingleton<TeacherQuickStartPdfService>();
builder.Services.AddSingleton<GradingReferencePdfService>();
builder.Services.AddSingleton<ParentWelcomeLetterPdfService>();
builder.Services.AddScoped<StudentBulkUploadService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddHttpClient<PaymentService>("Paystack", client =>
{
    client.BaseAddress = new Uri("https://api.paystack.co/");
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["Cors:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new[] { "http://localhost:5173", "http://localhost:3000" })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();

// Rate limiting for auth endpoints (brute-force protection)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("Auth", config =>
    {
        config.Window = TimeSpan.FromMinutes(1);
        config.PermitLimit = 10;
        config.QueueLimit = 0;
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors();
app.UseRateLimiter();

// Extract TenantId from X-Tenant-Id header so TenantService and EF can filter by School
app.UseMiddleware<TenantMiddleware>();

// Apply migrations and seed Identity (roles + SuperAdmin) on startup.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<RiseFlowDbContext>();
        await context.Database.MigrateAsync();

        await IdentitySeeder.SeedAdminUserAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// In containerized hosting (Railway, etc.) HTTPS is typically terminated at the proxy,
// so we skip UseHttpsRedirection here to avoid interfering with health checks.
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// Simple health/root endpoint so Railway health check on "/" receives 200 OK.
app.MapGet("/", () => Results.Ok("RiseFlow API OK"));

app.MapControllers();

app.Run();
