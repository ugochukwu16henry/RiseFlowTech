using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Middleware;
using RiseFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

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

// Multi-tenancy: TenantService holds TenantId (from header or claim) for the request; EF filters by School
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<SchoolOnboardingService>();
builder.Services.AddSingleton<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<TranscriptPdfService>();
builder.Services.AddScoped<StudentBulkUploadService>();
builder.Services.AddScoped<ExcelService>();

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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors();

// Extract TenantId from X-Tenant-Id header so TenantService and EF can filter by School
app.UseMiddleware<TenantMiddleware>();

// Seed roles on startup (idempotent)
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await RoleSeeder.SeedRolesAsync(roleManager);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
