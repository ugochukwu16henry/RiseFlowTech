# Railway PostgreSQL with RiseFlow.Api

## Which URL to use

| Variable | URL | When to use |
|----------|-----|-------------|
| **`DATABASE_URL`** | `postgresql://...@postgres.railway.internal:5432/railway` | **Inside Railway** (API service on the same project). Set automatically when you add the Postgres plugin. |
| **`DATABASE_PUBLIC_URL`** or **`ConnectionStrings:DefaultConnection`** | `postgresql://...@metro.proxy.rlwy.net:11801/railway` | **Your laptop / CI** running `dotnet ef database update`, or tools outside Railway’s private network. |

Do **not** commit real URLs or passwords to git. Use Railway **Variables** and optional local `appsettings.*.local.json` (gitignored).

## Railway service variables

On the **API** service in Railway:

1. `Database__Provider` = `Npgsql` (already default in `appsettings.json`).
2. **`DATABASE_URL`** — Railway usually injects this when Postgres is linked. Use the **internal** URL for the running API.
3. **`Encryption__Key`** — Base64 256-bit key for encrypted columns (generate once and keep stable per environment).

The API reads `DATABASE_URL` first, then `DATABASE_PUBLIC_URL`, then `ConnectionStrings:DefaultConnection` (see `DatabaseConnectionHelper`).

## Apply migrations from your machine

From the repo root (PowerShell):

```powershell
$env:DATABASE_PUBLIC_URL = "postgresql://postgres:YOUR_PASSWORD@metro.proxy.rlwy.net:11801/railway"
dotnet ef database update --project "src/RiseFlow.Api/RiseFlow.Api.csproj"
```

Or set `ConnectionStrings__DefaultConnection` to an Npgsql-style string instead of a URL.

Design-time (`RiseFlowDbContextFactory`) uses the same resolution as runtime, so `DATABASE_PUBLIC_URL` works for EF tools.

## After sharing credentials in chat

If a database password was exposed in Slack, email, or an AI chat, **rotate it** in Railway Postgres → **Connect** / credentials reset, then update all services and local env vars.
