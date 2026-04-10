import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './AffiliatePage.css';

export default function AffiliateSignupPage() {
  const [searchParams] = useSearchParams();
  const inviteToken = searchParams.get('invite') || '';
  const [validation, setValidation] = useState({ loading: true, valid: false, email: '', message: '' });
  const [form, setForm] = useState({ fullName: '', email: '', phoneNumber: '', countryCode: 'NG', password: '' });
  const [submitting, setSubmitting] = useState(false);
  const [status, setStatus] = useState({ type: null, message: null });

  useEffect(() => {
    let cancelled = false;
    if (!inviteToken) {
      setValidation({ loading: false, valid: false, email: '', message: 'No invite token was provided.' });
      return undefined;
    }

    apiFetch(`/api/affiliate-program/invites/${encodeURIComponent(inviteToken)}`, { skipTenantHeader: true })
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (cancelled) return;
        setValidation({ loading: false, valid: Boolean(data?.isValid), email: data?.email || '', message: data?.message || '' });
        setForm((current) => ({ ...current, email: data?.email || current.email }));
      })
      .catch(() => {
        if (!cancelled) setValidation({ loading: false, valid: false, email: '', message: 'Could not validate this invite right now.' });
      });

    return () => {
      cancelled = true;
    };
  }, [inviteToken]);

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((current) => ({ ...current, [name]: value }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (!inviteToken || !form.email.trim() || !form.password) {
      setStatus({ type: 'error', message: 'Email and password are required.' });
      return;
    }

    setSubmitting(true);
    setStatus({ type: null, message: null });
    try {
      const res = await apiFetch(`/api/affiliate-program/invites/${encodeURIComponent(inviteToken)}/complete`, {
        method: 'POST',
        skipTenantHeader: true,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fullName: form.fullName.trim() || null,
          email: form.email.trim(),
          phoneNumber: form.phoneNumber.trim() || null,
          countryCode: form.countryCode.trim() || 'NG',
          password: form.password,
        }),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        setStatus({ type: 'error', message: data || 'Could not complete affiliate signup.' });
        return;
      }
      setStatus({ type: 'success', message: data?.message || 'Affiliate account created successfully. You can now sign in.' });
    } catch {
      setStatus({ type: 'error', message: 'Network error. Please try again.' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <PageLayout
      variant="auth"
      authHeaderRight={(
        <>
          <Link to="/login" className="text-sm font-medium text-indigo-600 hover:text-indigo-700 dark:text-indigo-400">
            Sign in
          </Link>
        </>
      )}
    >
      <div className="mx-auto max-w-3xl px-4 py-8">
        <div className="progress-section">
          <h1 className="section-title">Complete your affiliate signup</h1>
          {validation.loading && <p className="empty-state">Validating invite…</p>}
          {!validation.loading && !validation.valid && (
            <p className="empty-state empty-state--error">{validation.message || 'This invite link is not valid.'}</p>
          )}

          {!validation.loading && validation.valid && (
            <form className="affiliate-form-grid" onSubmit={handleSubmit}>
              <label>
                <span className="dashboard-label">Invite email</span>
                <input className="form-input" name="email" value={form.email} onChange={handleChange} readOnly />
              </label>
              <label>
                <span className="dashboard-label">Full name</span>
                <input className="form-input" name="fullName" value={form.fullName} onChange={handleChange} placeholder="Your full name" />
              </label>
              <label>
                <span className="dashboard-label">Phone number</span>
                <input className="form-input" name="phoneNumber" value={form.phoneNumber} onChange={handleChange} placeholder="0800 000 0000" />
              </label>
              <label>
                <span className="dashboard-label">Country</span>
                <input className="form-input" name="countryCode" value={form.countryCode} onChange={handleChange} placeholder="NG" />
              </label>
              <label className="affiliate-form-grid__wide">
                <span className="dashboard-label">Create password</span>
                <input className="form-input" type="password" name="password" value={form.password} onChange={handleChange} placeholder="Minimum 8 characters" />
              </label>

              {status.message && (
                <p className={status.type === 'error' ? 'empty-state empty-state--error affiliate-form-grid__wide' : 'affiliate-note affiliate-form-grid__wide'}>
                  {status.message}
                </p>
              )}

              <div className="affiliate-form-grid__wide dashboard-actions">
                <button type="submit" className="btn-primary-action" disabled={submitting}>
                  {submitting ? 'Creating account…' : 'Finish affiliate signup'}
                </button>
                <Link to="/login" className="btn-primary-action btn-primary-action--ghost">
                  Go to sign in
                </Link>
              </div>
            </form>
          )}
        </div>
      </div>
    </PageLayout>
  );
}
