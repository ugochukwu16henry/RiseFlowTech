using System.Net;
using Microsoft.AspNetCore.Mvc;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/paystack/webhook")]
public class PaystackWebhookController : ControllerBase
{
    private readonly PaymentService _payments;
    private readonly IConfiguration _config;
    private readonly ILogger<PaystackWebhookController> _logger;

    public PaystackWebhookController(PaymentService payments, IConfiguration config, ILogger<PaystackWebhookController> logger)
    {
        _payments = payments;
        _config = config;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        // Optional IP allow-list in addition to HMAC signature validation.
        // Configure Paystack:WebhookTrustedIPs as a comma-separated list of IPs or CIDR ranges (e.g. "52.31.139.75,52.49.173.169/32").
        if (!IsRequestFromTrustedIp(HttpContext.Connection.RemoteIpAddress))
        {
            _logger.LogWarning("Rejected Paystack webhook from untrusted IP {Ip}", HttpContext.Connection.RemoteIpAddress);
            return Forbid();
        }

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

    private bool IsRequestFromTrustedIp(IPAddress? remoteIp)
    {
        if (remoteIp == null)
            return false;

        var configValue = _config["Paystack:WebhookTrustedIPs"];
        if (string.IsNullOrWhiteSpace(configValue))
        {
            // No allow-list configured: accept all (suitable for development; tighten in production).
            return true;
        }

        var entries = configValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            if (TryMatchIpOrCidr(remoteIp, entry))
                return true;
        }

        return false;
    }

    private static bool TryMatchIpOrCidr(IPAddress remoteIp, string pattern)
    {
        if (IPAddress.TryParse(pattern, out var singleIp))
        {
            return remoteIp.Equals(singleIp);
        }

        var slashIndex = pattern.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex <= 0 || !IPAddress.TryParse(pattern[..slashIndex], out var networkIp))
            return false;

        if (!int.TryParse(pattern[(slashIndex + 1)..], out var prefixLength))
            return false;

        if (networkIp.AddressFamily != remoteIp.AddressFamily)
            return false;

        if (networkIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var mask = PrefixLengthToMask(prefixLength);
            var addrBytes = remoteIp.GetAddressBytes();
            var networkBytes = networkIp.GetAddressBytes();
            for (var i = 0; i < addrBytes.Length; i++)
            {
                if ((addrBytes[i] & mask[i]) != (networkBytes[i] & mask[i]))
                    return false;
            }
            return true;
        }

        // IPv6: for simplicity, rely on exact IPs; CIDR handling can be extended if needed.
        return false;
    }

    private static byte[] PrefixLengthToMask(int prefixLength)
    {
        var mask = new byte[4];
        for (var i = 0; i < mask.Length; i++)
        {
            var bits = Math.Clamp(prefixLength - i * 8, 0, 8);
            mask[i] = bits == 0 ? (byte)0 : (byte)~(0xFF >> bits);
        }
        return mask;
    }
}

