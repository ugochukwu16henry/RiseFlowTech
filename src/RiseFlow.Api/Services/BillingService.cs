using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;

namespace RiseFlow.Api.Services;

public class BillingService
{
    private readonly RiseFlowDbContext _db;

    public BillingService(RiseFlowDbContext db) => _db = db;

    public static decimal ComputeAmountDue(int studentCount)
    {
        if (studentCount <= BillingConstants.FreeTierStudentCount) return 0;
        return (studentCount - BillingConstants.FreeTierStudentCount) * BillingConstants.RatePerStudentNaira;
    }

    public async Task<BillingRecord> CreateBillingRecordAsync(Guid schoolId, string periodLabel, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default)
    {
        var studentCount = await _db.Students.CountAsync(s => s.SchoolId == schoolId && s.IsActive, ct);
        var record = new BillingRecord
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            PeriodLabel = periodLabel,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            StudentCount = studentCount,
            AmountDueNaira = ComputeAmountDue(studentCount),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.BillingRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<decimal> GetTotalRevenueAsync(CancellationToken ct = default) =>
        await _db.BillingRecords.SumAsync(b => b.AmountPaidNaira ?? 0, ct);
}
