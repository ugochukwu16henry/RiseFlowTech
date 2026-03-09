import { useState } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';
import './ClaimChildPage.css';

export default function ClaimChildPage() {
  const [code, setCode] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const trimmed = (code || '').trim();
    if (!trimmed) {
      setError('Enter the access code from your school.');
      return;
    }
    setError(null);
    setResult(null);
    setSubmitting(true);
    try {
      const res = await apiFetch('/api/parents/link-by-code', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code: trimmed }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        if (res.status === 401) setError('Please sign in as a parent to claim your child.');
        else if (res.status === 404) setError(data?.message || 'Invalid or expired code. Check the code with your school.');
        else setError(data?.message || 'Something went wrong.');
        return;
      }
      setResult(data);
      setCode('');
    } catch (e) {
      setError(e.message || 'Network error.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <PageLayout title="Claim your child" backTo="/parent">
      <div className="claim-child">
        <p className="card-desc">
          Enter the <strong>Parent Access Code</strong> (e.g. <strong>RF-8821</strong>) that your school gave you. You will be linked to your child&apos;s profile and can view results and contact teachers.
        </p>

        <form onSubmit={handleSubmit} className="claim-form">
          <label htmlFor="claim-code" className="claim-label">
            Access code
          </label>
          <input
            id="claim-code"
            type="text"
            inputMode="text"
            autoComplete="off"
            placeholder="e.g. RF-8821"
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            className="claim-input"
            maxLength={16}
            aria-describedby={error ? 'claim-error' : undefined}
          />
          {error && (
            <p id="claim-error" className="claim-error" role="alert">
              {error}
            </p>
          )}
          <button type="submit" className="btn-claim" disabled={submitting}>
            {submitting ? 'Linking…' : 'Claim my child'}
          </button>
        </form>

        {result && (
          <div className="claim-success" role="status">
            <p className="claim-success-title">Child linked</p>
            <p className="claim-success-msg">{result.message}</p>
            <p className="card-desc">You can now see {result.studentName}&apos;s results and teacher contacts on your Parent dashboard.</p>
          </div>
        )}
      </div>
    </PageLayout>
  );
}
