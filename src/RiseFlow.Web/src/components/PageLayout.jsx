import { Link } from 'react-router-dom';

/**
 * Shared dashboard shell for all roles.
 * - Fixed left sidebar (logo + navigation)
 * - Top bar (title, quick back link, role shortcuts)
 * - Main content area where each role page renders its own cards, charts, and tables
 */
export default function PageLayout({ title, children, backTo = '/', showRoleLinks = true }) {
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
        {showRoleLinks && (
          <nav className="flex-1 px-3 py-4 space-y-1 text-sm">
            <SidebarLink to="/super-admin" label="Super Admin" />
            <SidebarLink to="/school" label="School Admin" />
            <SidebarLink to="/teacher" label="Teacher" />
            <SidebarLink to="/parent" label="Parent" />
            <SidebarLink to="/student" label="Student" />
          </nav>
        )}
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
              <Link
                to={backTo}
                className="inline-flex items-center text-xs font-medium text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-100"
                aria-label="Back to dashboard"
              >
                ← Back
              </Link>
              <span className="text-sm font-semibold text-slate-900 dark:text-slate-50">
                {title}
              </span>
            </div>
            {showRoleLinks && (
              <nav className="hidden sm:flex items-center gap-2 text-[11px] font-medium text-slate-500 dark:text-slate-400">
                <span className="uppercase tracking-wide text-slate-400 dark:text-slate-500">
                  Switch role
                </span>
                <Link to="/school" className="hover:text-indigo-600 dark:hover:text-indigo-400">School</Link>
                <Link to="/teacher" className="hover:text-indigo-600 dark:hover:text-indigo-400">Teacher</Link>
                <Link to="/parent" className="hover:text-indigo-600 dark:hover:text-indigo-400">Parent</Link>
                <Link to="/student" className="hover:text-indigo-600 dark:hover:text-indigo-400">Student</Link>
                <Link to="/super-admin" className="hover:text-indigo-600 dark:hover:text-indigo-400">Super</Link>
              </nav>
            )}
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

function SidebarLink({ to, label }) {
  return (
    <Link
      to={to}
      className="flex items-center justify-between rounded-xl px-3 py-2 text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800/80 dark:hover:text-slate-50 text-xs font-medium"
    >
      <span>{label}</span>
    </Link>
  );
}

