using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Extensions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RiseFlow.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RiseFlow.Api.Middleware;
using RiseFlow.Api.Services;

// Phased FSH migration: production remains this API + React. Platform host: src/RiseFlow.Platform.Api (see docs/PHASED_FSH_MIGRATION.md).
var builder = WebApplication.CreateBuilder(args);

// Sensitive data encryption at rest (NIN, phone numbers). Set Encryption:Key (Base64 256-bit) in config; if unset, values stay plaintext.
SensitiveDataEncryption.Initialize(builder.Configuration["Encryption:Key"]);

// Railway (and similar hosts): listen on PORT when set
if (Environment.GetEnvironmentVariable("PORT") is { } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Database: PostgreSQL by default. Railway/hosted deployments resolve `DATABASE_URL` automatically.
var dbProvider = builder.Configuration["Database:Provider"] ?? "Npgsql";
builder.Services.AddDbContext<RiseFlowDbContext>(options =>
{
    if (string.Equals(dbProvider, "Npgsql", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dbProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        var pg = DatabaseConnectionHelper.GetConnectionString(builder.Configuration);
        if (string.IsNullOrWhiteSpace(pg))
        {
            throw new InvalidOperationException(
                "Database:Provider requests PostgreSQL but no usable connection string was resolved from DATABASE_URL, DATABASE_PUBLIC_URL, or ConnectionStrings:DefaultConnection.");
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

// Health checks: `/health` is liveness for Railway/container probes; `/health/ready` includes DB readiness.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddDbContextCheck<RiseFlowDbContext>("database", tags: new[] { "ready" });

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
builder.Services.AddScoped<SchoolOnboardingService>();
builder.Services.AddScoped<SchoolOffboardingService>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddScoped<AffiliateService>();
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
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<RiseFlowDbContext>();

        try
        {
            if (context.Database.IsSqlite())
            {
                await context.Database.EnsureCreatedAsync();
                await EnsureSqliteDevelopmentSchemaAsync(context, logger);
            }
            else
            {
                await context.Database.MigrateAsync();
            }
        }
        catch (InvalidOperationException ex) when (
            context.Database.IsSqlite()
            && ex.Message.Contains("pending changes", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(ex, "SQLite startup migration hit pending model changes; creating the local development database from the current model instead.");
            await context.Database.EnsureCreatedAsync();
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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok("RiseFlow API OK"));
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();

static async Task EnsureSqliteDevelopmentSchemaAsync(RiseFlowDbContext context, ILogger logger)
{
    if (!context.Database.IsSqlite())
        return;

    var connection = context.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
        await connection.OpenAsync();

    try
    {
        async Task<HashSet<string>> GetColumnsAsync(string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{tableName}\")";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(1))
                    columns.Add(reader.GetString(1));
            }
            return columns;
        }

        var schoolColumns = await GetColumnsAsync("Schools");
        if (!schoolColumns.Contains("AffiliateId"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Schools\" ADD COLUMN \"AffiliateId\" TEXT NULL;");
        if (!schoolColumns.Contains("AffiliateReferralCodeUsed"))
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Schools\" ADD COLUMN \"AffiliateReferralCodeUsed\" TEXT NULL;");

        await context.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "Affiliates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Affiliates" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "UniqueCode" TEXT NOT NULL,
    "HeadshotPath" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "CountryCode" TEXT NULL,
    "BankName" TEXT NULL,
    "AccountNumber" TEXT NULL,
    "AccountName" TEXT NULL,
    "PaystackRecipientCode" TEXT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "ApprovedAtUtc" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Affiliates_UniqueCode" ON "Affiliates" ("UniqueCode");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Affiliates_UserId" ON "Affiliates" ("UserId");
""");

        await context.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "AffiliateLeadRequests" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AffiliateLeadRequests" PRIMARY KEY,
    "FullName" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "PhoneNumber" TEXT NULL,
    "CountryCode" TEXT NULL,
    "Note" TEXT NULL,
    "Status" TEXT NOT NULL,
    "InviteSentAtUtc" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_AffiliateLeadRequests_Email" ON "AffiliateLeadRequests" ("Email");
""");

        await context.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "AffiliateInvites" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AffiliateInvites" PRIMARY KEY,
    "AffiliateLeadRequestId" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "InviteToken" TEXT NOT NULL,
    "ExpiresAtUtc" TEXT NOT NULL,
    "UsedAtUtc" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_AffiliateInvites_AffiliateLeadRequestId" ON "AffiliateInvites" ("AffiliateLeadRequestId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AffiliateInvites_InviteToken" ON "AffiliateInvites" ("InviteToken");
""");

        await context.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "AffiliateTrainingVideos" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AffiliateTrainingVideos" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Topic" TEXT NULL,
    "Description" TEXT NULL,
    "YoutubeUrl" TEXT NOT NULL,
    "IsPublished" INTEGER NOT NULL DEFAULT 1,
    "SortOrder" INTEGER NOT NULL DEFAULT 0,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NULL
);
""");

        await context.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "AffiliatePayouts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AffiliatePayouts" PRIMARY KEY,
    "AffiliateId" TEXT NOT NULL,
    "Amount" TEXT NOT NULL,
    "CurrencyCode" TEXT NOT NULL,
    "PayoutType" TEXT NOT NULL,
    "PaystackTransferReference" TEXT NULL,
    "Status" TEXT NOT NULL,
    "PeriodStartUtc" TEXT NOT NULL,
    "PeriodEndUtc" TEXT NOT NULL,
    "PaidAtUtc" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NULL,
    "FailureReason" TEXT NULL
);
CREATE INDEX IF NOT EXISTS "IX_AffiliatePayouts_AffiliateId" ON "AffiliatePayouts" ("AffiliateId");
""");

        await context.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "AffiliateCommissionLedgers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AffiliateCommissionLedgers" PRIMARY KEY,
    "AffiliateId" TEXT NOT NULL,
    "SchoolId" TEXT NOT NULL,
    "BillingRecordId" TEXT NULL,
    "AffiliatePayoutId" TEXT NULL,
    "StudentCount" INTEGER NOT NULL DEFAULT 0,
    "BillableStudentCount" INTEGER NOT NULL DEFAULT 0,
    "ActivationCommissionAmount" TEXT NOT NULL DEFAULT 0,
    "MonthlyCommissionAmount" TEXT NOT NULL DEFAULT 0,
    "TotalCommissionAmount" TEXT NOT NULL DEFAULT 0,
    "CommissionType" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_AffiliateCommissionLedgers_AffiliateId" ON "AffiliateCommissionLedgers" ("AffiliateId");
CREATE INDEX IF NOT EXISTS "IX_AffiliateCommissionLedgers_SchoolId" ON "AffiliateCommissionLedgers" ("SchoolId");
CREATE INDEX IF NOT EXISTS "IX_AffiliateCommissionLedgers_BillingRecordId" ON "AffiliateCommissionLedgers" ("BillingRecordId");
CREATE INDEX IF NOT EXISTS "IX_AffiliateCommissionLedgers_AffiliatePayoutId" ON "AffiliateCommissionLedgers" ("AffiliatePayoutId");
""");

        await context.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "AffiliateNotifications" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AffiliateNotifications" PRIMARY KEY,
    "AffiliateId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "IsRead" INTEGER NOT NULL DEFAULT 0,
    "CreatedAtUtc" TEXT NOT NULL,
    "ReadAtUtc" TEXT NULL
);
CREATE INDEX IF NOT EXISTS "IX_AffiliateNotifications_AffiliateId" ON "AffiliateNotifications" ("AffiliateId");
""");

        await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Schools_AffiliateId\" ON \"Schools\" (\"AffiliateId\");");

        logger.LogInformation("SQLite development schema verified for Super Admin and affiliate features.");
    }
    finally
    {
        if (shouldClose)
            await connection.CloseAsync();
    }
}
