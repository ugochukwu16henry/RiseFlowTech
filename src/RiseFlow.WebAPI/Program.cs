using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Application;
using RiseFlow.Application.Support;
using RiseFlow.Infrastructure.Data;
using RiseFlow.Infrastructure.Identity;
using RiseFlow.Infrastructure.Support;

var builder = WebApplication.CreateBuilder(args);

// Configuration: SQLite connection string (e.g. in appsettings.json)
// "ConnectionStrings": { "Sqlite": "Data Source=riseflow-webapi.db" }
var sqliteConn = builder.Configuration.GetConnectionString("Sqlite");
if (string.IsNullOrWhiteSpace(sqliteConn))
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "riseflow-webapi.db");
    sqliteConn = $"Data Source={dbPath}";
}

builder.Services.AddDbContext<RiseFlowDbContext>(options =>
    options.UseSqlite(sqliteConn));

// ASP.NET Core Identity with Guid keys and PostgreSQL-backed stores
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
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

// HttpContext access + SchoolContext for tenant resolution
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ISchoolContext, SchoolContext>();
builder.Services.AddScoped<ISupportService, SupportService>();

builder.Services.AddSignalR();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
}).AddCookie(IdentityConstants.ApplicationScheme);

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RiseFlow.WebAPI.Hubs.SupportHub>("/hubs/support");

app.Run();

