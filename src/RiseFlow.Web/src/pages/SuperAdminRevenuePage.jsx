import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

function formatMoney(amount, currencyCode = 'NGN') {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: currencyCode, maximumFractionDigits: 0 }).format(n);
}

export default function SuperAdminRevenuePage() {
  const [revenue, setRevenue] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    apiFetch('/api/superadmin/revenue', { skipTenantHeader: true })
      .then((res) => {
        if (cancelled) return null;
        if (!res.ok) throw new Error('Could not load revenue');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setRevenue(data || null);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Failed to load revenue');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  return (
    <PageLayout title="Super Admin — Revenue Hub" role="super">
      <h2 className="section-title">Revenue hub</h2>
      <div className="dashboard-actions" style={{ flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Link to="/super-admin" className="btn-primary-action btn-primary-action--ghost">Control room</Link>
        <Link to="/super-admin/schools" className="btn-primary-action btn-primary-action--ghost">Schools</Link>
        <Link to="/super-admin/affiliates" className="btn-primary-action btn-primary-action--ghost">Affiliates</Link>
        <Link to="/super-admin/data-offboarding" className="btn-primary-action btn-primary-action--ghost">Data offboarding</Link>
      </div>
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}

      {!loading && !error && revenue && (
        <>
          <div className="summary-cards">
            <div className="summary-card">
              <span className="summary-value">{formatMoney(revenue.totalOneTimeFees)}</span>
              <span className="summary-label">One-time fees</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{formatMoney(revenue.totalMonthlySubscriptions)}</span>
              <span className="summary-label">Monthly subscriptions</span>
            </div>
            <div className="summary-card summary-card--warning">
              <span className="summary-value">{formatMoney(revenue.totalRevenue)}</span>
              <span className="summary-label">Total revenue</span>
            </div>
          </div>

          <h3 className="card-title" style={{ marginTop: '1.25rem' }}>Top revenue schools</h3>
          {Array.isArray(revenue.topRevenueSchools) && revenue.topRevenueSchools.length > 0 ? (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>School</th>
                    <th>Students</th>
                    <th>Monthly income</th>
                    <th>Total paid</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {revenue.topRevenueSchools.map((s) => (
                    <tr key={s.schoolId}>
                      <td>{s.schoolName}</td>
                      <td>{s.studentCount ?? 0}</td>
                      <td>{formatMoney(s.monthlyIncome)}</td>
                      <td>{formatMoney(s.totalPaidToDate)}</td>
                      <td>
                        <Link to={`/super-admin/schools?schoolId=${s.schoolId}`} className="btn-primary-action btn-primary-action--ghost">
                          View school
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="empty-state">No revenue rows yet.</p>
          )}
        </>
      )}
    </PageLayout>
  );
}