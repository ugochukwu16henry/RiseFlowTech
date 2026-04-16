namespace RiseFlow.Api.Entities;

/// <summary>
/// A single result/grade entry for a student in a subject for a term (teacher uploads, parents view).
/// </summary>
public class StudentResult : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TermId { get; set; }
    public Guid? ExamId { get; set; }
    public string AssessmentType { get; set; } = "Exam"; // e.g. Exam, Quiz, Assignment, MidTerm
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public string? GradeLetter { get; set; }
    public string? Comment { get; set; }
    public Guid? EnteredByTeacherId { get; set; }
    public ResultWorkflowStatus WorkflowStatus { get; set; } = ResultWorkflowStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedByTeacherId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? FinalApprovedAtUtc { get; set; }
    public Guid? FinalApprovedByUserId { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public School School { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public AcademicTerm Term { get; set; } = null!;
    public Exam? Exam { get; set; }
    public Teacher? EnteredByTeacher { get; set; }
}
