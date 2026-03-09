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

    public PublicController(PitchDeckPdfService pitchDeckPdf)
    {
        _pitchDeckPdf = pitchDeckPdf;
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
}
