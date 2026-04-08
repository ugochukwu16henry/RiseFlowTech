using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Extensions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RiseFlow.Api.Data;
using Microsoft.AspNetCore.Authentication;
using RiseFlow.Api.Middleware;
using RiseFlow.Api.Services;

// Phased FSH migration: production remains this API + React. Platform host: src/RiseFlow.Platform.Api (see docs/PHASED_FSH_MIGRATION.md).
var builder = WebApplication.CreateBuilder(args);

// Sensitive data encryption at rest (NIN, phone numbers). Set Encryption:Key (Base64 256-bit) in config; if unset, values stay plaintext.
SensitiveDataEncryption.Initialize(builder.Configuration["Encryption:Key"]);

// Railway (and similar hosts): listen on PORT when set
if (Environment.GetEnvironmentVariable("PORT") is { } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Database: local development uses SQLite, while deployed environments should use PostgreSQL for durable storage.
var configuredProvider = builder.Configuration["Database:Provider"];
var hasPostgresEnvironment = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_URL"))
    || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL"));
var dbProvider = !string.IsNullOrWhiteSpace(configuredProvider)
    ? configuredProvider
    : (builder.Environment.IsDevelopment() ? "Sqlite" : "Npgsql");

builder.Services.AddDbContext<RiseFlowDbContext>(options =>
{
    if (hasPostgresEnvironment
        || string.Equals(dbProvider, "Npgsql", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dbProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        var pg = DatabaseConnectionHelper.GetConnectionString(builder.Configuration);
        if (string.IsNullOrWhiteSpace(pg))
        {
            throw new InvalidOperationException(
                "PostgreSQL is configured but no valid connection string was found. Set DATABASE_URL, DATABASE_PUBLIC_URL, or ConnectionStrings:DefaultConnection.");
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
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = builder.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

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
builder.Services.AddScoped<IClaimsTransformation, EnsureSchoolIdClaimTransformation>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddScoped<SchoolOnboardingService>();
builder.Services.AddScoped<SchoolOffboardingService>();
builder.Services.AddSingleton<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<TranscriptPdfService>();
builder.Services.AddScoped<BillingReceiptPdfService>();
builder.Services.AddScoped<SchoolDashboardService>();
builder.Services.AddScoped<StudentAdmissionNumberService>();
builder.Services.AddSingleton<PitchDeckPdfService>();
builder.Services.AddSingleton<TeacherQuickStartPdfService>();
builder.Services.AddSingleton<GradingReferencePdfService>();
builder.Services.AddSingleton<ParentWelcomeLetterPdfService>();
builder.Services.AddScoped<StudentBulkUploadService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHttpClient("Paystack", client =>
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
var fileStorage = app.Services.GetRequiredService<FileStorageService>();

app.UseCors();
app.UseRateLimiter();
app.UseMultiTenant();

// Extract TenantId from X-Tenant-Id header so TenantService and EF can filter by School
app.UseMiddleware<TenantMiddleware>();

// Apply migrations and seed Identity (roles + SuperAdmin) on startup.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<RiseFlowDbContext>();

        try
        {
            await context.Database.MigrateAsync();
        }
        catch (InvalidOperationException ex) when (
            context.Database.IsSqlite()
            && ex.Message.Contains("pending changes", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(ex, "SQLite startup migration hit pending model changes; creating the local development database from the current model instead.");
            await context.Database.EnsureCreatedAsync();
        }

        if (context.Database.IsSqlite())
        {
            await EnsureSqliteDevelopmentSchemaAsync(context, logger);
        }

        await IdentitySeeder.SeedAdminUserAsync(services);
    }
    catch (Exception ex)
    {
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
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(fileStorage.RootPath),
    RequestPath = ""
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok("RiseFlow API OK"));
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();

static async Task EnsureSqliteDevelopmentSchemaAsync(RiseFlowDbContext context, ILogger logger)
{
    try
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Teachers\" ADD COLUMN \"Religion\" TEXT NULL;");
        }
        catch (Exception ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // Column already exists in this local DB.
        }

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "TeacherProfileFieldSettings" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_TeacherProfileFieldSettings" PRIMARY KEY,
                "SchoolId" TEXT NOT NULL,
                "FieldKey" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL,
                "IsCustom" INTEGER NOT NULL,
                "IsVisibleToTeacher" INTEGER NOT NULL,
                "IsEditableByTeacher" INTEGER NOT NULL,
                "IsAdminOnly" INTEGER NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NULL,
                CONSTRAINT "FK_TeacherProfileFieldSettings_Schools_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "Schools" ("Id") ON DELETE CASCADE
            );
            """);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "TeacherCustomFieldValues" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_TeacherCustomFieldValues" PRIMARY KEY,
                "TeacherId" TEXT NOT NULL,
                "SchoolId" TEXT NOT NULL,
                "FieldKey" TEXT NOT NULL,
                "Value" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NULL,
                CONSTRAINT "FK_TeacherCustomFieldValues_Schools_SchoolId" FOREIGN KEY ("SchoolId") REFERENCES "Schools" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_TeacherCustomFieldValues_Teachers_TeacherId" FOREIGN KEY ("TeacherId") REFERENCES "Teachers" ("Id") ON DELETE CASCADE
            );
            """);

        await context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TeacherProfileFieldSettings_SchoolId_FieldKey\" ON \"TeacherProfileFieldSettings\" (\"SchoolId\", \"FieldKey\");");
        await context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TeacherCustomFieldValues_TeacherId_FieldKey\" ON \"TeacherCustomFieldValues\" (\"TeacherId\", \"FieldKey\");");
        await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_TeacherCustomFieldValues_SchoolId\" ON \"TeacherCustomFieldValues\" (\"SchoolId\");");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not patch the local SQLite schema for teacher profile governance.");
    }
}
