using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly PitchDeckPdfService _pitchDeckPdf;
    private readonly TeacherQuickStartPdfService _teacherGuidePdf;
    private readonly GradingReferencePdfService _gradingReferencePdf;
    private readonly RiseFlowDbContext _db;

    public PublicController(
        PitchDeckPdfService pitchDeckPdf,
        TeacherQuickStartPdfService teacherGuidePdf,
        GradingReferencePdfService gradingReferencePdf,
        RiseFlowDbContext db)
    {
        _pitchDeckPdf = pitchDeckPdf;
        _teacherGuidePdf = teacherGuidePdf;
        _gradingReferencePdf = gradingReferencePdf;
        _db = db;
    }

    /// <summary>Download the RiseFlow "Future-Ready" School Pitch Deck as a PDF.</summary>
    [HttpGet("pitch-deck")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public IActionResult GetPitchDeckPdf()
    {
        var bytes = _pitchDeckPdf.GeneratePdf();
        const string fileName = "RiseFlow-Pitch-Deck.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    /// <summary>Download the RiseFlow Teacher's Quick Start Guide as a PDF.</summary>
    [HttpGet("teacher-quick-start")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public IActionResult GetTeacherQuickStartPdf()
    {
        var bytes = _teacherGuidePdf.GeneratePdf();
        const string fileName = "RiseFlow-Teacher-Quick-Start-Guide.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    /// <summary>Download the Standard Nigerian Grading Reference and Support Promise as a PDF.</summary>
    [HttpGet("grading-reference")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public IActionResult GetGradingReferencePdf()
    {
        var bytes = _gradingReferencePdf.GeneratePdf();
        const string fileName = "RiseFlow-Grading-Reference.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    /// <summary>Capture work email for the public "digitalizing your school" guide; Super Admin can list these leads.</summary>
    [HttpPost("marketing-leads")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitMarketingLead([FromBody] SubmitMarketingLeadRequest? request, CancellationToken ct)
    {
        var raw = request?.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(raw) || raw.Length > 256)
            return BadRequest("A valid work email is required.");

        var emailAttr = new EmailAddressAttribute();
        if (!emailAttr.IsValid(raw))
            return BadRequest("Please enter a valid email address.");

        var normalized = raw.ToLowerInvariant();
        const string source = "homepage_digital_guide";

        _db.MarketingLeads.Add(new MarketingLead
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            Source = source,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
