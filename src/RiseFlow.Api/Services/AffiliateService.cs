using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;

namespace RiseFlow.Api.Services;

public class AffiliateService
{
    private readonly RiseFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly FileStorageService _fileStorage;
    private readonly IHttpClientFactory _httpClientFactory;

    public AffiliateService(
        RiseFlowDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        FileStorageService fileStorage,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _userManager = userManager;
        _configuration = configuration;
        _fileStorage = fileStorage;
        _httpClientFactory = httpClientFactory;
    }

    public AffiliateProgramInfoDto GetProgramInfo()
    {
        const string summary = "Refer schools to RiseFlow and earn for life. The first 50 students stay free. From the 51st student onward, affiliates earn ₦60 one-time and ₦20 monthly per billable student as long as the school remains active on RiseFlow.";
        return new AffiliateProgramInfoDto(
            CountryBillingConfig.FreeTierStudentCount,
            CountryBillingConfig.GetActivationFeePerStudent("NGN"),
            CountryBillingConfig.GetMonthlyRatePerStudent("NGN"),
            AffiliateConstants.ActivationCommissionPerStudentNgn,
            AffiliateConstants.MonthlyCommissionPerStudentNgn,
            "NGN",
            summary);
    }

    public async Task<AffiliateLeadRequest> CreateLeadRequestAsync(SubmitAffiliateLeadRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("Full name and email are required.");

        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await _db.AffiliateLeadRequests
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.Email == email && x.Status != AffiliateConstants.LeadStatusRejected, ct);

        if (existing != null && existing.CreatedAtUtc >= DateTime.UtcNow.AddDays(-30))
            return existing;

        var entity = new AffiliateLeadRequest
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? "NG" : request.CountryCode.Trim().ToUpperInvariant(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = AffiliateConstants.LeadStatusPending,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.AffiliateLeadRequests.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<List<AffiliateLeadRequestDto>> GetLeadRequestsAsync(CancellationToken ct = default)
    {
        return await _db.AffiliateLeadRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AffiliateLeadRequestDto(
                x.Id,
                x.FullName,
                x.Email,
                x.PhoneNumber,
                x.CountryCode,
                x.Note,
                x.Status,
                x.InviteSentAtUtc,
                x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<SendAffiliateInviteResult> SendInviteAsync(Guid leadId, CancellationToken ct = default)
    {
        var lead = await _db.AffiliateLeadRequests.FirstOrDefaultAsync(x => x.Id == leadId, ct)
            ?? throw new InvalidOperationException("Affiliate request not found.");

        var token = Guid.NewGuid().ToString("N");
        var invite = new AffiliateInvite
        {
            Id = Guid.NewGuid(),
            AffiliateLeadRequestId = lead.Id,
            Email = lead.Email,
            InviteToken = token,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(AffiliateConstants.InviteExpiryDays),
            CreatedAtUtc = DateTime.UtcNow
        };

        lead.Status = AffiliateConstants.LeadStatusInvited;
        lead.InviteSentAtUtc = DateTime.UtcNow;
        _db.AffiliateInvites.Add(invite);
        await _db.SaveChangesAsync(ct);

        var inviteUrl = BuildInviteUrl(token);
        var emailSent = await TrySendEmailAsync(
            lead.Email,
            "Your RiseFlow affiliate invite link",
            $"Hello {lead.FullName},\n\nYour RiseFlow affiliate invite link is ready:\n{inviteUrl}\n\nThis secure link expires on {invite.ExpiresAtUtc:dd MMM yyyy HH:mm} UTC.",
            ct);

        return new SendAffiliateInviteResult(lead.Id, lead.Email, inviteUrl, emailSent, invite.ExpiresAtUtc);
    }

    public async Task<AffiliateInviteValidationDto> ValidateInviteAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new AffiliateInviteValidationDto(false, null, null, "Invite token is required.");

        var invite = await _db.AffiliateInvites
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InviteToken == token.Trim(), ct);

        if (invite == null)
            return new AffiliateInviteValidationDto(false, null, null, "This invite link was not found.");
        if (invite.UsedAtUtc.HasValue)
            return new AffiliateInviteValidationDto(false, invite.Email, invite.ExpiresAtUtc, "This invite link has already been used.");
        if (invite.ExpiresAtUtc < DateTime.UtcNow)
            return new AffiliateInviteValidationDto(false, invite.Email, invite.ExpiresAtUtc, "This invite link has expired.");

        return new AffiliateInviteValidationDto(true, invite.Email, invite.ExpiresAtUtc, "Invite link is valid.");
    }

    public async Task<(ApplicationUser User, Affiliate Affiliate)> CompleteInviteAsync(string token, CompleteAffiliateInviteRequest request, CancellationToken ct = default)
    {
        var invite = await _db.AffiliateInvites
            .Include(x => x.AffiliateLeadRequest)
            .FirstOrDefaultAsync(x => x.InviteToken == token.Trim(), ct)
            ?? throw new InvalidOperationException("Invite link was not found.");

        if (invite.UsedAtUtc.HasValue)
            throw new InvalidOperationException("This invite link has already been used.");
        if (invite.ExpiresAtUtc < DateTime.UtcNow)
            throw new InvalidOperationException("This invite link has expired.");
        if (string.IsNullOrWhiteSpace(request.Email) || !string.Equals(request.Email.Trim(), invite.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use the same email address that received the invite.");

        var existingUser = await _userManager.FindByEmailAsync(invite.Email);
        if (existingUser != null)
            throw new InvalidOperationException("An account already exists for this email. Please sign in instead.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = invite.Email,
            Email = invite.Email,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? invite.AffiliateLeadRequest.FullName : request.FullName.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));

        await _userManager.AddToRoleAsync(user, Roles.Affiliate);

        var affiliate = new Affiliate
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UniqueCode = await GenerateUniqueCodeAsync(ct),
            PhoneNumber = FirstNonEmpty(request.PhoneNumber, invite.AffiliateLeadRequest.PhoneNumber),
            CountryCode = FirstNonEmpty(request.CountryCode, invite.AffiliateLeadRequest.CountryCode) ?? "NG",
            IsActive = true,
            ApprovedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        invite.UsedAtUtc = DateTime.UtcNow;
        invite.AffiliateLeadRequest.Status = AffiliateConstants.LeadStatusApproved;
        _db.Affiliates.Add(affiliate);
        await _db.SaveChangesAsync(ct);

        await CreateNotificationAsync(affiliate.Id, "Welcome to RiseFlow Affiliates", "Your affiliate account is live. Your unique referral link is ready to share.", "Success", ct);

        return (user, affiliate);
    }

    public async Task<Affiliate?> FindByReferralCodeAsync(string? referralCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(referralCode))
            return null;

        var code = referralCode.Trim().ToUpperInvariant();
        return await _db.Affiliates.FirstOrDefaultAsync(x => x.UniqueCode == code && x.IsActive, ct);
    }

    public async Task EnsureCommissionForBillingRecordAsync(BillingRecord record, int previousBillableStudents, CancellationToken ct = default)
    {
        if (await _db.AffiliateCommissionLedgers.AnyAsync(x => x.BillingRecordId == record.Id, ct))
            return;

        var school = await _db.Schools
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == record.SchoolId, ct);

        if (school?.AffiliateId == null)
            return;

        var billableStudents = Math.Max(0, record.StudentCount - CountryBillingConfig.FreeTierStudentCount);
        if (billableStudents <= 0)
            return;

        var newBillableStudents = Math.Max(0, billableStudents - Math.Max(0, previousBillableStudents));
        var activationAmount = newBillableStudents * AffiliateConstants.ActivationCommissionPerStudentNgn;
        var monthlyAmount = billableStudents * AffiliateConstants.MonthlyCommissionPerStudentNgn;
        var totalAmount = activationAmount + monthlyAmount;

        if (totalAmount <= 0)
            return;

        _db.AffiliateCommissionLedgers.Add(new AffiliateCommissionLedger
        {
            Id = Guid.NewGuid(),
            AffiliateId = school.AffiliateId.Value,
            SchoolId = school.Id,
            BillingRecordId = record.Id,
            StudentCount = record.StudentCount,
            BillableStudentCount = billableStudents,
            ActivationCommissionAmount = activationAmount,
            MonthlyCommissionAmount = monthlyAmount,
            TotalCommissionAmount = totalAmount,
            CommissionType = activationAmount > 0 && monthlyAmount > 0 ? "Activation+Monthly" : activationAmount > 0 ? "Activation" : "Monthly",
            Status = record.AmountPaid.HasValue && record.AmountPaid.Value >= record.AmountDue
                ? AffiliateConstants.CommissionStatusReadyForPayout
                : AffiliateConstants.CommissionStatusPending,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await SyncPendingPayoutsAsync(ct);
    }

    public async Task MarkBillingRecordPaidAsync(Guid billingRecordId, CancellationToken ct = default)
    {
        var ledgers = await _db.AffiliateCommissionLedgers
            .Where(x => x.BillingRecordId == billingRecordId && x.Status == AffiliateConstants.CommissionStatusPending)
            .ToListAsync(ct);

        if (ledgers.Count == 0)
            return;

        foreach (var ledger in ledgers)
            ledger.Status = AffiliateConstants.CommissionStatusReadyForPayout;

        await _db.SaveChangesAsync(ct);
        await SyncPendingPayoutsAsync(ct);
    }

    public async Task SyncPendingPayoutsAsync(CancellationToken ct = default)
    {
        var readyLedgers = await _db.AffiliateCommissionLedgers
            .Include(x => x.BillingRecord)
            .Where(x => x.Status == AffiliateConstants.CommissionStatusReadyForPayout && x.AffiliatePayoutId == null)
            .ToListAsync(ct);

        if (readyLedgers.Count == 0)
            return;

        foreach (var group in readyLedgers.GroupBy(x => x.AffiliateId))
        {
            var totalAmount = group.Sum(x => x.TotalCommissionAmount);
            if (totalAmount <= 0)
                continue;

            var payout = new AffiliatePayout
            {
                Id = Guid.NewGuid(),
                AffiliateId = group.Key,
                Amount = totalAmount,
                CurrencyCode = "NGN",
                PayoutType = "Commission",
                Status = AffiliateConstants.PayoutStatusPending,
                PeriodStartUtc = group.Min(x => x.BillingRecord?.PeriodStart.ToDateTime(TimeOnly.MinValue) ?? x.CreatedAtUtc),
                PeriodEndUtc = group.Max(x => x.BillingRecord?.PeriodEnd.ToDateTime(TimeOnly.MaxValue) ?? x.CreatedAtUtc),
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.AffiliatePayouts.Add(payout);
            foreach (var ledger in group)
                ledger.AffiliatePayoutId = payout.Id;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<AffiliateSummaryDto>> GetAffiliateSummariesAsync(CancellationToken ct = default)
    {
        await SyncPendingPayoutsAsync(ct);

        var affiliates = await _db.Affiliates
            .AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var schoolCounts = await _db.Schools
            .IgnoreQueryFilters()
            .Where(x => x.AffiliateId != null)
            .GroupBy(x => x.AffiliateId!.Value)
            .Select(g => new { AffiliateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AffiliateId, x => x.Count, ct);

        var billableStudents = await _db.AffiliateCommissionLedgers
            .AsNoTracking()
            .GroupBy(x => x.AffiliateId)
            .Select(g => new { AffiliateId = g.Key, Total = g.Sum(x => x.BillableStudentCount) })
            .ToDictionaryAsync(x => x.AffiliateId, x => x.Total, ct);

        var pendingTotals = await _db.AffiliatePayouts
            .AsNoTracking()
            .Where(x => x.Status == AffiliateConstants.PayoutStatusPending || x.Status == AffiliateConstants.PayoutStatusProcessing)
            .GroupBy(x => x.AffiliateId)
            .Select(g => new { AffiliateId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.AffiliateId, x => x.Total, ct);

        var paidTotals = await _db.AffiliatePayouts
            .AsNoTracking()
            .Where(x => x.Status == AffiliateConstants.PayoutStatusPaid)
            .GroupBy(x => x.AffiliateId)
            .Select(g => new { AffiliateId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.AffiliateId, x => x.Total, ct);

        return affiliates.Select(x => new AffiliateSummaryDto(
            x.Id,
            x.User.FullName ?? x.User.Email ?? "Affiliate",
            x.User.Email ?? string.Empty,
            x.UniqueCode,
            x.IsActive,
            x.CountryCode,
            x.PhoneNumber,
            x.HeadshotPath,
            schoolCounts.GetValueOrDefault(x.Id, 0),
            billableStudents.GetValueOrDefault(x.Id, 0),
            pendingTotals.GetValueOrDefault(x.Id, 0m),
            paidTotals.GetValueOrDefault(x.Id, 0m),
            x.ApprovedAtUtc)).ToList();
    }

    public async Task<AffiliateAdminDetailDto?> GetAffiliateAdminDetailAsync(Guid affiliateId, CancellationToken ct = default)
    {
        var affiliate = await _db.Affiliates
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == affiliateId, ct);

        if (affiliate == null)
            return null;

        var summaries = await GetAffiliateSummariesAsync(ct);
        var summary = summaries.First(x => x.AffiliateId == affiliateId);
        var schools = await BuildSchoolSummariesAsync(affiliateId, ct);
        var payouts = await BuildPayoutHistoryAsync(affiliateId, ct);
        var notifications = await BuildNotificationsAsync(affiliateId, ct);

        return new AffiliateAdminDetailDto(
            summary,
            new AffiliatePayoutSettingsDto(affiliate.BankName, affiliate.AccountNumber, affiliate.AccountName, affiliate.CountryCode, affiliate.PhoneNumber, affiliate.HeadshotPath),
            schools,
            payouts,
            notifications);
    }

    public async Task<AffiliateDashboardDto?> GetAffiliateDashboardAsync(Guid userId, CancellationToken ct = default)
    {
        await SyncPendingPayoutsAsync(ct);

        var affiliate = await _db.Affiliates
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (affiliate == null)
            return null;

        var schoolSummaries = await BuildSchoolSummariesAsync(affiliate.Id, ct);
        var payoutHistory = await BuildPayoutHistoryAsync(affiliate.Id, ct);
        var trainingVideos = await ListTrainingVideoDtosAsync(includeUnpublished: false, ct);
        var notifications = await BuildNotificationsAsync(affiliate.Id, ct);

        var totalStudents = schoolSummaries.Sum(x => x.TotalStudents);
        var totalBillableStudents = schoolSummaries.Sum(x => x.BillableStudents);
        var pendingAmount = payoutHistory
            .Where(x => x.Status == AffiliateConstants.PayoutStatusPending || x.Status == AffiliateConstants.PayoutStatusProcessing)
            .Sum(x => x.Amount);
        var paidToDate = payoutHistory
            .Where(x => x.Status == AffiliateConstants.PayoutStatusPaid)
            .Sum(x => x.Amount);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var currentMonthEarnings = await _db.AffiliateCommissionLedgers
            .AsNoTracking()
            .Where(x => x.AffiliateId == affiliate.Id && x.CreatedAtUtc >= monthStart)
            .SumAsync(x => (decimal?)x.TotalCommissionAmount, ct) ?? 0m;

        return new AffiliateDashboardDto(
            affiliate.User.FullName ?? affiliate.User.Email ?? "Affiliate",
            affiliate.User.Email ?? string.Empty,
            affiliate.UniqueCode,
            BuildReferralUrl(affiliate.UniqueCode),
            affiliate.HeadshotPath,
            schoolSummaries.Count,
            totalStudents,
            totalBillableStudents,
            currentMonthEarnings,
            pendingAmount,
            paidToDate,
            new AffiliatePayoutSettingsDto(affiliate.BankName, affiliate.AccountNumber, affiliate.AccountName, affiliate.CountryCode, affiliate.PhoneNumber, affiliate.HeadshotPath),
            schoolSummaries,
            payoutHistory,
            trainingVideos,
            notifications);
    }

    public async Task<AffiliatePayoutSettingsDto?> UpdatePayoutSettingsAsync(Guid userId, UpdateAffiliatePayoutSettingsRequest request, CancellationToken ct = default)
    {
        var affiliate = await _db.Affiliates.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (affiliate == null)
            return null;

        affiliate.BankName = string.IsNullOrWhiteSpace(request.BankName) ? null : request.BankName.Trim();
        affiliate.AccountNumber = string.IsNullOrWhiteSpace(request.AccountNumber) ? null : request.AccountNumber.Trim();
        affiliate.AccountName = string.IsNullOrWhiteSpace(request.AccountName) ? null : request.AccountName.Trim();
        affiliate.CountryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? affiliate.CountryCode : request.CountryCode.Trim().ToUpperInvariant();
        affiliate.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? affiliate.PhoneNumber : request.PhoneNumber.Trim();
        affiliate.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new AffiliatePayoutSettingsDto(affiliate.BankName, affiliate.AccountNumber, affiliate.AccountName, affiliate.CountryCode, affiliate.PhoneNumber, affiliate.HeadshotPath);
    }

    public async Task<string?> SaveHeadshotAsync(Guid userId, IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Headshot file is required.");

        var affiliate = await _db.Affiliates.FirstOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw new InvalidOperationException("Affiliate account not found.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".png";

        var uploadsRoot = Path.Combine(_fileStorage.RootPath, "uploads", "affiliates");
        Directory.CreateDirectory(uploadsRoot);

        var storedName = $"{affiliate.Id:N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, storedName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        affiliate.HeadshotPath = $"uploads/affiliates/{storedName}";
        affiliate.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return affiliate.HeadshotPath;
    }

    public async Task<List<AffiliateTrainingVideoDto>> ListTrainingVideoDtosAsync(bool includeUnpublished, CancellationToken ct = default)
    {
        IQueryable<AffiliateTrainingVideo> query = _db.AffiliateTrainingVideos.AsNoTracking();
        if (!includeUnpublished)
            query = query.Where(x => x.IsPublished);

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new AffiliateTrainingVideoDto(
                x.Id,
                x.Title,
                x.Topic,
                x.Description,
                NormalizeYoutubeUrl(x.YoutubeUrl),
                x.IsPublished,
                x.SortOrder,
                x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<AffiliateTrainingVideoDto> SaveTrainingVideoAsync(Guid? id, SaveAffiliateTrainingVideoRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.YoutubeUrl))
            throw new InvalidOperationException("Title and YouTube URL are required.");

        AffiliateTrainingVideo entity;
        if (id.HasValue)
        {
            entity = await _db.AffiliateTrainingVideos.FirstOrDefaultAsync(x => x.Id == id.Value, ct)
                ?? throw new InvalidOperationException("Training video not found.");
        }
        else
        {
            entity = new AffiliateTrainingVideo { Id = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow };
            _db.AffiliateTrainingVideos.Add(entity);
        }

        entity.Title = request.Title.Trim();
        entity.Topic = string.IsNullOrWhiteSpace(request.Topic) ? null : request.Topic.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.YoutubeUrl = NormalizeYoutubeUrl(request.YoutubeUrl);
        entity.IsPublished = request.IsPublished;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new AffiliateTrainingVideoDto(entity.Id, entity.Title, entity.Topic, entity.Description, entity.YoutubeUrl, entity.IsPublished, entity.SortOrder, entity.CreatedAtUtc);
    }

    public async Task<bool> DeleteTrainingVideoAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.AffiliateTrainingVideos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return false;

        _db.AffiliateTrainingVideos.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<AffiliatePayoutDto>> GetPayoutsForSuperAdminAsync(CancellationToken ct = default)
    {
        await SyncPendingPayoutsAsync(ct);

        return await _db.AffiliatePayouts
            .AsNoTracking()
            .Include(x => x.Affiliate)
            .ThenInclude(x => x.User)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AffiliatePayoutDto(
                x.Id,
                x.AffiliateId,
                x.Affiliate.User.FullName ?? x.Affiliate.User.Email ?? "Affiliate",
                x.Amount,
                x.CurrencyCode,
                x.PayoutType,
                x.Status,
                x.PaystackTransferReference,
                x.PeriodStartUtc,
                x.PeriodEndUtc,
                x.PaidAtUtc,
                x.CreatedAtUtc,
                x.FailureReason))
            .ToListAsync(ct);
    }

    public async Task<AffiliatePayoutDto> PayPayoutAsync(Guid payoutId, CancellationToken ct = default)
    {
        var payout = await _db.AffiliatePayouts
            .Include(x => x.Affiliate)
            .ThenInclude(x => x.User)
            .Include(x => x.CommissionLedgers)
            .FirstOrDefaultAsync(x => x.Id == payoutId, ct)
            ?? throw new InvalidOperationException("Affiliate payout not found.");

        if (payout.Status == AffiliateConstants.PayoutStatusPaid)
        {
            return new AffiliatePayoutDto(
                payout.Id,
                payout.AffiliateId,
                payout.Affiliate.User.FullName ?? payout.Affiliate.User.Email ?? "Affiliate",
                payout.Amount,
                payout.CurrencyCode,
                payout.PayoutType,
                payout.Status,
                payout.PaystackTransferReference,
                payout.PeriodStartUtc,
                payout.PeriodEndUtc,
                payout.PaidAtUtc,
                payout.CreatedAtUtc,
                payout.FailureReason);
        }

        if (payout.Amount <= 0)
            throw new InvalidOperationException("This payout has no payable amount.");
        if (string.IsNullOrWhiteSpace(payout.Affiliate.BankName) || string.IsNullOrWhiteSpace(payout.Affiliate.AccountNumber) || string.IsNullOrWhiteSpace(payout.Affiliate.AccountName))
            throw new InvalidOperationException("Affiliate bank details are incomplete.");

        payout.Status = AffiliateConstants.PayoutStatusProcessing;
        payout.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            var paystackReference = await SendPaystackTransferAsync(payout, ct);
            payout.PaystackTransferReference = paystackReference;
            payout.Status = AffiliateConstants.PayoutStatusPaid;
            payout.PaidAtUtc = DateTime.UtcNow;
            payout.UpdatedAtUtc = DateTime.UtcNow;
            payout.FailureReason = null;

            foreach (var ledger in payout.CommissionLedgers.Where(x => x.Status == AffiliateConstants.CommissionStatusReadyForPayout))
                ledger.Status = AffiliateConstants.CommissionStatusPaid;

            await _db.SaveChangesAsync(ct);

            var displayAmount = $"₦{payout.Amount:N0}";
            await CreateNotificationAsync(payout.AffiliateId, "Affiliate payout sent", $"Your affiliate payout of {displayAmount} has been paid successfully.", "Payout", ct);
            await TrySendEmailAsync(
                payout.Affiliate.User.Email,
                "RiseFlow affiliate payout receipt",
                $"Hello {payout.Affiliate.User.FullName ?? "Affiliate"},\n\nYour payout of {displayAmount} for referred schools has been processed successfully. Reference: {payout.PaystackTransferReference}",
                ct);
        }
        catch (Exception ex)
        {
            payout.Status = AffiliateConstants.PayoutStatusFailed;
            payout.FailureReason = ex.Message;
            payout.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await CreateNotificationAsync(payout.AffiliateId, "Affiliate payout failed", ex.Message, "Error", ct);
        }

        return new AffiliatePayoutDto(
            payout.Id,
            payout.AffiliateId,
            payout.Affiliate.User.FullName ?? payout.Affiliate.User.Email ?? "Affiliate",
            payout.Amount,
            payout.CurrencyCode,
            payout.PayoutType,
            payout.Status,
            payout.PaystackTransferReference,
            payout.PeriodStartUtc,
            payout.PeriodEndUtc,
            payout.PaidAtUtc,
            payout.CreatedAtUtc,
            payout.FailureReason);
    }

    public async Task<AffiliateNotification> CreateNotificationAsync(Guid affiliateId, string title, string message, string type, CancellationToken ct = default)
    {
        var notification = new AffiliateNotification
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliateId,
            Title = title,
            Message = message,
            Type = type,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.AffiliateNotifications.Add(notification);
        await _db.SaveChangesAsync(ct);
        return notification;
    }

    private async Task<List<AffiliateSchoolSummaryDto>> BuildSchoolSummariesAsync(Guid affiliateId, CancellationToken ct)
    {
        var schools = await _db.Schools
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.AffiliateId == affiliateId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var commissionRows = await _db.AffiliateCommissionLedgers
            .AsNoTracking()
            .Where(x => x.AffiliateId == affiliateId)
            .ToListAsync(ct);

        var paidBillingMap = await _db.BillingRecords
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.School.AffiliateId == affiliateId && x.AmountPaid != null && x.AmountPaid >= x.AmountDue)
            .GroupBy(x => x.SchoolId)
            .Select(g => new { SchoolId = g.Key, LatestPaidAtUtc = g.Max(x => x.PaidAtUtc) })
            .ToDictionaryAsync(x => x.SchoolId, x => x.LatestPaidAtUtc, ct);

        return schools.Select(school =>
        {
            var billableStudents = Math.Max(0, school.Students.Count(st => st.IsActive) - CountryBillingConfig.FreeTierStudentCount);
            var rows = commissionRows.Where(x => x.SchoolId == school.Id).ToList();
            return new AffiliateSchoolSummaryDto(
                school.Id,
                school.Name,
                school.Students.Count(st => st.IsActive),
                billableStudents,
                school.CreatedAtUtc,
                paidBillingMap.GetValueOrDefault(school.Id),
                rows.Sum(x => x.TotalCommissionAmount),
                rows.Where(x => x.Status == AffiliateConstants.CommissionStatusPending || x.Status == AffiliateConstants.CommissionStatusReadyForPayout).Sum(x => x.TotalCommissionAmount),
                rows.Where(x => x.Status == AffiliateConstants.CommissionStatusPaid).Sum(x => x.TotalCommissionAmount));
        }).ToList();
    }

    private async Task<List<AffiliatePayoutDto>> BuildPayoutHistoryAsync(Guid affiliateId, CancellationToken ct)
    {
        return await _db.AffiliatePayouts
            .AsNoTracking()
            .Include(x => x.Affiliate)
            .ThenInclude(x => x.User)
            .Where(x => x.AffiliateId == affiliateId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AffiliatePayoutDto(
                x.Id,
                x.AffiliateId,
                x.Affiliate.User.FullName ?? x.Affiliate.User.Email ?? "Affiliate",
                x.Amount,
                x.CurrencyCode,
                x.PayoutType,
                x.Status,
                x.PaystackTransferReference,
                x.PeriodStartUtc,
                x.PeriodEndUtc,
                x.PaidAtUtc,
                x.CreatedAtUtc,
                x.FailureReason))
            .ToListAsync(ct);
    }

    private async Task<List<AffiliateNotificationDto>> BuildNotificationsAsync(Guid affiliateId, CancellationToken ct)
    {
        return await _db.AffiliateNotifications
            .AsNoTracking()
            .Where(x => x.AffiliateId == affiliateId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(20)
            .Select(x => new AffiliateNotificationDto(x.Id, x.Title, x.Message, x.Type, x.IsRead, x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = $"RF-AFF-{Guid.NewGuid():N}"[..13].ToUpperInvariant();
            var exists = await _db.Affiliates.AnyAsync(x => x.UniqueCode == code, ct);
            if (!exists)
                return code;
        }
    }

    private string BuildReferralUrl(string code)
    {
        var webBase = ResolveWebBaseUrl();
        return $"{webBase}/onboard?ref={Uri.EscapeDataString(code)}";
    }

    private string BuildInviteUrl(string token)
    {
        var webBase = ResolveWebBaseUrl();
        return $"{webBase}/affiliate/signup?invite={Uri.EscapeDataString(token)}";
    }

    private string ResolveWebBaseUrl()
    {
        var configured = _configuration["RiseFlow:WebAppBaseUrl"]
            ?? _configuration["RISEFLOW_WEB_APP_BASE_URL"]
            ?? _configuration["PUBLIC_WEB_BASE_URL"];

        return string.IsNullOrWhiteSpace(configured)
            ? "http://localhost:5173"
            : configured.TrimEnd('/');
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string NormalizeYoutubeUrl(string youtubeUrl)
    {
        if (string.IsNullOrWhiteSpace(youtubeUrl))
            return youtubeUrl;

        var value = youtubeUrl.Trim();
        if (value.Contains("youtube.com/embed/", StringComparison.OrdinalIgnoreCase))
            return value;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                var id = uri.AbsolutePath.Trim('/');
                if (!string.IsNullOrWhiteSpace(id))
                    return $"https://www.youtube.com/embed/{id}";
            }

            if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
            {
                var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Split('=', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]), StringComparer.OrdinalIgnoreCase);

                if (query.TryGetValue("v", out var id) && !string.IsNullOrWhiteSpace(id))
                    return $"https://www.youtube.com/embed/{id}";
            }
        }

        return value;
    }

    private async Task<bool> TrySendEmailAsync(string? recipientEmail, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            return false;

        var host = _configuration["Smtp:Host"];
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var from = _configuration["Smtp:From"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            return false;

        var port = int.TryParse(_configuration["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var enableSsl = !string.Equals(_configuration["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

        using var mail = new MailMessage(from, recipientEmail)
        {
            Subject = subject,
            Body = body
        };

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password ?? string.Empty);

        await client.SendMailAsync(mail, ct);
        return true;
    }

    private async Task<string> SendPaystackTransferAsync(AffiliatePayout payout, CancellationToken ct)
    {
        var secretKey = _configuration["Paystack:SecretKey"] ?? _configuration["PAYSTACK_SECRET_KEY"];
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Paystack secret key is not configured for payouts.");

        var client = _httpClientFactory.CreateClient("Paystack");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        var recipientCode = payout.Affiliate.PaystackRecipientCode;
        if (string.IsNullOrWhiteSpace(recipientCode))
        {
            recipientCode = await EnsureTransferRecipientAsync(client, payout.Affiliate, ct);
            payout.Affiliate.PaystackRecipientCode = recipientCode;
            payout.Affiliate.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        var fallbackReference = $"RF-AFF-PAYOUT-{payout.Id:N}".ToUpperInvariant();
        var transferBody = new
        {
            source = "balance",
            amount = (int)(payout.Amount * 100),
            recipient = recipientCode,
            reason = $"RiseFlow affiliate payout {payout.PeriodStartUtc:MMM yyyy}",
            reference = fallbackReference
        };

        using var response = await client.PostAsJsonAsync("transfer", transferBody, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Paystack payout failed: {payload}");

        var json = JsonSerializer.Deserialize<PaystackEnvelope<PaystackTransferData>>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (json?.Status != true || json.Data == null)
            throw new InvalidOperationException(json?.Message ?? "Paystack payout did not return a valid success response.");

        return string.IsNullOrWhiteSpace(json.Data.Reference) ? fallbackReference : json.Data.Reference;
    }

    private async Task<string> EnsureTransferRecipientAsync(HttpClient client, Affiliate affiliate, CancellationToken ct)
    {
        var bankCode = await ResolveBankCodeAsync(client, affiliate.BankName!, ct);
        var requestBody = new
        {
            type = "nuban",
            name = affiliate.AccountName,
            account_number = affiliate.AccountNumber,
            bank_code = bankCode,
            currency = "NGN"
        };

        using var response = await client.PostAsJsonAsync("transferrecipient", requestBody, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Could not create Paystack transfer recipient: {payload}");

        var json = JsonSerializer.Deserialize<PaystackEnvelope<PaystackRecipientData>>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (json?.Status != true || json.Data == null || string.IsNullOrWhiteSpace(json.Data.RecipientCode))
            throw new InvalidOperationException(json?.Message ?? "Paystack did not return a transfer recipient code.");

        return json.Data.RecipientCode;
    }

    private static async Task<string> ResolveBankCodeAsync(HttpClient client, string bankName, CancellationToken ct)
    {
        using var response = await client.GetAsync("bank?country=nigeria", ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Could not fetch Nigerian banks from Paystack.");

        var json = JsonSerializer.Deserialize<PaystackEnvelope<List<PaystackBankData>>>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var bank = json?.Data?.FirstOrDefault(x => string.Equals(x.Name, bankName, StringComparison.OrdinalIgnoreCase))
            ?? json?.Data?.FirstOrDefault(x => x.Name.Contains(bankName, StringComparison.OrdinalIgnoreCase));

        if (bank == null || string.IsNullOrWhiteSpace(bank.Code))
            throw new InvalidOperationException($"Could not find a Nigerian bank code for '{bankName}'.");

        return bank.Code;
    }

    private sealed class PaystackEnvelope<T>
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    private sealed class PaystackRecipientData
    {
        public string RecipientCode { get; set; } = string.Empty;
    }

    private sealed class PaystackTransferData
    {
        public string Reference { get; set; } = string.Empty;
    }

    private sealed class PaystackBankData
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
