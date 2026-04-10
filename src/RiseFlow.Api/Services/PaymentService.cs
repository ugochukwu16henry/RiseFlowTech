using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

/// <summary>
/// Payment integration for schools. Uses Paystack to initialize transactions and verify webhook events.
/// </summary>
public class PaymentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly RiseFlowDbContext _db;
    private readonly AffiliateService _affiliateService;

    public PaymentService(IHttpClientFactory httpClientFactory, IConfiguration config, RiseFlowDbContext db, AffiliateService affiliateService)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _db = db;
        _affiliateService = affiliateService;
    }

    /// <summary>
    /// Initialize a Paystack payment for a billing record. Returns authorization URL and reference.
    /// </summary>
    public async Task<(string AuthorizationUrl, string Reference)> InitializePaystackPaymentAsync(Guid billingRecordId, CancellationToken ct = default)
    {
        var record = await _db.BillingRecords.Include(b => b.School).FirstOrDefaultAsync(b => b.Id == billingRecordId, ct)
                     ?? throw new InvalidOperationException("Billing record not found.");
        if (record.AmountDue <= 0)
            throw new InvalidOperationException("No amount due for this billing record.");

        var secretKey = GetSetting("Paystack:SecretKey", "PAYSTACK_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Paystack secret key is not configured. Set Paystack:SecretKey or PAYSTACK_SECRET_KEY.");

        var client = _httpClientFactory.CreateClient("Paystack");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        var email = record.School.Email ?? $"billing+{record.SchoolId:N}@riseflow.com";
        var reference = $"RF-{record.SchoolId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var callbackUrl = ResolveCallbackUrl();

        var requestData = new
        {
            email,
            amount = (int)(record.AmountDue * 100), // Naira to Kobo
            reference,
            callback_url = callbackUrl,
            metadata = new
            {
                SchoolId = record.SchoolId,
                BillingRecordId = record.Id,
                PeriodLabel = record.PeriodLabel
            }
        };

        using var response = await client.PostAsJsonAsync("transaction/initialize", requestData, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PaystackInitializeResponse>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("Could not parse Paystack response.");
        if (!payload.Status || payload.Data == null || string.IsNullOrWhiteSpace(payload.Data.AuthorizationUrl))
            throw new InvalidOperationException($"Paystack returned an error: {payload.Message}");

        record.PaymentReference = payload.Data.Reference;
        await _db.SaveChangesAsync(ct);

        return (payload.Data.AuthorizationUrl, payload.Data.Reference);
    }

    public PaymentGatewayStatus GetGatewayStatus()
    {
        var secretKey = GetSetting("Paystack:SecretKey", "PAYSTACK_SECRET_KEY");
        var callbackUrl = ResolveCallbackUrl();
        var webhookSecret = GetSetting("Paystack:WebhookSecret", "PAYSTACK_WEBHOOK_SECRET");
        var isConfigured = !string.IsNullOrWhiteSpace(secretKey);
        var message = isConfigured
            ? (!string.IsNullOrWhiteSpace(webhookSecret)
                ? "Paystack is configured and ready to accept payments."
                : "Paystack can initialize payments now. Add a webhook secret for production-grade verification.")
            : "Paystack is not fully configured yet. Set PAYSTACK_SECRET_KEY (or Paystack:SecretKey) to enable live checkout.";

        return new PaymentGatewayStatus("Paystack", isConfigured, callbackUrl, !string.IsNullOrWhiteSpace(webhookSecret), message);
    }

    public async Task<bool> VerifyPaystackPaymentAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new InvalidOperationException("Payment reference is required.");

        var secretKey = GetSetting("Paystack:SecretKey", "PAYSTACK_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Paystack secret key is not configured. Set Paystack:SecretKey or PAYSTACK_SECRET_KEY.");

        var client = _httpClientFactory.CreateClient("Paystack");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await client.GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}", ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PaystackVerifyResponse>(cancellationToken: ct)
                      ?? throw new InvalidOperationException("Could not parse Paystack verification response.");

        if (!payload.Status || payload.Data == null)
            throw new InvalidOperationException($"Paystack verification failed: {payload.Message}");

        var record = await _db.BillingRecords.FirstOrDefaultAsync(b => b.PaymentReference == reference, ct)
                     ?? throw new InvalidOperationException("Billing record for this Paystack reference was not found.");

        if (string.Equals(payload.Data.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            record.AmountPaid = record.AmountDue;
            record.PaidAtUtc ??= DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _affiliateService.MarkBillingRecordPaidAsync(record.Id, ct);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handle Paystack webhook event. Validates signature (if secret configured) and marks billing record paid for charge.success.
    /// </summary>
    public async Task HandlePaystackWebhookAsync(string rawBody, string? signature, CancellationToken ct = default)
    {
        // Optional: verify signature using Paystack:WebhookSecret or PAYSTACK_WEBHOOK_SECRET
        var webhookSecret = GetSetting("Paystack:WebhookSecret", "PAYSTACK_WEBHOOK_SECRET");
        if (!string.IsNullOrWhiteSpace(webhookSecret) && !VerifySignature(rawBody, signature, webhookSecret))
            throw new InvalidOperationException("Invalid Paystack webhook signature.");

        var evt = JsonSerializer.Deserialize<PaystackWebhookEvent>(rawBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Invalid webhook payload.");

        if (!string.Equals(evt.Event, "charge.success", StringComparison.OrdinalIgnoreCase) || evt.Data == null)
            return;

        var reference = evt.Data.Reference;
        if (string.IsNullOrWhiteSpace(reference))
            return;

        var record = await _db.BillingRecords.FirstOrDefaultAsync(b => b.PaymentReference == reference, ct);
        if (record == null)
            return;

        if (record.AmountPaid.HasValue && record.AmountPaid.Value >= record.AmountDue)
            return; // already marked paid

        record.AmountPaid = record.AmountDue;
        record.PaidAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _affiliateService.MarkBillingRecordPaidAsync(record.Id, ct);
    }

    private string ResolveCallbackUrl()
    {
        var configured = GetSetting("Paystack:CallbackUrl", "PAYSTACK_CALLBACK_URL");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var publicWebBase = GetSetting("RiseFlow:WebAppBaseUrl", "RISEFLOW_WEB_APP_BASE_URL", "PUBLIC_WEB_BASE_URL");
        if (!string.IsNullOrWhiteSpace(publicWebBase))
            return $"{publicWebBase.TrimEnd('/')}/school/billing?payment=paystack";

        return "http://localhost:5173/school/billing?payment=paystack";
    }

    private string? GetSetting(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _config[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool VerifySignature(string body, string? signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;
        using var hmac = new System.Security.Cryptography.HMACSHA512(System.Text.Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(body));
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        return string.Equals(hash, signature, StringComparison.OrdinalIgnoreCase);
    }
}

public class PaystackInitializeResponse
{
    public bool Status { get; set; }
    public string? Message { get; set; }
    public PaystackInitializeData? Data { get; set; }
}

public class PaystackInitializeData
{
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string AccessCode { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}

public class PaystackWebhookEvent
{
    public string Event { get; set; } = string.Empty;
    public PaystackWebhookData? Data { get; set; }
}

public class PaystackWebhookData
{
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class PaystackVerifyResponse
{
    public bool Status { get; set; }
    public string? Message { get; set; }
    public PaystackVerifyData? Data { get; set; }
}

public class PaystackVerifyData
{
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public record PaymentGatewayStatus(string GatewayName, bool IsConfigured, string CallbackUrl, bool HasWebhookSecret, string Message);

