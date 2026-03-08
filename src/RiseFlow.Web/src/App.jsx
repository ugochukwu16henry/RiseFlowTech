import { useState, useEffect } from 'react';
import './App.css';

const API_BASE = import.meta.env.VITE_API_URL || '';

const ROLES = {
  Parent: 'Parent',
  Teacher: 'Teacher',
  SchoolAdmin: 'SchoolAdmin',
  Student: 'Student',
  SuperAdmin: 'SuperAdmin',
};
const ROLE_LABELS = {
  [ROLES.Parent]: 'Parent',
  [ROLES.Teacher]: 'Teacher',
  [ROLES.SchoolAdmin]: 'School Admin',
  [ROLES.Student]: 'Student',
  [ROLES.SuperAdmin]: 'Super Admin',
};
const ROLE_TAGLINES = {
  [ROLES.Parent]: 'Your child’s growth, in one place',
  [ROLES.Teacher]: 'Teach, grade, and stay connected',
  [ROLES.SchoolAdmin]: 'Manage your school in one place',
  [ROLES.Student]: 'Your results and classes',
  [ROLES.SuperAdmin]: 'Control room for RiseFlow',
};

const STORAGE_KEY = 'riseflow-preview-role';
const RESULTS_CACHE_KEY = 'riseflow-cache-my-children';

/* Inline SVGs */
const IconResults = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
    <polyline points="14 2 14 8 20 8" />
    <line x1="16" y1="13" x2="8" y2="13" />
    <line x1="16" y1="17" x2="8" y2="17" />
    <polyline points="10 9 9 9 8 9" />
  </svg>
);
const IconMessage = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
  </svg>
);
const IconPay = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <rect x="1" y="4" width="22" height="16" rx="2" ry="2" />
    <line x1="1" y1="10" x2="23" y2="10" />
  </svg>
);
const IconUpload = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
    <polyline points="17 8 12 3 7 8" />
    <line x1="12" y1="3" x2="12" y2="15" />
  </svg>
);
const IconUsers = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
    <circle cx="9" cy="7" r="4" />
    <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
    <path d="M16 3.13a4 4 0 0 1 0 7.75" />
  </svg>
);
const IconSchool = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="m4 10 8-5 8 5" />
    <path d="M4 14v6a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-6" />
    <path d="M4 10h16" />
  </svg>
);
const IconChart = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <line x1="18" y1="20" x2="18" y2="10" />
    <line x1="12" y1="20" x2="12" y2="4" />
    <line x1="6" y1="20" x2="6" y2="14" />
  </svg>
);
const IconBook = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
    <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
  </svg>
);
const IconSettings = () => (
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <circle cx="12" cy="12" r="3" />
    <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
  </svg>
);

function card(id, to, title, desc, icon, className) {
  return { id, to, title, desc, icon, className };
}

const DASHBOARD_BY_ROLE = {
  [ROLES.Parent]: [
    card('results', '#results', 'View Results', 'See latest grades and report cards', IconResults, 'card--results'),
    card('message', '#message', 'Message Teacher', 'Contact your child’s teacher', IconMessage, 'card--message'),
    card('pay', '#pay', 'Pay Fees', 'View and pay school fees', IconPay, 'card--pay'),
  ],
  [ROLES.Teacher]: [
    card('upload', '#upload', 'Upload Results', 'Enter grades for your classes', IconUpload, 'card--results'),
    card('classes', '#classes', 'My Classes', 'View and manage your classes', IconBook, 'card--message'),
    card('students', '#students', 'My Students', 'See student lists by class', IconUsers, 'card--pay'),
    card('parents', '#parents', 'Contact Parents', 'Message parents and guardians', IconMessage, 'card--message'),
  ],
  [ROLES.SchoolAdmin]: [
    card('school', '#school', 'Manage School', 'School profile and settings', IconSchool, 'card--message'),
    card('billing', '#billing', 'Billing & Fees', 'View billing and collect fees', IconPay, 'card--results'),
    card('people', '#people', 'Teachers & Students', 'Manage staff and students', IconUsers, 'card--pay'),
    card('reports', '#reports', 'Reports', 'View reports and analytics', IconChart, 'card--message'),
  ],
  [ROLES.Student]: [
    card('results', '#results', 'My Results', 'View your grades', IconResults, 'card--results'),
    card('classes', '#classes', 'My Classes', 'See your class and timetable', IconBook, 'card--message'),
    card('teachers', '#teachers', 'My Teachers', 'Contact your teachers', IconMessage, 'card--pay'),
  ],
  [ROLES.SuperAdmin]: [
    card('control', '#control', 'Control Room', 'Platform stats and overview', IconChart, 'card--message'),
    card('billing', '#billing', 'Billing', 'All schools billing and revenue', IconPay, 'card--results'),
    card('schools', '#schools', 'Schools', 'Manage all schools', IconSchool, 'card--pay'),
    card('transcripts', '#transcripts', 'Transcripts', 'Transcript verification', IconResults, 'card--message'),
  ],
};

function ProgressBar({ label, value, overall }) {
  const pct = Math.min(100, Math.max(0, Number(value) || 0));
  return (
    <>
      <div className="progress-header">
        <span className="progress-label">{label}</span>
        <span className="progress-value">{pct}%</span>
      </div>
      <div className="progress-track">
        <div className={`progress-fill ${overall ? 'progress-overall-fill' : ''}`} style={{ width: `${pct}%` }} />
      </div>
    </>
  );
}

function formatMoney(amount, currencyCode) {
  const code = currencyCode || 'NGN';
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: code, maximumFractionDigits: 0 }).format(n);
}

/** Build WhatsApp chat URL: wa.me/<digits only>. Use teacher's WhatsAppNumber or fallback Phone. */
function whatsAppUrl(whatsAppNumber, phone) {
  const raw = (whatsAppNumber || phone || '').replace(/\D/g, '');
  if (!raw) return null;
  return `https://wa.me/${raw}`;
}

function mapResultsToProgressBySubject(results) {
  if (!Array.isArray(results) || results.length === 0) return [];
  const bySubject = {};
  for (const r of results) {
    const name = r.subject?.name || 'Other';
    if (!bySubject[name]) bySubject[name] = { totalScore: 0, totalMax: 0 };
    bySubject[name].totalScore += Number(r.score) || 0;
    bySubject[name].totalMax += Number(r.maxScore) || 0;
  }
  return Object.entries(bySubject).map(([subject, { totalScore, totalMax }]) => ({
    subject,
    value: totalMax > 0 ? Math.round((totalScore / totalMax) * 100) : 0,
  })).sort((a, b) => a.subject.localeCompare(b.subject));
}

function App() {
  const [role, setRole] = useState(() => {
    try {
      const s = localStorage.getItem(STORAGE_KEY);
      return ROLES[s] || ROLES.Parent;
    } catch {
      return ROLES.Parent;
    }
  });
  const [progressBySubject, setProgressBySubject] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [needsAuth, setNeedsAuth] = useState(false);
  const [logoError, setLogoError] = useState(false);
  const [teacherContacts, setTeacherContacts] = useState([]);
  const [contactsLoading, setContactsLoading] = useState(false);
  const [contactsError, setContactsError] = useState(null);
  const [resultsCachedAt, setResultsCachedAt] = useState(null);
  const [retryResultsTrigger, setRetryResultsTrigger] = useState(0);
  const [schoolDashboard, setSchoolDashboard] = useState(null);
  const [schoolDashboardLoading, setSchoolDashboardLoading] = useState(false);
  const [superAdminDashboard, setSuperAdminDashboard] = useState(null);
  const [superAdminLoading, setSuperAdminLoading] = useState(false);
  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, role);
    } catch (_) {}
  }, [role]);

  useEffect(() => {
    if (role !== ROLES.Parent) {
      setLoading(false);
      setProgressBySubject([]);
      setError(null);
      setNeedsAuth(false);
      setResultsCachedAt(null);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    setNeedsAuth(false);
    setResultsCachedAt(null);
    fetch(`${API_BASE}/api/results/my-children`, { credentials: 'include' })
      .then((res) => {
        if (cancelled) return null;
        if (res.status === 401) {
          setNeedsAuth(true);
          setProgressBySubject([]);
          return null;
        }
        if (!res.ok) throw new Error('Could not load results');
        return res.json();
      })
      .then((data) => {
        if (cancelled) return;
        if (data != null) {
          setProgressBySubject(mapResultsToProgressBySubject(data));
          try {
            localStorage.setItem(RESULTS_CACHE_KEY, JSON.stringify({
              data,
              fetchedAt: new Date().toISOString(),
            }));
          } catch (_) {}
        }
      })
      .catch((err) => {
        if (!cancelled) {
          try {
            const raw = localStorage.getItem(RESULTS_CACHE_KEY);
            const cached = raw ? JSON.parse(raw) : null;
            if (cached?.data != null) {
              setProgressBySubject(mapResultsToProgressBySubject(cached.data));
              setResultsCachedAt(cached.fetchedAt || null);
              setError(null);
            } else {
              setError(err.message);
              setProgressBySubject([]);
            }
          } catch (_) {
            setError(err.message);
            setProgressBySubject([]);
          }
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [role, retryResultsTrigger]);

  // When back online and we were showing cached results, auto-retry fetch
  useEffect(() => {
    if (role !== ROLES.Parent || !resultsCachedAt) return;
    const onOnline = () => setRetryResultsTrigger((c) => c + 1);
    window.addEventListener('online', onOnline);
    return () => window.removeEventListener('online', onOnline);
  }, [role, resultsCachedAt]);

  // Parent: load teacher contacts for WhatsApp
  useEffect(() => {
    if (role !== ROLES.Parent) {
      setTeacherContacts([]);
      setContactsError(null);
      return;
    }
    let cancelled = false;
    setContactsLoading(true);
    setContactsError(null);
    fetch(`${API_BASE}/api/contacts/teachers`, { credentials: 'include' })
      .then((res) => {
        if (cancelled) return [];
        if (res.status === 401) return [];
        if (!res.ok) throw new Error('Could not load contacts');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setTeacherContacts(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        if (!cancelled) {
          setContactsError(err.message);
          setTeacherContacts([]);
        }
      })
      .finally(() => {
        if (!cancelled) setContactsLoading(false);
      });
    return () => { cancelled = true; };
  }, [role]);

  // School Admin: dashboard summary (active students, unpaid fees)
  useEffect(() => {
    if (role !== ROLES.SchoolAdmin) {
      setSchoolDashboard(null);
      return;
    }
    let cancelled = false;
    setSchoolDashboardLoading(true);
    fetch(`${API_BASE}/api/schools/dashboard`, { credentials: 'include' })
      .then((res) => (cancelled ? null : res.ok ? res.json() : null))
      .then((data) => { if (!cancelled) setSchoolDashboard(data); })
      .catch(() => { if (!cancelled) setSchoolDashboard(null); })
      .finally(() => { if (!cancelled) setSchoolDashboardLoading(false); });
    return () => { cancelled = true; };
  }, [role]);

  // Super Admin: dashboard (schools by country, monthly revenue)
  useEffect(() => {
    if (role !== ROLES.SuperAdmin) {
      setSuperAdminDashboard(null);
      return;
    }
    let cancelled = false;
    setSuperAdminLoading(true);
    fetch(`${API_BASE}/api/superadmin/dashboard`, { credentials: 'include' })
      .then((res) => (cancelled ? null : res.ok ? res.json() : null))
      .then((data) => { if (!cancelled) setSuperAdminDashboard(data); })
      .catch(() => { if (!cancelled) setSuperAdminDashboard(null); })
      .finally(() => { if (!cancelled) setSuperAdminLoading(false); });
    return () => { cancelled = true; };
  }, [role]);

  const overallPct = progressBySubject.length
    ? Math.round(progressBySubject.reduce((s, p) => s + p.value, 0) / progressBySubject.length)
    : 0;

  const logoWithName = '/logos/RiseFlow%20logo%20with%20name.png';
  const cards = DASHBOARD_BY_ROLE[role] || DASHBOARD_BY_ROLE[ROLES.Parent];
  const showPerformance = role === ROLES.Parent;

  return (
    <div className="app">
      <header className="header">
        <div className="header-row">
          <a href="/" className="header-brand" aria-label="RiseFlow home">
            {logoError ? (
              <span className="header-logo-text">RiseFlow</span>
            ) : (
              <img
                src={logoWithName}
                alt="RiseFlow"
                className="header-logo"
                width="140"
                height="40"
                onError={() => setLogoError(true)}
              />
            )}
          </a>
          <nav className="header-nav">
            <a href="/onboard" className="header-link">Register school</a>
          </nav>
          <label className="role-switcher">
            <span className="role-switcher-label">View as</span>
            <select
              value={role}
              onChange={(e) => setRole(e.target.value)}
              className="role-select"
              aria-label="Switch dashboard role"
            >
              {Object.entries(ROLE_LABELS).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </label>
        </div>
        <p className="header-tagline">{ROLE_TAGLINES[role]}</p>
      </header>

      <main className="main">
        {/* Parent: Feed-style — Recent Results and Teacher Contacts first */}
        {showPerformance && (
          <div className="dashboard-feed" aria-label="Your feed">
            <h2 className="section-title">Recent Results</h2>
            <section id="results" className="feed-card progress-section" aria-label="Performance at a glance">
              {resultsCachedAt && (
                <div className="offline-banner" role="status">
                  <span>Showing cached results from {new Date(resultsCachedAt).toLocaleString()}. Sync when back online.</span>
                  <button type="button" className="offline-retry" onClick={() => setRetryResultsTrigger((c) => c + 1)}>Retry</button>
                </div>
              )}
              {loading ? (
                <p className="empty-state" aria-busy="true">Loading results…</p>
              ) : error ? (
                <p className="empty-state empty-state--error">{error}</p>
              ) : needsAuth ? (
                <p className="empty-state">Sign in to see your child's results.</p>
              ) : progressBySubject.length > 0 ? (
                <>
                  <div className="progress-item progress-overall">
                    <ProgressBar label="Overall" value={overallPct} overall />
                  </div>
                  <ul className="progress-list">
                    {progressBySubject.map(({ subject, value }) => (
                      <li key={subject} className="progress-item">
                        <ProgressBar label={subject} value={value} />
                      </li>
                    ))}
                  </ul>
                </>
              ) : (
                <p className="empty-state">No results yet. Check back after grades are published.</p>
              )}
            </section>
            <h2 className="section-title">Teacher Contacts</h2>
            <section id="message" className="feed-card progress-section" aria-label="Message teacher">
              <p className="card-desc">Contact your child's teacher. Use WhatsApp to open a direct chat.</p>
              {contactsLoading && <p className="empty-state" aria-busy="true">Loading teachers…</p>}
              {contactsError && !contactsLoading && <p className="empty-state empty-state--error">{contactsError}</p>}
              {!contactsLoading && !contactsError && teacherContacts.length === 0 && (
                <p className="empty-state">Sign in as a parent to see your children's teachers.</p>
              )}
              {!contactsLoading && teacherContacts.length > 0 && (
                <ul className="teacher-list">
                  {teacherContacts.map((t) => {
                    const wa = whatsAppUrl(t.whatsAppNumber, t.phone);
                    return (
                      <li key={t.teacherId} className="teacher-item">
                        <span className="teacher-name">{t.fullName}</span>
                        {wa ? (
                          <a href={wa} target="_blank" rel="noopener noreferrer" className="btn-whatsapp" aria-label={`Open WhatsApp chat with ${t.fullName}`}>WhatsApp</a>
                        ) : (
                          <span className="teacher-no-wa">No WhatsApp number</span>
                        )}
                      </li>
                    );
                  })}
                </ul>
              )}
            </section>
            <section id="pay" className="feed-card progress-section section-spacer" aria-label="Pay fees">
              <h3 className="card-title">Pay Fees</h3>
              <p className="card-desc">View and pay school fees in your local currency.</p>
            </section>
          </div>
        )}

        {/* School Admin: high-level view — active students, unpaid fees */}
        {role === ROLES.SchoolAdmin && (
          <div className="dashboard-summary">
            <h2 className="section-title">Dashboard</h2>
            {schoolDashboardLoading ? (
              <p className="empty-state" aria-busy="true">Loading…</p>
            ) : schoolDashboard ? (
              <div className="summary-cards">
                <div className="summary-card">
                  <span className="summary-value">{schoolDashboard.activeStudentCount}</span>
                  <span className="summary-label">Active students</span>
                </div>
                <div className="summary-card summary-card--warning">
                  <span className="summary-value">{formatMoney(schoolDashboard.unpaidFeesTotal, schoolDashboard.currencyCode)}</span>
                  <span className="summary-label">Unpaid fees</span>
                </div>
              </div>
            ) : null}
          </div>
        )}

        {/* Super Admin: map of schools by country + monthly revenue */}
        {role === ROLES.SuperAdmin && (
          <div className="dashboard-summary">
            <h2 className="section-title">Control room</h2>
            {superAdminLoading ? (
              <p className="empty-state" aria-busy="true">Loading…</p>
            ) : superAdminDashboard ? (
              <>
                <div className="summary-cards">
                  <div className="summary-card">
                    <span className="summary-value">{formatMoney(superAdminDashboard.monthlyRevenueUsd, 'USD')}</span>
                    <span className="summary-label">Monthly revenue</span>
                  </div>
                  <div className="summary-card">
                    <span className="summary-value">{formatMoney(superAdminDashboard.totalRevenueUsd, 'USD')}</span>
                    <span className="summary-label">Total revenue</span>
                  </div>
                </div>
                <h3 className="card-title" style={{ marginTop: '1.25rem' }}>Schools by country</h3>
                <p className="card-desc">States and countries with the most active schools.</p>
                {superAdminDashboard.schoolsByCountry?.length > 0 ? (
                  <ul className="country-list">
                    {superAdminDashboard.schoolsByCountry.map((c) => (
                      <li key={c.countryCode} className="country-item">
                        <span className="country-name">{c.countryName}</span>
                        <span className="country-count">{c.schoolCount} schools</span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="empty-state">No schools by country yet.</p>
                )}
              </>
            ) : null}
          </div>
        )}

        <h2 className="section-title">Quick actions</h2>
        <div className="cards" role="navigation" aria-label="Quick actions">
          {cards.map(({ id, to, title, desc, icon: Icon, className }) => (
            <a
              key={id}
              href={to}
              className={`card ${className}`}
              style={{ minHeight: 'var(--touch-min)' }}
            >
              <div className="card-icon" aria-hidden="true">
                <Icon />
              </div>
              <h3 className="card-title">{title}</h3>
              <p className="card-desc">{desc}</p>
            </a>
          ))}
        </div>

        {showPerformance && (
          <>
            <h2 className="section-title section-title--secondary">More</h2>
            <section id="results-dupe" className="progress-section" aria-hidden="true" style={{ display: 'none' }}>
              {resultsCachedAt && (
                <div className="offline-banner" role="status">
                  <span>Showing cached results from {new Date(resultsCachedAt).toLocaleString()}. Sync when back online.</span>
                  <button type="button" className="offline-retry" onClick={() => setRetryResultsTrigger((c) => c + 1)}>Retry</button>
                </div>
              )}
              {loading ? (
                <p className="empty-state" aria-busy="true">Loading results…</p>
              ) : error ? (
                <p className="empty-state empty-state--error">{error}</p>
              ) : needsAuth ? (
                <p className="empty-state">Sign in to see your child’s results.</p>
              ) : progressBySubject.length > 0 ? (
                <>
                  <div className="progress-item progress-overall">
                    <ProgressBar label="Overall" value={overallPct} overall />
                  </div>
                  <ul className="progress-list">
                    {progressBySubject.map(({ subject, value }) => (
                      <li key={subject} className="progress-item">
                        <ProgressBar label={subject} value={value} />
                      </li>
                    ))}
                  </ul>
                </>
              ) : (
                <p className="empty-state">No results yet. Check back after grades are published.</p>
              )}
            </section>
            <section id="message" className="progress-section section-spacer" aria-label="Message teacher">
              <h3 className="card-title">Message Teacher</h3>
              <p className="card-desc">Contact your child’s teacher. Use the WhatsApp button to open a direct chat.</p>
              {contactsLoading && <p className="empty-state" aria-busy="true">Loading teachers…</p>}
              {contactsError && !contactsLoading && <p className="empty-state empty-state--error">{contactsError}</p>}
              {!contactsLoading && !contactsError && teacherContacts.length === 0 && (
                <p className="empty-state">Sign in as a parent to see your children's teachers.</p>
              )}
              {!contactsLoading && teacherContacts.length > 0 && (
                <ul className="teacher-list">
                  {teacherContacts.map((t) => {
                    const wa = whatsAppUrl(t.whatsAppNumber, t.phone);
                    return (
                      <li key={t.teacherId} className="teacher-item">
                        <span className="teacher-name">{t.fullName}</span>
                        {wa ? (
                          <a
                            href={wa}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="btn-whatsapp"
                            aria-label={`Open WhatsApp chat with ${t.fullName}`}
                          >
                            WhatsApp
                          </a>
                        ) : (
                          <span className="teacher-no-wa">No WhatsApp number</span>
                        )}
                      </li>
                    );
                  })}
                </ul>
              )}
            </section>
            <section id="pay" className="progress-section section-spacer" aria-label="Pay fees">
              <h3 className="card-title">Pay Fees</h3>
              <p className="card-desc">View and pay school fees in your local currency. Link to billing when ready.</p>
            </section>
          </>
        )}

        {role === ROLES.Teacher && (
          <section className="progress-section section-spacer">
            <h3 className="card-title">Teacher dashboard</h3>
            <p className="card-desc">Upload results, manage your classes, and contact parents. These actions will link to the API when you sign in as a teacher.</p>
          </section>
        )}

        {role === ROLES.SchoolAdmin && (
          <section className="progress-section section-spacer">
            <h3 className="card-title">School Admin dashboard</h3>
            <p className="card-desc">Manage your school profile, billing, teachers, students, and reports. Full features available after sign-in.</p>
            <div className="bulk-upload-block">
              <h4 className="card-title" style={{ marginTop: '1rem' }}>Bulk upload students</h4>
              <p className="card-desc">Download the Excel template, fill in your students (1000+ supported), then upload when signed in as School Admin.</p>
              <form
                method="GET"
                action={`${API_BASE}/api/students/bulk-upload-template`}
                target="_blank"
                className="bulk-download-form"
              >
                <button type="submit" className="btn-download-template">
                  Download Excel template
                </button>
              </form>
            </div>
          </section>
        )}

        {role === ROLES.Student && (
          <section id="results" className="progress-section section-spacer">
            <h3 className="card-title">My Results</h3>
            <p className="card-desc">View your grades and progress here once your teacher has uploaded results.</p>
          </section>
        )}

        {role === ROLES.SuperAdmin && (
          <section className="progress-section section-spacer">
            <h3 className="card-title">Super Admin control room</h3>
            <p className="card-desc">Platform-wide stats, billing across all schools, and school management. Available after sign-in as Super Admin.</p>
          </section>
        )}
      </main>
    </div>
  );
}

export default App;
