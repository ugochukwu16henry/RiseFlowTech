namespace RiseFlow.Api.Entities;

/// <summary>
/// Record of an issued transcript for verification via QR code.
/// </summary>
public class TranscriptVerification
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid SchoolId { get; set; }
    public string VerificationToken { get; set; } = string.Empty; // used in QR URL
    public DateTime IssuedAtUtc { get; set; }
    public string? IssuedToName { get; set; }

    public Student Student { get; set; } = null!;
    public School School { get; set; } = null!;
}
