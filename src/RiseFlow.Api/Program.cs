using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

        options
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .UseNpgsql(pg);
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
        var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();

        var allowedOrigins = configuredOrigins.Length > 0
            ? configuredOrigins
            : new[]
            {
                "http://localhost:5173",
                "http://localhost:3000",
                "https://rise-flow-tech.vercel.app",
                "https://www.riseflow.com",
                "https://riseflow.com"
            };

        var normalizedOrigins = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);

        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin))
                    return false;

                if (normalizedOrigins.Contains(origin))
                    return true;

                // Preview/staging hosts (Vercel + Railway frontends). Production custom domains: set Cors:AllowedOrigins.
                return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && (uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.EndsWith(".railway.app", StringComparison.OrdinalIgnoreCase));
            })
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

                if (context.Database.IsNpgsql())
                    await EnsurePostgresHostedSchemaAsync(context, logger);
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
        catch (Exception ex) when (context.Database.IsNpgsql())
        {
            logger.LogWarning(ex, "PostgreSQL startup migration hit an error; attempting idempotent hosted schema verification.");
            await EnsurePostgresHostedSchemaAsync(context, logger);
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

static async Task EnsurePostgresHostedSchemaAsync(RiseFlowDbContext context, ILogger logger)
{
    if (!context.Database.IsNpgsql())
        return;

    await context.Database.ExecuteSqlRawAsync("""
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "Email" text NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "Phone" text NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "Address" text NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "PrincipalName" text NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "LogoFileName" text NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "CacNumber" text NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "AffiliateId" uuid NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "AffiliateReferralCodeUsed" text NULL;
ALTER TABLE IF EXISTS "Schools" ADD COLUMN IF NOT EXISTS "DataConsentFormReceivedAt" timestamp with time zone NULL;
CREATE INDEX IF NOT EXISTS "IX_Schools_AffiliateId" ON "Schools" ("AffiliateId");

CREATE TABLE IF NOT EXISTS "Affiliates" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "UniqueCode" text NOT NULL,
    "HeadshotPath" text NULL,
    "PhoneNumber" text NULL,
    "CountryCode" text NULL,
    "BankName" text NULL,
    "AccountNumber" text NULL,
    "AccountName" text NULL,
    "PaystackRecipientCode" text NULL,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "ApprovedAtUtc" timestamp with time zone NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NULL
);
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "HeadshotPath" text NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "PhoneNumber" text NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "CountryCode" text NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "BankName" text NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "AccountNumber" text NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "AccountName" text NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "PaystackRecipientCode" text NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT TRUE;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "ApprovedAtUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
ALTER TABLE IF EXISTS "Affiliates" ADD COLUMN IF NOT EXISTS "UpdatedAtUtc" timestamp with time zone NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Affiliates_UniqueCode" ON "Affiliates" ("UniqueCode");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Affiliates_UserId" ON "Affiliates" ("UserId");

CREATE TABLE IF NOT EXISTS "AffiliateLeadRequests" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "FullName" text NOT NULL,
    "Email" text NOT NULL,
    "PhoneNumber" text NULL,
    "CountryCode" text NULL,
    "Note" text NULL,
    "Status" text NOT NULL,
    "InviteSentAtUtc" timestamp with time zone NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);
ALTER TABLE IF EXISTS "AffiliateLeadRequests" ADD COLUMN IF NOT EXISTS "PhoneNumber" text NULL;
ALTER TABLE IF EXISTS "AffiliateLeadRequests" ADD COLUMN IF NOT EXISTS "CountryCode" text NULL;
ALTER TABLE IF EXISTS "AffiliateLeadRequests" ADD COLUMN IF NOT EXISTS "Note" text NULL;
ALTER TABLE IF EXISTS "AffiliateLeadRequests" ADD COLUMN IF NOT EXISTS "Status" text NOT NULL DEFAULT 'Pending';
ALTER TABLE IF EXISTS "AffiliateLeadRequests" ADD COLUMN IF NOT EXISTS "InviteSentAtUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "AffiliateLeadRequests" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
CREATE INDEX IF NOT EXISTS "IX_AffiliateLeadRequests_Email" ON "AffiliateLeadRequests" ("Email");

CREATE TABLE IF NOT EXISTS "AffiliateInvites" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "AffiliateLeadRequestId" uuid NOT NULL,
    "Email" text NOT NULL,
    "InviteToken" text NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "UsedAtUtc" timestamp with time zone NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);
ALTER TABLE IF EXISTS "AffiliateInvites" ADD COLUMN IF NOT EXISTS "AffiliateLeadRequestId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE IF EXISTS "AffiliateInvites" ADD COLUMN IF NOT EXISTS "Email" text NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS "AffiliateInvites" ADD COLUMN IF NOT EXISTS "InviteToken" text NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS "AffiliateInvites" ADD COLUMN IF NOT EXISTS "ExpiresAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
ALTER TABLE IF EXISTS "AffiliateInvites" ADD COLUMN IF NOT EXISTS "UsedAtUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "AffiliateInvites" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
CREATE INDEX IF NOT EXISTS "IX_AffiliateInvites_AffiliateLeadRequestId" ON "AffiliateInvites" ("AffiliateLeadRequestId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AffiliateInvites_InviteToken" ON "AffiliateInvites" ("InviteToken");

CREATE TABLE IF NOT EXISTS "AffiliateTrainingVideos" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "Title" text NOT NULL,
    "Topic" text NULL,
    "Description" text NULL,
    "YoutubeUrl" text NOT NULL,
    "IsPublished" boolean NOT NULL DEFAULT TRUE,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NULL
);
ALTER TABLE IF EXISTS "AffiliateTrainingVideos" ADD COLUMN IF NOT EXISTS "Topic" text NULL;
ALTER TABLE IF EXISTS "AffiliateTrainingVideos" ADD COLUMN IF NOT EXISTS "Description" text NULL;
ALTER TABLE IF EXISTS "AffiliateTrainingVideos" ADD COLUMN IF NOT EXISTS "YoutubeUrl" text NOT NULL DEFAULT '';
ALTER TABLE IF EXISTS "AffiliateTrainingVideos" ADD COLUMN IF NOT EXISTS "IsPublished" boolean NOT NULL DEFAULT TRUE;
ALTER TABLE IF EXISTS "AffiliateTrainingVideos" ADD COLUMN IF NOT EXISTS "SortOrder" integer NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS "AffiliateTrainingVideos" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
ALTER TABLE IF EXISTS "AffiliateTrainingVideos" ADD COLUMN IF NOT EXISTS "UpdatedAtUtc" timestamp with time zone NULL;

CREATE TABLE IF NOT EXISTS "AffiliatePayouts" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "AffiliateId" uuid NOT NULL,
    "Amount" numeric NOT NULL,
    "CurrencyCode" text NOT NULL,
    "PayoutType" text NOT NULL,
    "PaystackTransferReference" text NULL,
    "Status" text NOT NULL,
    "PeriodStartUtc" timestamp with time zone NOT NULL,
    "PeriodEndUtc" timestamp with time zone NOT NULL,
    "PaidAtUtc" timestamp with time zone NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NULL,
    "FailureReason" text NULL
);
ALTER TABLE IF EXISTS "AffiliatePayouts" ADD COLUMN IF NOT EXISTS "PaystackTransferReference" text NULL;
ALTER TABLE IF EXISTS "AffiliatePayouts" ADD COLUMN IF NOT EXISTS "Status" text NOT NULL DEFAULT 'Pending';
ALTER TABLE IF EXISTS "AffiliatePayouts" ADD COLUMN IF NOT EXISTS "PeriodStartUtc" timestamp with time zone NOT NULL DEFAULT NOW();
ALTER TABLE IF EXISTS "AffiliatePayouts" ADD COLUMN IF NOT EXISTS "PeriodEndUtc" timestamp with time zone NOT NULL DEFAULT NOW();
ALTER TABLE IF EXISTS "AffiliatePayouts" ADD COLUMN IF NOT EXISTS "PaidAtUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "AffiliatePayouts" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
ALTER TABLE IF EXISTS "AffiliatePayouts" ADD COLUMN IF NOT EXISTS "UpdatedAtUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "AffiliatePayouts" ADD COLUMN IF NOT EXISTS "FailureReason" text NULL;
CREATE INDEX IF NOT EXISTS "IX_AffiliatePayouts_AffiliateId" ON "AffiliatePayouts" ("AffiliateId");

CREATE TABLE IF NOT EXISTS "AffiliateCommissionLedgers" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "AffiliateId" uuid NOT NULL,
    "SchoolId" uuid NOT NULL,
    "BillingRecordId" uuid NULL,
    "AffiliatePayoutId" uuid NULL,
    "StudentCount" integer NOT NULL DEFAULT 0,
    "BillableStudentCount" integer NOT NULL DEFAULT 0,
    "ActivationCommissionAmount" numeric NOT NULL DEFAULT 0,
    "MonthlyCommissionAmount" numeric NOT NULL DEFAULT 0,
    "TotalCommissionAmount" numeric NOT NULL DEFAULT 0,
    "CommissionType" text NOT NULL,
    "Status" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "BillingRecordId" uuid NULL;
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "AffiliatePayoutId" uuid NULL;
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "StudentCount" integer NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "BillableStudentCount" integer NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "ActivationCommissionAmount" numeric NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "MonthlyCommissionAmount" numeric NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "TotalCommissionAmount" numeric NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "CommissionType" text NOT NULL DEFAULT 'Monthly';
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "Status" text NOT NULL DEFAULT 'Pending';
ALTER TABLE IF EXISTS "AffiliateCommissionLedgers" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
CREATE INDEX IF NOT EXISTS "IX_AffiliateCommissionLedgers_AffiliateId" ON "AffiliateCommissionLedgers" ("AffiliateId");
CREATE INDEX IF NOT EXISTS "IX_AffiliateCommissionLedgers_SchoolId" ON "AffiliateCommissionLedgers" ("SchoolId");
CREATE INDEX IF NOT EXISTS "IX_AffiliateCommissionLedgers_BillingRecordId" ON "AffiliateCommissionLedgers" ("BillingRecordId");
CREATE INDEX IF NOT EXISTS "IX_AffiliateCommissionLedgers_AffiliatePayoutId" ON "AffiliateCommissionLedgers" ("AffiliatePayoutId");

CREATE TABLE IF NOT EXISTS "AffiliateNotifications" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "AffiliateId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Message" text NOT NULL,
    "Type" text NOT NULL,
    "IsRead" boolean NOT NULL DEFAULT FALSE,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ReadAtUtc" timestamp with time zone NULL
);
ALTER TABLE IF EXISTS "AffiliateNotifications" ADD COLUMN IF NOT EXISTS "Type" text NOT NULL DEFAULT 'Info';
ALTER TABLE IF EXISTS "AffiliateNotifications" ADD COLUMN IF NOT EXISTS "IsRead" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE IF EXISTS "AffiliateNotifications" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
ALTER TABLE IF EXISTS "AffiliateNotifications" ADD COLUMN IF NOT EXISTS "ReadAtUtc" timestamp with time zone NULL;
CREATE INDEX IF NOT EXISTS "IX_AffiliateNotifications_AffiliateId" ON "AffiliateNotifications" ("AffiliateId");

ALTER TABLE IF EXISTS "Grades" ADD COLUMN IF NOT EXISTS "LevelOrder" integer NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS "Grades" ADD COLUMN IF NOT EXISTS "UpdatedAtUtc" timestamp with time zone NULL;
ALTER TABLE IF EXISTS "Classes" ADD COLUMN IF NOT EXISTS "AcademicYear" text NULL;
ALTER TABLE IF EXISTS "Classes" ADD COLUMN IF NOT EXISTS "UpdatedAtUtc" timestamp with time zone NULL;

ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "MiddleName" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "DateOfBirth" date NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "Gender" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "Nationality" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "StateOfOrigin" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "LGA" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "NIN" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "NationalIdType" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "NationalIdNumber" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "AdmissionNumber" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "DateOfAdmission" date NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "GradeId" uuid NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "PreviousSchool" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "BloodGroup" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "Genotype" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "Allergies" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "EmergencyContactName" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "EmergencyContactPhone" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "ParentAccessCode" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "ProfilePhotoFileName" text NULL;
ALTER TABLE IF EXISTS "Students" ADD COLUMN IF NOT EXISTS "UpdatedAtUtc" timestamp with time zone NULL;
CREATE INDEX IF NOT EXISTS "IX_Students_GradeId" ON "Students" ("GradeId");

ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "MiddleName" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "Email" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "Phone" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "WhatsAppNumber" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "StaffId" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "SubjectSpecialization" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "DateOfBirth" date NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "Gender" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "Nationality" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "StateOfOrigin" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "LGA" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "NIN" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "NationalIdType" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "NationalIdNumber" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "TrcnNumber" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "ResidentialAddress" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "HighestQualification" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "FieldOfStudy" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "YearsOfExperience" integer NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "PreviousSchools" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "ProfessionalBodies" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "DateEmployed" date NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "EmploymentType" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "RoleTitle" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "Department" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "BaseSalaryAmount" numeric NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "BaseSalaryCurrency" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "AllowancesNote" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "PromotionHistory" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "Recognitions" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "ProfilePhotoFileName" text NULL;
ALTER TABLE IF EXISTS "Teachers" ADD COLUMN IF NOT EXISTS "UpdatedAtUtc" timestamp with time zone NULL;

ALTER TABLE IF EXISTS "BillingRecords" ADD COLUMN IF NOT EXISTS "MonthlyAmountDue" numeric NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS "BillingRecords" ADD COLUMN IF NOT EXISTS "ActivationAmountDue" numeric NOT NULL DEFAULT 0;
ALTER TABLE IF EXISTS "BillingRecords" ADD COLUMN IF NOT EXISTS "CurrencyCode" text NOT NULL DEFAULT 'NGN';
ALTER TABLE IF EXISTS "BillingRecords" ADD COLUMN IF NOT EXISTS "PaymentReference" text NULL;
ALTER TABLE IF EXISTS "BillingRecords" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
""");

    logger.LogInformation("PostgreSQL hosted schema verified for Super Admin and affiliate features.");
}
