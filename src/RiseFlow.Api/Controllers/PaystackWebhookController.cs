using Microsoft.AspNetCore.Mvc;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/paystack/webhook")]
public class PaystackWebhookController : ControllerBase
{
    private readonly PaymentService _payments;

    public PaystackWebhookController(PaymentService payments)
    {
        _payments = payments;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Paystack-Signature"].FirstOrDefault();

        try
        {
            await _payments.HandlePaystackWebhookAsync(rawBody, signature, ct);
        }
        catch
        {
            // Swallow errors to avoid repeated retries; logs can be added here.
        }

        return Ok("ok");
    }
}

