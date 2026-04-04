using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Services;

namespace RiseFlow.Platform.Api;

/// <summary>
/// Registers the legacy <see cref="RiseFlowDbContext"/> against the same store as <c>RiseFlow.Api</c> for phased migration reads.
/// Uses a constructor without <see cref="RiseFlow.Api.Services.ITenantContext"/> so global tenant filters are not applied (see <see cref="RiseFlowDbContext"/>).
/// </summary>
public static class RiseFlowProductDatabaseExtensions
{
    public static IHostApplicationBuilder AddRiseFlowProductDatabase(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Match RiseFlow.Api: encrypted columns need the same key when decrypting is required (counts do not decrypt).
        SensitiveDataEncryption.Initialize(
            builder.Configuration["RiseFlowProduct:Encryption:Key"]
            ?? builder.Configuration["Encryption:Key"]);

        var provider = builder.Configuration["RiseFlowProduct:DatabaseProvider"]
                       ?? builder.Configuration["Database:Provider"]
                       ?? "Sqlite";

        builder.Services.AddDbContext<RiseFlowDbContext>(options =>
        {
            if (string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                var pg = builder.Configuration["RiseFlowProduct:ConnectionStrings:DefaultConnection"]
                         ?? builder.Configuration.GetConnectionString("RiseFlowProduct")
                         ?? builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(pg))
                {
                    throw new InvalidOperationException(
                        "RiseFlow product PostgreSQL requested but RiseFlowProduct:ConnectionStrings:DefaultConnection (or ConnectionStrings:DefaultConnection) is missing.");
                }

                options.UseNpgsql(pg);
                return;
            }

            var sqliteConn = builder.Configuration["RiseFlowProduct:ConnectionStrings:Sqlite"]
                             ?? builder.Configuration.GetConnectionString("RiseFlowProductSqlite")
                             ?? builder.Configuration.GetConnectionString("Sqlite");
            if (string.IsNullOrWhiteSpace(sqliteConn))
            {
                // Default: sibling riseflow.db under RiseFlow.Api content root (set explicit path in config for Platform host).
                var apiRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "RiseFlow.Api"));
                var dbPath = Path.Combine(apiRoot, "riseflow.db");
                sqliteConn = $"Data Source={dbPath}";
            }

            options.UseSqlite(sqliteConn);
        });

        return builder;
    }
}
