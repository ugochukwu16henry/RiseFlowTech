import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './AffiliatePage.css';

const initialForm = {
  fullName: '',
  email: '',
  phoneNumber: '',
  countryCode: 'NG',
  note: '',
};

export default function AffiliateProgramPage() {
  const [info, setInfo] = useState(null);
  const [form, setForm] = useState(initialForm);
  const [submitting, setSubmitting] = useState(false);
  const [status, setStatus] = useState({ type: null, message: null });

  useEffect(() => {
    let cancelled = false;
    apiFetch('/api/affiliate-program/info', { skipTenantHeader: true })
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (!cancelled) setInfo(data);
      })
      .catch(() => {
        if (!cancelled) setInfo(null);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((current) => ({ ...current, [name]: value }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (!form.fullName.trim() || !form.email.trim()) {
      setStatus({ type: 'error', message: 'Full name and email are required.' });
      return;
    }

    setSubmitting(true);
    setStatus({ type: null, message: null });
    try {
      const res = await apiFetch('/api/affiliate-program/requests', {
        method: 'POST',
        skipTenantHeader: true,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fullName: form.fullName.trim(),
          email: form.email.trim(),
          phoneNumber: form.phoneNumber.trim() || null,
          countryCode: form.countryCode.trim() || 'NG',
          note: form.note.trim() || null,
        }),
      });

      const data = await res.json().catch(() => null);
      if (!res.ok) {
        setStatus({ type: 'error', message: data || 'Could not send your affiliate request.' });
        return;
      }

      setStatus({
        type: 'success',
        message: 'Your request has been received. The RiseFlow Super Admin team can now approve and send you a private affiliate invite link.',
      });
      setForm(initialForm);
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
      <div className="mx-auto max-w-6xl px-4 py-8 space-y-6">
        <section className="affiliate-public-hero">
          <div>
            <p className="dashboard-label">RiseFlow Affiliate Program</p>
            <h1 className="text-3xl font-bold text-slate-900 dark:text-slate-50">Refer schools. Earn for life.</h1>
            <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">
              Get approved by RiseFlow, receive your private referral link, and earn recurring income for every billable student above the free 50-student tier in schools you bring to the platform.
            </p>
          </div>
          <div className="summary-cards">
            <div className="summary-card">
              <span className="summary-value">₦60</span>
              <span className="summary-label">One-time per new billable student</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">₦20</span>
              <span className="summary-label">Monthly per billable student</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">50</span>
              <span className="summary-label">Students always free for each school</span>
            </div>
          </div>
        </section>

        {info && (
          <section className="progress-section">
            <h2 className="section-title">How the model works</h2>
            <p className="card-desc">{info.summary}</p>
            <div className="summary-cards">
              <div className="summary-card">
                <span className="summary-value">₦{Number(info.activationFeePerStudent || 0).toLocaleString()}</span>
                <span className="summary-label">School activation from student 51</span>
              </div>
              <div className="summary-card">
                <span className="summary-value">₦{Number(info.monthlyFeePerStudent || 0).toLocaleString()}</span>
                <span className="summary-label">Monthly school fee from student 51</span>
              </div>
              <div className="summary-card">
                <span className="summary-value">Invite-only</span>
                <span className="summary-label">Super Admin approval required</span>
              </div>
            </div>
          </section>
        )}

        <section className="progress-section">
          <h2 className="section-title">Request an affiliate invite</h2>
          <form className="affiliate-form-grid" onSubmit={handleSubmit}>
            <label>
              <span className="dashboard-label">Full name</span>
              <input className="form-input" name="fullName" value={form.fullName} onChange={handleChange} placeholder="Your full name" />
            </label>
            <label>
              <span className="dashboard-label">Email</span>
              <input className="form-input" type="email" name="email" value={form.email} onChange={handleChange} placeholder="you@example.com" />
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
              <span className="dashboard-label">Why do you want to join? (optional)</span>
              <textarea className="form-input" rows="4" name="note" value={form.note} onChange={handleChange} placeholder="Tell us about your network or experience introducing schools to software." />
            </label>

            {status.message && (
              <p className={status.type === 'error' ? 'empty-state empty-state--error affiliate-form-grid__wide' : 'affiliate-note affiliate-form-grid__wide'}>
                {status.message}
              </p>
            )}

            <div className="affiliate-form-grid__wide dashboard-actions">
              <button type="submit" className="btn-primary-action" disabled={submitting}>
                {submitting ? 'Sending…' : 'Request my affiliate link'}
              </button>
              <Link to="/onboard" className="btn-primary-action btn-primary-action--ghost">
                Register a school instead
              </Link>
            </div>
          </form>
        </section>
      </div>
    </PageLayout>
  );
}
