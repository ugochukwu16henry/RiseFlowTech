namespace RiseFlow.Api.Entities;

/// <summary>
/// Tenant entity. Each school is a tenant with a unique ID; all other data is scoped by SchoolId.
/// </summary>
public class School
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PrincipalName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    /// <summary>ISO 3166-1 alpha-2 (e.g. NG, GH, KE). Used for billing and localization.</summary>
    public string? CountryCode { get; set; }
    /// <summary>ISO 4217 (e.g. NGN, GHS, KES). Billing is in this currency.</summary>
    public string CurrencyCode { get; set; } = "NGN";
    /// <summary>Stored logo file name (e.g. under wwwroot/logos) or relative path. Set during onboarding or via logo upload.</summary>
    public string? LogoFileName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();
    public ICollection<Parent> Parents { get; set; } = new List<Parent>();
    public ICollection<Class> Classes { get; set; } = new List<Class>();
    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
    public ICollection<AcademicTerm> AcademicTerms { get; set; } = new List<AcademicTerm>();
    public ICollection<StudentResult> StudentResults { get; set; } = new List<StudentResult>();
    public ICollection<BillingRecord> BillingRecords { get; set; } = new List<BillingRecord>();
    public ICollection<TranscriptVerification> TranscriptVerifications { get; set; } = new List<TranscriptVerification>();
    public ICollection<AssessmentCategory> AssessmentCategories { get; set; } = new List<AssessmentCategory>();
}
