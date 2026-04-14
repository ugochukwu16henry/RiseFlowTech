using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FileAssetsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IWebHostEnvironment _env;
    private readonly FileStorageService _fileStorage;

    public FileAssetsController(RiseFlowDbContext db, ITenantContext tenant, IWebHostEnvironment env, FileStorageService fileStorage)
    {
        _db = db;
        _tenant = tenant;
        _env = env;
        _fileStorage = fileStorage;
    }

    /// <summary>
    /// Upload a file for the current school (photos, documents).
    /// File bytes are persisted in PostgreSQL via FileAssets.FileBytes.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)] // 20 MB
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FileAsset>> Upload([FromForm] IFormFile file, [FromForm] string? category, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File is required.");

        var schoolId = _tenant.CurrentSchoolId;
        if (!schoolId.HasValue)
            return Forbid();

        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var relativePath = Path.Combine("uploads", schoolId.Value.ToString(), storedName).Replace("\\", "/");

        await using (var input = file.OpenReadStream())
        {
            await _fileStorage.UploadAsync(relativePath, input, file.ContentType, ct);
        }

        var assetId = Guid.NewGuid();

        var asset = new FileAsset
        {
            Id = assetId,
            SchoolId = schoolId.Value,
            OriginalFileName = file.FileName,
            StoredFileName = storedName,
            RelativePath = relativePath,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            FileBytes = null,
            Category = string.IsNullOrWhiteSpace(category) ? null : category,
            UploadedBy = _tenant.CurrentUserEmail,
            UploadedAtUtc = DateTime.UtcNow
        };

        _db.FileAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = asset.Id }, asset);
    }

    /// <summary>Get metadata for a specific file belonging to the current school.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FileAsset>> GetById(Guid id, CancellationToken ct)
    {
        var schoolId = _tenant.CurrentSchoolId;
        if (!schoolId.HasValue)
            return Forbid();

        var asset = await _db.FileAssets.FirstOrDefaultAsync(f => f.Id == id && f.SchoolId == schoolId.Value, ct);
        if (asset == null)
            return NotFound();

        return Ok(asset);
    }

    /// <summary>Download the actual file bytes for a given asset.</summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var schoolId = _tenant.CurrentSchoolId;
        if (!schoolId.HasValue)
            return Forbid();

        var asset = await _db.FileAssets.FirstOrDefaultAsync(f => f.Id == id && f.SchoolId == schoolId.Value, ct);
        if (asset == null)
            return NotFound();

        var storageBytes = await _fileStorage.TryReadBytesAsync(asset.RelativePath, ct);
        if (storageBytes != null && storageBytes.Length > 0)
            return File(storageBytes, asset.ContentType ?? "application/octet-stream", asset.OriginalFileName);

        if (asset.FileBytes != null && asset.FileBytes.Length > 0)
        {
            return File(asset.FileBytes, asset.ContentType ?? "application/octet-stream", asset.OriginalFileName);
        }

        var fullPath = _fileStorage.ResolveReadPath(asset.RelativePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound("File not found on disk.");

        var contentType = asset.ContentType ?? "application/octet-stream";
        var stream = System.IO.File.OpenRead(fullPath);
        return File(stream, contentType, asset.OriginalFileName);
    }

    /// <summary>Get inline content for a file asset (DB-first, disk fallback).</summary>
    [HttpGet("content/{id:guid}")]
    public async Task<IActionResult> Content(Guid id, CancellationToken ct)
    {
        var schoolId = _tenant.CurrentSchoolId;
        if (!schoolId.HasValue)
            return Forbid();

        var asset = await _db.FileAssets.FirstOrDefaultAsync(f => f.Id == id && f.SchoolId == schoolId.Value, ct);
        if (asset == null)
            return NotFound();

        var storageBytes = await _fileStorage.TryReadBytesAsync(asset.RelativePath, ct);
        if (storageBytes != null && storageBytes.Length > 0)
            return File(storageBytes, asset.ContentType ?? "application/octet-stream");

        if (asset.FileBytes != null && asset.FileBytes.Length > 0)
            return File(asset.FileBytes, asset.ContentType ?? "application/octet-stream");

        var fullPath = _fileStorage.ResolveReadPath(asset.RelativePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound("File not found.");

        var stream = System.IO.File.OpenRead(fullPath);
        return File(stream, asset.ContentType ?? "application/octet-stream");
    }
}

