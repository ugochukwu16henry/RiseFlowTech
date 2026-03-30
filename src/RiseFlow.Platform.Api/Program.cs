using FSH.Framework.Web;
using FSH.Framework.Web.Modules;
using FSH.Modules.Auditing;
using FSH.Modules.Identity;
using FSH.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using FSH.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using FSH.Modules.Multitenancy;
using FSH.Modules.Multitenancy.Contracts.v1.GetTenantStatus;
using FSH.Modules.Multitenancy.Features.v1.GetTenantStatus;
using FSH.Modules.Webhooks;
using RiseFlow.Modules.School;
using RiseFlow.Modules.School.Contracts.v1.Ping;
using RiseFlow.Modules.School.Features.v1.Ping;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    static void Require(IConfiguration config, string key)
    {
        if (string.IsNullOrWhiteSpace(config[key]))
        {
            throw new InvalidOperationException($"Missing required configuration '{key}' in Production.");
        }
    }

    var config = builder.Configuration;
    Require(config, "DatabaseOptions:ConnectionString");
    Require(config, "CachingOptions:Redis");
    Require(config, "JwtOptions:SigningKey");
}

builder.Services.AddMediator(o =>
{
    o.ServiceLifetime = ServiceLifetime.Scoped;
    o.Assemblies =
    [
        typeof(GenerateTokenCommand).Assembly,
        typeof(GenerateTokenCommandHandler).Assembly,
        typeof(GetTenantStatusQuery).Assembly,
        typeof(GetTenantStatusQueryHandler).Assembly,
        typeof(FSH.Modules.Auditing.Contracts.AuditEnvelope).Assembly,
        typeof(FSH.Modules.Auditing.Persistence.AuditDbContext).Assembly,
        typeof(FSH.Modules.Webhooks.Contracts.v1.CreateWebhookSubscription.CreateWebhookSubscriptionCommand).Assembly,
        typeof(FSH.Modules.Webhooks.WebhooksModule).Assembly,
        typeof(GetSchoolPingQuery).Assembly,
        typeof(GetSchoolPingQueryHandler).Assembly,
    ];
});

var moduleAssemblies = new Assembly[]
{
    typeof(IdentityModule).Assembly,
    typeof(MultitenancyModule).Assembly,
    typeof(AuditingModule).Assembly,
    typeof(WebhooksModule).Assembly,
    typeof(SchoolModule).Assembly,
};

builder.AddHeroPlatform(o =>
{
    o.EnableCaching = true;
    o.EnableMailing = true;
    o.EnableJobs = true;
});

builder.AddModules(moduleAssemblies);
var app = builder.Build();

app.UseHeroMultiTenantDatabases();
app.UseHeroPlatform(p =>
{
    p.MapModules = true;
    p.ServeStaticFiles = true;
});

app.MapGet("/", () => Results.Ok(new { message = "RiseFlow Platform API (FSH + RiseFlow.Modules.School). See /scalar for docs." }))
   .WithTags("RiseFlow.Platform")
   .AllowAnonymous();
await app.RunAsync();
