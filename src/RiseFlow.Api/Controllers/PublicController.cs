using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public PublicController(PitchDeckPdfService pitchDeckPdf, TeacherQuickStartPdfService teacherGuidePdf, GradingReferencePdfService gradingReferencePdf)
    {
        _pitchDeckPdf = pitchDeckPdf;
        _teacherGuidePdf = teacherGuidePdf;
        _gradingReferencePdf = gradingReferencePdf;
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
}
