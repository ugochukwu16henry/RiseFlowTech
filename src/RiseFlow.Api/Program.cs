using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Extensions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RiseFlow.Api.Data;
using RiseFlow.Api.Middleware;
using RiseFlow.Api.Services;

// Phased FSH migration: production remains this API + React. Platform host: src/RiseFlow.Platform.Api (see docs/PHASED_FSH_MIGRATION.md).
var builder = WebApplication.CreateBuilder(args);

// Sensitive data encryption at rest (NIN, phone numbers). Set Encryption:Key (Base64 256-bit) in config; if unset, values stay plaintext.
SensitiveDataEncryption.Initialize(builder.Configuration["Encryption:Key"]);

// Railway (and similar hosts): listen on PORT when set
if (Environment.GetEnvironmentVariable("PORT") is { } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Database: Sqlite by default (local file). Set Database:Provider to Npgsql and ConnectionStrings:DefaultConnection for PostgreSQL (same engine family as FullStackHero playground).
var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
builder.Services.AddDbContext<RiseFlowDbContext>(options =>
{
    if (string.Equals(dbProvider, "Npgsql", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dbProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        var pg = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(pg))
        {
            throw new InvalidOperationException(
                "Database:Provider requests PostgreSQL but ConnectionStrings:DefaultConnection is missing or empty.");
        }

        options.UseNpgsql(pg);
        return;
    }

    var sqliteConn = builder.Configuration.GetConnectionString("Sqlite");
    if (string.IsNullOrWhiteSpace(sqliteConn))
    {
        var dbPath = Path.Combine(builder.Environment.ContentRootPath, "riseflow.db");
        sqliteConn = $"Data Source={dbPath}";
    }

    options.UseSqlite(sqliteConn);
});

// Health checks (DB) — aligns with starter-kit-style production readiness
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RiseFlowDbContext>("database");

// OpenTelemetry traces → OTLP (set OTEL_EXPORTER_OTLP_ENDPOINT or OpenTelemetry:OtlpEndpoint)
if (builder.Configuration.GetValue("OpenTelemetry:Enabled", true))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: builder.Configuration["OpenTelemetry:ServiceName"] ?? "RiseFlow.Api",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0"))
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation(o =>
            {
                o.Filter = static ctx => !ctx.Request.Path.StartsWithSegments("/health");
            });
            tracing.AddHttpClientInstrumentation();
            tracing.AddOtlpExporter(o =>
            {
                var ep = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
                if (!string.IsNullOrWhiteSpace(ep) && Uri.TryCreate(ep, UriKind.Absolute, out var uri))
                    o.Endpoint = uri;
            });
        });
}

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
builder.Services
    .AddMultiTenant<SchoolTenantInfo>()
    .WithStrategy<RiseFlowTenantStrategy>(ServiceLifetime.Singleton)
    .WithStore<SchoolTenantStore>(ServiceLifetime.Scoped);
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<SchoolOnboardingService>();
builder.Services.AddScoped<SchoolOffboardingService>();
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
app.UseMultiTenant();

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

app.MapGet("/", () => Results.Ok("RiseFlow API OK"));
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
