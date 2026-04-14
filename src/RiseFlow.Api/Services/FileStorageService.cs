using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
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
    private readonly IAmazonS3? _s3;
    private readonly string? _bucketName;
    private string? _rootPath;

    public FileStorageService(IWebHostEnvironment env, IConfiguration configuration, ILogger<FileStorageService> logger)
    {
        _env = env;
        _configuration = configuration;
        _logger = logger;

        _bucketName = ResolveObjectStorageBucketName();
        _s3 = CreateObjectStorageClient();
    }

    /// <summary>
    /// The physical root used for new uploads. Set <c>RiseFlow:StorageRoot</c> or
    /// <c>RISEFLOW_STORAGE_ROOT</c> in production to point at a mounted persistent volume.
    /// </summary>
    public string RootPath => _rootPath ??= InitializeRootPath();

    public bool UsesObjectStorage => _s3 != null && !string.IsNullOrWhiteSpace(_bucketName);

    public async Task UploadAsync(string relativePath, Stream content, string? contentType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path is required.", nameof(relativePath));

        if (content == null)
            throw new ArgumentNullException(nameof(content));

        if (UsesObjectStorage)
        {
            var key = NormalizeStorageKey(relativePath);
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = content,
                AutoCloseStream = false,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
            };

            await _s3!.PutObjectAsync(request, ct);
            return;
        }

        var writePath = EnsureWritePath(relativePath);
        await using var target = File.Create(writePath);
        await content.CopyToAsync(target, ct);
    }

    public async Task<byte[]?> TryReadBytesAsync(string relativePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (UsesObjectStorage)
        {
            var key = NormalizeStorageKey(relativePath);
            try
            {
                var response = await _s3!.GetObjectAsync(_bucketName!, key, ct);
                await using var ms = new MemoryStream();
                await response.ResponseStream.CopyToAsync(ms, ct);
                return ms.ToArray();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Object storage read failed for key {StorageKey}. Falling back to local disk lookup.", key);
            }
        }

        foreach (var root in GetCandidateRoots())
        {
            var candidate = CombineUnderRoot(root, relativePath);
            if (File.Exists(candidate))
                return await File.ReadAllBytesAsync(candidate, ct);
        }

        return null;
    }

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

    private IAmazonS3? CreateObjectStorageClient()
    {
        var endpoint = _configuration["RiseFlow:ObjectStorage:EndpointUrl"]
            ?? Environment.GetEnvironmentVariable("RISEFLOW_OBJECT_STORAGE_ENDPOINT_URL");
        var accessKey = _configuration["RiseFlow:ObjectStorage:AccessKeyId"]
            ?? Environment.GetEnvironmentVariable("RISEFLOW_OBJECT_STORAGE_ACCESS_KEY_ID");
        var secretKey = _configuration["RiseFlow:ObjectStorage:SecretAccessKey"]
            ?? Environment.GetEnvironmentVariable("RISEFLOW_OBJECT_STORAGE_SECRET_ACCESS_KEY");
        var region = _configuration["RiseFlow:ObjectStorage:Region"]
            ?? Environment.GetEnvironmentVariable("RISEFLOW_OBJECT_STORAGE_REGION")
            ?? "auto";

        if (string.IsNullOrWhiteSpace(endpoint)
            || string.IsNullOrWhiteSpace(accessKey)
            || string.IsNullOrWhiteSpace(secretKey)
            || string.IsNullOrWhiteSpace(_bucketName))
        {
            _logger.LogInformation("Object storage not configured. Using file system storage at {StorageRoot}.", RootPath);
            return null;
        }

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = region,
            ForcePathStyle = true
        };

        var client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
        _logger.LogInformation("Object storage enabled for bucket {BucketName} via endpoint {Endpoint}.", _bucketName, endpoint);
        return client;
    }

    private string? ResolveObjectStorageBucketName()
    {
        return _configuration["RiseFlow:ObjectStorage:BucketName"]
            ?? Environment.GetEnvironmentVariable("RISEFLOW_OBJECT_STORAGE_BUCKET_NAME");
    }

    private static string NormalizeStorageKey(string relativePath)
    {
        return relativePath.Replace('\\', '/').TrimStart('/');
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
