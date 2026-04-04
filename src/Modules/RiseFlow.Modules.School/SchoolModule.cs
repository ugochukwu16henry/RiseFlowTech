using Asp.Versioning;
using FSH.Framework.Web.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using RiseFlow.Modules.School.Features.v1.Ping;
using RiseFlow.Modules.School.Features.v1.ProductStats;

namespace RiseFlow.Modules.School;

public sealed class SchoolModule : IModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        // Phase 2+: register RiseFlowSchoolDbContext, application services, outbox.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints.MapGroup("api/v{version:apiVersion}/riseflow/school")
            .WithTags("RiseFlow School")
            .WithApiVersionSet(versionSet);

        GetSchoolPingEndpoint.Map(group);
        GetSchoolProductStatsEndpoint.Map(group);
    }
}
