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
    ])
      .then(([dash, revenueStats, list]) => {
        setDashboard(dash || null);
        setRevenue(revenueStats || null);
        setSchools(Array.isArray(list) ? list : []);
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

      <h2 className="section-title">Platform overview</h2>
      {dashboard && (
        <>
          <p className="control-room-intro">Top metrics for platform health (African markets — NDPA/NDPC aware).</p>
          <div className="summary-cards">
            <div className="summary-card">
              <span className="summary-value">{formatMoney(dashboard.monthlyRevenueUsd)}</span>
              <span className="summary-label">Total revenue (MRR) — this month</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{dashboard.activeStudents ?? 0}</span>
              <span className="summary-label">Active students</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{dashboard.totalSchools ?? 0}</span>
              <span className="summary-label">Total schools</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{dashboard.schoolsWithTermResultsCount ?? 0} / {dashboard.activeSchools ?? 0}</span>
              <span className="summary-label">Data health — schools with term results</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{formatMoney(dashboard.totalRevenueUsd)}</span>
              <span className="summary-label">Total revenue (all time)</span>
            </div>
          </div>
        </>
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
