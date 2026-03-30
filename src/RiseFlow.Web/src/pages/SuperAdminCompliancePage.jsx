import { useEffect, useState } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

export default function SuperAdminCompliancePage() {
  const [form, setForm] = useState({
    dataProtectionOfficerName: '',
    dataProtectionOfficerEmail: '',
    dpiaDocumentUrl: '',
  });
  const [lastUpdated, setLastUpdated] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    apiFetch('/api/superadmin/compliance-settings')
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (cancelled || !data) return;
        setForm({
          dataProtectionOfficerName: data.dataProtectionOfficerName || '',
          dataProtectionOfficerEmail: data.dataProtectionOfficerEmail || '',
          dpiaDocumentUrl: data.dpiaDocumentUrl || '',
        });
        setLastUpdated(data.lastUpdatedUtc || null);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Could not load settings');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  const onSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setMessage('');
    setError(null);
    try {
      const res = await apiFetch('/api/superadmin/compliance-settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(form),
      });
      if (!res.ok) throw new Error('Could not save compliance settings');
      const data = await res.json();
      setLastUpdated(data?.lastUpdatedUtc || null);
      setMessage('Compliance settings saved.');
    } catch (err) {
      setError(err.message || 'Could not save compliance settings');
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageLayout title="Super Admin — System Settings" role="super">
      <h2 className="section-title">Compliance settings</h2>
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}

      {!loading && (
        <form className="card" onSubmit={onSubmit} style={{ maxWidth: 720 }}>
          <label className="form-label">Data Protection Officer Name</label>
          <input
            className="form-input"
            value={form.dataProtectionOfficerName}
            onChange={(e) => setForm((p) => ({ ...p, dataProtectionOfficerName: e.target.value }))}
          />

          <label className="form-label" style={{ marginTop: '0.75rem' }}>Data Protection Officer Email</label>
          <input
            className="form-input"
            value={form.dataProtectionOfficerEmail}
            onChange={(e) => setForm((p) => ({ ...p, dataProtectionOfficerEmail: e.target.value }))}
          />

          <label className="form-label" style={{ marginTop: '0.75rem' }}>DPIA Document URL</label>
          <input
            className="form-input"
            value={form.dpiaDocumentUrl}
            onChange={(e) => setForm((p) => ({ ...p, dpiaDocumentUrl: e.target.value }))}
          />

          <div className="form-actions" style={{ marginTop: '1rem', justifyContent: 'space-between' }}>
            <small className="card-desc">Last updated: {lastUpdated ? new Date(lastUpdated).toLocaleString() : 'Never'}</small>
            <button type="submit" className="btn-primary-action" disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </button>
          </div>
          {message && <p className="empty-state" style={{ color: 'var(--color-success-text)', marginTop: '0.5rem' }}>{message}</p>}
        </form>
      )}
    </PageLayout>
  );
}