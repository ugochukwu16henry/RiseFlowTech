using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RiseFlow.Modules.School.Contracts.v1.Ping;

namespace RiseFlow.Modules.School.Features.v1.Ping;

public static class GetSchoolPingEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder group)
    {
        return group.MapGet("/ping", async (IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new GetSchoolPingQuery(), cancellationToken).ConfigureAwait(false)))
            .WithName("GetSchoolPing")
            .WithSummary("Migration health check for the RiseFlow School module")
            .AllowAnonymous()
            .Produces<SchoolPingDto>(StatusCodes.Status200OK);
    }
}
