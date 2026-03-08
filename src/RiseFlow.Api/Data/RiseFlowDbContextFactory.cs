using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RiseFlow.Api.Data;

/// <summary>
/// Used by EF Core tools at design time (e.g. migrations) when no HTTP context exists.
/// </summary>
public class RiseFlowDbContextFactory : IDesignTimeDbContextFactory<RiseFlowDbContext>
{
    public RiseFlowDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<RiseFlowDbContext>();
        var conn = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=RiseFlow;Username=postgres;Password=postgres;";
        optionsBuilder.UseNpgsql(conn);

        return new RiseFlowDbContext(optionsBuilder.Options);
    }
}
