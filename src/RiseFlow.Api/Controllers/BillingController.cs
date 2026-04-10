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
public class BillingController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly BillingService _billing;
    private readonly ITenantContext _tenant;
    private readonly PaymentService _payments;
    private readonly BillingReceiptPdfService _receipts;

    public BillingController(RiseFlowDbContext db, BillingService billing, ITenantContext tenant, PaymentService payments, BillingReceiptPdfService receipts)
    {
        _db = db;
        _billing = billing;
        _tenant = tenant;
        _payments = payments;
        _receipts = receipts;
    }

    /// <summary>SuperAdmin: generate billing for a period for all schools or one school.</summary>
    [HttpPost("generate")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(List<BillingRecord>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BillingRecord>>> GenerateBilling([FromBody] GenerateBillingRequest request, CancellationToken ct)
    {
        var periodStart = request.PeriodStart;
        var periodEnd = request.PeriodEnd;
        var periodLabel = request.PeriodLabel ?? $"{periodStart:yyyy-MM}";
        var schoolIds = request.SchoolId.HasValue
            ? new List<Guid> { request.SchoolId.Value }
            : await _db.Schools.Where(s => s.IsActive).Select(s => s.Id).ToListAsync(ct);
        var records = new List<BillingRecord>();
        foreach (var schoolId in schoolIds)
        {
            var existing = await _db.BillingRecords.AnyAsync(b => b.SchoolId == schoolId && b.PeriodLabel == periodLabel, ct);
            if (existing) continue;
            var record = await _billing.CreateBillingRecordAsync(schoolId, periodLabel, periodStart, periodEnd, ct);
            records.Add(record);
        }
        return Ok(records);
    }

    /// <summary>SuperAdmin: list all billing records. SchoolAdmin: list current school only.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<BillingRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BillingRecordDto>>> List([FromQuery] Guid? schoolId, CancellationToken ct)
    {
        IQueryable<BillingRecord> query = _db.BillingRecords.Include(b => b.School);
        if (User.IsInRole(Roles.SuperAdmin))
        {
            if (schoolId.HasValue) query = query.Where(b => b.SchoolId == schoolId.Value);
        }
        else if (_tenant.CurrentSchoolId.HasValue)
            query = query.Where(b => b.SchoolId == _tenant.CurrentSchoolId.Value);
        else
            return Forbid();
        var list = await query.OrderByDescending(b => b.PeriodStart).Select(b => new BillingRecordDto(
            b.Id, b.SchoolId, b.School.Name, b.PeriodLabel, b.PeriodStart, b.PeriodEnd,
            b.StudentCount, b.AmountDue, b.AmountPaid, b.CurrencyCode, b.PaidAtUtc)).ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>SuperAdmin: record payment for a billing record.</summary>
    [HttpPatch("{id:guid}/pay")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(BillingRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BillingRecord>> RecordPayment(Guid id, [FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        var record = await _db.BillingRecords.Include(b => b.School).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (record == null) return NotFound();
        record.AmountPaid = request.AmountPaid;
        record.PaidAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(record);
    }

    /// <summary>Convert an amount to USD and other African currencies using configured exchange rates.</summary>
    [HttpPost("convert")]
    [ProducesResponseType(typeof(ConvertedAmountsDto), StatusCodes.Status200OK)]
    public ActionResult<ConvertedAmountsDto> Convert([FromBody] ConvertAmountRequest request)
    {
        var amounts = _billing.ConvertToOtherCurrencies(request.Amount, request.FromCurrencyCode ?? "NGN");
        var usd = amounts.TryGetValue("USD", out var u) ? u : _billing.ConvertToUsd(request.Amount, request.FromCurrencyCode ?? "NGN");
        return Ok(new ConvertedAmountsDto(request.Amount, request.FromCurrencyCode ?? "NGN", usd, amounts));
    }

    /// <summary>Trigger payment gateway (Paystack) when AmountDue &gt; 0. Returns authorization URL and reference for the school to pay.</summary>
    [HttpPost("initiate-payment")]
    [ProducesResponseType(typeof(InitiatePaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InitiatePaymentResult>> InitiatePayment([FromBody] InitiatePaymentRequest request, CancellationToken ct)
    {
        var record = await _db.BillingRecords.Include(b => b.School).FirstOrDefaultAsync(b => b.Id == request.BillingRecordId, ct);
        if (record == null) return NotFound();
        if (_tenant.CurrentSchoolId.HasValue && record.SchoolId != _tenant.CurrentSchoolId.Value && !User.IsInRole(Roles.SuperAdmin))
            return Forbid();
        if (record.AmountDue <= 0)
            return BadRequest("No amount due for this billing record.");
        var (authorizationUrl, reference) = await _payments.InitializePaystackPaymentAsync(record.Id, ct);
        return Ok(new InitiatePaymentResult("Paystack", authorizationUrl, reference, record.AmountDue, record.CurrencyCode));
    }

    /// <summary>
    /// Download PDF receipt for a paid billing record.
    /// SchoolAdmin: only for their own school; SuperAdmin: any school.
    /// </summary>
    [HttpGet("{id:guid}/receipt")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken ct)
    {
        var record = await _db.BillingRecords.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (record == null)
            return NotFound();

        if (_tenant.CurrentSchoolId.HasValue && record.SchoolId != _tenant.CurrentSchoolId.Value && !User.IsInRole(Roles.SuperAdmin))
            return Forbid();

        byte[] pdf;
        try
        {
            pdf = await _receipts.GenerateReceiptAsync(id, ct);
        }
        catch (InvalidOperationException)
        {
            return BadRequest("Receipt is only available for paid billing records.");
        }

        var fileName = $"RiseFlow-Receipt-{id:N}.pdf";
        return File(pdf, "application/pdf", fileName);
    }
}

public record InitiatePaymentRequest(Guid BillingRecordId);
public record InitiatePaymentResult(string GatewayName, string AuthorizationUrl, string Reference, decimal AmountDue, string CurrencyCode);

public record ConvertAmountRequest(decimal Amount, string? FromCurrencyCode);
public record ConvertedAmountsDto(decimal OriginalAmount, string OriginalCurrency, decimal UsdAmount, IReadOnlyDictionary<string, decimal> AmountsByCurrency);

public record GenerateBillingRequest(DateOnly PeriodStart, DateOnly PeriodEnd, string? PeriodLabel, Guid? SchoolId);
public record RecordPaymentRequest(decimal AmountPaid);
public record BillingRecordDto(Guid Id, Guid SchoolId, string SchoolName, string PeriodLabel, DateOnly PeriodStart, DateOnly PeriodEnd, int StudentCount, decimal AmountDue, decimal? AmountPaid, string CurrencyCode, DateTime? PaidAtUtc);
