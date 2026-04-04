using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RiseFlow.Platform.Api;

/// <summary>
/// Read-only migration health for the phased FSH → RiseFlow Platform strategy.
/// </summary>
public static class RiseFlowMigrationStatusEndpoint
{
    public const int CurrentPhase = 2;
    public const string CurrentPhaseName = "Platform host + FSH modules + School module (ping); legacy API unchanged";

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
                    SchoolModule: "RiseFlow.Modules.School (sample ping)"),
                NextSteps: new[]
                {
                    "Stabilize RiseFlow.Api (production).",
                    "Port auth/session contract tests against RiseFlow.Platform.Api.",
                    "Add RiseFlow.Modules.School persistence + first read-only queries from shared DB or replicated read model.",
                    "Point Vite proxy to Platform API behind a feature flag; then retire duplicate endpoints on RiseFlow.Api last.",
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
