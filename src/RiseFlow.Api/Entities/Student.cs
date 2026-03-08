namespace RiseFlow.Api.Entities;

/// <summary>
/// Student profile aligned with Nigerian statutory admission register and other African ministry standards.
/// Tenant-scoped. A student can have multiple parents (many-to-many). ParentAccessCode links parents without manual admin.
/// </summary>
public class Student : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }

    // ——— Personal (Statutory / Ministry) ———
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? StateOfOrigin { get; set; }
    public string? LGA { get; set; }

    // ——— Identification (NIN Nigeria; other nations use NationalIdType + NationalIdNumber) ———
    public string? NIN { get; set; }
    public string? NationalIdType { get; set; }
    public string? NationalIdNumber { get; set; }

    // ——— Academic ———
    public string? AdmissionNumber { get; set; }
    public DateTime? DateOfAdmission { get; set; }
    public Guid? ClassId { get; set; }
    public Guid? GradeId { get; set; }
    public string? PreviousSchool { get; set; }

    // ——— Health ———
    public string? BloodGroup { get; set; }
    public string? Genotype { get; set; }
    public string? Allergies { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // ——— Parent link (unique code per student for parent to claim link) ———
    public string? ParentAccessCode { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Class? Class { get; set; }
    public Grade? Grade { get; set; }
    public ICollection<StudentParent> StudentParents { get; set; } = new List<StudentParent>();
    public ICollection<StudentResult> Results { get; set; } = new List<StudentResult>();
    public ICollection<TranscriptVerification> TranscriptVerifications { get; set; } = new List<TranscriptVerification>();
}
