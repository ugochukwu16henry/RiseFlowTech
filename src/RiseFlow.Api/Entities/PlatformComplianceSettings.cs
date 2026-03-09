namespace RiseFlow.Api.Entities;

/// <summary>
/// Singleton-style platform-wide compliance settings for NDPC / GDPR-style requirements.
/// Allows SuperAdmin to designate a Data Protection Officer (DPO) and link a DPIA document.
/// </summary>
public class PlatformComplianceSettings
{
    public int Id { get; set; }

    public string? DataProtectionOfficerName { get; set; }
    public string? DataProtectionOfficerEmail { get; set; }

    /// <summary>Public or internal URL where the platform's Data Protection Impact Assessment (DPIA) can be accessed.</summary>
    public string? DpiaDocumentUrl { get; set; }

    public DateTime? LastUpdatedUtc { get; set; }
}

