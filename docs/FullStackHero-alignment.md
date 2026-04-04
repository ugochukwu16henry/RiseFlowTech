# FullStackHero .NET Starter Kit — alignment with RiseFlow

Primary upstream reference is [fullstackhero/dotnet-starter-kit](https://github.com/fullstackhero/dotnet-starter-kit) ([`dotnet-starter-kit.git`](https://github.com/fullstackhero/dotnet-starter-kit.git)): **.NET 10**, **Blazor** + Tailwind admin UI, **Finbuckle** multitenancy, modular slices, Identity, auditing, Aspire, Hangfire, Redis cache, Mediator, OpenTelemetry, etc.

A second **reference clone** is [enkodellc/blazorboilerplate](https://github.com/enkodellc/blazorboilerplate) at `external/blazor-boilerplate` ([`blazorboilerplate.git`](https://github.com/enkodellc/blazorboilerplate.git)): **.NET 7**, **Blazor** + **MudBlazor**, Identity, Swagger, Serilog, optional SQL Server/SQLite/Postgres, dual WebAssembly / Server-Side Blazor. Use it like FSH — patterns and file-level comparison — **not** as a replacement for the RiseFlow React app unless you explicitly migrate the front end to Blazor.

A third **reference clone** is [pacheco4480/SchoolManagementSystem](https://github.com/pacheco4480/SchoolManagementSystem) at `external/school-management-system` ([`SchoolManagementSystem.git`](https://github.com/pacheco4480/SchoolManagementSystem.git)): **ASP.NET Core MVC**, **EF Core**, **SQL Server**, Identity roles (Admin, Staff, Student, etc.), CRUD for courses, subjects, teachers, students, **attendance**, **grades**, Bootstrap + Syncfusion ([project README](https://github.com/pacheco4480/SchoolManagementSystem/blob/master/README.md)). **“Use the whole system”** here means **reuse its domain and UX ideas** inside RiseFlow: multi-tenant `RiseFlow.Api` + React, not hosting their Razor app inside this repo. Compare controllers/repositories to our controllers and entities; port missing behaviors screen-by-screen.

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

**Clone / fork:** this repo includes **git submodules** at `external/fullstackhero-dotnet-starter-kit`, `external/blazor-boilerplate`, and `external/school-management-system` so upstream sits next to RiseFlow for comparison. A **GitHub fork** of any upstream is only needed if you contribute back or maintain a long-lived branch of *their* template.

**Structured migration (RiseFlow → FSH modules):** see **`docs/PHASED_FSH_MIGRATION.md`** (phases + runbooks) and **`docs/FSH-migration-plan.md`** (constraints) — `RiseFlow.Platform.Api` + `RiseFlow.Modules.School` are the first concrete steps; `RiseFlow.Api` remains until you complete Identity and endpoint porting.

## “Use everything” — what that actually means

You **cannot** drop the entire [dotnet-starter-kit](https://github.com/fullstackhero/dotnet-starter-kit) into RiseFlow in one step without **replacing** your architecture (Blazor host, module loader, Aspire, Hangfire storage, etc.). The right approach is **parity in layers**:

1. **Reference** — submodules under `external/` so you can diff and port patterns (FSH for modern .NET 10 / Tailwind admin; Blazor Boilerplate for MudBlazor-heavy examples; **SchoolManagementSystem** for school CRUD, attendance, and grade flows in classic MVC).
2. **RiseFlow product** — React UI + `RiseFlow.Api` monolith (Finbuckle tenants, `RiseFlowDbContext` already models students, teachers, classes, subjects, attendance, assessments, etc.); extend by **parity** with the MVC app where something is still missing in API or UI.

## Upstream code in this repo (git submodules)

### FullStackHero — `external/fullstackhero-dotnet-starter-kit`

- Remote: `https://github.com/fullstackhero/dotnet-starter-kit.git`

### Blazor Boilerplate — `external/blazor-boilerplate`

- Remote: `https://github.com/enkodellc/blazorboilerplate.git`
- **Note:** upstream targets **.NET 7** and **MudBlazor**; RiseFlow stays **React + .NET 10 API**. Borrow ideas (layout, API middleware, audit patterns); do not merge the whole solution without an intentional Blazor migration.

### School Management System (MVC) — `external/school-management-system`

- Remote: `https://github.com/pacheco4480/SchoolManagementSystem.git`
- **Stack:** MVC + Razor (not React). **Use:** diff their `Controllers`, `Models`, and repository logic against `RiseFlow.Api` endpoints and `Entities` when you want feature parity (e.g. attendance lists, grade entry, course/subject CRUD). **Do not** run this as the primary RiseFlow UI without a deliberate rewrite — React remains the client.

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

To bump School Management System (default branch is usually `master`):

```bash
cd external/school-management-system
git fetch origin
git checkout master
git pull
cd ../..
git add external/school-management-system
git commit -m "Bump school-management-system submodule"
```

## RiseFlowTech.slnx — full starter kit + RiseFlow in one workspace

The repo root **`RiseFlowTech.slnx`** includes **`src/RiseFlow.Api`**, **`src/RiseFlow.Web`**, the layered RiseFlow projects, and **`external/fullstackhero-dotnet-starter-kit/src/Playground/FSH.Api`** (the [FullStackHero](https://github.com/fullstackhero/dotnet-starter-kit) playground API: `AddHeroPlatform`, Identity, Multitenancy, Auditing, Webhooks, Hangfire, Redis, etc., per their [README](https://github.com/fullstackhero/dotnet-starter-kit/blob/develop/README.md)).

- **Build:** `dotnet build RiseFlowTech.slnx`
- **RiseFlow product (React homepage + app API):** `dotnet run --project src/RiseFlow.Api` — Vite (`src/RiseFlow.Web`) proxies `/api` to this host (default `http://localhost:5221` in `vite.config.js`). The marketing **homepage stays** in the React app; nothing switches the UI to FSH Blazor unless you choose to.
- **Full FSH host (everything in the starter kit):** configure Postgres, Redis, JWT, and mail as in FSH docs, then `dotnet run --project external/fullstackhero-dotnet-starter-kit/src/Playground/FSH.Api`. Optional: `dotnet run --project external/fullstackhero-dotnet-starter-kit/src/Playground/FSH.Playground.AppHost` for Aspire.

### Why FSH is not merged into `RiseFlow.Api` in one step

FSH’s **Identity** module uses **`FshUser` / `FshRole` (string keys)** and a dedicated **`IdentityDbContext`** wired through **`AddHeroPlatform` + `UseHeroMultiTenantDatabases`**. RiseFlow uses **`ApplicationUser` / `IdentityRole<Guid>`** and **`RiseFlowDbContext`** with school-domain entities. Registering both stacks in **one** process would duplicate Identity, JWT, and Finbuckle resolution. **Using the submodule + `FSH.Api` in the same solution** gives you the full framework to run and diff; **merging** requires a planned migration (one user model, one DbContext strategy, port of RiseFlow controllers into FSH modules).

### PostgreSQL for RiseFlow (aligned with FSH default engine)

`RiseFlow.Api` supports **`Database:Provider`** = `Sqlite` (default) or **`Npgsql`**. For PostgreSQL, set `Database:Provider` to **`Npgsql`**, set **`ConnectionStrings:DefaultConnection`**, and apply migrations against that database. Existing EF migrations target PostgreSQL-style types (`uuid`, etc.).

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
- School Management System reference: `external/school-management-system` (MVC screens and workflows to mirror in React).
- RiseFlow shell: `src/RiseFlow.Web/src/components/PageLayout.jsx`, `tailwind.config.cjs`.

## References

- FullStackHero .NET Starter Kit: https://github.com/fullstackhero/dotnet-starter-kit  
- Clone: `https://github.com/fullstackhero/dotnet-starter-kit.git`
- Blazor Boilerplate: https://github.com/enkodellc/blazorboilerplate  
- Clone: `https://github.com/enkodellc/blazorboilerplate.git`
- School Management System (MVC): https://github.com/pacheco4480/SchoolManagementSystem  
- Clone: `https://github.com/pacheco4480/SchoolManagementSystem.git`
