namespace RiseFlow.Api.Models;

/// <summary>
/// One attendance item from a client (online or offline).
/// This is designed to be easy for a local SQLite-backed app to construct and sync.
/// </summary>
public record AttendanceUpsertItem(
    Guid StudentId,
    DateOnly Date,
    string Status,
    string? Period,
    string? Note,
    string? SourceDeviceId,
    DateTime ClientTimestampUtc
);

/// <summary>Batch payload from an offline/online client to upsert many attendance items at once.</summary>
public record AttendanceBatchUpsertRequest(
    IReadOnlyList<AttendanceUpsertItem> Items
);

/// <summary>Response giving the caller enough information to mark local rows as synced.</summary>
public record AttendanceBatchUpsertResponse(
    IReadOnlyList<AttendanceUpsertResultItem> Results
);

public record AttendanceUpsertResultItem(
    Guid StudentId,
    DateOnly Date,
    string? Period,
    string Status,
    bool Created,
    DateTime ServerTimestampUtc
);

