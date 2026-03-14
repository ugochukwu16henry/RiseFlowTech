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

    public FileAssetsController(RiseFlowDbContext db, ITenantContext tenant, IWebHostEnvironment env)
    {
        _db = db;
        _tenant = tenant;
        _env = env;
    }

    /// <summary>
    /// Upload a file for the current school (photos, documents). Stores on disk under wwwroot/uploads/{SchoolId}/
    /// and records metadata in the FileAssets table.
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

        // Ensure uploads folder exists: wwwroot/uploads/{SchoolId}
        var uploadsRoot = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", schoolId.Value.ToString());
        Directory.CreateDirectory(uploadsRoot);

        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(uploadsRoot, storedName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var relativePath = Path.Combine("uploads", schoolId.Value.ToString(), storedName).Replace("\\", "/");

        var asset = new FileAsset
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId.Value,
            OriginalFileName = file.FileName,
            StoredFileName = storedName,
            RelativePath = relativePath,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
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

        var fullPath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), asset.RelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!System.IO.File.Exists(fullPath))
            return NotFound("File not found on disk.");

        var contentType = asset.ContentType ?? "application/octet-stream";
        var stream = System.IO.File.OpenRead(fullPath);
        return File(stream, contentType, asset.OriginalFileName);
    }
}

