namespace RiseFlow.Web.Models;

// ─── Shared API DTOs ──────────────────────────────────────────────────────────

public record SchoolDashboard(int ActiveStudentCount, decimal UnpaidFeesTotal, string? CurrencyCode);
public record SuperAdminDashboard(int TotalSchools, int TotalStudents, decimal TotalRevenue, int? ActiveCountries);
public record TeacherInfo(Guid Id, string? FirstName, string? MiddleName, string? LastName, string? Email, string? Phone);
public record StudentInfo(Guid Id, string? FirstName, string? MiddleName, string? LastName, string? AdmissionNumber, ClassInfo? Class);
public record ClassInfo(Guid Id, string? Name);
public record BillingRecord(Guid Id, string? PeriodLabel, decimal AmountDue, decimal? AmountPaid, string? CurrencyCode);
public record ChildLink(Guid StudentId, string? FirstName, string? MiddleName, string? LastName, string? ClassName, double? TermAverage);
public record ResultRecord(Guid StudentId, string? SubjectName, double Score, double MaxScore, string? AssessmentType, string? Term);
public record ContactTeacher(Guid Id, string? FirstName, string? LastName, string? Email, string? WhatsAppNumber, string? Phone, string? SubjectName);
public record AssessmentInfo(Guid Id, string? Name, string? SubjectName, string? ClassName, string? Term);

/// <summary>Grade row used in the Teacher grading QuickGrid.</summary>
public class StudentGradeVm
{
    public Guid StudentId { get; init; }
    public string FullName { get; set; } = string.Empty;
    public string AdmissionNumber { get; set; } = string.Empty;
    public int CAScore { get; set; }
    public int ExamScore { get; set; }
    public int Total => CAScore + ExamScore;
}
