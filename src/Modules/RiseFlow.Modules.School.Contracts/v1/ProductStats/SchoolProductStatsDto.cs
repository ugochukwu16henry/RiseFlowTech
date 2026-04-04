namespace RiseFlow.Modules.School.Contracts.v1.ProductStats;

/// <summary>Read-only aggregates from the legacy RiseFlow product database (RiseFlowDbContext).</summary>
public sealed record SchoolProductStatsDto(
    int ActiveSchoolCount,
    int ActiveStudentCount,
    int ActiveTeacherCount,
    DateTimeOffset GeneratedAtUtc);
