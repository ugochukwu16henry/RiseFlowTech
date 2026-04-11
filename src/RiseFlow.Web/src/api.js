/**
 * Shared API client for RiseFlow backend.
 * - Base URL from VITE_API_URL (set in Vercel / .env)
 * - Sends credentials (cookies) and X-Tenant-Id when set (required for tenant-scoped endpoints)
 */

export const API_BASE = (import.meta.env.VITE_API_URL || '').replace(/\/$/, '');
export const TENANT_HEADER = 'X-Tenant-Id';
export const STORAGE_TENANT_KEY = 'riseflow-tenant-id';
export const STORAGE_ONBOARDING_KEY = 'riseflow-onboarding-school';

export function getApiBase() {
  return API_BASE;
}

/** Default headers for authenticated, tenant-scoped requests. */
export function getApiHeaders() {
  const headers = {};
  try {
    const tenantId = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_TENANT_KEY) : null;
    if (tenantId) headers[TENANT_HEADER] = tenantId;
  } catch (_) {}
  return headers;
}

/** Fetch from backend: URL = API_BASE + path, with credentials and X-Tenant-Id when set. */
export function apiFetch(path, options = {}) {
  const url = path.startsWith('http') ? path : `${API_BASE}${path.startsWith('/') ? '' : '/'}${path}`;
  const { headers: userHeaders, skipTenantHeader = false, ...rest } = options;
  const headers = { ...getApiHeaders(), ...userHeaders };
  if (skipTenantHeader) {
    delete headers[TENANT_HEADER];
  }
  return fetch(url, { credentials: 'include', ...rest, headers }).catch((err) => {
    // Browser uses "Failed to fetch" / "Load failed" when CORS blocks, TLS/DNS fails, or mixed content.
    if (err instanceof TypeError && /failed to fetch|load failed|networkerror/i.test(String(err.message))) {
      const hint =
        API_BASE
          ? `Request to ${url} was blocked or unreachable. Confirm the API is up, uses HTTPS if the site does, and that Cors:AllowedOrigins on the API includes this page’s exact origin.`
          : `Request to ${url} failed. For production builds set VITE_API_URL to your API base URL (e.g. https://your-api.up.railway.app). Empty VITE_API_URL only works when the API is served on the same origin or via dev proxy.`;
      throw new Error(hint);
    }
    throw err;
  });
}
