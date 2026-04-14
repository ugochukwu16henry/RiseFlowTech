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
    public DbSet<StudentPortalAccess> StudentPortalAccesses => Set<StudentPortalAccess>();
    public DbSet<StudentProfileVisibilitySetting> StudentProfileVisibilitySettings => Set<StudentProfileVisibilitySetting>();
    public DbSet<StudentParentEditWindow> StudentParentEditWindows => Set<StudentParentEditWindow>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<StudentParent> StudentParents => Set<StudentParent>();
    public DbSet<TeacherClass> TeacherClasses => Set<TeacherClass>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<ClassSubject> ClassSubjects => Set<ClassSubject>();
    public DbSet<TeacherClassSubject> TeacherClassSubjects => Set<TeacherClassSubject>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<MarkSubmissionWindow> MarkSubmissionWindows => Set<MarkSubmissionWindow>();
    public DbSet<StudentResult> StudentResults => Set<StudentResult>();
    public DbSet<StudentPromotion> StudentPromotions => Set<StudentPromotion>();
    public DbSet<ClassPromotionRequest> ClassPromotionRequests => Set<ClassPromotionRequest>();
    public DbSet<ClassPromotionRequestItem> ClassPromotionRequestItems => Set<ClassPromotionRequestItem>();
    public DbSet<ClassRoutine> ClassRoutines => Set<ClassRoutine>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<SchoolNotice> SchoolNotices => Set<SchoolNotice>();
    public DbSet<SchoolEvent> SchoolEvents => Set<SchoolEvent>();
    public DbSet<GradingSystem> GradingSystems => Set<GradingSystem>();
    public DbSet<GradeRule> GradeRules => Set<GradeRule>();
    public DbSet<BillingRecord> BillingRecords => Set<BillingRecord>();
    public DbSet<TranscriptVerification> TranscriptVerifications => Set<TranscriptVerification>();
    public DbSet<AssessmentCategory> AssessmentCategories => Set<AssessmentCategory>();
    public DbSet<AssessmentItem> AssessmentItems => Set<AssessmentItem>();
    public DbSet<StudentAssessment> StudentAssessments => Set<StudentAssessment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PlatformComplianceSettings> PlatformComplianceSettings => Set<PlatformComplianceSettings>();
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Affiliate> Affiliates => Set<Affiliate>();
    public DbSet<AffiliateLeadRequest> AffiliateLeadRequests => Set<AffiliateLeadRequest>();
    public DbSet<AffiliateInvite> AffiliateInvites => Set<AffiliateInvite>();
    public DbSet<AffiliateTrainingVideo> AffiliateTrainingVideos => Set<AffiliateTrainingVideo>();
    public DbSet<AffiliateTrainingCompletion> AffiliateTrainingCompletions => Set<AffiliateTrainingCompletion>();
    public DbSet<AffiliatePayout> AffiliatePayouts => Set<AffiliatePayout>();
    public DbSet<AffiliateCommissionLedger> AffiliateCommissionLedgers => Set<AffiliateCommissionLedger>();
    public DbSet<AffiliateNotification> AffiliateNotifications => Set<AffiliateNotification>();
    public DbSet<TeacherProfileFieldSetting> TeacherProfileFieldSettings => Set<TeacherProfileFieldSetting>();
    public DbSet<TeacherCustomFieldValue> TeacherCustomFieldValues => Set<TeacherCustomFieldValue>();
    public DbSet<SchoolBankDetails> SchoolBankDetails => Set<SchoolBankDetails>();
    public DbSet<TermFeeSchedule> TermFeeSchedules => Set<TermFeeSchedule>();
    public DbSet<FeePaymentRecord> FeePaymentRecords => Set<FeePaymentRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // SQLite stores Guid values as case-sensitive TEXT in the local dev database.
        // Normalize every Guid/Guid? property to uppercase text so tenant- and id-based lookups
        // continue to match the rows we just inserted during local verification flows.
        ApplySqliteGuidTextNormalization(builder);

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
            e.Property(x => x.OwnerName).HasMaxLength(128);
            e.Property(x => x.SchoolAdminName).HasMaxLength(128);
            e.Property(x => x.Phone).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.WhatsAppNumber).HasMaxLength(512).HasConversion(sensitiveConverter);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.CacNumber).HasMaxLength(64);
            e.Property(x => x.AffiliateReferralCodeUsed).HasMaxLength(64);
            e.Property(x => x.CountryCode).HasMaxLength(2);
            e.Property(x => x.CurrencyCode).HasMaxLength(3);
            e.Property(x => x.LogoFileName).HasMaxLength(256);
            e.Property(x => x.RegistrationDocumentPath).HasMaxLength(512);
            e.HasOne(x => x.Affiliate)
                .WithMany(x => x.ReferredSchools)
                .HasForeignKey(x => x.AffiliateId)
                .OnDelete(DeleteBehavior.SetNull);
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

        builder.Entity<TeacherProfileFieldSetting>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FieldKey).IsRequired().HasMaxLength(64);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);
            e.HasIndex(x => new { x.SchoolId, x.FieldKey }).IsUnique();
            e.HasOne(x => x.School).WithMany(s => s.TeacherProfileFieldSettings).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeacherCustomFieldValue>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FieldKey).IsRequired().HasMaxLength(64);
            e.Property(x => x.Value).HasMaxLength(2048);
            e.HasIndex(x => new { x.TeacherId, x.FieldKey }).IsUnique();
            e.HasOne(x => x.School).WithMany(s => s.TeacherCustomFieldValues).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
        });

        // ─── School Fees ──────────────────────────────────────────────────────────

        builder.Entity<SchoolBankDetails>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BankName).IsRequired().HasMaxLength(128);
            e.Property(x => x.AccountName).IsRequired().HasMaxLength(256);
            e.Property(x => x.AccountNumber).IsRequired().HasMaxLength(64);
            e.Property(x => x.BranchOrSortCode).HasMaxLength(128);
            e.Property(x => x.PaymentInstructions).HasMaxLength(1024);
            e.HasOne(x => x.School).WithMany(s => s.SchoolBankDetails).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TermFeeSchedule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TermLabel).IsRequired().HasMaxLength(128);
            e.Property(x => x.AcademicYear).IsRequired().HasMaxLength(32);
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.Description).HasMaxLength(512);
            e.HasIndex(x => new { x.SchoolId, x.TermLabel, x.AcademicYear, x.GradeId, x.ClassId });
            e.HasOne(x => x.School).WithMany(s => s.TermFeeSchedules).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Grade).WithMany().HasForeignKey(x => x.GradeId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<FeePaymentRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ReceiptFilePath).HasMaxLength(512);
            e.Property(x => x.ReceiptFileName).HasMaxLength(256);
            e.Property(x => x.ParentNote).HasMaxLength(1024);
            e.Property(x => x.AdminNote).HasMaxLength(1024);
            e.HasIndex(x => new { x.SchoolId, x.ScheduleId, x.StudentId });
            e.HasIndex(x => new { x.SchoolId, x.Status });
            e.HasOne(x => x.School).WithMany(s => s.FeePaymentRecords).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Schedule).WithMany(s => s.Payments).HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
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

        // Student portal access (parent-managed student login + privacy settings)
        builder.Entity<StudentPortalAccess>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.LoginId).IsRequired().HasMaxLength(256);
            e.HasIndex(x => new { x.SchoolId, x.LoginId }).IsUnique();
            e.HasIndex(x => x.StudentId).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StudentProfileVisibilitySetting>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SchoolId).IsUnique();
            e.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
        });

        // Parent student profile edit cooldown windows
        builder.Entity<StudentParentEditWindow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ParentId, x.StudentId }).IsUnique();
            e.HasIndex(x => new { x.SchoolId, x.StudentId });
            e.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
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
            e.Property(x => x.Description).HasMaxLength(512);
            e.HasOne(x => x.School).WithMany(s => s.AcademicTerms).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Exam
        builder.Entity<Exam>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(128);
            e.HasIndex(x => new { x.SchoolId, x.TermId, x.ClassId, x.SubjectId });
            e.HasOne(x => x.School).WithMany(s => s.Exams).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByTeacher).WithMany().HasForeignKey(x => x.CreatedByTeacherId).OnDelete(DeleteBehavior.SetNull);
        });

        // MarkSubmissionWindow
        builder.Entity<MarkSubmissionWindow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SchoolId, x.TermId }).IsUnique();
            e.HasOne(x => x.School).WithMany(s => s.MarkSubmissionWindows).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
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

        // FileAsset (uploaded files/photos stored on disk; metadata in SQLite)
        builder.Entity<FileAsset>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(256);
            e.Property(x => x.StoredFileName).IsRequired().HasMaxLength(256);
            e.Property(x => x.RelativePath).IsRequired().HasMaxLength(512);
            e.Property(x => x.ContentType).HasMaxLength(128);
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.UploadedBy).HasMaxLength(256);
        });

        // AttendanceRecord (central daily attendance per student)
        builder.Entity<AttendanceRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).IsRequired().HasMaxLength(16);
            e.Property(x => x.Period).HasMaxLength(16);
            e.Property(x => x.Note).HasMaxLength(256);
            e.Property(x => x.SourceDeviceId).HasMaxLength(64);

            // One logical record per (SchoolId, StudentId, Date, Period) to make sync idempotent
            e.HasIndex(x => new { x.SchoolId, x.StudentId, x.Date, x.Period }).IsUnique();
        });

        // StudentResult
        builder.Entity<StudentResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExamId);
            e.Property(x => x.AssessmentType).IsRequired().HasMaxLength(64);
            e.Property(x => x.GradeLetter).HasMaxLength(16);
            e.Property(x => x.Comment).HasMaxLength(512);
            e.HasOne(x => x.School).WithMany(s => s.StudentResults).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany(s => s.Results).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany(s => s.StudentResults).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Term).WithMany(t => t.StudentResults).HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Exam).WithMany(x => x.Results).HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.EnteredByTeacher).WithMany(t => t.EnteredResults).HasForeignKey(x => x.EnteredByTeacherId).OnDelete(DeleteBehavior.SetNull);
        });

        // StudentPromotion
        builder.Entity<StudentPromotion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PromotionSessionLabel).HasMaxLength(64);
            e.Property(x => x.Notes).HasMaxLength(512);
            e.HasIndex(x => new { x.SchoolId, x.StudentId, x.FromClassId, x.FromTermId }).IsUnique();
            e.HasOne(x => x.School).WithMany(s => s.StudentPromotions).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.FromClass).WithMany().HasForeignKey(x => x.FromClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ToClass).WithMany().HasForeignKey(x => x.ToClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FromTerm).WithMany().HasForeignKey(x => x.FromTermId).OnDelete(DeleteBehavior.SetNull);
        });

        // ClassPromotionRequest
        builder.Entity<ClassPromotionRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PromotionSessionLabel).HasMaxLength(64);
            e.Property(x => x.Notes).HasMaxLength(512);
            e.Property(x => x.Status).IsRequired().HasMaxLength(16);
            e.HasIndex(x => new { x.SchoolId, x.Status, x.RequestedAtUtc });
            e.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FromClass).WithMany().HasForeignKey(x => x.FromClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ToClass).WithMany().HasForeignKey(x => x.ToClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FromTerm).WithMany().HasForeignKey(x => x.FromTermId).OnDelete(DeleteBehavior.SetNull);
        });

        // ClassPromotionRequestItem
        builder.Entity<ClassPromotionRequestItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RequestId, x.StudentId }).IsUnique();
            e.HasIndex(x => new { x.SchoolId, x.StudentId });
            e.HasOne(x => x.Request).WithMany(x => x.Items).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        // ClassRoutine
        builder.Entity<ClassRoutine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.StartTime).IsRequired().HasMaxLength(5);
            e.Property(x => x.EndTime).IsRequired().HasMaxLength(5);
            e.Property(x => x.Room).HasMaxLength(64);
            e.HasIndex(x => new { x.SchoolId, x.ClassId, x.Weekday, x.StartTime, x.EndTime });
            e.HasIndex(x => new { x.SchoolId, x.TeacherId, x.Weekday, x.StartTime });
            e.HasOne(x => x.School).WithMany(s => s.ClassRoutines).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.SetNull);
        });

        // TeacherAssignment
        builder.Entity<TeacherAssignment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(160);
            e.Property(x => x.Description).HasMaxLength(2048);
            e.HasIndex(x => new { x.SchoolId, x.ClassId, x.TermId, x.SubjectId });
            e.HasOne(x => x.School).WithMany(s => s.TeacherAssignments).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FileAsset).WithMany().HasForeignKey(x => x.FileAssetId).OnDelete(DeleteBehavior.Restrict);
        });

        // SchoolNotice
        builder.Entity<SchoolNotice>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(180);
            e.Property(x => x.Body).IsRequired().HasMaxLength(8000);
            e.Property(x => x.TargetRolesCsv).IsRequired().HasMaxLength(128);
            e.HasIndex(x => new { x.SchoolId, x.IsActive, x.PublishedAtUtc });
            e.HasOne(x => x.School).WithMany(s => s.SchoolNotices).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
        });

        // SchoolEvent
        builder.Entity<SchoolEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(180);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.ColorHex).HasMaxLength(16);
            e.HasIndex(x => new { x.SchoolId, x.StartAtUtc });
            e.HasOne(x => x.School).WithMany(s => s.SchoolEvents).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
        });

        // GradingSystem
        builder.Entity<GradingSystem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(128);
            e.HasIndex(x => new { x.SchoolId, x.ClassId, x.TermId, x.Name });
            e.HasOne(x => x.School).WithMany(s => s.GradingSystems).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Term).WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.SetNull);
        });

        // GradeRule
        builder.Entity<GradeRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.GradeLetter).IsRequired().HasMaxLength(16);
            e.Property(x => x.Remarks).HasMaxLength(256);
            e.HasIndex(x => new { x.GradingSystemId, x.MinPercent, x.MaxPercent });
            e.HasOne(x => x.School).WithMany(s => s.GradeRules).HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.GradingSystem).WithMany(g => g.Rules).HasForeignKey(x => x.GradingSystemId).OnDelete(DeleteBehavior.Cascade);
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

        // Affiliate program
        builder.Entity<Affiliate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UniqueCode).IsRequired().HasMaxLength(32);
            e.Property(x => x.HeadshotPath).HasMaxLength(512);
            e.Property(x => x.PhoneNumber).HasMaxLength(64);
            e.Property(x => x.CountryCode).HasMaxLength(8);
            e.Property(x => x.BankName).HasMaxLength(128);
            e.Property(x => x.AccountNumber).HasMaxLength(64);
            e.Property(x => x.AccountName).HasMaxLength(128);
            e.Property(x => x.PaystackRecipientCode).HasMaxLength(128);
            e.Property(x => x.HeadshotContentType).HasMaxLength(128);
            e.HasIndex(x => x.UniqueCode).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AffiliateLeadRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(128);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.Property(x => x.PhoneNumber).HasMaxLength(64);
            e.Property(x => x.CountryCode).HasMaxLength(8);
            e.Property(x => x.Note).HasMaxLength(1024);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.HasIndex(x => x.Email);
        });

        builder.Entity<AffiliateInvite>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.Property(x => x.InviteToken).IsRequired().HasMaxLength(128);
            e.HasIndex(x => x.InviteToken).IsUnique();
            e.HasOne(x => x.AffiliateLeadRequest)
                .WithMany(x => x.Invites)
                .HasForeignKey(x => x.AffiliateLeadRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AffiliateTrainingVideo>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(256);
            e.Property(x => x.Topic).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(2048);
            e.Property(x => x.YoutubeUrl).IsRequired().HasMaxLength(1024);
            e.HasMany(x => x.Completions)
                .WithOne(x => x.TrainingVideo)
                .HasForeignKey(x => x.TrainingVideoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AffiliateTrainingCompletion>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AffiliateId, x.TrainingVideoId }).IsUnique();
            e.HasIndex(x => x.TrainingVideoId);
            e.HasOne(x => x.Affiliate)
                .WithMany(x => x.TrainingCompletions)
                .HasForeignKey(x => x.AffiliateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AffiliatePayout>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
            e.Property(x => x.PayoutType).IsRequired().HasMaxLength(32);
            e.Property(x => x.PaystackTransferReference).HasMaxLength(128);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.Property(x => x.FailureReason).HasMaxLength(1024);
            e.HasOne(x => x.Affiliate)
                .WithMany(x => x.Payouts)
                .HasForeignKey(x => x.AffiliateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AffiliateCommissionLedger>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CommissionType).IsRequired().HasMaxLength(32);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.HasOne(x => x.Affiliate)
                .WithMany(x => x.CommissionLedgers)
                .HasForeignKey(x => x.AffiliateId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.School)
                .WithMany()
                .HasForeignKey(x => x.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BillingRecord)
                .WithMany()
                .HasForeignKey(x => x.BillingRecordId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AffiliatePayout)
                .WithMany(x => x.CommissionLedgers)
                .HasForeignKey(x => x.AffiliatePayoutId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AffiliateNotification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(256);
            e.Property(x => x.Message).IsRequired().HasMaxLength(2048);
            e.Property(x => x.Type).IsRequired().HasMaxLength(32);
            e.HasOne(x => x.Affiliate)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.AffiliateId)
                .OnDelete(DeleteBehavior.Cascade);
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

        // PlatformComplianceSettings: singleton record (Id = 1)
        builder.Entity<PlatformComplianceSettings>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DataProtectionOfficerName).HasMaxLength(128);
            e.Property(x => x.DataProtectionOfficerEmail).HasMaxLength(256);
            e.Property(x => x.DpiaDocumentUrl).HasMaxLength(512);
        });
    }

    private void ApplySqliteGuidTextNormalization(ModelBuilder builder)
    {
        if (!Database.IsSqlite())
            return;

        var guidConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Guid, string>(
            value => value.ToString().ToUpperInvariant(),
            value => Guid.Parse(value));

        var nullableGuidConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Guid?, string?>(
            value => value.HasValue ? value.Value.ToString().ToUpperInvariant() : null,
            value => string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value));

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(Guid))
                {
                    property.SetValueConverter(guidConverter);
                    property.SetColumnType("TEXT");
                }
                else if (property.ClrType == typeof(Guid?))
                {
                    property.SetValueConverter(nullableGuidConverter);
                    property.SetColumnType("TEXT");
                }
            }
        }
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
                    comparableTenantId = Expression.Call(
                        currentTenantIdProperty,
                        typeof(Guid?).GetMethod(nameof(Nullable<Guid>.GetValueOrDefault), Type.EmptyTypes)!);
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
            var noFilter = Expression.OrElse(contextIsNull, currentTenantIdIsNull);
            var filterBody = Expression.OrElse(noFilter, tenantIdEquals);

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
