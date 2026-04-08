import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

export default function SuperAdminDataOffboardingPage() {
  const [params] = useSearchParams();
  const [schools, setSchools] = useState([]);
  const [schoolId, setSchoolId] = useState(params.get('schoolId') || '');
  const [reason, setReason] = useState('');
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [result, setResult] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    apiFetch('/api/superadmin/schools', { skipTenantHeader: true })
      .then((res) => {
        if (cancelled) return null;
        if (!res.ok) throw new Error('Could not load schools');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setSchools(Array.isArray(data) ? data : []);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Could not load schools');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  const onSubmit = async (e) => {
    e.preventDefault();
    if (!schoolId) {
      setError('Please select a school.');
      return;
    }
    setSubmitting(true);
    setError(null);
    setResult(null);
    try {
      const res = await apiFetch(`/api/superadmin/schools/${schoolId}/offboard`, {
        method: 'POST',
        skipTenantHeader: true,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason, exportRecipientEmail: email || null }),
      });
      if (!res.ok) throw new Error('Offboarding failed');
      const data = await res.json();
      setResult(data);
    } catch (err) {
      setError(err.message || 'Offboarding failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <PageLayout title="Super Admin — Data Offboarding" role="super">
      <h2 className="section-title">Data offboarding</h2>
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}

      {!loading && (
        <form className="card" onSubmit={onSubmit} style={{ maxWidth: 760 }}>
          <label className="form-label">School</label>
          <select className="form-input" value={schoolId} onChange={(e) => setSchoolId(e.target.value)}>
            <option value="">Select school...</option>
            {schools.map((s) => (
              <option key={s.id} value={s.id}>{s.name} ({s.countryCode || '—'})</option>
            ))}
          </select>

          <label className="form-label" style={{ marginTop: '0.75rem' }}>Reason</label>
          <textarea className="form-input" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />

          <label className="form-label" style={{ marginTop: '0.75rem' }}>Export recipient email (optional)</label>
          <input className="form-input" value={email} onChange={(e) => setEmail(e.target.value)} />

          <div className="form-actions" style={{ marginTop: '1rem' }}>
            <button type="submit" className="btn-primary-action" disabled={submitting}>
              {submitting ? 'Processing…' : 'Export and Offboard'}
            </button>
          </div>
        </form>
      )}

      {result && (
        <div className="access-codes-result" style={{ marginTop: '1rem' }}>
          <p><strong>Offboarding completed</strong> for {result.schoolName}.</p>
          <p>Export package: <a href={result.exportUrl} target="_blank" rel="noreferrer">{result.exportFile}</a></p>
          <p>Email notification: {result.notificationSent ? 'Sent' : 'Skipped'}</p>
        </div>
      )}
    </PageLayout>
  );
}