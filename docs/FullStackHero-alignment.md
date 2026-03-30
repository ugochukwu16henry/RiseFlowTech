# FullStackHero .NET Starter Kit — alignment with RiseFlow

The upstream reference is [fullstackhero/dotnet-starter-kit](https://github.com/fullstackhero/dotnet-starter-kit) ([`dotnet-starter-kit.git`](https://github.com/fullstackhero/dotnet-starter-kit.git)): **.NET 10**, **Blazor** + Tailwind admin UI, **Finbuckle** multitenancy, modular slices, Identity, auditing, Aspire, Hangfire, Redis cache, Mediator, OpenTelemetry, etc.

## “Use everything” — what that actually means

You **cannot** drop the entire [dotnet-starter-kit](https://github.com/fullstackhero/dotnet-starter-kit) into RiseFlow in one step without **replacing** your architecture (Blazor host, module loader, Aspire, Hangfire storage, etc.). The right approach is **parity in layers**:

1. **Reference** — submodule at `external/fullstackhero-dotnet-starter-kit` so you can diff and port patterns.
2. **RiseFlow product** — React UI + `RiseFlow.Api` monolith; extend with the same *categories* of features FSH ships (tenancy, health, traces, jobs, cache) using the same *libraries* where it fits.

## Upstream code in this repo (git submodule)

- Path: `external/fullstackhero-dotnet-starter-kit`
- Remote: `https://github.com/fullstackhero/dotnet-starter-kit.git`

After clone, initialize submodules:

```bash
git submodule update --init --recursive
```

To bump upstream:

```bash
cd external/fullstackhero-dotnet-starter-kit
git fetch origin
git checkout develop
git pull
cd ../..
git add external/fullstackhero-dotnet-starter-kit
git commit -m "Bump fullstackhero-dotnet-starter-kit submodule"
```

## Stack difference (important)

| FullStackHero | RiseFlow (this repo) |
|---------------|------------------------|
| Blazor + Tailwind dashboard | **React (Vite)** + Tailwind + `PageLayout` shell |
| `FSH.Api` + module loader + `AddHeroPlatform` | `RiseFlow.Api` (controllers, services) |
| Finbuckle + module DB helpers | Finbuckle + `X-Tenant-Id` / claims + EF `ITenantEntity` filters |

Use **FSH Blazor** under `external/...` only as a **layout/visual reference**; implement the product UI in React.

## Backend parity in RiseFlow (implemented vs still optional)

| FullStackHero / starter-kit style | RiseFlow status |
|-----------------------------------|-----------------|
| .NET 10 | Yes — `RiseFlow.Api` → `net10.0` |
| Finbuckle multitenancy | Yes — `Program.cs`, `RiseFlowTenantStrategy`, `SchoolTenantStore` |
| Identity + roles | Yes |
| Auditing | Yes — `AuditLog` / `IAuditLogService` |
| **Health checks (DB)** | Yes — `AddHealthChecks().AddDbContextCheck<RiseFlowDbContext>()`, `MapHealthChecks("/health")` |
| **OpenTelemetry → OTLP** | Yes — ASP.NET + HttpClient traces; OTLP endpoint via `OpenTelemetry:OtlpEndpoint` or standard `OTEL_*` env vars; `OpenTelemetry:Enabled` (off in Development by default) |
| Hangfire jobs | Not yet — needs storage + hosting decision |
| Redis distributed cache | Not yet — add when you have Redis |
| Mediator / CQRS | Not yet — large refactor |
| Aspire AppHost | Not yet — optional parallel hosting story |
| API versioning + Scalar | Partial — Swagger today; versioning/Scalar optional |
| Blazor client | No — React is the client |

## OpenTelemetry configuration

- **Production / staging:** set `OTEL_EXPORTER_OTLP_ENDPOINT` (and related `OTEL_*` vars) or `OpenTelemetry:OtlpEndpoint` in configuration.
- **Local Development:** `appsettings.Development.json` sets `"OpenTelemetry": { "Enabled": false }` so you are not required to run a collector.

## UI parity (React)

- FSH reference: search `external/fullstackhero-dotnet-starter-kit` for layout/shell components.
- RiseFlow shell: `src/RiseFlow.Web/src/components/PageLayout.jsx`, `tailwind.config.cjs`.

## References

- FullStackHero .NET Starter Kit: https://github.com/fullstackhero/dotnet-starter-kit  
- Clone: `https://github.com/fullstackhero/dotnet-starter-kit.git`
