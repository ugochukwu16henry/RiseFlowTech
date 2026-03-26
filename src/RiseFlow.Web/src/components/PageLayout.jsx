import { useNavigate } from 'react-router-dom';
import { apiFetch, STORAGE_TENANT_KEY } from '../api';

/**
 * Shared dashboard shell for all roles.
 * - Fixed left sidebar (logo + navigation)
 * - Top bar (title)
 * - Main content area where each role page renders its own cards, charts, and tables
 */
export default function PageLayout({ title, children }) {
  const navigate = useNavigate();

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

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 dark:bg-slate-950 dark:text-slate-100 flex">
      {/* Sidebar */}
      <aside className="hidden md:flex md:w-60 lg:w-64 flex-col border-r border-slate-200 bg-white/90 backdrop-blur-sm dark:border-slate-800 dark:bg-slate-900/90">
        <div className="flex items-center gap-2 px-4 pt-4 pb-3 border-b border-slate-200 dark:border-slate-800">
          <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-indigo-600 text-xs font-bold text-white">
            RF
          </div>
          <div>
            <p className="text-sm font-semibold">RiseFlow</p>
            <p className="text-[11px] text-slate-500 dark:text-slate-400">School OS dashboard</p>
          </div>
        </div>
        <div className="flex-1" />
        <div className="px-4 pb-4 mt-auto text-[11px] text-slate-400 dark:text-slate-500">
          <p>© {new Date().getFullYear()} RiseFlow</p>
        </div>
      </aside>

      {/* Main column */}
      <div className="flex-1 flex flex-col">
        {/* Top bar */}
        <header className="border-b border-slate-200 bg-white/90 backdrop-blur-sm dark:border-slate-800 dark:bg-slate-900/90">
          <div className="mx-auto max-w-6xl px-4 py-3 flex items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <span className="text-sm font-semibold text-slate-900 dark:text-slate-50">
                {title}
              </span>
            </div>
            <button
              type="button"
              onClick={handleSignOut}
              className="rounded-md border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              Sign out
            </button>
          </div>
        </header>

        {/* Main content area */}
        <main className="flex-1 mx-auto w-full max-w-6xl px-4 py-6 space-y-4">
          {children}
        </main>
      </div>
    </div>
  );
}

