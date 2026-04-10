namespace RiseFlow.Api.Constants;

/// <summary>
/// RBAC roles for RiseFlow. SuperAdmin (platform), SchoolAdmin, Teacher, Parent, Student.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string SchoolAdmin = "SchoolAdmin";
    public const string Teacher = "Teacher";
    public const string Parent = "Parent";
    public const string Student = "Student";

    public static readonly IReadOnlyList<string> All = new[] { SuperAdmin, SchoolAdmin, Teacher, Parent, Student };
}
