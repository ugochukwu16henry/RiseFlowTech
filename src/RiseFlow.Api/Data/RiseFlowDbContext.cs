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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global query filter: tenant-scoped entities only see their school's data. Evaluated at query time via _tenantContext.
        builder.Entity<Student>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<Teacher>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<Parent>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<Grade>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<Class>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<Subject>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<AcademicTerm>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<StudentResult>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<BillingRecord>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<TranscriptVerification>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        // StudentParent/TeacherClass/TeacherSubject/ClassSubject/TeacherClassSubject are accessed via tenant-scoped entities.

        // School
        builder.Entity<School>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.Address).HasMaxLength(512);
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.Email).HasMaxLength(256);
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
            e.Property(x => x.AdmissionNumber).HasMaxLength(64);
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
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.WhatsAppNumber).HasMaxLength(32);
            e.Property(x => x.StaffId).HasMaxLength(64);
            e.Property(x => x.SubjectSpecialization).HasMaxLength(128);
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
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.Relationship).HasMaxLength(64);
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
            e.HasOne(x => x.School).WithMany(s => s.BillingRecords).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // TranscriptVerification
        builder.Entity<TranscriptVerification>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.VerificationToken).IsUnique();
            e.Property(x => x.VerificationToken).IsRequired().HasMaxLength(64);
            e.Property(x => x.IssuedToName).HasMaxLength(256);
            e.HasOne(x => x.Student).WithMany(s => s.TranscriptVerifications).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.School).WithMany(s => s.TranscriptVerifications).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Identity: use Guid for User and Role
        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.SchoolId).IsRequired(false);
        });
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
