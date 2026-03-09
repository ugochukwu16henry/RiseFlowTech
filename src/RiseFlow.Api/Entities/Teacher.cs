namespace RiseFlow.Api.Entities;

/// <summary>
/// Teacher profile. Tenant-scoped. A teacher can be assigned to multiple classes (many-to-many).
/// Includes HR/identity fields aligned with African ministry standards.
/// </summary>
public class Teacher : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? WhatsAppNumber { get; set; }
    public string? StaffId { get; set; }
    public string? SubjectSpecialization { get; set; }

    // Personal / identity
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? StateOfOrigin { get; set; }
    public string? LGA { get; set; }
    public string? NIN { get; set; }
    public string? NationalIdType { get; set; }
    public string? NationalIdNumber { get; set; }
    public string? TrcnNumber { get; set; }

    // Contact / address
    public string? ResidentialAddress { get; set; }

    // Professional / qualification
    public string? HighestQualification { get; set; }
    public string? FieldOfStudy { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? PreviousSchools { get; set; }
    public string? ProfessionalBodies { get; set; }

    // Employment (admin-only)
    public DateOnly? DateEmployed { get; set; }
    public string? EmploymentType { get; set; } // e.g. Full-time, Part-time, Contract, NYSC
    public string? RoleTitle { get; set; }      // e.g. Teacher, Head Teacher, HOD
    public string? Department { get; set; }     // e.g. Primary, JSS, SSS, Science, Arts
    public decimal? BaseSalaryAmount { get; set; }
    public string? BaseSalaryCurrency { get; set; }
    public string? AllowancesNote { get; set; }
    public string? PromotionHistory { get; set; }
    public string? Recognitions { get; set; }

    /// <summary>Relative path for passport-size profile photo (e.g. teachers/{schoolId}/{teacherId}.jpg).</summary>
    public string? ProfilePhotoFileName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public ICollection<TeacherClass> TeacherClasses { get; set; } = new List<TeacherClass>();
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    public ICollection<TeacherClassSubject> TeacherClassSubjects { get; set; } = new List<TeacherClassSubject>();
    public ICollection<StudentResult> EnteredResults { get; set; } = new List<StudentResult>();
}
