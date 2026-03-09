import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import './LoginPage.css';
import { apiFetch, STORAGE_TENANT_KEY } from '../api';

const ROLE_ROUTE = {
  SchoolAdmin: '/school',
  Teacher: '/teacher',
  Parent: '/parent',
  Student: '/student',
  SuperAdmin: '/super-admin',
};

export default function LoginPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!email || !password) {
      setError('Enter your email and password.');
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
        setError('Incorrect email or password.');
        return;
      }
      if (!res.ok) {
        setError('Could not sign in. Please try again.');
        return;
      }
      const data = await res.json();
      if (!data?.success) {
        setError(data?.message || 'Incorrect email or password.');
        return;
      }
      try {
        if (data.schoolId) {
          localStorage.setItem(STORAGE_TENANT_KEY, data.schoolId);
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
    <div className="login-root">
      <div className="login-card">
        <div className="login-header">
          <div className="login-logo-dot" aria-hidden="true" />
          <div>
            <h1>Sign in to RiseFlow</h1>
            <p>School Admins, Teachers, Parents and Super Admins sign in here.</p>
          </div>
        </div>
        <form onSubmit={handleSubmit} className="login-form">
          <label className="login-field">
            <span>Email</span>
            <input
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@schoolname.com"
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
  );
}

