using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

/// <summary>
/// Generates simple PDF receipts for paid billing records.
/// </summary>
public class BillingReceiptPdfService
{
    private readonly RiseFlowDbContext _db;

    public BillingReceiptPdfService(RiseFlowDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> GenerateReceiptAsync(Guid billingRecordId, CancellationToken ct = default)
    {
        var record = await _db.BillingRecords
            .Include(b => b.School)
            .FirstOrDefaultAsync(b => b.Id == billingRecordId, ct)
            ?? throw new InvalidOperationException("Billing record not found.");

        if (!record.AmountPaid.HasValue || record.AmountPaid.Value <= 0)
            throw new InvalidOperationException("Billing record is not marked as paid.");

        QuestPDF.Settings.License = LicenseType.Community;

        var school = record.School;
        var amount = record.AmountPaid.Value;
        var currency = record.CurrencyCode;
        var paidAt = record.PaidAtUtc ?? DateTime.UtcNow;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(c =>
                {
                    c.Item().Text("PAYMENT RECEIPT").Bold().FontSize(14);
                    c.Item().Text(school.Name).FontSize(12);
                    c.Item().Text($"Issued: {paidAt:yyyy-MM-dd HH:mm} UTC").FontSize(9);
                });

                page.Content().Column(c =>
                {
                    c.Spacing(10);
                    c.Item().Text($"School: {school.Name}").Bold();
                    c.Item().Text($"Billing period: {record.PeriodLabel} ({record.PeriodStart:yyyy-MM-dd} to {record.PeriodEnd:yyyy-MM-dd})");
                    c.Item().Text($"Student count: {record.StudentCount}");
                    c.Item().Text($"Amount paid: {amount} {currency}");
                    c.Item().Text($"Payment reference: {record.PaymentReference ?? "—"}");
                    c.Spacing(10);
                    c.Item().Text("Thank you for your payment. This receipt confirms that your RiseFlow subscription for the above period has been settled in full.");
                });
            });
        }).GeneratePdf();
    }
}

