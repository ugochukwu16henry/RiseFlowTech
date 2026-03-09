# Railway environment variables (RiseFlow API)

Set these in your Railway service **Variables** (or in **Settings → Environment**).  
ASP.NET Core maps `__` (double underscore) to nested config keys.

## Required

| Variable | Description | Example |
|----------|-------------|---------|
| **DATABASE_URL** | Postgres connection URL. Auto-set when you **link the Postgres plugin** to this service. | `postgresql://postgres:xxx@postgres.railway.internal:5432/railway` |

## Optional (recommended for production)

| Variable | Description | Example |
|----------|-------------|---------|
| **Cors__AllowedOrigins** | Allowed frontend origins (comma-separated). Required if frontend is on another domain (e.g. Vercel). | `https://riseflow.vercel.app,https://www.riseflow.com` |
| **ASPNETCORE_ENVIRONMENT** | Environment name. | `Production` |
| **RiseFlow__VerificationBaseUrl** | Base URL for transcript verification links (e.g. your frontend). | `https://riseflow.vercel.app/verify` |

## Auto-set by Railway

| Variable | Description |
|----------|-------------|
| **PORT** | Port the app must listen on. The API is already configured to use this. |
| **DATABASE_URL** | Set automatically when Postgres is linked to the service. |

## Notes

- **CORS**: If your frontend (e.g. on Vercel) calls this API, add its origin to `Cors__AllowedOrigins`.
- **Database**: Link the Postgres service in Railway so `DATABASE_URL` is injected; no need to add it manually.
- **Local vs Railway**: Locally set `DATABASE_PUBLIC_URL` in your environment (or in a local `.env` that is not committed) or use `DefaultConnection` in appsettings; on Railway use the linked Postgres (DATABASE_URL). **Do not commit database URLs to Git.**
