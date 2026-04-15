using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RiseFlow.Api.Controllers;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;
using Xunit;

namespace RiseFlow.Api.Tests;

public class PromotionTransitionRulesTests
{
    [Fact]
    public void TryNormalizeTransitionJson_ValidPayload_TrimsAndDeduplicates()
    {
        var method = GetPrivateStaticMethod(typeof(SchoolsController), "TryNormalizeTransitionJson");
        var args = new object?[]
        {
            "{\" Primary 1 \":[\"Primary 2\",\" Primary 2 \",\"Primary 3\"]}",
            null,
            null
        };

        var isValid = (bool)(method.Invoke(null, args) ?? false);
        var normalizedJson = args[1] as string;
        var error = args[2] as string;

        Assert.True(isValid);
        Assert.Null(error);
        Assert.NotNull(normalizedJson);

        var normalized = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(normalizedJson!);
        Assert.NotNull(normalized);
        Assert.True(normalized!.ContainsKey("Primary 1"));
        Assert.Equal(new[] { "Primary 2", "Primary 3" }, normalized["Primary 1"]);
    }

    [Fact]
    public void TryNormalizeTransitionJson_InvalidJson_ReturnsFriendlyError()
    {
        var method = GetPrivateStaticMethod(typeof(SchoolsController), "TryNormalizeTransitionJson");
        var args = new object?[] { "{not-json}", null, null };

        var isValid = (bool)(method.Invoke(null, args) ?? true);
        var error = args[2] as string;

        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("valid JSON", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BulkPromote_StrictValidation_UsesSchoolOverrideBeforeProfile()
    {
        var schoolId = Guid.NewGuid();
        var (controller, db, fromClassId, toClassId, studentId) = BuildControllerWithFixture(
            schoolId,
            strictValidationEnabled: true,
            schoolOverrideJson: "{\"Primary 1\":[\"SS1\"]}");

        var response = await controller.BulkPromote(
            new BulkPromoteStudentsRequest(fromClassId, toClassId, null, "2025/2026", new List<Guid> { studentId }, null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        var message = ExtractMessage(badRequest.Value);

        Assert.Contains("can only promote to [SS1]", message, StringComparison.OrdinalIgnoreCase);

        var promotions = await db.StudentPromotions.CountAsync();
        Assert.Equal(0, promotions);
    }

    [Fact]
    public async Task BulkPromote_StrictValidation_FallsBackToProfileWhenNoOverride()
    {
        var schoolId = Guid.NewGuid();
        var (controller, db, fromClassId, toClassId, studentId) = BuildControllerWithFixture(
            schoolId,
            strictValidationEnabled: true,
            schoolOverrideJson: null);

        var response = await controller.BulkPromote(
            new BulkPromoteStudentsRequest(fromClassId, toClassId, null, "2025/2026", new List<Guid> { studentId }, "Promote"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var promotedCount = ExtractInt(ok.Value, "promotedCount");
        Assert.Equal(1, promotedCount);

        var student = await db.Students.SingleAsync(x => x.Id == studentId);
        Assert.Equal(toClassId, student.ClassId);
    }

    private static (PromotionsController Controller, RiseFlowDbContext Db, Guid FromClassId, Guid ToClassId, Guid StudentId) BuildControllerWithFixture(
        Guid schoolId,
        bool strictValidationEnabled,
        string? schoolOverrideJson)
    {
        var options = new DbContextOptionsBuilder<RiseFlowDbContext>()
            .UseInMemoryDatabase($"promotions-{Guid.NewGuid()}")
            .Options;

        var db = new RiseFlowDbContext(options);

        var profileId = Guid.NewGuid();
        var grade1Id = Guid.NewGuid();
        var grade2Id = Guid.NewGuid();
        var classAId = Guid.NewGuid();
        var classBId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        db.AcademicSystemProfiles.Add(new AcademicSystemProfile
        {
            Id = profileId,
            Code = "NG_6334",
            Name = "Nigeria 6-3-3-4",
            GradeTemplatesJson = "[]",
            PromotionTransitionJson = "{\"Primary 1\":[\"Primary 2\"]}",
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        });

        db.Schools.Add(new School
        {
            Id = schoolId,
            Name = "Test School",
            AcademicSystemProfileId = profileId,
            PromotionTransitionOverrideJson = schoolOverrideJson,
            CreatedAtUtc = DateTime.UtcNow
        });

        db.Grades.AddRange(
            new Grade { Id = grade1Id, SchoolId = schoolId, Name = "Primary 1", LevelOrder = 10, CreatedAtUtc = DateTime.UtcNow },
            new Grade { Id = grade2Id, SchoolId = schoolId, Name = "Primary 2", LevelOrder = 11, CreatedAtUtc = DateTime.UtcNow });

        db.Classes.AddRange(
            new Class { Id = classAId, SchoolId = schoolId, GradeId = grade1Id, Name = "Primary 1A", CreatedAtUtc = DateTime.UtcNow },
            new Class { Id = classBId, SchoolId = schoolId, GradeId = grade2Id, Name = "Primary 2A", CreatedAtUtc = DateTime.UtcNow });

        db.Students.Add(new Student
        {
            Id = studentId,
            SchoolId = schoolId,
            FirstName = "Ada",
            LastName = "Okafor",
            ClassId = classAId,
            GradeId = grade1Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        db.SaveChanges();

        var tenant = new TestTenantContext { CurrentSchoolId = schoolId };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:EnableStrictAcademicPromotionValidation"] = strictValidationEnabled ? "true" : "false"
            })
            .Build();

        var controller = new PromotionsController(db, tenant, configuration);
        return (controller, db, classAId, classBId, studentId);
    }

    private static MethodInfo GetPrivateStaticMethod(Type type, string methodName)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!;
    }

    private static string ExtractMessage(object? payload)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        return doc.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    private static int ExtractInt(object? payload, string propertyName)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        return doc.RootElement.GetProperty(propertyName).GetInt32();
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? CurrentSchoolId { get; set; }
        public bool IsSuperAdmin => false;
        public string? CurrentUserEmail => null;
    }
}
