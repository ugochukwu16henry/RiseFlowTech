namespace RiseFlow.Api.Entities;

/// <summary>
/// Represents a file (photo, document, etc.) uploaded by a school.
/// The actual bytes are stored on disk; this table holds metadata and paths.
/// </summary>
public class FileAsset : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }

    public string? Category { get; set; } // e.g. "student-photo", "document"
    public string? UploadedBy { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

