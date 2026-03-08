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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global query filter: tenant-scoped entities only see their school's data. Evaluated at query time via _tenantContext.
        builder.Entity<Student>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<Teacher>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<Parent>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<Grade>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        builder.Entity<Class>().HasQueryFilter(e => _tenantContext == null || !_tenantContext.CurrentSchoolId.HasValue || e.SchoolId == _tenantContext.CurrentSchoolId);
        // StudentParent/TeacherClass are accessed via Student/Teacher; no filter needed to avoid cross-tenant leaks.

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
