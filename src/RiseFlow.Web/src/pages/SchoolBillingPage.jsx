import { useCallback, useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';

function formatMoney(amount, currencyCode) {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: currencyCode || 'NGN', maximumFractionDigits: 0 }).format(n);
}

export default function SchoolBillingPage() {
  const location = useLocation();
  const [billing, setBilling] = useState([]);
  const [gatewayStatus, setGatewayStatus] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [payingId, setPayingId] = useState(null);
  const [statusMessage, setStatusMessage] = useState(null);

  const readJsonOrThrow = useCallback(async (response, fallbackMessage) => {
    if (response.status === 401 || response.status === 403) {
      throw new Error('Your session expired or your school access is missing. Please sign in again as School Admin.');
    }
    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(text || fallbackMessage);
    }
    return response.json();
  }, []);

  const loadBilling = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      await apiFetch('/api/billing/ensure-current', { method: 'POST' }).catch(() => null);
      const [billingRes, gatewayRes] = await Promise.all([
        apiFetch('/api/billing'),
        apiFetch('/api/billing/gateway-status'),
      ]);
      const [billingData, gatewayData] = await Promise.all([
        readJsonOrThrow(billingRes, 'Could not load billing records.'),
        readJsonOrThrow(gatewayRes, 'Could not load Paystack status.'),
      ]);
      setBilling(Array.isArray(billingData) ? billingData : []);
      setGatewayStatus(gatewayData || null);
    } catch (e) {
      setError(e.message || 'Failed to load billing');
    } finally {
      setLoading(false);
    }
  }, [readJsonOrThrow]);

  useEffect(() => {
    loadBilling();
  }, [loadBilling]);

  const searchParams = new URLSearchParams(location.search);
  const paystackReference = searchParams.get('reference');
  const returnedFromPaystack = searchParams.get('payment') === 'paystack';

  useEffect(() => {
    if (!paystackReference) {
      if (returnedFromPaystack) {
        setStatusMessage('Returned from Paystack. If payment confirmation is still pending, refresh this page in a few seconds.');
      }
      return;
    }

    let cancelled = false;
    (async () => {
      try {
        const res = await apiFetch(`/api/billing/verify-payment?reference=${encodeURIComponent(paystackReference)}`);
        const data = await readJsonOrThrow(res, 'Could not verify your Paystack payment yet.');
        if (cancelled) return;
        setStatusMessage(data.message || 'Payment verification completed.');
        await loadBilling();
      } catch (e) {
        if (!cancelled) {
          setStatusMessage(e.message || 'Could not verify the Paystack payment yet.');
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [paystackReference, returnedFromPaystack, readJsonOrThrow, loadBilling]);

  const currentBilling = billing[0] || null;
  const currencyCode = currentBilling?.currencyCode || 'NGN';
  const studentCount = Number(currentBilling?.studentCount || 0);
  const billableStudents = Number(currentBilling?.billableStudents || Math.max(0, studentCount - 50));
  const monthlyDue = Number(currentBilling?.monthlyAmountDue || 0);
  const activationDue = Number(currentBilling?.activationAmountDue || 0);
  const totalDue = Number(currentBilling?.amountDue || 0);
  const totalPaid = Number(currentBilling?.amountPaid || 0);
  const outstanding = Math.max(0, totalDue - totalPaid);

  const handlePayWithPaystack = async (billingRecordId) => {
    if (!billingRecordId || payingId) return;
    setPayingId(billingRecordId);
    setError(null);
    try {
      const res = await apiFetch('/api/billing/initiate-payment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ billingRecordId }),
      });
      const text = await res.text();
      if (!res.ok) throw new Error(text || 'Could not start Paystack payment.');
      const data = text ? JSON.parse(text) : null;
      if (!data?.authorizationUrl) {
        throw new Error('Paystack did not return an authorization URL.');
      }
      window.location.assign(data.authorizationUrl);
    } catch (e) {
      setError(e.message || 'Could not start Paystack payment.');
    } finally {
      setPayingId(null);
    }
  };

  return (
    <PageLayout title="School Admin — Billing" role="school">
      <h2 className="section-title">Transparent pricing for growing schools</h2>
      <p className="card-desc">
        First <strong>50 students are lifetime free</strong>. From student <strong>51</strong>, you pay a <strong>₦500 one-time activation</strong>
        {' '}and a <strong>₦100 monthly subscription</strong> for each billable student.
      </p>

      {statusMessage && (
        <div className="access-codes-result" style={{ marginBottom: '1rem' }}>
          <p style={{ margin: 0 }}>{statusMessage}</p>
        </div>
      )}

      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}

      {!loading && !error && (
        <>
          <div className="summary-cards" style={{ marginTop: '1rem' }}>
            <div className="summary-card">
              <span className="summary-value">{studentCount}</span>
              <span className="summary-label">Active students</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{billableStudents}</span>
              <span className="summary-label">Billable students</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{formatMoney(monthlyDue, currencyCode)}</span>
              <span className="summary-label">Monthly subscription</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{formatMoney(activationDue, currencyCode)}</span>
              <span className="summary-label">One-time activation</span>
            </div>
          </div>

          {gatewayStatus && (
            <div className={`access-codes-result ${gatewayStatus.isConfigured ? '' : 'access-codes-result--error'}`} style={{ marginTop: '1rem' }}>
              <p style={{ margin: 0 }}>
                <strong>{gatewayStatus.gatewayName}:</strong> {gatewayStatus.message}
              </p>
              <p className="card-desc" style={{ marginTop: '0.35rem' }}>
                Callback URL: <code>{gatewayStatus.callbackUrl}</code>
              </p>
            </div>
          )}

          {currentBilling && (
            <div className="access-codes-result" style={{ marginTop: '1rem', marginBottom: '1rem' }}>
              <p style={{ margin: 0 }}>
                Current cycle: <strong>{currentBilling.periodLabel}</strong> — outstanding balance <strong>{formatMoney(outstanding, currencyCode)}</strong>.
              </p>
              {outstanding > 0 && gatewayStatus?.isConfigured ? (
                <button
                  type="button"
                  className="btn-excel btn-generate"
                  style={{ marginTop: '0.5rem' }}
                  onClick={() => handlePayWithPaystack(currentBilling.id)}
                  disabled={payingId === currentBilling.id}
                >
                  {payingId === currentBilling.id ? 'Redirecting…' : 'Pay with Paystack'}
                </button>
              ) : outstanding > 0 ? (
                <p className="card-desc" style={{ marginTop: '0.5rem' }}>
                  Add your Paystack secret key to enable checkout from this page.
                </p>
              ) : (
                <p className="card-desc" style={{ marginTop: '0.5rem' }}>
                  You are within the free tier or fully paid for the current cycle.
                </p>
              )}
            </div>
          )}

          {billing.length === 0 ? (
            <p className="empty-state">No billing records found yet.</p>
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Period</th>
                    <th>Students</th>
                    <th>Billable</th>
                    <th>Monthly</th>
                    <th>Activation</th>
                    <th>Due</th>
                    <th>Paid</th>
                    <th>Outstanding</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {billing.map((b) => {
                    const due = Number(b.amountDue || 0);
                    const paid = Number(b.amountPaid || 0);
                    const itemOutstanding = Math.max(0, due - paid);
                    return (
                      <tr key={b.id}>
                        <td>{b.periodLabel || '—'}</td>
                        <td>{b.studentCount ?? 0}</td>
                        <td>{b.billableStudents ?? 0}</td>
                        <td>{formatMoney(b.monthlyAmountDue || 0, b.currencyCode)}</td>
                        <td>{formatMoney(b.activationAmountDue || 0, b.currencyCode)}</td>
                        <td>{formatMoney(due, b.currencyCode)}</td>
                        <td>{formatMoney(paid, b.currencyCode)}</td>
                        <td>{formatMoney(itemOutstanding, b.currencyCode)}</td>
                        <td>
                          {itemOutstanding > 0 ? (
                            <button
                              type="button"
                              className="btn-excel btn-generate"
                              onClick={() => handlePayWithPaystack(b.id)}
                              disabled={!gatewayStatus?.isConfigured || payingId === b.id}
                            >
                              {payingId === b.id ? 'Redirecting…' : 'Pay'}
                            </button>
                          ) : b.paidAtUtc ? (
                            <a
                              href={`${getApiBase()}/api/billing/${b.id}/receipt`}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="btn-excel btn-link"
                            >
                              Receipt
                            </a>
                          ) : (
                            '—'
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </PageLayout>
  );
}
