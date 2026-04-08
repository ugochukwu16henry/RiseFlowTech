/**
 * Shared API client for RiseFlow backend.
 * - Base URL from VITE_API_URL (set in Vercel / .env)
 * - Sends credentials (cookies) and X-Tenant-Id when set (required for tenant-scoped endpoints)
 */

export const API_BASE = (import.meta.env.VITE_API_URL || '').replace(/\/$/, '');
export const TENANT_HEADER = 'X-Tenant-Id';
export const STORAGE_TENANT_KEY = 'riseflow-tenant-id';
export const STORAGE_ONBOARDING_KEY = 'riseflow-onboarding-school';

export function clearAuthStorage() {
  try {
    if (typeof localStorage === 'undefined') return;
    [
      STORAGE_TENANT_KEY,
      STORAGE_ONBOARDING_KEY,
      'riseflow-preview-role',
      'riseflow-cache-my-children',
    ].forEach((key) => {
      localStorage.removeItem(key);
    });
  } catch (_) {}
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
  const isAuthRequest = typeof path === 'string' && /\/api\/auth\/(login|logout)$/i.test(path);
  const headers = { ...getApiHeaders(), ...userHeaders };
  if (skipTenantHeader || isAuthRequest) {
    delete headers[TENANT_HEADER];
  }
  const fetchOptions = { credentials: 'include', ...rest, headers };
  if (isAuthRequest && !fetchOptions.cache) {
    fetchOptions.cache = 'no-store';
  }
  return fetch(url, fetchOptions);
}
