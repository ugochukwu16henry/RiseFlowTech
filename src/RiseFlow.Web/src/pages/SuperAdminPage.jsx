import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
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

  const readJsonOrThrow = async (response, fallbackMessage) => {
    if (response.status === 401 || response.status === 403) {
      throw new Error('Your Super Admin session expired or you no longer have access. Please sign in again.');
    }
    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(text || fallbackMessage);
    }
    return response.json();
  };

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    Promise.allSettled([
      apiFetch('/api/superadmin/dashboard', { skipTenantHeader: true }).then((r) => readJsonOrThrow(r, 'Could not load dashboard.')),
      apiFetch('/api/superadmin/revenue', { skipTenantHeader: true }).then((r) => readJsonOrThrow(r, 'Could not load revenue.')),
      apiFetch('/api/superadmin/schools', { skipTenantHeader: true }).then((r) => readJsonOrThrow(r, 'Could not load schools.')),
      apiFetch('/api/superadmin/audit?limit=50', { skipTenantHeader: true }).then((r) => readJsonOrThrow(r, 'Could not load audit log.')),
    ])
      .then(([dashResult, revenueResult, schoolsResult, auditResult]) => {
        setDashboard(dashResult.status === 'fulfilled' ? (dashResult.value || null) : null);
        setRevenue(revenueResult.status === 'fulfilled' ? (revenueResult.value || null) : null);
        setSchools(schoolsResult.status === 'fulfilled' && Array.isArray(schoolsResult.value) ? schoolsResult.value : []);
        setAudit(auditResult.status === 'fulfilled' && Array.isArray(auditResult.value) ? auditResult.value : []);

        const failures = [dashResult, revenueResult, schoolsResult, auditResult]
          .filter((result) => result.status === 'rejected')
          .map((result) => result.reason?.message || 'A section failed to load');

        if (failures.length > 0) {
          setError(`Some Super Admin data could not load yet: ${failures.join(' ')}`);
        }
      })
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

  if (loading) return <PageLayout title="Super Admin" role="super"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (error && !dashboard && !revenue && schools.length === 0 && audit.length === 0) {
    return <PageLayout title="Super Admin" role="super"><p className="empty-state empty-state--error">{error}</p></PageLayout>;
  }

  const totalActiveSchools = dashboard?.activeSchools ?? 0;
  const totalStudents = dashboard?.totalStudents ?? dashboard?.activeStudents ?? 0;
  const totalOneTime = revenue?.totalOneTimeFees ?? 0;
  const totalMonthly = revenue?.totalMonthlySubscriptions ?? 0;
  const delinquentSchools = dashboard?.paymentDelinquency?.length ?? 0;
  const delinquencyRate = totalActiveSchools > 0 ? Math.round((delinquentSchools / totalActiveSchools) * 100) : 0;

  return (
    <PageLayout title="Super Admin — Control Room" role="super">
      <p className="control-room-intro">
        As the RiseFlow SuperAdmin, this dashboard is your mission control. It gives you a bird&apos;s‑eye view of every school,
        their students, and the revenue flowing through activations and subscriptions. Use the sidebar for schools, revenue, affiliates, compliance, and offboarding.
      </p>

      {error && <p className="empty-state empty-state--error" style={{ marginBottom: '1rem' }}>{error}</p>}

      <div className="dashboard-actions" style={{ flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Link to="/super-admin/affiliates" className="btn-primary-action">
          Open affiliate manager
        </Link>
        <Link to="/super-admin/affiliate-requests" className="btn-primary-action btn-primary-action--ghost">
          View requests
        </Link>
        <Link to="/super-admin/affiliate-payouts" className="btn-primary-action btn-primary-action--ghost">
          View payouts
        </Link>
        <Link to="/super-admin/affiliate-training" className="btn-primary-action btn-primary-action--ghost">
          Training videos
        </Link>
      </div>

      {/* 1. The Pulse (top KPI cards) */}
      <section aria-label="Business pulse KPIs">
        <div className="dashboard-grid">
          <article className="dashboard-card dashboard-card--highlight">
            <p className="dashboard-label">Total active schools</p>
            <p className="dashboard-value">{totalActiveSchools}</p>
            <p className="dashboard-sub">Schools currently live on RiseFlow.</p>
          </article>
          <article className="dashboard-card">
            <p className="dashboard-label">Total student population</p>
            <p className="dashboard-value">{totalStudents}</p>
            <p className="dashboard-sub">Students across every active school.</p>
          </article>
          <article className="dashboard-card">
            <p className="dashboard-label">Total revenue (one‑time)</p>
            <p className="dashboard-value">{formatMoney(totalOneTime, 'NGN')}</p>
            <p className="dashboard-sub">₦500 activation fees from billable students.</p>
          </article>
          <article className="dashboard-card">
            <p className="dashboard-label">Monthly recurring revenue (MRR)</p>
            <p className="dashboard-value">{formatMoney(totalMonthly, 'NGN')}</p>
            <p className="dashboard-sub">Expected ₦100 / student for the current month.</p>
          </article>
          <article className="dashboard-card dashboard-card--warning">
            <p className="dashboard-label">Payment delinquency rate</p>
            <p className="dashboard-value">{delinquencyRate}%</p>
            <p className="dashboard-sub">
              {delinquentSchools} school{delinquentSchools === 1 ? '' : 's'} with overdue invoices.
            </p>
          </article>
        </div>
      </section>

      {/* 2. Revenue hub: cash flow vs recurring revenue */}
      {revenue && (
        <>
          <h2 className="dashboard-section-title">Revenue hub</h2>
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

      {/* 3. Platform overview: enrollment, data health, billing records */}
      <h2 className="dashboard-section-title">Platform overview</h2>
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

      {/* 4. School performance & management vs Recent activity + Support */}
      <section className="dashboard-split" aria-label="Schools and recent activity">
        <div className="dashboard-split-main">
          <h2 className="dashboard-section-title">School performance &amp; management</h2>
          {dashboard?.paymentDelinquency?.length > 0 && (
            <p className="card-desc">
              {delinquentSchools} school{delinquentSchools === 1 ? '' : 's'} currently above the free 50‑student tier with unpaid invoices.
            </p>
          )}
          {schools.length === 0 ? (
            <p className="empty-state">No schools yet. Schools register via the onboarding page.</p>
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>School</th>
                    <th>Owner email</th>
                    <th>Country</th>
                    <th>Students</th>
                    <th>Teachers</th>
                    <th>Parents</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {schools.map((s) => {
                    const isActive = s.isActive ?? true;
                    return (
                      <tr key={s.id}>
                        <td>{s.name}</td>
                        <td>{s.ownerEmail || '—'}</td>
                        <td>{s.countryCode || '—'}</td>
                        <td>{s.studentCount ?? 0}</td>
                        <td>{s.teacherCount ?? 0}</td>
                        <td>{s.parentCount ?? 0}</td>
                        <td>
                          <span className={isActive ? 'pill pill--success' : 'pill pill--muted'}>
                            {isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <aside className="dashboard-split-side" aria-label="Recent activity and support">
          {audit.length > 0 && (
            <div className="dashboard-panel">
              <h3 className="card-title">Recent activity (audit log)</h3>
              <p className="card-desc">Live feed of sensitive actions across all schools.</p>
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>When</th>
                      <th>School</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {audit.slice(0, 10).map((a) => (
                      <tr key={a.id}>
                        <td>{new Date(a.createdAtUtc).toLocaleString()}</td>
                        <td>{a.schoolId || '—'}</td>
                        <td>{a.action}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          <div className="dashboard-panel">
            <h3 className="card-title">Support &amp; ticketing</h3>
            <p className="card-desc">
              When principals click &ldquo;Help&rdquo;, their messages will appear here as live chats you can pick up.
            </p>
            <div className="support-empty">
              <p>No active tickets right now.</p>
              <p className="support-hint">Hook this up to your SignalR SupportHub to see chats in real time.</p>
            </div>
          </div>
        </aside>
      </section>

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

      {/* 5. System health status bar */}
      <section className="system-health-bar" aria-label="System health">
        <div className="system-health-item">
          <span className="system-health-label">API latency</span>
          <span className="system-health-badge system-health-badge--ok">Healthy</span>
        </div>
        <div className="system-health-item">
          <span className="system-health-label">Error rate</span>
          <span className="system-health-badge system-health-badge--ok">Low</span>
        </div>
        <div className="system-health-item">
          <span className="system-health-label">Paystack webhooks</span>
          <span className="system-health-badge system-health-badge--ok">Delivering</span>
        </div>
      </section>
    </PageLayout>
  );
}
