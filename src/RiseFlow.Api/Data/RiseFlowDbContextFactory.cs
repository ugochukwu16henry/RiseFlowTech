using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RiseFlow.Api.Data;

/// <summary>
/// Used by EF Core tools at design time (e.g. migrations) when no HTTP context exists.
/// Must use the same provider as runtime (<see cref="Program.cs"/>) so migrations stay Npgsql-compatible.
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

        var optionsBuilder = new DbContextOptionsBuilder<RiseFlowDbContext>();

        var dbProvider = config["Database:Provider"] ?? "Sqlite";
        if (string.Equals(dbProvider, "Npgsql", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dbProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            var pg = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(pg))
            {
                throw new InvalidOperationException(
                    "Design-time: Database:Provider is Npgsql but ConnectionStrings:DefaultConnection is missing. " +
                    "Set it in appsettings.Development.json or environment.");
            }

            optionsBuilder.UseNpgsql(pg);
        }
        else
        {
            var sqliteConn = config.GetConnectionString("Sqlite");
            if (string.IsNullOrWhiteSpace(sqliteConn))
            {
                var dbPath = Path.Combine(basePath, "riseflow.db");
                sqliteConn = $"Data Source={dbPath}";
            }

            optionsBuilder.UseSqlite(sqliteConn);
        }

        return new RiseFlowDbContext(optionsBuilder.Options);
    }
}
