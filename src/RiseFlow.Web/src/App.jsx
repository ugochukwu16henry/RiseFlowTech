import { useState, useEffect } from 'react';
import './App.css';

const API_BASE = import.meta.env.VITE_API_URL || '';

/* Inline SVGs - no extra requests, small payload */
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

const ACTION_CARDS = [
  {
    id: 'results',
    to: '#results',
    title: 'View Results',
    desc: 'See latest grades and report cards',
    icon: IconResults,
    className: 'card--results',
  },
  {
    id: 'message',
    to: '#message',
    title: 'Message Teacher',
    desc: 'Contact your child’s teacher',
    icon: IconMessage,
    className: 'card--message',
  },
  {
    id: 'pay',
    to: '#pay',
    title: 'Pay Fees',
    desc: 'View and pay school fees',
    icon: IconPay,
    className: 'card--pay',
  },
];

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
  const [progressBySubject, setProgressBySubject] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [needsAuth, setNeedsAuth] = useState(false);
  const [logoError, setLogoError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    setNeedsAuth(false);
    fetch(`${API_BASE}/api/results/my-children`, { credentials: 'include' })
      .then((res) => {
        if (cancelled) return;
        if (res.status === 401) {
          setNeedsAuth(true);
          setProgressBySubject([]);
          return [];
        }
        if (!res.ok) throw new Error('Could not load results');
        return res.json();
      })
      .then((data) => {
        if (cancelled) return;
        setProgressBySubject(mapResultsToProgressBySubject(data));
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err.message);
          setProgressBySubject([]);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  const overallPct = progressBySubject.length
    ? Math.round(progressBySubject.reduce((s, p) => s + p.value, 0) / progressBySubject.length)
    : 0;

  const logoWithName = '/logos/RiseFlow%20logo%20with%20name.png';

  return (
    <div className="app">
      <header className="header">
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
        <p className="header-tagline">Your child’s growth, in one place</p>
      </header>

      <main className="main">
        <h2 className="section-title">Quick actions</h2>
        <div className="cards" role="navigation" aria-label="Quick actions">
          {ACTION_CARDS.map(({ id, to, title, desc, icon: Icon, className }) => (
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

        <h2 className="section-title">Academic performance</h2>
        <section id="results" className="progress-section" aria-label="Performance at a glance">
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

        <section id="message" className="progress-section" style={{ marginTop: '1.5rem' }} aria-label="Message teacher">
          <h3 className="card-title">Message Teacher</h3>
          <p className="card-desc">Contact your child’s teacher. This will connect to the API when you sign in.</p>
        </section>

        <section id="pay" className="progress-section" style={{ marginTop: '1.5rem' }} aria-label="Pay fees">
          <h3 className="card-title">Pay Fees</h3>
          <p className="card-desc">View and pay school fees in your local currency. Link to billing when ready.</p>
        </section>
      </main>
    </div>
  );
}

export default App;
