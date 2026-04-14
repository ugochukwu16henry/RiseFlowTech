using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;
using RiseFlow.Api.Data;
using RiseFlow.Api.Entities;
using RiseFlow.Api.Models;
using RiseFlow.Api.Services;

namespace RiseFlow.Api.Controllers;

[ApiController]
[Route("api/school-fees")]
[Authorize]
public class SchoolFeesController : ControllerBase
{
    private readonly RiseFlowDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly FileStorageService _fileStorage;
    private readonly IWebHostEnvironment _env;

    public SchoolFeesController(
        RiseFlowDbContext db,
        ITenantContext tenant,
        FileStorageService fileStorage,
        IWebHostEnvironment env)
    {
        _db = db;
        _tenant = tenant;
        _fileStorage = fileStorage;
        _env = env;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bank Details
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Get school's bank details for fee collection.</summary>
    [HttpGet("bank-details")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Parent}")]
    public async Task<ActionResult<BankDetailsDto?>> GetBankDetails(CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        var details = await _db.SchoolBankDetails
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (details is null) return Ok(null);

        return Ok(new BankDetailsDto(
            details.Id,
            details.BankName,
            details.AccountName,
            details.AccountNumber,
            details.BranchOrSortCode,
            details.PaymentInstructions));
    }

    /// <summary>Create or replace the school's bank details.</summary>
    [HttpPut("bank-details")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<BankDetailsDto>> SaveBankDetails(
        [FromBody] SaveBankDetailsRequest request,
        CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        if (string.IsNullOrWhiteSpace(request.BankName)) return BadRequest("Bank name is required.");
        if (string.IsNullOrWhiteSpace(request.AccountName)) return BadRequest("Account name is required.");
        if (string.IsNullOrWhiteSpace(request.AccountNumber)) return BadRequest("Account number is required.");

        // Upsert: update the most recent record or create new
        var existing = await _db.SchoolBankDetails
            .Where(x => x.SchoolId == schoolId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            existing.BankName = request.BankName.Trim();
            existing.AccountName = request.AccountName.Trim();
            existing.AccountNumber = request.AccountNumber.Trim();
            existing.BranchOrSortCode = request.BranchOrSortCode?.Trim();
            existing.PaymentInstructions = request.PaymentInstructions?.Trim();
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            existing = new SchoolBankDetails
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                BankName = request.BankName.Trim(),
                AccountName = request.AccountName.Trim(),
                AccountNumber = request.AccountNumber.Trim(),
                BranchOrSortCode = request.BranchOrSortCode?.Trim(),
                PaymentInstructions = request.PaymentInstructions?.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.SchoolBankDetails.Add(existing);
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new BankDetailsDto(
            existing.Id,
            existing.BankName,
            existing.AccountName,
            existing.AccountNumber,
            existing.BranchOrSortCode,
            existing.PaymentInstructions));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fee Schedules
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>List all fee schedules for the school.</summary>
    [HttpGet("schedules")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Parent}")]
    public async Task<ActionResult<List<FeeScheduleDto>>> ListSchedules(CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        var schedules = await _db.TermFeeSchedules
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .Include(x => x.Grade)
            .Include(x => x.Class)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var ids = schedules.Select(s => s.Id).ToList();
        var counts = await _db.FeePaymentRecords
            .AsNoTracking()
            .Where(x => ids.Contains(x.ScheduleId))
            .GroupBy(x => x.ScheduleId)
            .Select(g => new { g.Key, Total = g.Count(), Confirmed = g.Count(p => p.Status == FeePaymentStatus.Confirmed) })
            .ToListAsync(ct);

        var countMap = counts.ToDictionary(x => x.Key, x => (x.Total, x.Confirmed));

        var result = schedules.Select(s =>
        {
            var (total, confirmed) = countMap.TryGetValue(s.Id, out var v) ? v : (0, 0);
            return new FeeScheduleDto(
                s.Id,
                s.TermLabel,
                s.AcademicYear,
                s.GradeId,
                s.Grade?.Name,
                s.ClassId,
                s.Class?.Name,
                s.Amount,
                s.Description,
                s.IsActive,
                total,
                confirmed);
        }).ToList();

        return Ok(result);
    }

    /// <summary>Create a new fee schedule.</summary>
    [HttpPost("schedules")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<FeeScheduleDto>> CreateSchedule(
        [FromBody] CreateFeeScheduleRequest request,
        CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        if (string.IsNullOrWhiteSpace(request.TermLabel)) return BadRequest("Term label is required.");
        if (string.IsNullOrWhiteSpace(request.AcademicYear)) return BadRequest("Academic year is required.");
        if (request.Amount <= 0) return BadRequest("Amount must be greater than zero.");

        var schedule = new TermFeeSchedule
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            TermLabel = request.TermLabel.Trim(),
            AcademicYear = request.AcademicYear.Trim(),
            GradeId = request.GradeId,
            ClassId = request.ClassId,
            Amount = request.Amount,
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _db.TermFeeSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        return Ok(new FeeScheduleDto(
            schedule.Id,
            schedule.TermLabel,
            schedule.AcademicYear,
            schedule.GradeId,
            null,
            schedule.ClassId,
            null,
            schedule.Amount,
            schedule.Description,
            schedule.IsActive,
            0,
            0));
    }

    /// <summary>Update a fee schedule.</summary>
    [HttpPut("schedules/{id:guid}")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<IActionResult> UpdateSchedule(Guid id, [FromBody] UpdateFeeScheduleRequest request, CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        var schedule = await _db.TermFeeSchedules.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId, ct);
        if (schedule is null) return NotFound();

        schedule.TermLabel = request.TermLabel.Trim();
        schedule.AcademicYear = request.AcademicYear.Trim();
        schedule.GradeId = request.GradeId;
        schedule.ClassId = request.ClassId;
        schedule.Amount = request.Amount;
        schedule.Description = request.Description?.Trim();
        schedule.IsActive = request.IsActive;
        schedule.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Delete a fee schedule (only if no payments are linked).</summary>
    [HttpDelete("schedules/{id:guid}")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        var schedule = await _db.TermFeeSchedules.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId, ct);
        if (schedule is null) return NotFound();

        var hasPayments = await _db.FeePaymentRecords.AnyAsync(x => x.ScheduleId == id, ct);
        if (hasPayments) return BadRequest("Cannot delete a fee schedule that has payment records. Deactivate it instead.");

        _db.TermFeeSchedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Payments — School Admin view
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>List all payment records for the school. Optionally filter by scheduleId or status.</summary>
    [HttpGet("payments")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<List<FeePaymentRowDto>>> ListPayments(
        [FromQuery] Guid? scheduleId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        IQueryable<FeePaymentRecord> query = _db.FeePaymentRecords
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId)
            .Include(x => x.Schedule)
            .Include(x => x.Student)
            .Include(x => x.Parent);

        if (scheduleId.HasValue)
            query = query.Where(x => x.ScheduleId == scheduleId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<FeePaymentStatus>(status, true, out var parsed))
            query = query.Where(x => x.Status == parsed);

        var records = await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

        return Ok(records.Select(ToRowDto).ToList());
    }

    /// <summary>School admin confirms a payment.</summary>
    [HttpPost("payments/{id:guid}/confirm")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<IActionResult> ConfirmPayment(Guid id, [FromBody] ConfirmPaymentRequest? request, CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        var record = await _db.FeePaymentRecords.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId, ct);
        if (record is null) return NotFound();

        record.Status = FeePaymentStatus.Confirmed;
        record.AdminNote = request?.AdminNote?.Trim();
        record.ConfirmedAtUtc = DateTime.UtcNow;
        record.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Payments — Parent view
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets fee overview for all of the calling parent's children.
    /// For each child, returns all relevant fee schedules (matched by grade/class),
    /// with existing payment records if any.
    /// </summary>
    [HttpGet("my-fees")]
    [Authorize(Roles = Roles.Parent)]
    public async Task<ActionResult<List<ParentChildFeeOverviewDto>>> GetMyFees(CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value;
        if (string.IsNullOrWhiteSpace(userEmail)) return Forbid();

        var parent = await _db.Parents
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SchoolId == schoolId && p.Email == userEmail, ct);

        if (parent is null) return Ok(new List<ParentChildFeeOverviewDto>());

        var children = await _db.StudentParents
            .AsNoTracking()
            .Where(sp => sp.ParentId == parent.Id)
            .Include(sp => sp.Student).ThenInclude(s => s.Class)
            .Include(sp => sp.Student).ThenInclude(s => s.Grade)
            .ToListAsync(ct);

        var schedules = await _db.TermFeeSchedules
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId && x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var studentIds = children.Select(c => c.StudentId).ToList();
        var existingPayments = await _db.FeePaymentRecords
            .AsNoTracking()
            .Where(x => x.SchoolId == schoolId && studentIds.Contains(x.StudentId))
            .ToListAsync(ct);

        var result = new List<ParentChildFeeOverviewDto>();

        foreach (var sp in children)
        {
            var student = sp.Student;

            // Find applicable schedules: school-wide (no grade/class), grade-level, or class-specific
            var applicable = schedules.Where(s =>
                (s.GradeId == null && s.ClassId == null) ||
                (s.GradeId == student.GradeId && s.ClassId == null) ||
                (s.ClassId == student.ClassId && student.ClassId != null) ||
                (s.GradeId == student.GradeId && s.ClassId == student.ClassId)).ToList();

            var feeItems = applicable.Select(s =>
            {
                var payment = existingPayments.FirstOrDefault(p => p.StudentId == student.Id && p.ScheduleId == s.Id);
                return new ParentFeeItemDto(
                    payment?.Id ?? Guid.Empty,
                    s.Id,
                    s.TermLabel,
                    s.AcademicYear,
                    s.Amount,
                    payment is null ? "NotSubmitted" : payment.Status.ToString(),
                    payment?.ReceiptFilePath,
                    payment?.ReceiptFileName,
                    payment?.ParentNote,
                    payment?.AdminNote,
                    payment?.SubmittedAtUtc,
                    payment?.ConfirmedAtUtc);
            }).ToList();

            result.Add(new ParentChildFeeOverviewDto(
                student.Id,
                $"{student.FirstName} {student.LastName}",
                student.AdmissionNumber,
                student.Class?.Name,
                student.Grade?.Name,
                feeItems));
        }

        return Ok(result);
    }

    /// <summary>
    /// Parent submits a payment claim (bank transfer or in-person).
    /// Creates a FeePaymentRecord if one does not already exist.
    /// </summary>
    [HttpPost("payments")]
    [Authorize(Roles = Roles.Parent)]
    public async Task<ActionResult<FeePaymentRowDto>> SubmitPayment(
        [FromBody] SubmitPaymentRequest request,
        CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value;
        var parent = await _db.Parents.FirstOrDefaultAsync(p => p.SchoolId == schoolId && p.Email == userEmail, ct);
        if (parent is null) return Forbid();

        // Verify student belongs to this parent
        var linked = await _db.StudentParents.AnyAsync(sp => sp.ParentId == parent.Id && sp.StudentId == request.StudentId, ct);
        if (!linked) return Forbid();

        var schedule = await _db.TermFeeSchedules.FirstOrDefaultAsync(x => x.Id == request.ScheduleId && x.SchoolId == schoolId, ct);
        if (schedule is null) return BadRequest("Fee schedule not found.");

        // Prevent duplicate submission
        var existing = await _db.FeePaymentRecords
            .FirstOrDefaultAsync(x => x.ScheduleId == request.ScheduleId && x.StudentId == request.StudentId, ct);

        FeePaymentRecord record;
        if (existing is not null)
        {
            // If already confirmed, don't allow re-submission
            if (existing.Status == FeePaymentStatus.Confirmed)
                return BadRequest("This fee has already been confirmed as paid.");

            existing.Status = request.IsInPerson ? FeePaymentStatus.InPersonPending : existing.Status;
            existing.ParentNote = request.ParentNote?.Trim();
            existing.SubmittedAtUtc = DateTime.UtcNow;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            record = existing;
        }
        else
        {
            record = new FeePaymentRecord
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                ScheduleId = request.ScheduleId,
                StudentId = request.StudentId,
                ParentId = parent.Id,
                Status = request.IsInPerson ? FeePaymentStatus.InPersonPending : FeePaymentStatus.Pending,
                ParentNote = request.ParentNote?.Trim(),
                SubmittedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
            };
            _db.FeePaymentRecords.Add(record);
        }

        await _db.SaveChangesAsync(ct);

        await _db.Entry(record).Reference(x => x.Schedule).LoadAsync(ct);
        await _db.Entry(record).Reference(x => x.Student).LoadAsync(ct);

        return Ok(ToRowDto(record));
    }

    /// <summary>
    /// Parent uploads a payment receipt for a specific fee payment record.
    /// </summary>
    [HttpPost("payments/{id:guid}/upload-receipt")]
    [Authorize(Roles = Roles.Parent)]
    [RequestSizeLimit(10_000_000)] // 10 MB
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadReceipt(Guid id, [FromForm] IFormFile file, CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value;
        var parent = await _db.Parents.FirstOrDefaultAsync(p => p.SchoolId == schoolId && p.Email == userEmail, ct);
        if (parent is null) return Forbid();

        var record = await _db.FeePaymentRecords
            .FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId && x.ParentId == parent.Id, ct);
        if (record is null) return NotFound();

        if (record.Status == FeePaymentStatus.Confirmed)
            return BadRequest("Payment already confirmed — no need to upload a receipt.");

        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        // Validate content type
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
        if (!allowed.Contains(file.ContentType?.ToLowerInvariant()))
            return BadRequest("Only JPEG, PNG, WebP, or PDF receipts are accepted.");

        var storedName = $"receipt-{record.Id:N}{Path.GetExtension(file.FileName)}";
        var relativePath = $"receipts/{schoolId}/{storedName}";

        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;
            await _fileStorage.UploadAsync(relativePath, ms, file.ContentType, ct);
        }

        _db.FileAssets.Add(new FileAsset
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            OriginalFileName = file.FileName,
            StoredFileName = storedName,
            RelativePath = relativePath,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            FileBytes = null,
            Category = "fee-receipt",
            UploadedBy = parent.Email,
            UploadedAtUtc = DateTime.UtcNow
        });

        record.ReceiptFilePath = relativePath;
        record.ReceiptFileName = file.FileName;
        record.Status = FeePaymentStatus.ReceiptUploaded;
        record.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new { path = relativePath });
    }

    /// <summary>
    /// Download a fee payment receipt (school admin or the parent who owns it).
    /// </summary>
    [HttpGet("receipts/{*relativePath}")]
    [Authorize(Roles = $"{Roles.SchoolAdmin},{Roles.Parent}")]
    public IActionResult DownloadReceipt(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return BadRequest("Path is required.");

        // Security: normalize and prevent traversal
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains(".."))
            return BadRequest("Invalid path.");

        string fullPath;
        try
        {
            fullPath = _fileStorage.ResolveReadPath(normalized);
        }
        catch
        {
            return NotFound();
        }

        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var mime = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };

        var stream = System.IO.File.OpenRead(fullPath);
        return File(stream, mime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Roster — school admin views which students paid per schedule
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns all students for a fee schedule with their payment status.</summary>
    [HttpGet("roster")]
    [Authorize(Roles = Roles.SchoolAdmin)]
    public async Task<ActionResult<List<StudentFeeRosterRow>>> GetRoster([FromQuery] Guid scheduleId, CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        var schedule = await _db.TermFeeSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == scheduleId && x.SchoolId == schoolId, ct);
        if (schedule is null) return NotFound();

        IQueryable<Student> studentQuery = _db.Students.AsNoTracking()
            .Where(s => s.SchoolId == schoolId)
            .Include(s => s.Class)
            .Include(s => s.Grade);

        if (schedule.ClassId.HasValue)
            studentQuery = studentQuery.Where(s => s.ClassId == schedule.ClassId.Value);
        else if (schedule.GradeId.HasValue)
            studentQuery = studentQuery.Where(s => s.GradeId == schedule.GradeId.Value);

        var students = await studentQuery.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync(ct);

        var studentIds = students.Select(s => s.Id).ToList();
        var payments = await _db.FeePaymentRecords.AsNoTracking()
            .Where(x => x.ScheduleId == scheduleId && studentIds.Contains(x.StudentId))
            .ToListAsync(ct);

        var paymentMap = payments.ToDictionary(p => p.StudentId);

        return Ok(students.Select(s =>
        {
            var p = paymentMap.GetValueOrDefault(s.Id);
            return new StudentFeeRosterRow(
                s.Id,
                $"{s.FirstName} {s.LastName}",
                s.AdmissionNumber,
                s.Class?.Name,
                s.Grade?.Name,
                p is null ? "NotSubmitted" : p.Status.ToString(),
                p?.ConfirmedAtUtc);
        }).ToList());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Class fee status — teacher views fee payment status for their class
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fee payment status for all students in a class across active schedules.
    /// Teachers must be assigned to the class; school admins have full access.
    /// </summary>
    [HttpGet("class-fee-status")]
    [Authorize(Roles = $"{Roles.Teacher},{Roles.SchoolAdmin}")]
    public async Task<ActionResult<object>> GetClassFeeStatus([FromQuery] Guid classId, CancellationToken ct)
    {
        var schoolId = GetSchoolId();
        if (schoolId == Guid.Empty) return Forbid();

        if (!User.IsInRole(Roles.SchoolAdmin))
        {
            var teacherEmail = User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;
            var teacher = await _db.Teachers.AsNoTracking()
                .FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.Email == teacherEmail, ct);
            if (teacher is null) return Forbid();
            var hasClass = await _db.TeacherClasses.AnyAsync(tc => tc.TeacherId == teacher.Id && tc.ClassId == classId, ct);
            if (!hasClass) return Forbid();
        }

        var students = await _db.Students.AsNoTracking()
            .Where(s => s.SchoolId == schoolId && s.ClassId == classId)
            .Include(s => s.Grade)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync(ct);

        var activeSchedules = await _db.TermFeeSchedules.AsNoTracking()
            .Where(x => x.SchoolId == schoolId && x.IsActive && (x.ClassId == classId || x.ClassId == null))
            .OrderBy(x => x.AcademicYear).ThenBy(x => x.TermLabel)
            .ToListAsync(ct);

        var studentIds = students.Select(s => s.Id).ToList();
        var scheduleIds = activeSchedules.Select(s => s.Id).ToList();
        var payments = await _db.FeePaymentRecords.AsNoTracking()
            .Where(x => x.SchoolId == schoolId && studentIds.Contains(x.StudentId) && scheduleIds.Contains(x.ScheduleId))
            .ToListAsync(ct);

        return Ok(new
        {
            schedules = activeSchedules.Select(s => new { s.Id, s.TermLabel, s.AcademicYear, s.Amount }).ToList(),
            students = students.Select(s => new
            {
                studentId = s.Id,
                studentName = $"{s.FirstName} {s.LastName}",
                admissionNumber = s.AdmissionNumber,
                gradeName = s.Grade?.Name,
                feeStatuses = activeSchedules.Select(sch =>
                {
                    var p = payments.FirstOrDefault(x => x.StudentId == s.Id && x.ScheduleId == sch.Id);
                    return new
                    {
                        scheduleId = sch.Id,
                        termLabel = sch.TermLabel,
                        academicYear = sch.AcademicYear,
                        amount = sch.Amount,
                        status = p is null ? "NotSubmitted" : p.Status.ToString(),
                        confirmedAt = p?.ConfirmedAtUtc,
                    };
                }).ToList(),
            }).ToList(),
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private Guid GetSchoolId() => _tenant.CurrentSchoolId ?? Guid.Empty;

    private static FeePaymentRowDto ToRowDto(FeePaymentRecord r) =>
        new(
            r.Id,
            r.ScheduleId,
            r.Schedule?.TermLabel ?? string.Empty,
            r.Schedule?.AcademicYear ?? string.Empty,
            r.StudentId,
            r.Student is not null ? $"{r.Student.FirstName} {r.Student.LastName}" : string.Empty,
            r.Student?.AdmissionNumber,
            r.ParentId,
            r.Parent is not null ? $"{r.Parent.FirstName} {r.Parent.LastName}" : null,
            r.Status.ToString(),
            r.Schedule?.Amount ?? 0,
            r.ReceiptFilePath,
            r.ReceiptFileName,
            r.ParentNote,
            r.AdminNote,
            r.SubmittedAtUtc,
            r.ConfirmedAtUtc);
}
