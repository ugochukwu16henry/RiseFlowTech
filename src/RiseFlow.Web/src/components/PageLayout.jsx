import { useEffect, useState } from 'react';
import { useNavigate, NavLink, Link } from 'react-router-dom';
import { apiFetch, getApiBase, STORAGE_TENANT_KEY } from '../api';
import {
  Activity,
  BadgeDollarSign,
  Bell,
  BookOpen,
  BriefcaseBusiness,
  Building2,
  CalendarDays,
  CircleFadingArrowUp,
  ClipboardList,
  ContactRound,
  CreditCard,
  FileBarChart2,
  FileCog,
  GraduationCap,
  LayoutDashboard,
  MailCheck,
  School,
  Settings2,
  ShieldCheck,
  Upload,
  UserPlus,
  Users,
  Wallet,
} from 'lucide-react';

/** Preset sidebar links (multi-tenant SaaS shell — one school’s data never mixed with another’s at the API). */
const NAV_BY_ROLE = {
  school: [
    { to: '/school', label: 'Dashboard', end: true, icon: LayoutDashboard },
    { to: '/school?tab=operations', label: 'School profile', icon: Building2 },
    { to: '/school/students', label: 'People', icon: Users },
    { to: '/school/students/add', label: 'Add student', icon: UserPlus },
    { to: '/school/classes', label: 'Classes', icon: School },
    { to: '/school/fees', label: 'School fees', icon: Wallet },
    { to: '/school/terms', label: 'Terms & calendar', icon: CalendarDays },
    { to: '/school/grading-systems', label: 'Grading systems', icon: Settings2 },
    { to: '/school/promotions', label: 'Promotions', icon: CircleFadingArrowUp },
    { to: '/school/timetable', label: 'Timetable', icon: ClipboardList },
    { to: '/school/communications', label: 'Notices & events', icon: Bell },
    { to: '/school/billing', label: 'Billing', icon: CreditCard },
    { to: '/school/reports', label: 'Reports', icon: FileBarChart2 },
    { to: '/school/import', label: 'Import', icon: Upload },
    { to: '/school/access-codes', label: 'Access codes', icon: ShieldCheck },
  ],
  super: [
    { to: '/super-admin', label: 'Control room', end: true, icon: LayoutDashboard },
    { to: '/super-admin/schools', label: 'Schools', icon: Building2 },
    { to: '/super-admin/revenue', label: 'Revenue', icon: BadgeDollarSign },
    { to: '/super-admin/affiliates', label: 'Affiliates', icon: BriefcaseBusiness },
    { to: '/super-admin/compliance', label: 'System settings', icon: FileCog },
    { to: '/super-admin/data-offboarding', label: 'Data offboarding', icon: ShieldCheck },
  ],
  affiliate: [
    { to: '/affiliate', label: 'Dashboard', end: true, icon: LayoutDashboard },
    { to: '/affiliate/schools', label: 'My schools', icon: Building2 },
    { to: '/affiliate/payouts', label: 'Payouts', icon: Wallet },
    { to: '/affiliate/training', label: 'Training', icon: BookOpen },
  ],
  teacher: [
    { to: '/teacher', label: 'Dashboard', end: true, icon: LayoutDashboard },
    { to: '/teacher/grading', label: 'Grading', icon: GraduationCap },
    { to: '/teacher/promotions', label: 'Promotions', icon: CircleFadingArrowUp },
    { to: '/teacher/assignments', label: 'Assignments', icon: ClipboardList },
  ],
  parent: [
    { to: '/parent', label: 'Family', end: true, icon: ContactRound },
    { to: '/parent/fees', label: 'School fees', icon: Wallet },
    { to: '/parent/dashboard', label: 'Dashboard', icon: LayoutDashboard },
    { to: '/parent/claim', label: 'Claim child', icon: MailCheck },
  ],
  student: [
    { to: '/student', label: 'My dashboard', end: true, icon: Activity },
    { to: '/student/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  ],
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

function navIconWrapClass(isActive) {
  return [
    'inline-flex h-5 w-5 items-center justify-center rounded-md transition-colors',
    isActive
      ? 'bg-indigo-100 text-primary-700 dark:bg-indigo-900/50 dark:text-indigo-200'
      : 'bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-300',
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
 * @param {'school'|'super'|'affiliate'|'teacher'|'parent'|'student'|'legal'|undefined} role — sidebar links; omit for empty aside
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
  const navigate = useNavigate();
  const items = role ? NAV_BY_ROLE[role] : null;
  const showSignOutButton = showSignOut ?? variant === 'app';
  const [schoolBrand, setSchoolBrand] = useState(null);
  const [logoFailed, setLogoFailed] = useState(false);
  const [brandReloadToken, setBrandReloadToken] = useState(0);
  const usesPlatformBrand = !role || role === 'super' || role === 'legal' || role === 'affiliate';

  useEffect(() => {
    const onBrandUpdated = () => setBrandReloadToken((n) => n + 1);
    window.addEventListener('riseflow:school-brand-updated', onBrandUpdated);
    return () => window.removeEventListener('riseflow:school-brand-updated', onBrandUpdated);
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLogoFailed(false);

    if (variant !== 'app' || usesPlatformBrand) {
      setSchoolBrand(null);
      return undefined;
    }

    const applyBrand = (data) => {
      if (!data) return;
      const schoolId = data.id || data.schoolId || null;
      if (schoolId) {
        try {
          localStorage.setItem(STORAGE_TENANT_KEY, schoolId);
        } catch {
          // ignore
        }
      }

      setSchoolBrand({
        id: schoolId,
        name: data.name || data.schoolName || 'School',
        logo: (() => {
          const baseLogo = buildPublicUrl(data.logoPath || data.logoFileName);
          if (!baseLogo) return null;
          const sep = baseLogo.includes('?') ? '&' : '?';
          return `${baseLogo}${sep}v=${Date.now()}`;
        })(),
        registrationDocumentPath: data.registrationDocumentPath ? buildPublicUrl(data.registrationDocumentPath) : null,
      });
    };

    // Preferred source: tenant-aware branding endpoint for school/teacher/parent/student roles.
    apiFetch('/api/schools/branding')
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (cancelled || !data) return;
        applyBrand(data);
      })
      .catch(() => {
        // Fallback to explicit school lookup by local storage id.
        let schoolId = null;
        try {
          schoolId = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_TENANT_KEY) : null;
        } catch {
          schoolId = null;
        }

        if (!schoolId) {
          if (!cancelled) setSchoolBrand(null);
          return;
        }

        apiFetch(`/api/schools/${schoolId}`)
          .then((res) => (res.ok ? res.json() : null))
          .then((data) => {
            if (!cancelled && data) applyBrand(data);
          })
          .catch(() => {
            if (!cancelled) setSchoolBrand(null);
          });
      });

    return () => { cancelled = true; };
  }, [usesPlatformBrand, variant, brandReloadToken]);

  useEffect(() => {
    // If a previous fetch failed and the logo URL changed, allow image rendering to retry.
    setLogoFailed(false);
  }, [schoolBrand?.logo]);

  const brandName = usesPlatformBrand
    ? 'RiseFlow'
    : (schoolBrand?.name || 'School Portal');
  const brandTagline = role === 'affiliate'
    ? 'Affiliate partner hub'
    : usesPlatformBrand
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
      await apiFetch('/api/auth/logout', { method: 'POST' });
    } catch {
      // best effort
    }
    try {
      localStorage.removeItem(STORAGE_TENANT_KEY);
    } catch {
      // ignore
    }
    navigate('/login', { replace: true });
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
              {({ isActive }) => (
                <span className="flex items-center gap-2">
                  {item.icon ? (
                    <span className={navIconWrapClass(isActive)}>
                      <item.icon size={14} strokeWidth={2.1} aria-hidden="true" />
                    </span>
                  ) : null}
                  <span>{item.label}</span>
                </span>
              )}
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
                  <span className="inline-flex items-center gap-1.5">
                    {item.icon ? <item.icon size={13} strokeWidth={2.1} aria-hidden="true" /> : null}
                    <span>{item.label}</span>
                  </span>
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
