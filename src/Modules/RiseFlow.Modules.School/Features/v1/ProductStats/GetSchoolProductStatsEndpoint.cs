using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using RiseFlow.Modules.School.Contracts.v1.ProductStats;

namespace RiseFlow.Modules.School.Features.v1.ProductStats;

public static class GetSchoolProductStatsEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder group)
    {
        return group.MapGet("/product-stats", async (
                HttpContext http,
                IMediator mediator,
                IConfiguration config,
                CancellationToken cancellationToken) =>
            {
                var requiredKey = config["RiseFlowProduct:ReadApiKey"];
                if (!string.IsNullOrWhiteSpace(requiredKey))
                {
                    var sent = http.Request.Headers["X-RiseFlow-Product-Read-Key"].ToString();
                    if (!string.Equals(sent, requiredKey, StringComparison.Ordinal))
                        return Results.Unauthorized();
                }

                var dto = await mediator.Send(new GetSchoolProductStatsQuery(), cancellationToken).ConfigureAwait(false);
                return Results.Ok(dto);
            })
            .WithName("GetSchoolProductStats")
            .WithSummary("Read-only aggregates from the RiseFlow product database (legacy RiseFlow.Api schema)")
            .Produces<SchoolProductStatsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
