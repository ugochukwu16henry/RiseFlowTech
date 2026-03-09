# Frontend ↔ Backend API Links

All frontend API calls use the shared `api.js` helper: base URL from **VITE_API_URL**, **credentials: 'include'**, and **X-Tenant-Id** when set in `localStorage` (key: `riseflow-tenant-id`). The backend requires **X-Tenant-Id** (school GUID) for tenant-scoped endpoints.

## Endpoints in use

| Frontend call | Backend route | Auth | Notes |
|---------------|---------------|------|--------|
| `GET /verify/transcript/:token` | `GET /verify/transcript/{token}` | Anonymous | Transcript verification (QR scan). |
| `POST /api/schools/onboard-with-logo` | `POST /api/schools/onboard-with-logo` | Anonymous | School registration + logo (FormData). |
| `GET /api/results/my-children` | `GET /api/results/my-children` | Parent | Results for parent's linked children. |
| `GET /api/contacts/teachers` | `GET /api/contacts/teachers` | Parent | Teachers for parent's children's classes. |
| `GET /api/schools/dashboard` | `GET /api/schools/dashboard` | SchoolAdmin | Active students, unpaid fees. |
| `GET /api/superadmin/dashboard` | `GET /api/superadmin/dashboard` | SuperAdmin | Platform stats, revenue, schools by country. |
| `GET /api/students/bulk-upload-template` | `GET /api/students/bulk-upload-template` | Anonymous | Download Excel template (form action). |

## Tenant header

For **SchoolAdmin**, **Parent**, **Teacher**, and **Student** flows the backend expects **X-Tenant-Id** = current school ID (GUID). After login, set:

```js
localStorage.setItem('riseflow-tenant-id', schoolId);
```

The `apiFetch()` helper sends this header on every request. **SuperAdmin** endpoints may not require tenant (platform-wide). **Anonymous** endpoints (onboard, verify, bulk-upload-template) do not need it.

## Backend CORS

Ensure **Cors__AllowedOrigins** on the backend includes your frontend origin (e.g. `https://your-app.vercel.app`) so browser allows these requests.
