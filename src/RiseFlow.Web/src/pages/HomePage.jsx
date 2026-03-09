import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import './HomePage.css';

const THEME_STORAGE_KEY = 'riseflow-theme';

function getInitialTheme() {
  if (typeof window === 'undefined') return 'light';
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
  } catch {
    // ignore
  }
  return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light';
}

export default function HomePage() {
  const [theme, setTheme] = useState(getInitialTheme);

  useEffect(() => {
    try {
      document.documentElement.setAttribute('data-theme', theme);
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // ignore
    }
  }, [theme]);

  const toggleTheme = () => {
    setTheme((t) => (t === 'light' ? 'dark' : 'light'));
  };

  return (
    <div className="home-root">
      <header className="home-header">
        <div className="home-header-inner">
          <div className="home-brand">
            <span className="home-logo-dot" aria-hidden="true" />
            <span className="home-logo-text">RiseFlow</span>
          </div>
          <div className="home-header-actions">
            <button
              type="button"
              onClick={toggleTheme}
              className="home-theme-toggle"
              aria-label={theme === 'light' ? 'Switch to night view' : 'Switch to day view'}
            >
              <span className="home-theme-icon" aria-hidden="true">
                {theme === 'light' ? '🌙' : '☀️'}
              </span>
              <span className="home-theme-label">
                {theme === 'light' ? 'Night view' : 'Day view'}
              </span>
            </button>
            <Link to="/login" className="home-link-button home-link-ghost">
              Log in
            </Link>
            <Link to="/onboard" className="home-link-button home-link-primary">
              Register your school
            </Link>
          </div>
        </div>
      </header>

      <main className="home-main">
        <section className="home-hero">
          <div className="home-hero-text">
            <h1>All-in-one school OS for African schools.</h1>
            <p className="home-hero-subtitle">
              RiseFlow connects school owners, teachers, parents, and students in one simple,
              modern platform. Results, fees, attendance, and communication&mdash;built for
              Nigerian and African schools from day one.
            </p>
            <div className="home-hero-ctas">
              <Link to="/onboard" className="home-hero-cta-primary">
                Register your school in 5 minutes
              </Link>
              <Link to="/login" className="home-hero-cta-secondary">
                Already using RiseFlow? Log in
              </Link>
            </div>
            <div className="home-hero-meta">
              <span>• First 50 students free every month</span>
              <span>• Paystack-powered billing in Naira</span>
              <span>• Designed for primary &amp; secondary schools</span>
            </div>
          </div>
          <div className="home-hero-panel" aria-label="RiseFlow overview">
            <div className="home-hero-card home-hero-card--owner">
              <h2>For school owners</h2>
              <ul>
                <li>Real-time view of students, teachers, and fees</li>
                <li>Lock result printing until debts are cleared</li>
                <li>Country-aware billing and control room metrics</li>
              </ul>
            </div>
            <div className="home-hero-card home-hero-card--teacher">
              <h2>For teachers</h2>
              <ul>
                <li>Fast grid entry for scores and assessments</li>
                <li>See only students assigned to your classes</li>
                <li>WhatsApp-ready parent contact directory</li>
              </ul>
            </div>
            <div className="home-hero-card home-hero-card--parent">
              <h2>For parents</h2>
              <ul>
                <li>Family view for 2&ndash;4 children in one app</li>
                <li>Secure access codes for linking each child</li>
                <li>Instant result notifications and PDF report cards</li>
              </ul>
            </div>
          </div>
        </section>

        <section className="home-section">
          <div className="home-section-header">
            <h2>Built for African realities</h2>
            <p>
              RiseFlow understands how schools really work across Nigeria, Ghana, Kenya and
              beyond. We support dense ranking, competency-based assessments, and flexible
              fee models that match your community.
            </p>
          </div>
          <div className="home-grid">
            <article className="home-tile">
              <h3>Secondary: Score &amp; rank engine</h3>
              <p>
                30% CA + 70% exam, automatic dense ranking, and subject-by-subject performance
                snapshots. Export-ready report cards with your school logo and RiseFlow seal.
              </p>
            </article>
            <article className="home-tile">
              <h3>Primary: Class assessments</h3>
              <p>
                Track handwriting, punctuality, reading fluency, social habits and more with
                custom assessment categories that fit your curriculum.
              </p>
            </article>
            <article className="home-tile">
              <h3>Parents stay in the loop</h3>
              <p>
                One parent account, many children. View pictures, teachers, results and fees
                in one clean dashboard, on any smartphone.
              </p>
            </article>
            <article className="home-tile">
              <h3>Safe, hosted &amp; future-proof</h3>
              <p>
                Cloud-hosted on modern infrastructure with daily backups. Your records are
                safe for future generations and accessible from anywhere.
              </p>
            </article>
          </div>
        </section>

        <section className="home-section home-section-alt">
          <div className="home-section-header">
            <h2>How schools get started</h2>
            <p>Three simple steps to bring your school onto RiseFlow.</p>
          </div>
          <ol className="home-steps">
            <li>
              <span className="home-step-badge">1</span>
              <div>
                <h3>Register your school</h3>
                <p>
                  Click &ldquo;Register your school&rdquo; and fill in a short form. You get
                  a secure link for your teachers and parents.
                </p>
              </div>
            </li>
            <li>
              <span className="home-step-badge">2</span>
              <div>
                <h3>Import or add students</h3>
                <p>
                  Upload an Excel file or add students one by one. RiseFlow checks for
                  duplicates and keeps your data clean.
                </p>
              </div>
            </li>
            <li>
              <span className="home-step-badge">3</span>
              <div>
                <h3>Share links with staff &amp; parents</h3>
                <p>
                  Share your school link so teachers and parents can sign up with their own
                  profiles and photos.
                </p>
              </div>
            </li>
          </ol>
        </section>
      </main>

      <footer className="home-footer">
        <div className="home-footer-inner">
          <p>RiseFlow &mdash; School management built for African schools.</p>
          <p className="home-footer-meta">Day &amp; night view for busy school owners and parents.</p>
        </div>
      </footer>
    </div>
  );
}

