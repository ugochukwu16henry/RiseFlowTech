import { useEffect, useState } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

function formatMoney(amount, currencyCode) {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: currencyCode || 'NGN', maximumFractionDigits: 0 }).format(n);
}

export default function SchoolBillingPage() {
  const [billing, setBilling] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    apiFetch('/api/billing')
      .then((res) => {
        if (cancelled) return null;
        if (!res.ok) throw new Error('Could not load billing records');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setBilling(Array.isArray(data) ? data : []);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Failed to load billing');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  return (
    <PageLayout title="School Admin — Billing" role="school">
      <h2 className="section-title">Billing records</h2>
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}
      {!loading && !error && billing.length === 0 && <p className="empty-state">No billing records found.</p>}
      {!loading && billing.length > 0 && (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Period</th>
                <th>Due</th>
                <th>Paid</th>
                <th>Outstanding</th>
              </tr>
            </thead>
            <tbody>
              {billing.map((b) => {
                const due = Number(b.amountDue || 0);
                const paid = Number(b.amountPaid || 0);
                return (
                  <tr key={b.id}>
                    <td>{b.periodLabel || '—'}</td>
                    <td>{formatMoney(due, b.currencyCode)}</td>
                    <td>{formatMoney(paid, b.currencyCode)}</td>
                    <td>{formatMoney(Math.max(0, due - paid), b.currencyCode)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </PageLayout>
  );
}
