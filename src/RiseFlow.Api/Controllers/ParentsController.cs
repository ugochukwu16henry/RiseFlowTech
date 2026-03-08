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
public class ParentsController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;

    public ParentsController(RiseFlowDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// Link the current parent to a student using the student's Parent Access Code.
    /// When a parent enters the code on the web/app, they are instantly linked to their child's profile.
    /// </summary>
    [HttpPost("link-by-code")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(typeof(LinkByCodeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LinkByCodeResult>> LinkByCode([FromBody] LinkByCodeRequest request, CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue || string.IsNullOrWhiteSpace(request?.Code))
            return BadRequest("Code is required.");
        var schoolId = _tenant.CurrentSchoolId.Value;
        var email = _tenant.CurrentUserEmail;
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var parent = await _db.Parents.FirstOrDefaultAsync(p => p.SchoolId == schoolId && p.Email == email, ct);
        if (parent == null)
            return NotFound("Parent profile not found for this school.");

        var code = request.Code.Trim().ToUpperInvariant();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.SchoolId == schoolId && s.ParentAccessCode == code, ct);
        if (student == null)
            return NotFound("Invalid or expired access code.");

        var alreadyLinked = await _db.StudentParents.AnyAsync(sp => sp.StudentId == student.Id && sp.ParentId == parent.Id, ct);
        if (alreadyLinked)
            return Ok(new LinkByCodeResult(true, student.Id, $"{student.FirstName} {student.LastName}", "Already linked."));

        _db.StudentParents.Add(new StudentParent
        {
            StudentId = student.Id,
            ParentId = parent.Id,
            IsPrimaryContact = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return Ok(new LinkByCodeResult(true, student.Id, $"{student.FirstName} {student.LastName}", "Linked successfully."));
    }
}

public record LinkByCodeRequest(string Code);
public record LinkByCodeResult(bool Success, Guid StudentId, string StudentName, string Message);
