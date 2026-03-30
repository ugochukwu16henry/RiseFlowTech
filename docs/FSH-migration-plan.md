# Migrating RiseFlow onto FullStackHero modules

This document describes the **structured migration** from the monolithic `RiseFlow.Api` (controllers + `RiseFlowDbContext` + `ApplicationUser` / `Guid`) to the [FullStackHero](https://github.com/fullstackhero/dotnet-starter-kit) modular host (`AddHeroPlatform`, `AddModules`, Mediator vertical slices, `FshUser` / `string`, tenant-per-connection patterns).

## Current state (Phase 0 — done)

| Piece | Location |
|-------|----------|
| **FSH-aligned host** | `src/RiseFlow.Platform.Api` — same startup shape as `FSH.Api`: Identity, Multitenancy, Auditing, Webhooks, Hangfire, Redis, etc. |
| **First RiseFlow module** | `src/Modules/RiseFlow.Modules.School` — `[FshModule(Order = 500)]`, `SchoolModule`, sample `GET /api/v1/riseflow/school/ping` |
| **Contracts** | `src/Modules/RiseFlow.Modules.School.Contracts` — queries/DTOs for the School vertical slice |
| **Legacy API** | `src/RiseFlow.Api` — unchanged; still the backend the React app uses until you cut over |

**Run the platform API:** `dotnet run --project src/RiseFlow.Platform.Api` (see `Properties/launchSettings.json`; default HTTP `http://localhost:5288`). Configure PostgreSQL, Redis, and JWT in `appsettings` like FSH.

**Mediator:** Only `RiseFlow.Platform.Api` references `Mediator.SourceGenerator`. Module projects use `Mediator.Abstractions` only so the host generates a single dispatcher (no duplicate generated types).

## Architectural constraints

1. **Two Identity models** — FSH uses `FshUser` / `string` and `IdentityDbContext`; RiseFlow uses `ApplicationUser` / `Guid` and `RiseFlowDbContext`. You cannot merge user stores without a **data migration** and a **single chosen model**.
2. **Two tenancy stories** — FSH provisions **per-tenant databases** and connection strings; RiseFlow uses **Finbuckle** + `SchoolId` filters on a shared database. Aligning them is a product decision (shared DB vs database-per-tenant).
3. **React** — Keep calling `RiseFlow.Api` until you switch `vite.config` (or env) to `RiseFlow.Platform.Api` and align routes (FSH uses `/api/v{version}/...`).

## Phased roadmap

### Phase 1 — Persistence boundary

- Introduce `RiseFlowSchoolDbContext` (or equivalent) inside `RiseFlow.Modules.School` **or** a small `RiseFlow.Modules.School.Persistence` project.
- Decide: **move** `RiseFlowDbContext` entities into the module piecemeal, or **map** from existing DB with EF configurations that match current tables.
- Add migrations under a dedicated migrations assembly (pattern: `FSH.Migrations.PostgreSQL` for FSH; you may add `RiseFlow.Migrations.PostgreSQL` for school tables only).

### Phase 2 — Identity and auth

- **Option A (recommended for FSH parity):** Treat FSH Identity as source of truth; add **profile / extension** tables keyed by `FshUser.Id` for school-specific fields (`SchoolId`, etc.); migrate users from `ApplicationUser` with a one-time script.
- **Option B:** Keep RiseFlow JWT issuance in `RiseFlow.Api` until Phase 4; only use Platform API for new endpoints (dual auth is painful).

### Phase 3 — Port controllers to vertical slices

For each area (e.g. `StudentsController`):

1. Add commands/queries + DTOs in `RiseFlow.Modules.School.Contracts`.
2. Add handlers + validators + `Map*Endpoint` under `Features/v1/...`.
3. Apply permissions using FSH patterns (`RequirePermission` / policies) once roles are mapped.
4. Remove or thin the old controller in `RiseFlow.Api` (or gate behind feature flag).

Suggested order: read-only list/detail endpoints first, then mutations, then file uploads and webhooks.

### Phase 4 — Cutover

- Point Vite proxy to `RiseFlow.Platform.Api`; update JWT settings and CORS.
- Decommission `RiseFlow.Api` or reduce it to a compatibility shim.
- Run load/QA on Hangfire jobs, Redis cache, and tenant provisioning.

### Phase 5 — Cleanup

- Delete duplicate Finbuckle wiring if fully on FSH multitenancy.
- Consolidate OpenTelemetry and health checks.
- Align package versions (e.g. `Microsoft.IdentityModel.Tokens`) to remove MSB3277 warnings on `RiseFlow.Platform.Api`.

## Reference

- FSH architecture: `external/fullstackhero-dotnet-starter-kit/docs/framework` (in submodule).
- Alignment notes: `docs/FullStackHero-alignment.md`.
