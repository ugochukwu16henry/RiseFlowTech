import { useEffect, useState, useCallback } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';

function fmt(amount, currency = 'NGN') {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 0 }).format(n);
}

function toDate(value) {
  if (!value) return null;
  const d = new Date(`${value}T00:00:00`);
  return Number.isNaN(d.getTime()) ? null : d;
}

function toLabel(value) {
  const d = toDate(value);
  return d ? d.toLocaleDateString() : '—';
}

function pctWithinRange(point, min, max) {
  if (!point || !min || !max) return null;
  const total = max.getTime() - min.getTime();
  if (total <= 0) return 0;
  const offset = point.getTime() - min.getTime();
  return Math.max(0, Math.min(100, (offset / total) * 100));
}

const STATUS_LABELS = {
  NotSubmitted: 'Not yet submitted',
  Pending: 'Submitted — awaiting receipt',
  ReceiptUploaded: 'Receipt uploaded — pending confirmation',
  InPersonPending: 'In-person payment declared',
  Confirmed: '✓ Confirmed — Paid',
};

const STATUS_CLASS = {
  NotSubmitted: 'badge--neutral',
  Pending: 'badge--warning',
  ReceiptUploaded: 'badge--info',
  InPersonPending: 'badge--info',
  Confirmed: 'badge--success',
};

export default function ParentFeesPage() {
  const [children, setChildren] = useState([]);
  const [bank, setBank] = useState(null);
  const [terms, setTerms] = useState([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState(null);
  const [uploading, setUploading] = useState(null); // paymentId being uploaded
  const [submitting, setSubmitting] = useState(null); // scheduleId+studentId being submitted

  const apiBase = getApiBase();

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [feesRes, bankRes, termsRes] = await Promise.all([
        apiFetch('/api/school-fees/my-fees'),
        apiFetch('/api/school-fees/bank-details'),
        apiFetch('/api/academicterms'),
      ]);
      setChildren(feesRes.ok ? await feesRes.json() : []);
      setBank(bankRes.ok ? await bankRes.json() : null);
      setTerms(termsRes.ok ? await termsRes.json() : []);
    } catch {
      setMessage('Could not load fee information.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  // ─── Declare in-person payment ────────────────────────────────────────────
  const declareInPerson = async (scheduleId, studentId, existingPaymentId) => {
    const key = `${scheduleId}:${studentId}`;
    setSubmitting(key);
    setMessage(null);
    try {
      let res;
      if (existingPaymentId && existingPaymentId !== '00000000-0000-0000-0000-000000000000') {
        // Update existing record: submit as in-person
        res = await apiFetch('/api/school-fees/payments', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ scheduleId, studentId, isInPerson: true, parentNote: 'Will pay in person at school' }),
        });
      } else {
        res = await apiFetch('/api/school-fees/payments', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ scheduleId, studentId, isInPerson: true, parentNote: 'Will pay in person at school' }),
        });
      }
      if (!res.ok) throw new Error(await res.text());
      setMessage('School notified that you will pay in person.');
      await loadData();
    } catch (err) {
      setMessage(err.message || 'Could not submit in-person declaration.');
    } finally {
      setSubmitting(null);
    }
  };

  // ─── Upload receipt ───────────────────────────────────────────────────────
  const handleReceiptUpload = async (paymentId, scheduleId, studentId, file) => {
    if (!file) return;
    setUploading(paymentId || `${scheduleId}:${studentId}`);
    setMessage(null);
    try {
      // If no payment record yet, create one first
      let pid = paymentId;
      if (!pid || pid === '00000000-0000-0000-0000-000000000000') {
        const createRes = await apiFetch('/api/school-fees/payments', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ scheduleId, studentId, isInPerson: false, parentNote: null }),
        });
        if (!createRes.ok) throw new Error(await createRes.text());
        const created = await createRes.json();
        pid = created.id;
      }

      const form = new FormData();
      form.append('file', file);
      const res = await apiFetch(`/api/school-fees/payments/${pid}/upload-receipt`, {
        method: 'POST',
        body: form,
      });
      if (!res.ok) throw new Error(await res.text());
      setMessage('Receipt uploaded successfully. The school will review and confirm your payment.');
      await loadData();
    } catch (err) {
      setMessage(err.message || 'Could not upload receipt.');
    } finally {
      setUploading(null);
    }
  };

  return (
    <PageLayout title="School Fees" role="parent">
      <h2 className="section-title">School Fees</h2>

      {message && (
        <p className={`empty-state${message.toLowerCase().includes('could not') || message.toLowerCase().includes('error') ? ' empty-state--error' : ''}`}>
          {message}
        </p>
      )}

      {/* ── Bank Details Banner ─────────────────────────────────────────── */}
      {bank && (
        <div className="card" style={{ marginBottom: '1.5rem', background: 'var(--surface-elevated, #f8f9fb)', borderLeft: '4px solid var(--color-primary, #1f7a8c)' }}>
          <h3 style={{ marginTop: 0, marginBottom: '0.5rem' }}>School payment account</h3>
          <p style={{ margin: '0.2rem 0' }}><strong>Bank:</strong> {bank.bankName}</p>
          <p style={{ margin: '0.2rem 0' }}><strong>Account name:</strong> {bank.accountName}</p>
          <p style={{ margin: '0.2rem 0' }}><strong>Account number:</strong> {bank.accountNumber}</p>
          {bank.branchOrSortCode && <p style={{ margin: '0.2rem 0' }}><strong>Branch:</strong> {bank.branchOrSortCode}</p>}
          {bank.paymentInstructions && (
            <p style={{ margin: '0.5rem 0 0', color: 'var(--text-muted)', fontSize: '0.9rem' }}>
              <strong>Instructions:</strong> {bank.paymentInstructions}
            </p>
          )}
        </div>
      )}

      {loading && <p className="empty-state" aria-busy="true">Loading fee information…</p>}

      {!loading && children.length === 0 && (
        <p className="empty-state">No fee records found. Make sure your children are linked to your account.</p>
      )}

      {/* ── One card per child ──────────────────────────────────────────── */}
      {!loading && children.map((child) => (
        <div key={child.studentId} className="card" style={{ marginBottom: '1.5rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '0.5rem' }}>
            <div>
              <h3 style={{ margin: 0 }}>{child.studentName}</h3>
              <span style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>
                {child.gradeName}{child.className ? ` — ${child.className}` : ''}{child.admissionNumber ? ` | Adm: ${child.admissionNumber}` : ''}
              </span>
            </div>
          </div>

          {child.feeItems.length === 0 && (
            <p style={{ color: 'var(--text-muted)', marginTop: '0.75rem' }}>No fee schedules assigned yet.</p>
          )}

          {child.feeItems.map((item) => {
            const isPaid = item.status === 'Confirmed';
            const isUploading = uploading === item.paymentId || uploading === `${item.scheduleId}:${child.studentId}`;
            const isSubmitting = submitting === `${item.scheduleId}:${child.studentId}`;
            const hasPayment = item.paymentId && item.paymentId !== '00000000-0000-0000-0000-000000000000';

            return (
              <div key={item.scheduleId} style={{
                marginTop: '1rem',
                padding: '1rem',
                borderRadius: 8,
                background: isPaid ? 'var(--surface-success-soft, #f0fdf4)' : 'var(--surface, #f4f6f9)',
                border: `1px solid ${isPaid ? 'var(--color-success, #22c55e)' : 'var(--border, #e5e7eb)'}`,
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.5rem' }}>
                  <div>
                    <p style={{ margin: '0 0 0.2rem', fontWeight: 600 }}>{item.termLabel} — {item.academicYear}</p>
                    <p style={{ margin: 0, fontSize: '1.1rem', color: 'var(--color-primary, #1f7a8c)', fontWeight: 700 }}>
                      {fmt(item.amount)}
                    </p>
                  </div>
                  <span className={`badge ${STATUS_CLASS[item.status] || 'badge--neutral'}`} style={{ alignSelf: 'center' }}>
                    {STATUS_LABELS[item.status] || item.status}
                  </span>
                </div>

                {isPaid && (
                  <p style={{ color: 'var(--color-success)', marginTop: '0.5rem', fontWeight: 500, fontSize: '0.9rem' }}>
                    Payment confirmed by school on {item.confirmedAtUtc ? new Date(item.confirmedAtUtc).toLocaleDateString() : '—'}
                    {item.adminNote ? ` — ${item.adminNote}` : ''}
                  </p>
                )}

                {item.receiptFilePath && (
                  <p style={{ marginTop: '0.5rem', fontSize: '0.9rem' }}>
                    <a href={`${apiBase}/api/school-fees/receipts/${item.receiptFilePath}`} target="_blank" rel="noreferrer" className="link">
                      View uploaded receipt
                    </a>
                  </p>
                )}

                {item.parentNote && !isPaid && (
                  <p style={{ color: 'var(--text-muted)', fontSize: '0.85rem', marginTop: '0.4rem' }}>
                    Your note: {item.parentNote}
                  </p>
                )}

                {!isPaid && (
                  <div style={{ marginTop: '0.75rem', display: 'flex', gap: '0.75rem', flexWrap: 'wrap', alignItems: 'center' }}>
                    {/* Upload receipt */}
                    <label className="btn-primary-action" style={{ cursor: 'pointer', fontSize: '0.85rem' }}>
                      {isUploading ? 'Uploading…' : item.receiptFilePath ? 'Replace receipt' : 'Upload bank receipt'}
                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/webp,application/pdf"
                        style={{ display: 'none' }}
                        disabled={isUploading}
                        onChange={(e) => {
                          const file = e.target.files?.[0];
                          if (file) handleReceiptUpload(hasPayment ? item.paymentId : null, item.scheduleId, child.studentId, file);
                          e.target.value = '';
                        }}
                      />
                    </label>

                    {/* In-person option */}
                    {item.status !== 'InPersonPending' && (
                      <button
                        className="btn-primary-action btn-primary-action--ghost"
                        style={{ fontSize: '0.85rem' }}
                        disabled={isSubmitting}
                        onClick={() => declareInPerson(item.scheduleId, child.studentId, hasPayment ? item.paymentId : null)}>
                        {isSubmitting ? 'Submitting…' : 'I will pay at school'}
                      </button>
                    )}
                    {item.status === 'InPersonPending' && (
                      <span style={{ color: 'var(--text-muted)', fontSize: '0.85rem' }}>
                        School is aware you will pay in person.
                      </span>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      ))}

      {/* ── Payment History ─────────────────────────────────────────────── */}
      {!loading && children.some(c => c.feeItems.some(f => f.status !== 'NotSubmitted')) && (
        <div className="card" style={{ marginTop: '2rem' }}>
          <h3 style={{ marginTop: 0 }}>Payment history</h3>
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Child</th>
                  <th>Term</th>
                  <th>Year</th>
                  <th>Amount</th>
                  <th>Status</th>
                  <th>Submitted</th>
                  <th>Confirmed</th>
                </tr>
              </thead>
              <tbody>
                {children.flatMap(child =>
                  child.feeItems
                    .filter(f => f.status !== 'NotSubmitted')
                    .map(item => (
                      <tr key={`${child.studentId}-${item.scheduleId}`}>
                        <td>{child.studentName}</td>
                        <td>{item.termLabel}</td>
                        <td>{item.academicYear}</td>
                        <td>{fmt(item.amount)}</td>
                        <td>
                          <span className={`badge ${STATUS_CLASS[item.status] || 'badge--neutral'}`}>
                            {STATUS_LABELS[item.status] || item.status}
                          </span>
                        </td>
                        <td>{item.submittedAtUtc ? new Date(item.submittedAtUtc).toLocaleDateString() : '—'}</td>
                        <td>{item.confirmedAtUtc ? new Date(item.confirmedAtUtc).toLocaleDateString() : '—'}</td>
                      </tr>
                    ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {!loading && terms.length > 0 && (
        <div className="card" style={{ marginTop: '2rem' }}>
          <h3 style={{ marginTop: 0 }}>Academic term calendar</h3>

          {(() => {
            const sorted = [...terms]
              .filter(t => t.startDate && t.endDate)
              .sort((a, b) => {
                if ((a.sortOrder ?? 0) !== (b.sortOrder ?? 0)) return (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
                return String(a.startDate).localeCompare(String(b.startDate));
              });

            if (sorted.length === 0) return null;

            const starts = sorted.map(t => toDate(t.startDate)).filter(Boolean);
            const ends = sorted.map(t => toDate(t.endDate)).filter(Boolean);
            const min = starts.length ? new Date(Math.min(...starts.map(d => d.getTime()))) : null;
            const max = ends.length ? new Date(Math.max(...ends.map(d => d.getTime()))) : null;

            if (!min || !max) return null;

            return (
              <div style={{ marginBottom: '1rem', padding: '0.9rem', background: 'var(--surface-elevated, #f8f9fb)', borderRadius: 10, border: '1px solid var(--border, #e5e7eb)' }}>
                <p style={{ margin: '0 0 0.75rem', color: 'var(--text-muted)', fontSize: '0.88rem' }}>
                  Visual timeline: each bar shows a term range; highlighted inner segment shows midterm break.
                </p>
                <div style={{ display: 'grid', gap: '0.6rem' }}>
                  {sorted.map((t) => {
                    const start = toDate(t.startDate);
                    const end = toDate(t.endDate);
                    const left = pctWithinRange(start, min, max) ?? 0;
                    const right = pctWithinRange(end, min, max) ?? left;
                    const width = Math.max(2, right - left);

                    const midStart = pctWithinRange(toDate(t.midtermBreakStart), min, max);
                    const midEnd = pctWithinRange(toDate(t.midtermBreakEnd), min, max);
                    const midWidth = (midStart != null && midEnd != null) ? Math.max(1, midEnd - midStart) : 0;

                    return (
                      <div key={`timeline-${t.id}`}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
                          <strong style={{ fontSize: '0.9rem' }}>{t.name} {t.academicYear ? `(${t.academicYear})` : ''}</strong>
                          {t.isCurrent && <span className="badge badge--success">Current</span>}
                        </div>
                        <div style={{ position: 'relative', height: 18, background: '#e5edf3', borderRadius: 999 }}>
                          <div
                            style={{
                              position: 'absolute',
                              left: `${left}%`,
                              width: `${width}%`,
                              height: '100%',
                              borderRadius: 999,
                              background: t.isCurrent ? '#22c55e' : '#3b82f6',
                              opacity: 0.85,
                            }}
                            title={`${toLabel(t.startDate)} - ${toLabel(t.endDate)}`}
                          />
                          {(midStart != null && midEnd != null) && (
                            <div
                              style={{
                                position: 'absolute',
                                left: `${midStart}%`,
                                width: `${midWidth}%`,
                                top: 3,
                                height: 12,
                                borderRadius: 999,
                                background: '#f59e0b',
                              }}
                              title={`Midterm: ${toLabel(t.midtermBreakStart)} - ${toLabel(t.midtermBreakEnd)}`}
                            />
                          )}
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 4, fontSize: '0.78rem', color: 'var(--text-muted)' }}>
                          <span>{toLabel(t.startDate)}</span>
                          <span>{toLabel(t.endDate)}</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })()}

          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Term</th>
                  <th>Year</th>
                  <th>Start</th>
                  <th>End</th>
                  <th>Midterm break</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {terms.map((t) => (
                  <tr key={t.id}>
                    <td>{t.name}</td>
                    <td>{t.academicYear}</td>
                    <td>{t.startDate ? new Date(t.startDate).toLocaleDateString() : '—'}</td>
                    <td>{t.endDate ? new Date(t.endDate).toLocaleDateString() : '—'}</td>
                    <td>
                      {t.midtermBreakStart
                        ? `${new Date(t.midtermBreakStart).toLocaleDateString()} - ${t.midtermBreakEnd ? new Date(t.midtermBreakEnd).toLocaleDateString() : '—'}`
                        : '—'}
                    </td>
                    <td>
                      <span className={`badge ${t.isCurrent ? 'badge--success' : 'badge--neutral'}`}>
                        {t.isCurrent ? 'Current' : 'Upcoming/Past'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
