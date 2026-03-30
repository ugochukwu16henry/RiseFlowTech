# FullStackHero .NET Starter Kit — alignment with RiseFlow

Primary upstream reference is [fullstackhero/dotnet-starter-kit](https://github.com/fullstackhero/dotnet-starter-kit) ([`dotnet-starter-kit.git`](https://github.com/fullstackhero/dotnet-starter-kit.git)): **.NET 10**, **Blazor** + Tailwind admin UI, **Finbuckle** multitenancy, modular slices, Identity, auditing, Aspire, Hangfire, Redis cache, Mediator, OpenTelemetry, etc.

A second **reference clone** is [enkodellc/blazorboilerplate](https://github.com/enkodellc/blazorboilerplate) at `external/blazor-boilerplate` ([`blazorboilerplate.git`](https://github.com/enkodellc/blazorboilerplate.git)): **.NET 7**, **Blazor** + **MudBlazor**, Identity, Swagger, Serilog, optional SQL Server/SQLite/Postgres, dual WebAssembly / Server-Side Blazor. Use it like FSH — patterns and file-level comparison — **not** as a replacement for the RiseFlow React app unless you explicitly migrate the front end to Blazor.

## “Base Foundation” (SaaS boilerplate) — how that maps to RiseFlow

The [FullStackHero starter kit](https://github.com/fullstackhero/dotnet-starter-kit) advertises multi-tenancy, sidebar/dashboard UX, roles, modular architecture, audit visibility for admins, and a Tailwind UI. **Cloning the repo** gives you their full solution; **RiseFlow** reuses the *same building blocks* where they fit, without deleting your product.

| Foundation claim | In RiseFlow |
|------------------|-------------|
| Multi-tenancy (Finbuckle; schools isolated) | **Yes** — Finbuckle + `SchoolTenantStore` / `RiseFlowTenantStrategy` + EF `ITenantEntity` filters. |
| Sidebar / app shell | **React + Tailwind** — `PageLayout.jsx` (not Blazor; same UX pattern). |
| User roles | **Yes** — ASP.NET Core Identity + JWT and role claims. |
| Modular architecture | **Partial** — layered projects; **not** FSH’s `AddModules` / `UseHeroPlatform` plugin host (that would be a separate migration). |
| Audit logging (e.g. SuperAdmin visibility) | **Yes** — `AuditLog` / `IAuditLogService` / Super Admin endpoints. |
| Tailwind dashboard | **Yes** — Tailwind admin shell; marketing homepage stays separate unless you route it through the shell. |
| Full FSH stack (Aspire, Hangfire, Redis, Mediator, Blazor host, Scalar, etc.) | **Not all wired** — see table below; add per need. |

**Clone / fork:** this repo includes **git submodules** at `external/fullstackhero-dotnet-starter-kit` and `external/blazor-boilerplate` so upstream sits next to RiseFlow for comparison. A **GitHub fork** of either upstream is only needed if you contribute back or maintain a long-lived branch of *their* template.

## “Use everything” — what that actually means

You **cannot** drop the entire [dotnet-starter-kit](https://github.com/fullstackhero/dotnet-starter-kit) into RiseFlow in one step without **replacing** your architecture (Blazor host, module loader, Aspire, Hangfire storage, etc.). The right approach is **parity in layers**:

1. **Reference** — submodules under `external/` so you can diff and port patterns (FSH for modern .NET 10 / Tailwind admin; Blazor Boilerplate for MudBlazor-heavy examples and older community patterns).
2. **RiseFlow product** — React UI + `RiseFlow.Api` monolith; extend with the same *categories* of features those templates ship (tenancy, health, traces, jobs, cache) using the same *libraries* where it fits.

## Upstream code in this repo (git submodules)

### FullStackHero — `external/fullstackhero-dotnet-starter-kit`

- Remote: `https://github.com/fullstackhero/dotnet-starter-kit.git`

### Blazor Boilerplate — `external/blazor-boilerplate`

- Remote: `https://github.com/enkodellc/blazorboilerplate.git`
- **Note:** upstream targets **.NET 7** and **MudBlazor**; RiseFlow stays **React + .NET 10 API**. Borrow ideas (layout, API middleware, audit patterns); do not merge the whole solution without an intentional Blazor migration.

After clone, initialize submodules:

```bash
git submodule update --init --recursive
```

To bump FullStackHero:

```bash
cd external/fullstackhero-dotnet-starter-kit
git fetch origin
git checkout develop
git pull
cd ../..
git add external/fullstackhero-dotnet-starter-kit
git commit -m "Bump fullstackhero-dotnet-starter-kit submodule"
```

To bump Blazor Boilerplate (default branch is often `master`):

```bash
cd external/blazor-boilerplate
git fetch origin
git checkout master
git pull
cd ../..
git add external/blazor-boilerplate
git commit -m "Bump blazor-boilerplate submodule"
```

## Stack difference (important)

| FullStackHero | RiseFlow (this repo) |
|---------------|------------------------|
| Blazor + Tailwind dashboard | **React (Vite)** + Tailwind + `PageLayout` shell |
| `FSH.Api` + module loader + `AddHeroPlatform` | `RiseFlow.Api` (controllers, services) |
| Finbuckle + module DB helpers | Finbuckle + `X-Tenant-Id` / claims + EF `ITenantEntity` filters |

Use **FSH Blazor** and **Blazor Boilerplate** under `external/...` only as **layout / API / middleware references**; implement the product UI in React.

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
- Blazor Boilerplate reference: `external/blazor-boilerplate` (MudBlazor admin shell, server project patterns).
- RiseFlow shell: `src/RiseFlow.Web/src/components/PageLayout.jsx`, `tailwind.config.cjs`.

## References

- FullStackHero .NET Starter Kit: https://github.com/fullstackhero/dotnet-starter-kit  
- Clone: `https://github.com/fullstackhero/dotnet-starter-kit.git`
- Blazor Boilerplate: https://github.com/enkodellc/blazorboilerplate  
- Clone: `https://github.com/enkodellc/blazorboilerplate.git`
