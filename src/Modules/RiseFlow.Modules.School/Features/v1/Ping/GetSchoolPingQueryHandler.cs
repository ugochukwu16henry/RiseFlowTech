using Mediator;
using RiseFlow.Modules.School.Contracts.v1.Ping;

namespace RiseFlow.Modules.School.Features.v1.Ping;

public sealed class GetSchoolPingQueryHandler : IQueryHandler<GetSchoolPingQuery, SchoolPingDto>
{
    public ValueTask<SchoolPingDto> Handle(GetSchoolPingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ValueTask.FromResult(new SchoolPingDto(
            "RiseFlow school vertical slice is active. Port controllers here as Mediator features.",
            "RiseFlow.Modules.School"));
    }
}
