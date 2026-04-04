using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RiseFlow.Platform.Api;

/// <summary>
/// Read-only migration health for the phased FSH → RiseFlow Platform strategy.
/// </summary>
public static class RiseFlowMigrationStatusEndpoint
{
    public const int CurrentPhase = 3;
    public const string CurrentPhaseName = "Platform host + first read-only product data (GET /api/v1/riseflow/school/product-stats); legacy RiseFlow.Api remains canonical for writes";

    public static void MapRiseFlowMigrationStatus(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/riseflow/platform/migration-status", () =>
            Results.Ok(new RiseFlowMigrationStatusDto(
                Product: "RiseFlow",
                Host: "RiseFlow.Platform.Api",
                Phase: CurrentPhase,
                PhaseName: CurrentPhaseName,
                LegacyApi: "RiseFlow.Api",
                LegacyApiRemainsCanonicalForProduct: true,
                SubmoduleReference: "external/fullstackhero-dotnet-starter-kit",
                Components: new RiseFlowPlatformComponentsDto(
                    Identity: "FSH.Modules.Identity (engine; RiseFlow product still uses RiseFlow.Api Identity until cutover)",
                    Multitenancy: "FSH.Modules.Multitenancy",
                    Auditing: "FSH.Modules.Auditing",
                    Webhooks: "FSH.Modules.Webhooks",
                    SchoolModule: "RiseFlow.Modules.School (ping + product-stats from RiseFlowDbContext)"),
                NextSteps: new[]
                {
                    "Extract RiseFlowDbContext to RiseFlow.Persistence and drop RiseFlow.Modules.School → RiseFlow.Api project reference.",
                    "Align JWT claims (RiseFlow.Api vs FSH Identity) or BFF pattern; see docs/RISEFLOW_PRODUCT_API_AUTH.md.",
                    "Port more read-only school endpoints; then Vite proxy to Platform API behind a feature flag.",
                    "Retire duplicate endpoints on RiseFlow.Api last after cutover.",
                })))
            .WithTags("RiseFlow Platform")
            .WithSummary("Phased migration status (FSH engine under RiseFlow Platform host)")
            .AllowAnonymous();
    }
}

public sealed record RiseFlowMigrationStatusDto(
    string Product,
    string Host,
    int Phase,
    string PhaseName,
    string LegacyApi,
    bool LegacyApiRemainsCanonicalForProduct,
    string SubmoduleReference,
    RiseFlowPlatformComponentsDto Components,
    IReadOnlyList<string> NextSteps);

public sealed record RiseFlowPlatformComponentsDto(
    string Identity,
    string Multitenancy,
    string Auditing,
    string Webhooks,
    string SchoolModule);
