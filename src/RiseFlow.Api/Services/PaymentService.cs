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

    public PaymentService(IHttpClientFactory httpClientFactory, IConfiguration config, RiseFlowDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _db = db;
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

        var secretKey = _config["Paystack:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Paystack secret key is not configured.");

        var client = _httpClientFactory.CreateClient("Paystack");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        var email = record.School.Email ?? $"billing+{record.SchoolId:N}@riseflow.com";
        var reference = $"RF-{record.SchoolId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var callbackUrl = _config["Paystack:CallbackUrl"] ?? "https://riseflow.com/payment-success";

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

        record.PaymentReference = reference;
        await _db.SaveChangesAsync(ct);

        return (payload.Data.AuthorizationUrl, reference);
    }

    /// <summary>
    /// Handle Paystack webhook event. Validates signature (if secret configured) and marks billing record paid for charge.success.
    /// </summary>
    public async Task HandlePaystackWebhookAsync(string rawBody, string? signature, CancellationToken ct = default)
    {
        // Optional: verify signature using Paystack:WebhookSecret
        var webhookSecret = _config["Paystack:WebhookSecret"];
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

