using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RiseFlow.Api.Data;

/// <summary>
/// Used by EF Core tools at design time (e.g. migrations) when no HTTP context exists.
/// This project keeps a single Npgsql-shaped migration chain; design-time always uses PostgreSQL
/// so snapshots never drift to SQLite types (env vars like Database__Provider must not affect scaffolding).
/// Runtime provider remains configurable in <see cref="Program.cs"/>.
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

        var pg = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(pg))
        {
            throw new InvalidOperationException(
                "Design-time: ConnectionStrings:DefaultConnection is required for EF migrations (Npgsql). " +
                "Set it in appsettings.Development.json or environment.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<RiseFlowDbContext>();
        optionsBuilder.UseNpgsql(pg);
        return new RiseFlowDbContext(optionsBuilder.Options);
    }
}
