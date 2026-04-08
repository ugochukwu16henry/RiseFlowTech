import { useEffect, useState } from 'react';
import { NavLink, Link } from 'react-router-dom';
import { apiFetch, clearAuthStorage, getApiBase, STORAGE_TENANT_KEY } from '../api';

/** Preset sidebar links (multi-tenant SaaS shell — one school’s data never mixed with another’s at the API). */
const NAV_BY_ROLE = {
  school: [
    { to: '/school', label: 'Dashboard', end: true },
    { to: '/school/students', label: 'People' },
    { to: '/school/billing', label: 'Billing' },
    { to: '/school/reports', label: 'Reports' },
    { to: '/school/import', label: 'Import' },
    { to: '/school/access-codes', label: 'Access codes' },
  ],
  super: [
    { to: '/super-admin', label: 'Control room', end: true },
    { to: '/super-admin/schools', label: 'Schools' },
    { to: '/super-admin/revenue', label: 'Revenue' },
    { to: '/super-admin/compliance', label: 'System settings' },
    { to: '/super-admin/data-offboarding', label: 'Data offboarding' },
  ],
  teacher: [{ to: '/teacher', label: 'Dashboard', end: true }],
  parent: [
    { to: '/parent', label: 'Family', end: true },
    { to: '/parent/claim', label: 'Claim child' },
  ],
  student: [{ to: '/student', label: 'My results', end: true }],
  legal: [
    { to: '/', label: 'Home' },
    { to: '/login', label: 'Sign in' },
    { to: '/terms', label: 'Terms' },
    { to: '/privacy', label: 'Privacy' },
  ],
};

function navClass({ isActive }) {
  return [
    'block rounded-r-lg pl-3 pr-3 py-2.5 text-sm font-medium transition-colors border-l-[3px]',
    isActive
      ? 'border-primary-600 bg-indigo-50/90 text-primary-800 dark:border-primary-400 dark:bg-indigo-950/40 dark:text-indigo-100'
      : 'border-transparent text-slate-600 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800/80',
  ].join(' ');
}

function buildPublicUrl(relativePath) {
  if (!relativePath) return null;
  if (relativePath.startsWith('http://') || relativePath.startsWith('https://')) return relativePath;
  const normalizedPath = relativePath.replace(/^\/+/, '');
  const base = getApiBase();
  return base ? `${base}/${normalizedPath}` : `/${normalizedPath}`;
}

function getBrandInitials(name) {
  return (name || 'School')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('') || 'SC';
}

/**
 * Shared app shell: sidebar + top bar (homepage stays standalone elsewhere).
 * @param {'app'|'auth'} variant — app = full dashboard; auth = slim header for login/onboarding
 * @param {'school'|'super'|'teacher'|'parent'|'student'|'legal'|undefined} role — sidebar links; omit for empty aside
 */
export default function PageLayout({
  title,
  children,
  backTo,
  role,
  variant = 'app',
  showSignOut,
  authHeaderRight,
}) {
  const items = role ? NAV_BY_ROLE[role] : null;
  const showSignOutButton = showSignOut ?? variant === 'app';
  const [schoolBrand, setSchoolBrand] = useState(null);
  const [logoFailed, setLogoFailed] = useState(false);
  const usesPlatformBrand = !role || role === 'super' || role === 'legal';

  useEffect(() => {
    let cancelled = false;
    setLogoFailed(false);

    if (variant !== 'app' || usesPlatformBrand) {
      setSchoolBrand(null);
      return undefined;
    }

    let schoolId = null;
    try {
      schoolId = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_TENANT_KEY) : null;
    } catch {
      schoolId = null;
    }

    if (!schoolId) {
      setSchoolBrand(null);
      return undefined;
    }

    apiFetch(`/api/schools/${schoolId}`)
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (!cancelled && data) {
          setSchoolBrand({
            name: data.name || data.schoolName || 'School',
            logo: buildPublicUrl(data.logoFileName),
          });
        }
      })
      .catch(() => {
        if (!cancelled) setSchoolBrand(null);
      });

    return () => { cancelled = true; };
  }, [usesPlatformBrand, variant]);

  const brandName = usesPlatformBrand
    ? 'RiseFlow'
    : (schoolBrand?.name || 'School Portal');
  const brandTagline = usesPlatformBrand
    ? 'School OS'
    : role === 'school'
      ? 'Admin dashboard'
      : role === 'teacher'
        ? 'Teacher workspace'
        : role === 'parent'
          ? 'Family portal'
          : role === 'student'
            ? 'Student portal'
            : 'Dashboard';
  const brandInitials = usesPlatformBrand ? 'RF' : getBrandInitials(brandName);
  const brandLogo = usesPlatformBrand ? null : schoolBrand?.logo;

  async function handleSignOut() {
    try {
      await apiFetch('/api/auth/logout', { method: 'POST', skipTenantHeader: true });
    } catch {
      // best effort
    }
    clearAuthStorage();
    window.location.replace('/login');
  }

  if (variant === 'auth') {
    return (
      <div className="min-h-screen bg-slate-100/90 text-slate-900 dark:bg-slate-950 dark:text-slate-100 flex flex-col">
        <header className="border-b border-slate-200/90 bg-white/95 shadow-shell backdrop-blur-sm dark:border-slate-800 dark:bg-slate-900/95">
          <div className="mx-auto flex max-w-6xl items-center justify-between gap-3 px-4 py-3">
            <Link to="/" className="flex items-center gap-2 text-slate-900 dark:text-slate-100">
              <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-primary-600 to-primary-800 text-xs font-bold text-white shadow-sm">
                RF
              </span>
              <span className="text-sm font-semibold">RiseFlow</span>
            </Link>
            <div className="flex items-center gap-2 text-sm">
              {authHeaderRight}
            </div>
          </div>
        </header>
        <div className="flex-1">{children}</div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-100/90 text-slate-900 dark:bg-slate-950 dark:text-slate-100 flex">
      <aside className="hidden md:flex md:w-60 lg:w-64 flex-col border-r border-slate-200/90 bg-white shadow-shell dark:border-slate-800 dark:bg-slate-900 dark:shadow-none">
        <div className="flex items-center gap-2 px-4 pt-4 pb-3 border-b border-slate-100 dark:border-slate-800">
          {brandLogo && !logoFailed ? (
            <img
              src={brandLogo}
              alt={brandName}
              className="h-9 w-9 rounded-xl border border-slate-200 bg-white object-contain p-1 shadow-sm dark:border-slate-700 dark:bg-slate-950"
              loading="lazy"
              onError={() => setLogoFailed(true)}
            />
          ) : (
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-primary-600 to-primary-800 text-xs font-bold text-white shadow-sm">
              {brandInitials}
            </div>
          )}
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold tracking-tight">{brandName}</p>
            <p className="text-[11px] text-slate-500 dark:text-slate-400">{brandTagline}</p>
          </div>
        </div>
        <nav className="flex-1 overflow-y-auto px-2 py-3 space-y-0.5" aria-label="App">
          {items?.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={navClass}>
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="px-4 pb-4 mt-auto text-[11px] text-slate-400 dark:text-slate-500">
          <p>© {new Date().getFullYear()} RiseFlow</p>
        </div>
      </aside>

      <div className="flex-1 flex flex-col min-w-0">
        <header className="border-b border-slate-200/90 bg-white/95 shadow-shell backdrop-blur-sm dark:border-slate-800 dark:bg-slate-900/95">
          <div className="mx-auto max-w-6xl px-4 py-3 flex flex-wrap items-center justify-between gap-3">
            <div className="flex min-w-0 items-center gap-3">
              {backTo && (
                <button
                  type="button"
                  onClick={() => navigate(backTo)}
                  className="shrink-0 rounded-md border border-slate-300 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                  ← Back
                </button>
              )}
              <span className="text-sm font-semibold text-slate-900 dark:text-slate-50 truncate">
                {title}
              </span>
            </div>
            <div className="flex items-center gap-2">
              {showSignOutButton && (
                <button
                  type="button"
                  onClick={handleSignOut}
                  className="rounded-md border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                  Sign out
                </button>
              )}
            </div>
          </div>
          {items && items.length > 0 && (
            <nav
              className="md:hidden border-t border-slate-100 dark:border-slate-800 px-2 py-2 flex gap-1 overflow-x-auto"
              aria-label="App sections"
            >
              {items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  className={({ isActive }) =>
                    `whitespace-nowrap rounded-full px-3 py-1 text-xs font-medium ${
                      isActive
                        ? 'bg-indigo-100 text-indigo-900 dark:bg-indigo-950 dark:text-indigo-100'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300'
                    }`
                  }
                >
                  {item.label}
                </NavLink>
              ))}
            </nav>
          )}
        </header>

        <main className="flex-1 mx-auto w-full max-w-6xl px-4 py-6 space-y-4 md:px-6">
          {children}
        </main>
      </div>
    </div>
  );
}
