# RiseFlow product reads on Platform API — auth and tenancy

During phased migration, **`RiseFlow.Platform.Api`** may expose read-only endpoints that query the same database as **`RiseFlow.Api`** (via `RiseFlowDbContext`). Identity and multitenancy are not identical between the two hosts yet. This document sets expectations so new endpoints stay consistent.

## Two issuers (until cutover)

| Host | Typical JWT issuer / audience | Purpose |
|------|-------------------------------|---------|
| **`RiseFlow.Api`** | Product tokens your SPA and mobile clients use today | Canonical writes and most reads for the live product |
| **`RiseFlow.Platform.Api`** | FSH **`JwtOptions`** (e.g. `riseflow.platform`) | Platform modules (Identity, Multitenancy, Auditing, Webhooks) |

Do not assume a token from **`RiseFlow.Api`** validates on Platform without explicit configuration (shared signing key, matching validation parameters, and claim mapping). Prefer a **BFF** or **gateway** that issues the right token per surface until you unify issuers.

## Tenant vs school

- **FSH multitenancy** uses headers such as **`tenant`** / **`X-Tenant-Id`** (see `CorsOptions` on Platform) to route to tenant databases and identity context.
- **RiseFlow product data** is keyed by **`SchoolId`** (and related entities) in the legacy schema.

Product-read endpoints that aggregate **across** schools (for example `GET /api/v1/riseflow/school/product-stats`) intentionally avoid applying per-school filters from `ITenantContext` by using a `RiseFlowDbContext` constructor that does not inject tenant filters. Endpoints that expose **per-tenant** data must explicitly filter by **`SchoolId`** (or a mapped tenant id) once you define how FSH tenant id maps to `SchoolId`.

## Optional read key

Configuration **`RiseFlowProduct:ReadApiKey`** (non-empty) requires callers to send header **`X-RiseFlow-Product-Read-Key`** with the same value. Use this for internal dashboards, CI smoke tests, or staging before JWT rules are wired.

- If **`ReadApiKey`** is empty or unset, that check is skipped (endpoint remains anonymous from a key perspective; add **`[Authorize]`** or policy when you wire product JWT validation).

## Configuration

See **`RiseFlowProduct`** in `src/RiseFlow.Platform.Api/appsettings.json`:

- **`DatabaseProvider`**: `Sqlite` or `Npgsql` / `PostgreSQL` (match how **`RiseFlow.Api`** points at the same store).
- **`ConnectionStrings:Sqlite`** / **`ConnectionStrings:DefaultConnection`**: must resolve to the same logical database as the product API.
- **`Encryption:Key`**: if encrypted columns are read, use the same key as the product API (`RiseFlowProduct:Encryption:Key` or fallback `Encryption:Key` in shared config).

## Related

- `docs/PHASED_FSH_MIGRATION.md` — overall phase map and runbooks.
