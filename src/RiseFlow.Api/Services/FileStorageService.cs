using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RiseFlow.Api.Services;

/// <summary>
/// Resolves a stable location for uploaded files (logos, profile photos, documents)
/// so they are not tied to the current build output or cleared on app restarts.
/// </summary>
public sealed class FileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileStorageService> _logger;
    private string? _rootPath;

    public FileStorageService(IWebHostEnvironment env, IConfiguration configuration, ILogger<FileStorageService> logger)
    {
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// The physical root used for new uploads. Set <c>RiseFlow:StorageRoot</c> or
    /// <c>RISEFLOW_STORAGE_ROOT</c> in production to point at a mounted persistent volume.
    /// </summary>
    public string RootPath => _rootPath ??= InitializeRootPath();

    public string EnsureWritePath(string relativePath)
    {
        var fullPath = CombineUnderRoot(RootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        return fullPath;
    }

    public string ResolveReadPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path is required.", nameof(relativePath));

        foreach (var root in GetCandidateRoots())
        {
            var candidate = CombineUnderRoot(root, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        return CombineUnderRoot(RootPath, relativePath);
    }

    private string InitializeRootPath()
    {
        var configuredRoot = _configuration["RiseFlow:StorageRoot"];
        if (string.IsNullOrWhiteSpace(configuredRoot))
            configuredRoot = Environment.GetEnvironmentVariable("RISEFLOW_STORAGE_ROOT");

        var root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(_env.ContentRootPath, "App_Data", "uploads")
            : configuredRoot;

        root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root));

        Directory.CreateDirectory(root);
        foreach (var folder in new[] { "logos", "cac", "students", "teachers", "uploads" })
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
        }

        _logger.LogInformation("RiseFlow file storage root: {StorageRoot}", root);
        return root;
    }

    private IEnumerable<string> GetCandidateRoots()
    {
        yield return RootPath;

        if (!string.IsNullOrWhiteSpace(_env.WebRootPath))
            yield return Path.GetFullPath(_env.WebRootPath);

        yield return Path.GetFullPath(Path.Combine(_env.ContentRootPath, "wwwroot"));
        yield return Path.GetFullPath(_env.ContentRootPath);
    }

    private static string CombineUnderRoot(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        var safeRelativePath = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, safeRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file storage path.");

        return fullPath;
    }
}
