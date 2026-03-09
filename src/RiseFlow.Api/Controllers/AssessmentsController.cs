using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssessmentsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public AssessmentsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>List assessment categories and items for the current school (primary/competency-based). SchoolAdmin/Teacher.</summary>
    [HttpGet("definitions")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    [ProducesResponseType(typeof(List<AssessmentCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AssessmentCategoryDto>>> GetDefinitions(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        var categories = await _db.AssessmentCategories
            .AsNoTracking()
            .Include(c => c.Items)
            .Where(c => c.SchoolId == schoolId && c.IsActive)
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
        var list = categories.Select(c => new AssessmentCategoryDto(
            c.Id,
            c.Name,
            c.Order,
            c.Items.Where(i => i.IsActive).OrderBy(i => i.Order).ThenBy(i => i.Label)
                .Select(i => new AssessmentItemDto(i.Id, i.Label, i.Order))
                .ToList()
        )).ToList();
        return Ok(list);
    }

    /// <summary>Create or update categories/items. SchoolAdmin only.</summary>
    [HttpPost("definitions")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UpsertDefinitions([FromBody] UpsertDefinitionsRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        foreach (var cat in request.Categories)
        {
            AssessmentCategory entity;
            if (cat.Id.HasValue)
            {
                entity = await _db.AssessmentCategories.FirstOrDefaultAsync(c => c.Id == cat.Id.Value && c.SchoolId == schoolId, ct)
                         ?? new AssessmentCategory { Id = cat.Id.Value, SchoolId = schoolId, CreatedAtUtc = DateTime.UtcNow };
            }
            else
            {
                entity = new AssessmentCategory { Id = Guid.NewGuid(), SchoolId = schoolId, CreatedAtUtc = DateTime.UtcNow };
                _db.AssessmentCategories.Add(entity);
            }
            entity.Name = cat.Name.Trim();
            entity.Order = cat.Order;
            entity.IsActive = true;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            foreach (var item in cat.Items)
            {
                AssessmentItem itemEntity;
                if (item.Id.HasValue)
                {
                    itemEntity = await _db.AssessmentItems.FirstOrDefaultAsync(i => i.Id == item.Id.Value && i.CategoryId == entity.Id, ct)
                                 ?? new AssessmentItem { Id = item.Id.Value, CategoryId = entity.Id, CreatedAtUtc = DateTime.UtcNow };
                }
                else
                {
                    itemEntity = new AssessmentItem { Id = Guid.NewGuid(), CategoryId = entity.Id, CreatedAtUtc = DateTime.UtcNow };
                    _db.AssessmentItems.Add(itemEntity);
                }
                itemEntity.Label = item.Label.Trim();
                itemEntity.Order = item.Order;
                itemEntity.IsActive = true;
                itemEntity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Get competency/behaviour assessments for a student in a term.</summary>
    [HttpGet("student")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher},{Roles.Parent}")]
    [ProducesResponseType(typeof(List<StudentAssessmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StudentAssessmentDto>>> GetStudentAssessments([FromQuery] Guid studentId, [FromQuery] Guid termId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        // Parents can only see their own children
        if (User.IsInRole(Roles.Parent))
        {
            var allowed = await _db.StudentParents
                .Where(sp => sp.Parent.SchoolId == schoolId && sp.Parent.Email == _tenant.CurrentUserEmail)
                .Select(sp => sp.StudentId)
                .ToListAsync(ct);
            if (!allowed.Contains(studentId))
                return Forbid();
        }

        var rows = await _db.StudentAssessments
            .AsNoTracking()
            .Include(sa => sa.Item).ThenInclude(i => i.Category)
            .Where(sa => sa.SchoolId == schoolId && sa.StudentId == studentId && sa.TermId == termId)
            .ToListAsync(ct);

        var list = rows.Select(sa => new StudentAssessmentDto(
            sa.Id,
            sa.AssessmentItemId,
            sa.Item.Label,
            sa.Item.Category.Name,
            sa.Value,
            sa.Comment
        )).ToList();
        return Ok(list);
    }

    /// <summary>Upsert student assessments (primary/competency grid). Teachers/SchoolAdmin only.</summary>
    [HttpPost("student")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UpsertStudentAssessments([FromBody] UpsertStudentAssessmentsRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;

        foreach (var row in request.Rows)
        {
            var entity = await _db.StudentAssessments.FirstOrDefaultAsync(sa =>
                sa.SchoolId == schoolId &&
                sa.StudentId == row.StudentId &&
                sa.TermId == row.TermId &&
                sa.AssessmentItemId == row.AssessmentItemId, ct);
            if (entity == null)
            {
                if (string.IsNullOrWhiteSpace(row.Value) && string.IsNullOrWhiteSpace(row.Comment))
                    continue; // nothing to save
                entity = new StudentAssessment
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    StudentId = row.StudentId,
                    TermId = row.TermId,
                    AssessmentItemId = row.AssessmentItemId,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.StudentAssessments.Add(entity);
            }
            entity.Value = string.IsNullOrWhiteSpace(row.Value) ? null : row.Value.Trim();
            entity.Comment = string.IsNullOrWhiteSpace(row.Comment) ? null : row.Comment.Trim();
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record AssessmentCategoryDto(Guid Id, string Name, int Order, IReadOnlyList<AssessmentItemDto> Items);
public record AssessmentItemDto(Guid Id, string Label, int Order);

public record UpsertDefinitionsRequest(IReadOnlyList<UpsertCategoryDto> Categories);
public record UpsertCategoryDto(Guid? Id, string Name, int Order, IReadOnlyList<UpsertItemDto> Items);
public record UpsertItemDto(Guid? Id, string Label, int Order);

public record StudentAssessmentDto(Guid Id, Guid AssessmentItemId, string ItemLabel, string CategoryName, string? Value, string? Comment);

public record UpsertStudentAssessmentsRequest(IReadOnlyList<UpsertStudentAssessmentRow> Rows);
public record UpsertStudentAssessmentRow(Guid StudentId, Guid TermId, Guid AssessmentItemId, string? Value, string? Comment);

