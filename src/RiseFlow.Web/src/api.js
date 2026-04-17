/**
 * Shared API client for RiseFlow backend.
 * - Base URL from VITE_API_URL (set in Vercel / .env)
 * - Sends credentials (cookies) and X-Tenant-Id when set (required for tenant-scoped endpoints)
 *
 * Dev note: `npm run dev` serves on :5173. Any non-empty VITE_API_URL (including your Railway URL) makes the
 * browser call that host directly → cross-origin, cookies on :5173 won’t go to Railway, and CORS can fail
 * (“Could not reach the API”). In development we therefore ignore VITE_API_URL and use same-origin + Vite
 * proxy to http://127.0.0.1:5221. Set VITE_API_URL_FORCE=1 only to test the deployed API from localhost.
 */

function resolveApiBase() {
  const raw = (import.meta.env.VITE_API_URL || '').trim().replace(/\/$/, '');
  if (typeof window === 'undefined') return raw;

  const forceRemote =
    import.meta.env.VITE_API_URL_FORCE === '1' || import.meta.env.VITE_API_URL_FORCE === 'true';

  const host = window.location.hostname.toLowerCase();
  const preferSameOrigin = host === 'localhost'
    || host === '127.0.0.1'
    || host === '::1'
    || host.endsWith('.vercel.app')
    || host === 'riseflow.com'
    || host === 'www.riseflow.com';

  // Local dev should use the Vite proxy. On deployed web hosts, prefer same-origin `/api`
  // so auth cookies stay first-party via the hosting rewrite/proxy instead of cross-site.
  if (!forceRemote && (import.meta.env.DEV || preferSameOrigin)) {
    return '';
  }

  return raw;
}

export const API_BASE = resolveApiBase();
export const TENANT_HEADER = 'X-Tenant-Id';
export const STORAGE_TENANT_KEY = 'riseflow-tenant-id';
export const STORAGE_ONBOARDING_KEY = 'riseflow-onboarding-school';
export const SESSION_EXPIRED_BROADCAST_KEY = 'riseflow-session-expired-at';

function isPublicPath(pathname) {
  return pathname === '/'
    || pathname === '/login'
    || pathname === '/onboard'
    || pathname === '/terms'
    || pathname === '/privacy'
    || pathname.startsWith('/verify/transcript/')
    || pathname.startsWith('/affiliate')
    // Self-signup / claim flows: anonymous users hit authenticated endpoints (e.g. branding) → 401 must not redirect to login
    || pathname === '/parent/signup'
    || pathname === '/parent/claim'
    || pathname === '/teacher/signup'
    || pathname === '/staff/signup';
}

function handleSessionExpiredRedirect() {
  if (typeof window === 'undefined') return;
  const currentPath = window.location.pathname || '/';
  if (isPublicPath(currentPath)) return;

  try {
    localStorage.removeItem(STORAGE_TENANT_KEY);
    localStorage.setItem(SESSION_EXPIRED_BROADCAST_KEY, String(Date.now()));
  } catch {
    // ignore storage errors
  }

  const next = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
  const target = `/login?reason=session_expired&next=${next}`;
  if (`${window.location.pathname}${window.location.search}` !== target) {
    window.location.assign(target);
  }
}

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
  return fetch(url, { credentials: 'include', ...rest, headers })
    .then((res) => {
      const authPath = typeof path === 'string' && path.startsWith('/api/auth/login');
      const expired = res.status === 401 || res.status === 419;
      if (expired && !authPath) {
        handleSessionExpiredRedirect();
      }
      return res;
    })
    .catch((err) => {
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
