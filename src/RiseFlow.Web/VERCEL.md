# Deploy RiseFlow Web to Vercel

## 1. Connect the repo

- In [Vercel](https://vercel.com), **Add New Project** and import your Git repository.
- Set **Root Directory** to **`src/RiseFlow.Web`** (this frontend lives in a subfolder).

## 2. Build settings

Vercel usually detects Vite; `vercel.json` in this folder sets:

- **Build Command:** `npm run build`
- **Output Directory:** `dist`
- **Framework:** Vite

SPA routing is handled by rewrites so client-side routes (e.g. `/login`, `/onboard`) work.

## 3. Environment variable (required for production)

Add this in **Project → Settings → Environment Variables**:

| Name            | Value                    | Environments  |
|-----------------|--------------------------|---------------|
| **VITE_API_URL** | Your backend API URL     | Production, Preview |

- **Value:** Your Railway backend URL, **no trailing slash**, e.g.  
  `https://riseflow-api-production.up.railway.app`
- Without this, the app will call the API with a relative URL and production requests will fail.

## 4. Backend CORS

On the **Railway** backend, set **Cors__AllowedOrigins** to include your Vercel URL, e.g.:

- `https://your-project.vercel.app`
- Or your custom domain.

## 5. Deploy

Push to your connected branch or use **Redeploy** in Vercel. The frontend will use `VITE_API_URL` at build time to talk to your Railway API.
