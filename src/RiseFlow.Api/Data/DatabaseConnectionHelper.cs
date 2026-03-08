namespace RiseFlow.Api.Data;

/// <summary>
/// Resolves database connection string from config or DATABASE_URL (e.g. Railway, Heroku).
/// </summary>
public static class DatabaseConnectionHelper
{
    /// <summary>
    /// Gets the Npgsql connection string: DATABASE_URL env var first (e.g. Railway), then DefaultConnection from config.
    /// </summary>
    public static string GetConnectionString(IConfiguration config)
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(url))
            return ParsePostgresUrl(url.Trim());

        var fromConfig = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig.Trim();

        return "Host=localhost;Database=RiseFlow;Username=postgres;Password=postgres;Include Error Detail=true";
    }

    /// <summary>
    /// Converts a postgresql:// URL into Npgsql key-value connection string.
    /// </summary>
    public static string ParsePostgresUrl(string databaseUrl)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            throw new ArgumentException("Database URL is required.", nameof(databaseUrl));

        // Support both postgres:// and postgresql://
        var url = databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? databaseUrl
            : databaseUrl.Replace("postgres://", "postgresql://", StringComparison.OrdinalIgnoreCase);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri?.Host == null)
            throw new ArgumentException("Invalid DATABASE_URL format.", nameof(databaseUrl));

        var userInfo = uri.UserInfo?.Split(':', 2, StringSplitOptions.None) ?? Array.Empty<string>();
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = string.IsNullOrEmpty(uri.AbsolutePath) ? "railway" : uri.AbsolutePath.TrimStart('/');

        var parts = new List<string>
        {
            $"Host={host}",
            $"Port={port}",
            $"Database={database}",
            $"Username={username}",
            $"Password={password}"
        };

        // Prefer SSL when not localhost (e.g. Railway)
        if (!host.Contains("localhost", StringComparison.OrdinalIgnoreCase))
            parts.Add("SSL Mode=Prefer");

        parts.Add("Include Error Detail=true");
        return string.Join(";", parts);
    }
}
