import { useState } from 'react';
import { useSearchParams, useNavigate, Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { getApiBase } from '../api';
import './RolePages.css';
import './ClaimChildPage.css';

export default function ParentSignupPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const schoolId = searchParams.get('school')?.trim() || '';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phone, setPhone] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!schoolId) {
      setError('Invalid signup link. Get the correct link from your school.');
      return;
    }
    const trimmedEmail = email.trim();
    if (!trimmedEmail) {
      setError('Email is required.');
      return;
    }
    if (!password || password.length < 6) {
      setError('Password must be at least 6 characters.');
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch(`${getApiBase()}/api/parents/signup`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          schoolId,
          email: trimmedEmail,
          password,
          firstName: firstName.trim() || undefined,
          lastName: lastName.trim() || undefined,
          phone: phone.trim() || undefined,
        }),
      });
      const text = await res.text();
      const data = text ? (() => { try { return JSON.parse(text); } catch { return { message: text }; } })() : {};
      if (!res.ok) {
        setError(data?.message || data?.title || text || 'Signup failed. Try again.');
        return;
      }
      navigate('/parent/claim?signedUp=1', { replace: true });
    } catch (e) {
      setError(e.message || 'Network error.');
    } finally {
      setSubmitting(false);
    }
  };

  if (!schoolId) {
    return (
      <PageLayout title="Parent signup">
        <div className="claim-child">
          <p className="empty-state empty-state--error">
            This signup link is invalid or missing the school. Ask your school for the correct parent signup link.
          </p>
          <Link to="/parent/claim" className="header-link" style={{ marginTop: '1rem', display: 'inline-block' }}>
            I already have an account — Claim my child
          </Link>
        </div>
      </PageLayout>
    );
  }

  return (
    <PageLayout title="Parent signup" backTo="/parent">
      <div className="claim-child parent-signup">
        <p className="card-desc">
          Create your parent account for this school. After signup, sign in and enter the <strong>Parent Access Code</strong> from your school to link your child.
        </p>

        <form onSubmit={handleSubmit} className="claim-form signup-form">
          <label htmlFor="signup-email" className="claim-label">Email</label>
          <input
            id="signup-email"
            type="email"
            autoComplete="email"
            placeholder="you@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="claim-input signup-input"
            required
          />

          <label htmlFor="signup-password" className="claim-label">Password</label>
          <input
            id="signup-password"
            type="password"
            autoComplete="new-password"
            placeholder="At least 6 characters"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="claim-input signup-input"
            minLength={6}
            required
          />

          <label htmlFor="signup-first" className="claim-label">First name</label>
          <input
            id="signup-first"
            type="text"
            autoComplete="given-name"
            placeholder="First name"
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            className="claim-input signup-input"
          />

          <label htmlFor="signup-last" className="claim-label">Last name</label>
          <input
            id="signup-last"
            type="text"
            autoComplete="family-name"
            placeholder="Last name"
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            className="claim-input signup-input"
          />

          <label htmlFor="signup-phone" className="claim-label">Phone (optional)</label>
          <input
            id="signup-phone"
            type="tel"
            autoComplete="tel"
            placeholder="Phone number"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            className="claim-input signup-input"
          />

          {error && (
            <p className="claim-error" role="alert">{error}</p>
          )}
          <button type="submit" className="btn-claim" disabled={submitting}>
            {submitting ? 'Creating account…' : 'Create account'}
          </button>
        </form>

        <p className="card-desc" style={{ marginTop: '1.25rem' }}>
          Already have an account? <Link to="/parent/claim" className="header-link">Sign in and claim your child</Link>.
        </p>
      </div>
    </PageLayout>
  );
}
