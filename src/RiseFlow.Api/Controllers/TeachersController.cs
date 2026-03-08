using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeachersController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly Services.ITenantContext _tenant;

    public TeachersController(RiseFlowDbContext db, Services.ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Teacher>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Teacher>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var list = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses)
            .ThenInclude(tc => tc.Class)
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Teacher), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Teacher>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var teacher = await _db.Teachers
            .AsNoTracking()
            .Include(t => t.TeacherClasses)
            .ThenInclude(tc => tc.Class)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null)
            return NotFound();
        return Ok(teacher);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Teacher), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Teacher>> Create([FromBody] CreateTeacherRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            Email = request.Email,
            Phone = request.Phone,
            WhatsAppNumber = request.WhatsAppNumber,
            StaffId = request.StaffId,
            SubjectSpecialization = request.SubjectSpecialization,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Teachers.Add(teacher);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = teacher.Id }, teacher);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Teacher), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Teacher>> Update(Guid id, [FromBody] UpdateTeacherRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null)
            return NotFound();
        teacher.FirstName = request.FirstName;
        teacher.LastName = request.LastName;
        teacher.MiddleName = request.MiddleName;
        teacher.Email = request.Email;
        teacher.Phone = request.Phone;
        teacher.WhatsAppNumber = request.WhatsAppNumber;
        teacher.StaffId = request.StaffId;
        teacher.SubjectSpecialization = request.SubjectSpecialization;
        teacher.IsActive = request.IsActive;
        teacher.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(teacher);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (teacher == null)
            return NotFound();
        _db.Teachers.Remove(teacher);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{teacherId:guid}/classes/{classId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AssignToClass(Guid teacherId, Guid classId, [FromBody] AssignTeacherToClassRequest? request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var exists = await _db.TeacherClasses.AnyAsync(tc => tc.TeacherId == teacherId && tc.ClassId == classId, ct);
        if (exists)
            return NoContent();
        var link = new TeacherClass
        {
            TeacherId = teacherId,
            ClassId = classId,
            RoleInClass = request?.RoleInClass,
            AssignedAtUtc = DateTime.UtcNow
        };
        _db.TeacherClasses.Add(link);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{teacherId:guid}/classes/{classId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UnassignFromClass(Guid teacherId, Guid classId, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var link = await _db.TeacherClasses.FirstOrDefaultAsync(tc => tc.TeacherId == teacherId && tc.ClassId == classId, ct);
        if (link != null)
        {
            _db.TeacherClasses.Remove(link);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }
}
