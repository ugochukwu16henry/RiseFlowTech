import { useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import './LoginPage.css';
import { apiFetch, clearAuthStorage, STORAGE_TENANT_KEY } from '../api';

const ROLE_ROUTE = {
  SchoolAdmin: '/school',
  Teacher: '/teacher',
  Parent: '/parent',
  Student: '/student',
  SuperAdmin: '/super-admin',
};

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const trimmedEmail = email.trim();
    if (!trimmedEmail || !password) {
      setError('Enter your email or login ID and password.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const res = await apiFetch('/api/auth/login', {
        method: 'POST',
        skipTenantHeader: true,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: trimmedEmail, password }),
      });
      if (res.status === 401) {
        setError('Incorrect email, login ID or password.');
        return;
      }
      if (!res.ok) {
        setError('Could not sign in. Please try again.');
        return;
      }
      const data = await res.json();
      if (!data?.success) {
        setError(data?.message || 'Incorrect email, login ID or password.');
        return;
      }
      clearAuthStorage();
      try {
        if (data.schoolId) {
          localStorage.setItem(STORAGE_TENANT_KEY, data.schoolId);
        }
      } catch {
        // ignore
      }
      const route = data.primaryRole && ROLE_ROUTE[data.primaryRole];
      window.location.replace(route || '/school');
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
            <p>School Admins, Teachers, Parents, Students and Super Admins sign in here.</p>
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
              placeholder="you@schoolname.com or stu-rf0001"
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

