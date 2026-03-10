namespace RiseFlow.Api.Models;

/// <summary>
/// Revenue breakdown for the SuperAdmin "Revenue Hub" view.
/// Separates one‑time activation fees from recurring monthly subscriptions.
/// </summary>
public class SuperAdminRevenueViewModel
{
    /// <summary>Total one‑time activation fees collected across all schools (e.g. ₦500 per billable student).</summary>
    public decimal TotalOneTimeFees { get; set; }

    /// <summary>Total recurring monthly subscription revenue (e.g. ₦100 per billable student per month).</summary>
    public decimal TotalMonthlySubscriptions { get; set; }

    /// <summary>Combined cash flow from one‑time + recurring.</summary>
    public decimal TotalRevenue => TotalOneTimeFees + TotalMonthlySubscriptions;

    public int TotalSchools { get; set; }

    /// <summary>Total number of billable students (students above the 50‑student free tier across all schools).</summary>
    public int TotalBillableStudents { get; set; }

    /// <summary>Top revenue‑generating schools for quick insight.</summary>
    public List<SchoolRevenueBreakdown> TopRevenueSchools { get; set; } = new();
}

public class SchoolRevenueBreakdown
{
    public Guid SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int StudentCount { get; set; }

    /// <summary>Current recurring monthly income for this school (based on paid billing records).</summary>
    public decimal MonthlyIncome { get; set; }

    /// <summary>Total amount paid to date across all billing records for this school.</summary>
    public decimal TotalPaidToDate { get; set; }
}

