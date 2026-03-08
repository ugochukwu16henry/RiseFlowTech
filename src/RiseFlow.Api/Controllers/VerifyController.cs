using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("verify")]
[AllowAnonymous]
public class VerifyController : ControllerBase
{
    private readonly RiseFlowDbContext _db;

    public VerifyController(RiseFlowDbContext db)
    {
        _db = db;
    }

    /// <summary>Public verification for transcript QR code. Returns JSON with validity and details.</summary>
    [HttpGet("transcript/{token}")]
    [ProducesResponseType(typeof(TranscriptVerificationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TranscriptVerificationResult>> Transcript(string token, CancellationToken ct)
    {
        var verification = await _db.TranscriptVerifications
            .AsNoTracking()
            .Include(v => v.Student)
            .Include(v => v.School)
            .FirstOrDefaultAsync(v => v.VerificationToken == token, ct);
        if (verification == null)
            return NotFound();
        return Ok(new TranscriptVerificationResult(
            Valid: true,
            StudentName: $"{verification.Student.FirstName} {verification.Student.LastName}",
            SchoolName: verification.School.Name,
            IssuedAtUtc: verification.IssuedAtUtc,
            IssuedToName: verification.IssuedToName));
    }
}

public record TranscriptVerificationResult(bool Valid, string StudentName, string SchoolName, DateTime IssuedAtUtc, string? IssuedToName);
