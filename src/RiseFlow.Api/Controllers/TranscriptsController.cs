using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TranscriptsController : ControllerBase
{
    private readonly TranscriptPdfService _transcriptPdf;
    private readonly Services.ITenantContext _tenant;
    private readonly IConfiguration _config;

    public TranscriptsController(TranscriptPdfService transcriptPdf, Services.ITenantContext tenant, IConfiguration config)
    {
        _transcriptPdf = transcriptPdf;
        _tenant = tenant;
        _config = config;
    }

    /// <summary>Generate a PDF transcript with verification QR code. SchoolAdmin or Teacher.</summary>
    [HttpGet("generate")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Teacher}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generate(
        [FromQuery] Guid studentId,
        [FromQuery] string? termIds,
        [FromQuery] string? issuedToName,
        CancellationToken ct)
    {
        if (!_tenant.CurrentSchoolId.HasValue)
            return Forbid();
        var schoolId = _tenant.CurrentSchoolId.Value;
        IEnumerable<Guid>? termIdList = null;
        if (!string.IsNullOrWhiteSpace(termIds))
        {
            var parts = termIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var list = new List<Guid>();
            foreach (var p in parts)
                if (Guid.TryParse(p.Trim(), out var id))
                    list.Add(id);
            if (list.Count > 0) termIdList = list;
        }
        // QR code in PDF should point to the web app verification page so scanners see a friendly result.
        var baseUrl = _config["RiseFlow:VerificationBaseUrl"]?.Trim();
        if (string.IsNullOrEmpty(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}";
        try
        {
            var (_, pdfBytes) = await _transcriptPdf.GenerateTranscriptAsync(studentId, schoolId, termIdList, issuedToName, baseUrl, ct);
            var fileName = $"Transcript_{studentId:N}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Student not found.")
        {
            return NotFound();
        }
    }
}
