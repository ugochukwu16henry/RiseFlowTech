using Mediator;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Data;
using RiseFlow.Modules.School.Contracts.v1.ProductStats;

namespace RiseFlow.Modules.School.Features.v1.ProductStats;

public sealed class GetSchoolProductStatsQueryHandler : IQueryHandler<GetSchoolProductStatsQuery, SchoolProductStatsDto>
{
    private readonly RiseFlowDbContext _db;

    public GetSchoolProductStatsQueryHandler(RiseFlowDbContext db)
    {
        _db = db;
    }

    public async ValueTask<SchoolProductStatsDto> Handle(GetSchoolProductStatsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var schools = await _db.Schools.AsNoTracking().CountAsync(s => s.IsActive, cancellationToken).ConfigureAwait(false);
        var students = await _db.Students.AsNoTracking().CountAsync(s => s.IsActive, cancellationToken).ConfigureAwait(false);
        var teachers = await _db.Teachers.AsNoTracking().CountAsync(s => s.IsActive, cancellationToken).ConfigureAwait(false);

        return new SchoolProductStatsDto(schools, students, teachers, DateTimeOffset.UtcNow);
    }
}
