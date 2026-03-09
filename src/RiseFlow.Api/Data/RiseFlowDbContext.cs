using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Data;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Data;

public class RiseFlowDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ITenantContext? _tenantContext;

    public RiseFlowDbContext(DbContextOptions<RiseFlowDbContext> options) : base(options) { }

    public RiseFlowDbContext(DbContextOptions<RiseFlowDbContext> options, ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<School> Schools => Set<School>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<StudentParent> StudentParents => Set<StudentParent>();
    public DbSet<TeacherClass> TeacherClasses => Set<TeacherClass>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<ClassSubject> ClassSubjects => Set<ClassSubject>();
    public DbSet<TeacherClassSubject> TeacherClassSubjects => Set<TeacherClassSubject>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<StudentResult> StudentResults => Set<StudentResult>();
    public DbSet<BillingRecord> BillingRecords => Set<BillingRecord>();
    public DbSet<TranscriptVerification> TranscriptVerifications => Set<TranscriptVerification>();
    public DbSet<AssessmentCategory> AssessmentCategories => Set<AssessmentCategory>();
    public DbSet<AssessmentItem> AssessmentItems => Set<AssessmentItem>();
    public DbSet<StudentAssessment> StudentAssessments => Set<StudentAssessment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global query filter: every entity implementing ITenantEntity is filtered by current tenant (Where(x => x.TenantId == _currentTenantId)).
        ApplyTenantQueryFilters(builder);

        var sensitiveConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<string?, string?>(
            v => SensitiveDataEncryption.Encrypt(v),
            v => SensitiveDataEncryption.Decrypt(v));

        // School
        builder.Entity<School>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.Address).HasMaxLength(512);
            e.Property(x => x.SchoolType).HasMaxLength(64);
            e.Property(x => x.PrincipalName).HasMaxLength(128);
            e.Property(x => x.Phone).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.CacNumber).HasMaxLength(64);
            e.Property(x => x.CountryCode).HasMaxLength(2);
            e.Property(x => x.CurrencyCode).HasMaxLength(3);
            e.Property(x => x.LogoFileName).HasMaxLength(256);
        });

        // Grade
        builder.Entity<Grade>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(64);
            e.HasOne(x => x.School).WithMany(s => s.Grades).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Class
        builder.Entity<Class>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(64);
            e.Property(x => x.AcademicYear).HasMaxLength(16);
            e.HasOne(x => x.School).WithMany(s => s.Classes).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Grade).WithMany(g => g.Classes).HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        });

        // Student
        builder.Entity<Student>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).IsRequired().HasMaxLength(128);
            e.Property(x => x.LastName).IsRequired().HasMaxLength(128);
            e.Property(x => x.MiddleName).HasMaxLength(128);
            e.Property(x => x.Gender).HasMaxLength(32);
            e.Property(x => x.Nationality).HasMaxLength(128);
            e.Property(x => x.StateOfOrigin).HasMaxLength(128);
            e.Property(x => x.LGA).HasMaxLength(128);
            e.Property(x => x.NIN).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.NationalIdType).HasMaxLength(32);
            e.Property(x => x.NationalIdNumber).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.AdmissionNumber).HasMaxLength(64);
            e.Property(x => x.PreviousSchool).HasMaxLength(256);
            e.Property(x => x.BloodGroup).HasMaxLength(16);
            e.Property(x => x.Genotype).HasMaxLength(16);
            e.Property(x => x.Allergies).HasMaxLength(512);
            e.Property(x => x.EmergencyContactName).HasMaxLength(128);
            e.Property(x => x.EmergencyContactPhone).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.ParentAccessCode).HasMaxLength(16);
            e.Property(x => x.ProfilePhotoFileName).HasMaxLength(256);
            e.HasIndex(x => new { x.SchoolId, x.ParentAccessCode }).IsUnique();
            e.HasOne(x => x.School).WithMany(s => s.Students).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Class).WithMany(c => c.Students).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Grade).WithMany(g => g.Students).HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.SetNull);
        });

        // Teacher
        builder.Entity<Teacher>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).IsRequired().HasMaxLength(128);
            e.Property(x => x.LastName).IsRequired().HasMaxLength(128);
            e.Property(x => x.MiddleName).HasMaxLength(128);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.WhatsAppNumber).HasMaxLength(32);
            e.Property(x => x.StaffId).HasMaxLength(64);
            e.Property(x => x.SubjectSpecialization).HasMaxLength(128);
            e.Property(x => x.Gender).HasMaxLength(32);
            e.Property(x => x.Nationality).HasMaxLength(128);
            e.Property(x => x.StateOfOrigin).HasMaxLength(128);
            e.Property(x => x.LGA).HasMaxLength(128);
            e.Property(x => x.NIN).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.NationalIdType).HasMaxLength(32);
            e.Property(x => x.NationalIdNumber).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.TrcnNumber).HasMaxLength(64);
            e.Property(x => x.ResidentialAddress).HasMaxLength(512);
            e.Property(x => x.HighestQualification).HasMaxLength(128);
            e.Property(x => x.FieldOfStudy).HasMaxLength(128);
            e.Property(x => x.PreviousSchools).HasMaxLength(512);
            e.Property(x => x.ProfessionalBodies).HasMaxLength(256);
            e.Property(x => x.EmploymentType).HasMaxLength(64);
            e.Property(x => x.RoleTitle).HasMaxLength(128);
            e.Property(x => x.Department).HasMaxLength(128);
            e.Property(x => x.BaseSalaryCurrency).HasMaxLength(8);
            e.Property(x => x.AllowancesNote).HasMaxLength(512);
            e.Property(x => x.PromotionHistory).HasMaxLength(1024);
            e.Property(x => x.Recognitions).HasMaxLength(512);
            e.Property(x => x.ProfilePhotoFileName).HasMaxLength(256);
            e.HasOne(x => x.School).WithMany(s => s.Teachers).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Parent
        builder.Entity<Parent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).IsRequired().HasMaxLength(128);
            e.Property(x => x.LastName).IsRequired().HasMaxLength(128);
            e.Property(x => x.MiddleName).HasMaxLength(128);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.Relationship).HasMaxLength(64);
            e.Property(x => x.WhatsAppNumber).HasMaxLength(32);
            e.Property(x => x.ResidentialAddress).HasMaxLength(512);
            e.Property(x => x.Occupation).HasMaxLength(128);
            e.HasOne(x => x.School).WithMany(s => s.Parents).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // StudentParent (many-to-many)
        builder.Entity<StudentParent>(e =>
        {
            e.HasKey(x => new { x.StudentId, x.ParentId });
            e.Property(x => x.RelationshipToStudent).HasMaxLength(64);
            e.HasOne(x => x.Student).WithMany(s => s.StudentParents).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany(p => p.StudentParents).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
        });

        // TeacherClass (many-to-many)
        builder.Entity<TeacherClass>(e =>
        {
            e.HasKey(x => new { x.TeacherId, x.ClassId });
            e.Property(x => x.RoleInClass).HasMaxLength(64);
            e.HasOne(x => x.Teacher).WithMany(t => t.TeacherClasses).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Class).WithMany(c => c.TeacherClasses).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        });

        // Subject
        builder.Entity<Subject>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(128);
            e.Property(x => x.Code).HasMaxLength(32);
            e.HasOne(x => x.School).WithMany(s => s.Subjects).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // TeacherSubject (many-to-many)
        builder.Entity<TeacherSubject>(e =>
        {
            e.HasKey(x => new { x.TeacherId, x.SubjectId });
            e.HasOne(x => x.Teacher).WithMany(t => t.TeacherSubjects).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Subject).WithMany(s => s.TeacherSubjects).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
        });

        // ClassSubject (many-to-many)
        builder.Entity<ClassSubject>(e =>
        {
            e.HasKey(x => new { x.ClassId, x.SubjectId });
            e.HasOne(x => x.Class).WithMany(c => c.ClassSubjects).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Subject).WithMany(s => s.ClassSubjects).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
        });

        // TeacherClassSubject (teacher teaches subject in class)
        builder.Entity<TeacherClassSubject>(e =>
        {
            e.HasKey(x => new { x.TeacherId, x.ClassId, x.SubjectId });
            e.HasOne(x => x.Teacher).WithMany(t => t.TeacherClassSubjects).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Class).WithMany(c => c.TeacherClassSubjects).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Subject).WithMany(s => s.TeacherClassSubjects).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
        });

        // AcademicTerm
        builder.Entity<AcademicTerm>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(64);
            e.Property(x => x.AcademicYear).IsRequired().HasMaxLength(16);
            e.HasOne(x => x.School).WithMany(s => s.AcademicTerms).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // AssessmentCategory
        builder.Entity<AssessmentCategory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(128);
            e.HasOne(x => x.School).WithMany(s => s.AssessmentCategories).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // AssessmentItem
        builder.Entity<AssessmentItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Label).IsRequired().HasMaxLength(256);
            e.HasOne(x => x.Category).WithMany(c => c.Items).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        // StudentAssessment
        builder.Entity<StudentAssessment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Value).HasMaxLength(32);
            e.Property(x => x.Comment).HasMaxLength(512);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Term).WithMany(t => t.StudentAssessments).HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Item).WithMany(i => i.StudentAssessments).HasForeignKey(x => x.AssessmentItemId).OnDelete(DeleteBehavior.Cascade);
        });

        // StudentResult
        builder.Entity<StudentResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AssessmentType).IsRequired().HasMaxLength(64);
            e.Property(x => x.GradeLetter).HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(512);
            e.HasOne(x => x.School).WithMany(s => s.StudentResults).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany(s => s.Results).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany(s => s.StudentResults).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Term).WithMany(t => t.StudentResults).HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.EnteredByTeacher).WithMany(t => t.EnteredResults).HasForeignKey(x => x.EnteredByTeacherId).OnDelete(DeleteBehavior.SetNull);
        });

        // BillingRecord
        builder.Entity<BillingRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PeriodLabel).IsRequired().HasMaxLength(32);
            e.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(3);
            e.Property(x => x.PaymentReference).HasMaxLength(128);
            e.HasOne(x => x.School).WithMany(s => s.BillingRecords).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // TranscriptVerification
        builder.Entity<TranscriptVerification>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.VerificationToken).IsUnique();
            e.Property(x => x.VerificationToken).IsRequired().HasMaxLength(64);
            e.Property(x => x.ContentHash).HasMaxLength(64);
            e.Property(x => x.IssuedToName).HasMaxLength(256);
            e.HasOne(x => x.Student).WithMany(s => s.TranscriptVerifications).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.School).WithMany(s => s.TranscriptVerifications).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Identity: use Guid for User and Role
        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.SchoolId).IsRequired(false);
        });

        // AuditLog: no tenant filter; Super Admin can query all
        builder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).IsRequired().HasMaxLength(32);
            e.Property(x => x.EntityType).IsRequired().HasMaxLength(64);
            e.Property(x => x.EntityId).HasMaxLength(36);
            e.Property(x => x.UserEmail).HasMaxLength(256);
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.Details).HasMaxLength(1024);
            e.HasIndex(x => new { x.SchoolId, x.CreatedAtUtc });
        });
    }

    /// <summary>
    /// Applies a global query filter to every entity that implements <see cref="ITenantEntity"/>:
    /// Where(x => x.TenantId == _currentTenantId). The tenant key property is <see cref="ITenantEntity.SchoolId"/>.
    /// When tenant context or current tenant ID is null, no filter is applied (all rows visible for that entity).
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder builder)
    {
        if (_tenantContext == null)
            return;

        var tenantContextConstant = Expression.Constant(_tenantContext);
        var currentTenantIdProperty = Expression.Property(tenantContextConstant, nameof(ITenantContext.CurrentSchoolId));

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var tenantIdPropertyInfo = entityType.ClrType.GetProperty(nameof(ITenantEntity.SchoolId));
            if (tenantIdPropertyInfo == null)
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "x");
            var entityTenantId = Expression.Property(parameter, tenantIdPropertyInfo);

            Expression comparableTenantId = currentTenantIdProperty;
            if (entityTenantId.Type != currentTenantIdProperty.Type)
            {
                if (entityTenantId.Type == typeof(Guid) && currentTenantIdProperty.Type == typeof(Guid?))
                {
                    comparableTenantId = Expression.Property(currentTenantIdProperty, nameof(Nullable<Guid>.Value));
                }
                else
                {
                    comparableTenantId = Expression.Convert(currentTenantIdProperty, entityTenantId.Type);
                }
            }

            var tenantIdEquals = Expression.Equal(entityTenantId, comparableTenantId);

            // When _tenantContext is null or CurrentSchoolId has no value, do not filter (allow all)
            var contextIsNull = Expression.Equal(tenantContextConstant, Expression.Constant(null, typeof(ITenantContext)));
            var currentTenantIdHasValue = Expression.Property(currentTenantIdProperty, "HasValue");
            var currentTenantIdIsNull = Expression.Not(currentTenantIdHasValue);
            var noFilter = Expression.Or(contextIsNull, currentTenantIdIsNull);
            var filterBody = Expression.Or(noFilter, tenantIdEquals);

            var lambda = Expression.Lambda(filterBody, parameter);
            entityType.SetQueryFilter(lambda);
        }
    }

    /// <summary>
    /// Call this to run queries without tenant filter (e.g. SuperAdmin listing all schools).
    /// </summary>
    public void DisableTenantFilter()
    {
        // EF Core doesn't allow changing the filter at runtime on the same context easily;
        // tenant is set in ctor. For SuperAdmin use a context created without tenantId.
    }
}
