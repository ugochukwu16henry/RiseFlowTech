namespace RiseFlow.Api.Entities;

public class AcademicSystemProfile
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? SuggestedTermsPerYear { get; set; }
    public string GradeTemplatesJson { get; set; } = "[]";
    public string? StageOrderJson { get; set; }
    public string? PromotionTransitionJson { get; set; }
    public string? DefaultGradingScaleCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<School> Schools { get; set; } = new List<School>();
}
