using FSH.Framework.Web;
using FSH.Modules.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace RiseFlow.Platform.Api;

/// <summary>
/// RiseFlow-branded entry points over the FullStackHero framework libraries in
/// <c>external/fullstackhero-dotnet-starter-kit</c>. Names are RiseFlow; behavior delegates to FSH
/// (<see cref="Extensions.AddHeroPlatform"/>, <see cref="Extensions.UseHeroPlatform"/>, tenant pipeline).
/// Renaming upstream namespaces inside the submodule would break updates—use these wrappers for product code.
/// </summary>
public static class RiseFlowPlatformHostingExtensions
{
    /// <inheritdoc cref="Extensions.AddHeroPlatform" />
    public static IHostApplicationBuilder AddRiseFlowPlatform(
        this IHostApplicationBuilder builder,
        Action<FshPlatformOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddHeroPlatform(configure);
    }

    /// <inheritdoc cref="Extensions.UseHeroPlatform" />
    public static WebApplication UseRiseFlowPlatform(
        this WebApplication app,
        Action<FshPipelineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseHeroPlatform(configure);
    }

    /// <summary>Finbuckle tenant resolution pipeline (FSH multitenancy module).</summary>
    public static WebApplication UseRiseFlowMultiTenantDatabases(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseHeroMultiTenantDatabases();
    }
}
