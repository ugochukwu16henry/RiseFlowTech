import { useMemo, useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import './LoginPage.css';
import { apiFetch, STORAGE_TENANT_KEY } from '../api';

const ROLE_ROUTE = {
  SchoolAdmin: '/school',
  Teacher: '/teacher',
  Parent: '/parent',
  Student: '/student',
  Affiliate: '/affiliate',
  SuperAdmin: '/super-admin',
  Staff: '/staff',
};
const REMEMBER_ME_KEY = 'riseflow-remembered-signin';

export default function LoginPage() {
  const navigate = useNavigate();
  const remembered = useMemo(() => {
    try {
      const raw = localStorage.getItem(REMEMBER_ME_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }, []);
  const [email, setEmail] = useState(remembered?.email || '');
  const [password, setPassword] = useState(remembered?.password || '');
  const [rememberMe, setRememberMe] = useState(Boolean(remembered?.email || remembered?.password));
  const [submitting, setSubmitting] = useState(false);
  const params = typeof window !== 'undefined' ? new URLSearchParams(window.location.search) : null;
  const expiredMessage = params?.get('reason') === 'session_expired'
    ? 'Your session expired. Please sign in again.'
    : null;
  const [error, setError] = useState(expiredMessage);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!email || !password) {
      setError('Enter your email or login ID and password.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const res = await apiFetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      });
      if (res.status === 401) {
        setError('Incorrect email/login ID or password.');
        return;
      }
      if (!res.ok) {
        setError('Could not sign in. Please try again.');
        return;
      }
      const data = await res.json();
      if (!data?.success) {
        setError(data?.message || 'Incorrect email/login ID or password.');
        return;
      }
      try {
        if (data.schoolId) {
          localStorage.setItem(STORAGE_TENANT_KEY, data.schoolId);
        } else {
          // Important: clear stale tenant when signing in as SuperAdmin/global user.
          localStorage.removeItem(STORAGE_TENANT_KEY);
        }
      } catch {
        // ignore
      }
      try {
        if (rememberMe) {
          localStorage.setItem(REMEMBER_ME_KEY, JSON.stringify({ email, password }));
        } else {
          localStorage.removeItem(REMEMBER_ME_KEY);
        }
      } catch {
        // ignore
      }
      const route = data.primaryRole && ROLE_ROUTE[data.primaryRole];
      navigate(route || '/school');
    } catch {
      setError('Network error. Check your connection and try again.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <PageLayout
      variant="auth"
      authHeaderRight={(
        <>
          <Link to="/onboard" className="text-sm font-medium text-indigo-600 hover:text-indigo-700 dark:text-indigo-400">
            Register your school
          </Link>
        </>
      )}
    >
      <div className="login-root login-root--in-shell">
      <div className="login-card">
        <div className="login-header">
          <img
            src="/logos/RiseFlow%20logo.jpg"
            alt=""
            className="login-logo-img"
            aria-hidden="true"
            onError={(e) => {
              e.target.style.display = 'none';
              const fallback = e.target.nextElementSibling;
              if (fallback) fallback.style.display = 'block';
            }}
          />
          <div className="login-logo-dot" style={{ display: 'none' }} aria-hidden="true" />
          <div className="login-header-text">
            <h1>Sign in to RiseFlow</h1>
            <p>School Admins, Teachers, Staff, Parents, Students, Affiliates, and Super Admins sign in here.</p>
          </div>
        </div>
        <form onSubmit={handleSubmit} className="login-form">
          <label className="login-field">
            <span>Email or login ID</span>
            <input
              type="text"
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@schoolname.com or stu-hen20260015"
              required
            />
          </label>
          <label className="login-field">
            <span>Password</span>
            <input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Enter your password"
              required
            />
          </label>
          <label className="login-field" style={{ gap: '0.5rem' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <input
                type="checkbox"
                checked={rememberMe}
                onChange={(e) => setRememberMe(e.target.checked)}
              />
              Remember sign-in details on this device
            </span>
          </label>
          {error && <p className="login-error" role="alert">{error}</p>}
          <button type="submit" className="login-submit" disabled={submitting}>
            {submitting ? 'Signing in…' : 'Sign in'}
          </button>
          <p className="login-footer-text">
            New school?{' '}
            <a href="/onboard">
              Register your school
            </a>
          </p>
        </form>
      </div>
    </div>
    </PageLayout>
  );
}

