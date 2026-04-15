using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace RiseFlow.Api.Data;

/// <summary>
/// Used by EF Core tools at design time (e.g. migrations) when no HTTP context exists.
/// Uses the same resolution as runtime: <c>DATABASE_URL</c>, <c>DATABASE_PUBLIC_URL</c>, then
/// <c>ConnectionStrings:DefaultConnection</c> (see <see cref="DatabaseConnectionHelper"/>).
/// </summary>
public class RiseFlowDbContextFactory : IDesignTimeDbContextFactory<RiseFlowDbContext>
{
    public RiseFlowDbContext CreateDbContext(string[] args)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var basePath = File.Exists(Path.Combine(currentDir, "appsettings.json"))
            ? currentDir
            : Path.Combine(currentDir, "src", "RiseFlow.Api");
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var dbProvider = config["Database:Provider"] ?? "Npgsql";

        var optionsBuilder = new DbContextOptionsBuilder<RiseFlowDbContext>();
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

        if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dbProvider, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            var sqliteConn = config.GetConnectionString("Sqlite");
            if (string.IsNullOrWhiteSpace(sqliteConn))
            {
                var dbPath = Path.Combine(basePath, "riseflow.dev.db");
                sqliteConn = $"Data Source={dbPath}";
            }

            optionsBuilder.UseSqlite(sqliteConn);
            return new RiseFlowDbContext(optionsBuilder.Options);
        }

        var pg = DatabaseConnectionHelper.GetConnectionString(config);
        if (string.IsNullOrWhiteSpace(pg))
        {
            throw new InvalidOperationException(
                "Design-time: set DATABASE_URL, DATABASE_PUBLIC_URL, or ConnectionStrings:DefaultConnection for EF migrations. " +
                "See docs/RAILWAY_POSTGRES.md.");
        }

        optionsBuilder.UseNpgsql(pg);
        return new RiseFlowDbContext(optionsBuilder.Options);
    }
}
