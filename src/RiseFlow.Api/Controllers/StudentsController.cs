using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public StudentsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Student>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Student>>> List(CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var list = await _db.Students
            .AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.Grade)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Student), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Student>> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.Grade)
            .Include(s => s.StudentParents)
            .ThenInclude(sp => sp.Parent)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
            return NotFound();
        return Ok(student);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Student), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Student>> Create([FromBody] CreateStudentRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = new Student
        {
            Id = Guid.NewGuid(),
            SchoolId = _tenant.CurrentSchoolId.Value,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            AdmissionNumber = request.AdmissionNumber,
            ClassId = request.ClassId,
            GradeId = request.GradeId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Students.Add(student);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = student.Id }, student);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Student), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Student>> Update(Guid id, [FromBody] UpdateStudentRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
            return NotFound();
        student.FirstName = request.FirstName;
        student.LastName = request.LastName;
        student.MiddleName = request.MiddleName;
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender;
        student.AdmissionNumber = request.AdmissionNumber;
        student.ClassId = request.ClassId;
        student.GradeId = request.GradeId;
        student.IsActive = request.IsActive;
        student.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(student);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (student == null)
            return NotFound();
        _db.Students.Remove(student);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
