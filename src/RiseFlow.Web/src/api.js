/**
 * Shared API client for RiseFlow backend.
 * - Base URL from VITE_API_URL (set in Vercel / .env)
 * - Sends credentials (cookies) and X-Tenant-Id when set (required for tenant-scoped endpoints)
 */

export const API_BASE = (import.meta.env.VITE_API_URL || '').replace(/\/$/, '');
export const TENANT_HEADER = 'X-Tenant-Id';
export const STORAGE_TENANT_KEY = 'riseflow-tenant-id';

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
  const { headers: userHeaders, ...rest } = options;
  const headers = { ...getApiHeaders(), ...userHeaders };
  return fetch(url, { credentials: 'include', ...rest, headers });
}
