# FullStackHero .NET Starter Kit — alignment with RiseFlow

The upstream reference is [fullstackhero/dotnet-starter-kit](https://github.com/fullstackhero/dotnet-starter-kit) ([`dotnet-starter-kit.git`](https://github.com/fullstackhero/dotnet-starter-kit.git)): **.NET 10**, **Blazor** + Tailwind admin UI, **Finbuckle** multitenancy, modular slices, Identity, auditing, Aspire, etc.

RiseFlow **does not replace** that repo with your app. You keep RiseFlow as the product; FullStackHero lives beside it for **design and API patterns**.

## Upstream code in this repo (git submodule)

A **submodule** pins the exact upstream tree so you can diff, search, and copy ideas without merging unrelated history into RiseFlow’s main line:

- Path: `external/fullstackhero-dotnet-starter-kit`
- Remote: `https://github.com/fullstackhero/dotnet-starter-kit.git`

After clone, initialize submodules:

```bash
git submodule update --init --recursive
```

To move upstream forward (when you choose):

```bash
cd external/fullstackhero-dotnet-starter-kit
git fetch origin
git checkout develop   # or the branch you track
git pull
cd ../..
git add external/fullstackhero-dotnet-starter-kit
git commit -m "Bump fullstackhero-dotnet-starter-kit submodule"
```

**Fork vs submodule:** A **GitHub fork** of FullStackHero is only needed if you plan to **contribute upstream** or maintain a long-lived private branch of *their* template. For RiseFlow, a **submodule** (or a second clone outside the repo) is usually enough.

## Stack difference (important)

| FullStackHero | RiseFlow (this repo) |
|---------------|------------------------|
| Blazor + Tailwind dashboard | **React (Vite)** + Tailwind + `PageLayout` shell |
| `FSH.Api` + module loader | `RiseFlow.Api` (controllers, services) |
| Finbuckle + tenant-aware hosts | Finbuckle + `X-Tenant-Id` / claims + EF `ITenantEntity` filters |

You **do not** merge the Blazor UI into your React app. Use **FSH Blazor** under `external/...` as a **visual and layout reference** (sidebar, spacing, primary color), then mirror those patterns in React.

## UI parity (React, not Blazor)

- **Where to look in FSH:** Blazor layout/shell components under `external/fullstackhero-dotnet-starter-kit` (search for `MainLayout`, `NavMenu`, or similar in their `src` tree after submodule init).
- **Where RiseFlow implements the shell:** `src/RiseFlow.Web/src/components/PageLayout.jsx`, Tailwind in `tailwind.config.cjs` (primary palette + shell shadow aligned with starter-kit-style admin shells).

Homepage and marketing routes stay as you define them; only **app** routes use the shared shell.

## Backend parity (already in RiseFlow)

| FullStackHero idea | RiseFlow location |
|--------------------|-------------------|
| .NET 10 API | `src/RiseFlow.Api/RiseFlow.Api.csproj` → `net10.0` |
| Finbuckle | `Finbuckle.MultiTenant` packages; `Program.cs` → `AddMultiTenant` / `UseMultiTenant` |
| Tenant resolution | `Services/RiseFlowTenantStrategy.cs`, `Middleware/TenantMiddleware.cs` |
| Tenant store | `Services/SchoolTenantInfo.cs`, `Services/SchoolTenantStore.cs` |
| Per-request tenant for EF | `ITenantService` / `TenantService`, `ITenantEntity` + `RiseFlowDbContext` filters |
| Auditing | `AuditLog`, `IAuditLogService`, Super Admin audit APIs |

## When to copy more from FSH

- **Mediator / FluentValidation / Hangfire / OTel / Aspire:** add **incrementally** when a feature needs them.
- **Blazor as a second client:** only if you explicitly want two UIs (large ongoing cost).

## References

- FullStackHero .NET Starter Kit: https://github.com/fullstackhero/dotnet-starter-kit  
- Clone URL: `https://github.com/fullstackhero/dotnet-starter-kit.git`
