import { useState, useEffect } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';

function formatMoney(amount, currencyCode) {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: currencyCode || 'NGN', maximumFractionDigits: 0 }).format(n);
}

export default function SchoolAdminPage() {
  const [dashboard, setDashboard] = useState(null);
  const [teachers, setTeachers] = useState([]);
  const [students, setStudents] = useState([]);
  const [billing, setBilling] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    Promise.all([
      apiFetch('/api/schools/dashboard').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/teachers').then((r) => (r.ok ? r.json() : [])),
      apiFetch('/api/students').then((r) => (r.ok ? r.json() : [])),
      apiFetch('/api/billing').then((r) => (r.ok ? r.json() : [])),
    ])
      .then(([dash, tList, sList, bList]) => {
        if (cancelled) return;
        setDashboard(dash);
        setTeachers(Array.isArray(tList) ? tList : []);
        setStudents(Array.isArray(sList) ? sList : []);
        setBilling(Array.isArray(bList) ? bList : []);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message || 'Failed to load data');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  if (loading) return <PageLayout title="School Admin"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (error) return <PageLayout title="School Admin"><p className="empty-state empty-state--error">{error}</p></PageLayout>;

  const currencyCode = dashboard?.currencyCode || 'NGN';

  return (
    <PageLayout title="School Admin">
      <h2 className="section-title">Dashboard (from database)</h2>
      {dashboard && (
        <div className="summary-cards">
          <div className="summary-card">
            <span className="summary-value">{dashboard.activeStudentCount ?? 0}</span>
            <span className="summary-label">Active students</span>
          </div>
          <div className="summary-card summary-card--warning">
            <span className="summary-value">{formatMoney(dashboard.unpaidFeesTotal, currencyCode)}</span>
            <span className="summary-label">Unpaid fees</span>
          </div>
        </div>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Teachers</h2>
      {teachers.length === 0 ? (
        <p className="empty-state">No teachers yet.</p>
      ) : (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Phone</th>
              </tr>
            </thead>
            <tbody>
              {teachers.map((t) => (
                <tr key={t.id}>
                  <td>{[t.firstName, t.middleName, t.lastName].filter(Boolean).join(' ')}</td>
                  <td>{t.email || '—'}</td>
                  <td>{t.phone || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Students</h2>
      {students.length === 0 ? (
        <p className="empty-state">No students yet. Use bulk upload or add manually.</p>
      ) : (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Admission #</th>
                <th>Class</th>
              </tr>
            </thead>
            <tbody>
              {students.slice(0, 50).map((s) => (
                <tr key={s.id}>
                  <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                  <td>{s.admissionNumber || '—'}</td>
                  <td>{s.class?.name || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {students.length > 50 && <p className="card-desc" style={{ marginTop: '0.5rem' }}>Showing first 50 of {students.length} students.</p>}
        </div>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Bulk upload</h2>
      <form method="GET" action={`${getApiBase()}/api/students/bulk-upload-template`} target="_blank" className="bulk-download-form">
        <button type="submit" className="btn-download-template">Download Excel template</button>
      </form>

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Billing records (from database)</h2>
      {billing.length === 0 ? (
        <p className="empty-state">No billing records yet.</p>
      ) : (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Period</th>
                <th>Amount due</th>
                <th>Amount paid</th>
              </tr>
            </thead>
            <tbody>
              {billing.map((b) => (
                <tr key={b.id}>
                  <td>{b.periodLabel || '—'}</td>
                  <td>{formatMoney(b.amountDue, b.currencyCode)}</td>
                  <td>{b.amountPaid != null ? formatMoney(b.amountPaid, b.currencyCode) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </PageLayout>
  );
}
