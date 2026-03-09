import { useState, useEffect } from 'react';
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

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    Promise.all([
      apiFetch('/api/superadmin/dashboard').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/schools').then((r) => (r.ok ? r.json() : null)),
    ])
      .then(([dash, list]) => {
        if (cancelled) return;
        setDashboard(dash || null);
        setSchools(Array.isArray(list) ? list : []);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message || 'Failed to load data');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  if (loading) return <PageLayout title="Super Admin"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (error) return <PageLayout title="Super Admin"><p className="empty-state empty-state--error">{error}</p></PageLayout>;

  return (
    <PageLayout title="Super Admin — Control Room">
      <h2 className="section-title">Platform overview</h2>
      {dashboard && (
        <div className="summary-cards">
          <div className="summary-card">
            <span className="summary-value">{dashboard.totalSchools ?? 0}</span>
            <span className="summary-label">Total schools</span>
          </div>
          <div className="summary-card">
            <span className="summary-value">{dashboard.activeSchools ?? 0}</span>
            <span className="summary-label">Active schools</span>
          </div>
          <div className="summary-card">
            <span className="summary-value">{dashboard.totalStudents ?? 0}</span>
            <span className="summary-label">Total students</span>
          </div>
          <div className="summary-card">
            <span className="summary-value">{formatMoney(dashboard.monthlyRevenueUsd)}</span>
            <span className="summary-label">Monthly revenue</span>
          </div>
          <div className="summary-card">
            <span className="summary-value">{formatMoney(dashboard.totalRevenueUsd)}</span>
            <span className="summary-label">Total revenue</span>
          </div>
        </div>
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

      <h2 className="section-title" style={{ marginTop: '2rem' }}>All schools (from database)</h2>
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
