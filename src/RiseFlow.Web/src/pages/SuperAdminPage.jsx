import { useState, useEffect, useCallback } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

function formatMoney(amount, currencyCode = 'USD') {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: currencyCode, maximumFractionDigits: 0 }).format(n);
}

export default function SuperAdminPage() {
  const [dashboard, setDashboard] = useState(null);
  const [schools, setSchools] = useState([]);
  const [audit, setAudit] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [markingId, setMarkingId] = useState(null);
  const [revenue, setRevenue] = useState(null);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    Promise.all([
      apiFetch('/api/superadmin/dashboard').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/superadmin/revenue').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/schools').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/superadmin/audit?limit=50').then((r) => (r.ok ? r.json() : [])),
    ])
      .then(([dash, revenueStats, list, auditLog]) => {
        setDashboard(dash || null);
        setRevenue(revenueStats || null);
        setSchools(Array.isArray(list) ? list : []);
        setAudit(Array.isArray(auditLog) ? auditLog : []);
      })
      .catch((err) => setError(err.message || 'Failed to load data'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function markDataConsentReceived(schoolId) {
    setMarkingId(schoolId);
    try {
      const r = await apiFetch(`/api/schools/${schoolId}/data-consent-received`, { method: 'PATCH' });
      if (r.ok) load();
    } finally {
      setMarkingId(null);
    }
  }

  if (loading) return <PageLayout title="Super Admin"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (error) return <PageLayout title="Super Admin"><p className="empty-state empty-state--error">{error}</p></PageLayout>;

  return (
    <PageLayout title="Super Admin — Control Room">
      {/* Revenue hub: cash flow vs recurring revenue */}
      {revenue && (
        <>
          <h2 className="section-title">Revenue hub</h2>
          <div className="sa-revenue-grid">
            <div className="sa-revenue-card sa-revenue-card--total">
              <p className="sa-revenue-label">Total combined revenue</p>
              <h3 className="sa-revenue-value-big">
                {formatMoney(revenue.totalRevenue, 'NGN')}
              </h3>
              <div className="sa-revenue-chip-row">
                <span className="sa-revenue-chip">+12% from last month</span>
              </div>
            </div>

            <div className="sa-revenue-card">
              <p className="sa-revenue-label-muted">Activation fees (one‑time)</p>
              <h3 className="sa-revenue-value">
                {formatMoney(revenue.totalOneTimeFees, 'NGN')}
              </h3>
              <p className="sa-revenue-sub">Generated from ₦500/new student after 50.</p>
            </div>

            <div className="sa-revenue-card">
              <p className="sa-revenue-label-accent">Monthly subscriptions (MRR)</p>
              <h3 className="sa-revenue-value">
                {formatMoney(revenue.totalMonthlySubscriptions, 'NGN')}
              </h3>
              <p className="sa-revenue-sub">Recurring ₦100 per billable student.</p>
            </div>
          </div>

          {revenue.topRevenueSchools?.length > 0 && (
            <div className="sa-revenue-table">
              <div className="sa-revenue-table-header">
                <h4>Highest revenue schools</h4>
                {/* Future: link to full schools list / filters */}
                <button type="button" className="sa-revenue-view-all">
                  View all schools
                </button>
              </div>
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>School name</th>
                      <th>Students</th>
                      <th>Monthly income</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {revenue.topRevenueSchools.map((s) => {
                      const billable = Math.max(0, (s.studentCount || 0) - 50);
                      return (
                        <tr key={s.schoolId}>
                          <td className="sa-revenue-school-name">{s.schoolName}</td>
                          <td>{s.studentCount} ({billable} billable)</td>
                          <td className="sa-revenue-monthly">
                            {formatMoney(s.monthlyIncome, 'NGN')}
                          </td>
                          <td>
                            <span className="sa-revenue-status-pill">Active</span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}

      {/* Platform overview: enrollment, schools, data health, billing records */}
      <h2 className="section-title">Platform overview</h2>
      {dashboard && (
        <div className="sa-revenue-grid" style={{ marginBottom: '1.5rem' }}>
          <div className="sa-revenue-card">
            <p className="sa-revenue-label-muted">Active students</p>
            <h3 className="sa-revenue-value">
              {dashboard.activeStudents ?? 0}
            </h3>
            <p className="sa-revenue-sub">Across all active schools.</p>
          </div>
          <div className="sa-revenue-card">
            <p className="sa-revenue-label-muted">Total schools</p>
            <h3 className="sa-revenue-value">
              {dashboard.totalSchools ?? 0}
            </h3>
            <p className="sa-revenue-sub">{dashboard.activeSchools ?? 0} currently active.</p>
          </div>
          <div className="sa-revenue-card">
            <p className="sa-revenue-label-muted">Data health</p>
            <h3 className="sa-revenue-value">
              {dashboard.schoolsWithTermResultsCount ?? 0} / {dashboard.activeSchools ?? 0}
            </h3>
            <p className="sa-revenue-sub">Schools with term results uploaded.</p>
          </div>
          <div className="sa-revenue-card">
            <p className="sa-revenue-label-muted">Billing records</p>
            <h3 className="sa-revenue-value">
              {dashboard.billingRecordsCount ?? 0}
            </h3>
            <p className="sa-revenue-sub">Total invoices generated to date.</p>
          </div>
        </div>
      )}

      {dashboard?.paymentDelinquency?.length > 0 && (
        <>
          <h3 className="card-title" style={{ marginTop: '1.5rem' }}>Payment delinquency</h3>
          <p className="card-desc">Schools with more than 50 students that have not paid (may result in read-only access).</p>
          <div className="data-table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>School</th>
                  <th>Students</th>
                  <th>Amount due</th>
                </tr>
              </thead>
              <tbody>
                {dashboard.paymentDelinquency.map((d) => (
                  <tr key={d.schoolId}>
                    <td>{d.schoolName}</td>
                    <td>{d.studentCount}</td>
                    <td>{formatMoney(d.amountDue, d.currencyCode)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {dashboard?.compliancePending?.length > 0 && (
        <>
          <h3 className="card-title" style={{ marginTop: '1.5rem' }}>Compliance status — signed Data Consent not yet received</h3>
          <p className="card-desc">Schools that have not yet uploaded their signed Data Consent forms (NDPA 2023). Mark when you receive them.</p>
          <ul className="compliance-list">
            {dashboard.compliancePending.map((s) => (
              <li key={s.schoolId} className="compliance-item">
                <span>{s.schoolName}</span>
                <button type="button" className="btn-mark-received" onClick={() => markDataConsentReceived(s.schoolId)} disabled={markingId === s.schoolId}>
                  {markingId === s.schoolId ? 'Saving…' : 'Mark received'}
                </button>
              </li>
            ))}
          </ul>
        </>
      )}

      {dashboard?.schoolsByCountry?.length > 0 && (
        <>
          <h3 className="card-title" style={{ marginTop: '1.5rem' }}>Schools by country</h3>
          <ul className="country-list">
            {dashboard.schoolsByCountry.map((c) => (
              <li key={c.countryCode} className="country-item">
                <span className="country-name">{c.countryName}</span>
                <span className="country-count">{c.schoolCount} schools</span>
              </li>
            ))}
          </ul>
        </>
      )}

      {/* Recent audit log: who did what, and where */}
      {audit.length > 0 && (
        <>
          <h3 className="card-title" style={{ marginTop: '1.5rem' }}>Recent activity (audit log)</h3>
          <p className="card-desc">Grade changes, billing events, and sensitive actions across all schools.</p>
          <div className="data-table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>When</th>
                  <th>School</th>
                  <th>Action</th>
                  <th>Entity</th>
                  <th>User</th>
                </tr>
              </thead>
              <tbody>
                {audit.map((a) => (
                  <tr key={a.id}>
                    <td>{new Date(a.createdAtUtc).toLocaleString()}</td>
                    <td>{a.schoolId || '—'}</td>
                    <td>{a.action}</td>
                    <td>{a.entityType}</td>
                    <td>{a.userEmail || a.userName || '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      <h2 className="section-title" style={{ marginTop: '2rem' }}>All schools</h2>
      {schools.length === 0 ? (
        <p className="empty-state">No schools yet. Schools register via the onboarding page.</p>
      ) : (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>School name</th>
                <th>Address</th>
                <th>Country</th>
                <th>Currency</th>
              </tr>
            </thead>
            <tbody>
              {schools.map((s) => (
                <tr key={s.id}>
                  <td>{s.name}</td>
                  <td>{s.address || '—'}</td>
                  <td>{s.countryCode || '—'}</td>
                  <td>{s.currencyCode || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </PageLayout>
  );
}
