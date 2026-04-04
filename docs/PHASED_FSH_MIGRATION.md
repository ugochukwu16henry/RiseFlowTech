# Phased migration to FullStackHero architecture (RiseFlow)

**Decision:** Keep **`RiseFlow.Api`** running for the product while **`RiseFlow.Platform.Api`** grows as the FSH-aligned host. **No big-bang rewrite.**

Upstream reference: [fullstackhero/dotnet-starter-kit](https://github.com/fullstackhero/dotnet-starter-kit) (git submodule at `external/fullstackhero-dotnet-starter-kit`).

**Branding:** Product code uses **RiseFlow** names (`AddRiseFlowPlatform`, `UseRiseFlowPlatform`, `UseRiseFlowMultiTenantDatabases`). Framework types inside the submodule stay **`FSH.*`** so submodule updates keep working—do not rename namespaces inside `external/` without a fork.

---

## Phase map (current target: **Phase 2**)

| Phase | Goal | Status |
|-------|------|--------|
| **0** | Submodule + `RiseFlow.Platform.Api` + `RiseFlow.Modules.School` ping | Done |
| **1** | Stabilize `RiseFlow.Api` (health, OTel, migrations) | Ongoing |
| **2** | Platform host + Identity / Multitenancy / Auditing / Webhooks (FSH modules) + School module shell | **Current** — see `/api/v1/riseflow/platform/migration-status` on Platform API |
| **3** | Persistence boundary for school data (read models or shared DB strategy) | Planned |
| **4** | Identity parity + token contract tests (`ApplicationUser`/Guid → FSH patterns or bridge) | Planned |
| **5** | Port endpoints module-by-module; feature-flag Vite proxy to Platform API | Planned |
| **6** | Frontend default → Platform API; retire duplicate routes on `RiseFlow.Api` | Planned |
| **7** | Decommission monolith API or keep thin compatibility shim | Planned |

---

## Runbooks

**Product (today):** `dotnet run --project src/RiseFlow.Api` — React (`src/RiseFlow.Web`) proxies `/api` here.

**Platform (migration host):** `dotnet run --project src/RiseFlow.Platform.Api` — configure Postgres, Redis, JWT in `appsettings` (see `src/RiseFlow.Platform.Api/appsettings.json`).

**Check migration status (JSON):** `GET http://localhost:<platform-port>/api/v1/riseflow/platform/migration-status`

---

## Code locations

| Piece | Path |
|-------|------|
| RiseFlow-branded host extensions | `src/RiseFlow.Platform.Api/RiseFlowPlatformHostingExtensions.cs` |
| Migration status endpoint | `src/RiseFlow.Platform.Api/RiseFlowMigrationStatusEndpoint.cs` |
| School module | `src/Modules/RiseFlow.Modules.School/` |
| Legacy API | `src/RiseFlow.Api/` |

---

## Related docs

- `docs/FSH-migration-plan.md` — technical constraints (dual Identity models, tenancy).
- `docs/FullStackHero-alignment.md` — stack comparison table.
