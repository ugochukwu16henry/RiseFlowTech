import { useEffect, useState, useCallback } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';

function fmt(amount, currency = 'NGN') {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 0 }).format(n);
}

const STATUS_LABELS = {
  Pending: 'Awaiting receipt',
  ReceiptUploaded: 'Receipt uploaded',
  InPersonPending: 'In-person (pending)',
  Confirmed: 'Confirmed — Paid',
  NotSubmitted: 'Not submitted',
};

const STATUS_CLASS = {
  Pending: 'badge--warning',
  ReceiptUploaded: 'badge--info',
  InPersonPending: 'badge--info',
  Confirmed: 'badge--success',
  NotSubmitted: 'badge--neutral',
};

export default function SchoolFeesPage() {
  const [tab, setTab] = useState('schedules'); // schedules | bank | payments | roster
  const [schedules, setSchedules] = useState([]);
  const [grades, setGrades] = useState([]);
  const [classes, setClasses] = useState([]);
  const [payments, setPayments] = useState([]);
  const [bank, setBank] = useState(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState(null);
  const [filterSchedule, setFilterSchedule] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [rosterScheduleId, setRosterScheduleId] = useState('');
  const [roster, setRoster] = useState([]);
  const [loadingRoster, setLoadingRoster] = useState(false);

  // Schedule form
  const emptySchedule = { termLabel: '', academicYear: '', gradeId: '', classId: '', amount: '', description: '' };
  const [scheduleForm, setScheduleForm] = useState(emptySchedule);
  const [editingScheduleId, setEditingScheduleId] = useState(null);
  const [savingSchedule, setSavingSchedule] = useState(false);

  // Bank form
  const emptyBank = { bankName: '', accountName: '', accountNumber: '', branchOrSortCode: '', paymentInstructions: '' };
  const [bankForm, setBankForm] = useState(emptyBank);
  const [savingBank, setSavingBank] = useState(false);

  const loadAll = useCallback(async () => {
    setLoading(true);
    try {
      const [schRes, gradeRes, classRes, bankRes] = await Promise.all([
        apiFetch('/api/school-fees/schedules'),
        apiFetch('/api/schools/grades'),
        apiFetch('/api/schools/classes'),
        apiFetch('/api/school-fees/bank-details'),
      ]);
      setSchedules(schRes.ok ? await schRes.json() : []);
      setGrades(gradeRes.ok ? await gradeRes.json() : []);
      setClasses(classRes.ok ? await classRes.json() : []);
      const bankData = bankRes.ok ? await bankRes.json() : null;
      setBank(bankData);
      if (bankData) {
        setBankForm({
          bankName: bankData.bankName || '',
          accountName: bankData.accountName || '',
          accountNumber: bankData.accountNumber || '',
          branchOrSortCode: bankData.branchOrSortCode || '',
          paymentInstructions: bankData.paymentInstructions || '',
        });
      }
    } catch {
      setMessage('Could not load fee data.');
    } finally {
      setLoading(false);
    }
  }, []);

  const loadPayments = useCallback(async () => {
    try {
      const params = new URLSearchParams();
      if (filterSchedule) params.set('scheduleId', filterSchedule);
      if (filterStatus) params.set('status', filterStatus);
      const res = await apiFetch(`/api/school-fees/payments?${params}`);
      setPayments(res.ok ? await res.json() : []);
    } catch {
      setPayments([]);
    }
  }, [filterSchedule, filterStatus]);

  useEffect(() => { loadAll(); }, [loadAll]);
  useEffect(() => { if (tab === 'payments') loadPayments(); }, [tab, loadPayments]);

  const loadRoster = useCallback(async (scheduleId) => {
    if (!scheduleId) return;
    setLoadingRoster(true);
    try {
      const res = await apiFetch(`/api/school-fees/roster?scheduleId=${scheduleId}`);
      setRoster(res.ok ? await res.json() : []);
    } catch {
      setRoster([]);
    } finally {
      setLoadingRoster(false);
    }
  }, []);

  useEffect(() => {
    if (tab === 'roster' && rosterScheduleId) loadRoster(rosterScheduleId);
  }, [tab, rosterScheduleId, loadRoster]);

  // ─── Schedule CRUD ────────────────────────────────────────────────────────

  const saveSchedule = async (e) => {
    e.preventDefault();
    if (!scheduleForm.termLabel.trim()) return setMessage('Term label is required.');
    if (!scheduleForm.academicYear.trim()) return setMessage('Academic year is required.');
    if (!scheduleForm.amount || Number(scheduleForm.amount) <= 0) return setMessage('Amount must be greater than zero.');
    setSavingSchedule(true);
    setMessage(null);
    try {
      const body = {
        termLabel: scheduleForm.termLabel.trim(),
        academicYear: scheduleForm.academicYear.trim(),
        gradeId: scheduleForm.gradeId || null,
        classId: scheduleForm.classId || null,
        amount: Number(scheduleForm.amount),
        description: scheduleForm.description.trim() || null,
        isActive: true,
      };
      const url = editingScheduleId
        ? `/api/school-fees/schedules/${editingScheduleId}`
        : '/api/school-fees/schedules';
      const method = editingScheduleId ? 'PUT' : 'POST';
      const res = await apiFetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
      if (!res.ok) throw new Error(await res.text());
      setScheduleForm(emptySchedule);
      setEditingScheduleId(null);
      setMessage('Fee schedule saved.');
      await loadAll();
    } catch (err) {
      setMessage(err.message || 'Could not save schedule.');
    } finally {
      setSavingSchedule(false);
    }
  };

  const editSchedule = (s) => {
    setScheduleForm({
      termLabel: s.termLabel,
      academicYear: s.academicYear,
      gradeId: s.gradeId || '',
      classId: s.classId || '',
      amount: String(s.amount),
      description: s.description || '',
    });
    setEditingScheduleId(s.id);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const deleteSchedule = async (id) => {
    if (!window.confirm('Delete this fee schedule? This cannot be undone.')) return;
    try {
      const res = await apiFetch(`/api/school-fees/schedules/${id}`, { method: 'DELETE' });
      if (!res.ok) {
        const text = await res.text();
        return setMessage(text || 'Could not delete schedule.');
      }
      setMessage('Schedule deleted.');
      await loadAll();
    } catch {
      setMessage('Could not delete schedule.');
    }
  };

  // ─── Bank Details ─────────────────────────────────────────────────────────

  const saveBank = async (e) => {
    e.preventDefault();
    if (!bankForm.bankName.trim() || !bankForm.accountName.trim() || !bankForm.accountNumber.trim()) {
      return setMessage('Bank name, account name, and account number are required.');
    }
    setSavingBank(true);
    setMessage(null);
    try {
      const res = await apiFetch('/api/school-fees/bank-details', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          bankName: bankForm.bankName.trim(),
          accountName: bankForm.accountName.trim(),
          accountNumber: bankForm.accountNumber.trim(),
          branchOrSortCode: bankForm.branchOrSortCode.trim() || null,
          paymentInstructions: bankForm.paymentInstructions.trim() || null,
        }),
      });
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      setBank(data);
      setMessage('Bank details saved.');
    } catch (err) {
      setMessage(err.message || 'Could not save bank details.');
    } finally {
      setSavingBank(false);
    }
  };

  // ─── Confirm Payment ──────────────────────────────────────────────────────

  const confirmPayment = async (id) => {
    try {
      const note = window.prompt('Add a note for this confirmation (optional):') ?? '';
      const res = await apiFetch(`/api/school-fees/payments/${id}/confirm`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ adminNote: note || null }),
      });
      if (!res.ok) throw new Error(await res.text());
      setMessage('Payment confirmed.');
      await loadPayments();
    } catch (err) {
      setMessage(err.message || 'Could not confirm payment.');
    }
  };

  const apiBase = getApiBase();

  const filteredClasses = scheduleForm.gradeId
    ? classes.filter((c) => c.gradeId === scheduleForm.gradeId)
    : classes;

  return (
    <PageLayout title="School Fees" role="school">
      <h2 className="section-title">School Fees</h2>

      <div className="dashboard-actions" style={{ flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Link to="/school" className="btn-primary-action btn-primary-action--ghost">Dashboard</Link>
        <button
          className={`btn-primary-action${tab === 'schedules' ? '' : ' btn-primary-action--ghost'}`}
          onClick={() => setTab('schedules')}>
          Fee Schedules
        </button>
        <button
          className={`btn-primary-action${tab === 'bank' ? '' : ' btn-primary-action--ghost'}`}
          onClick={() => setTab('bank')}>
          Bank Details
        </button>
        <button
          className={`btn-primary-action${tab === 'payments' ? '' : ' btn-primary-action--ghost'}`}
          onClick={() => { setTab('payments'); }}>
          Payments Tracker
        </button>
        <button
          className={`btn-primary-action${tab === 'roster' ? '' : ' btn-primary-action--ghost'}`}
          onClick={() => setTab('roster')}>
          Student Roster
        </button>
      </div>

      {message && (
        <p className={`empty-state${message.toLowerCase().includes('error') || message.toLowerCase().includes('could not') || message.toLowerCase().includes('required') ? ' empty-state--error' : ''}`}>
          {message}
        </p>
      )}

      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}

      {/* ── Fee Schedules tab ─────────────────────────────────────────── */}
      {!loading && tab === 'schedules' && (
        <div>
          <div className="card" style={{ maxWidth: 700, marginBottom: '1.5rem' }}>
            <h3 style={{ marginBottom: '0.75rem' }}>{editingScheduleId ? 'Edit fee schedule' : 'Add fee schedule'}</h3>
            <form onSubmit={saveSchedule}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                <div>
                  <label className="form-label">Term label *</label>
                  <input className="form-input" placeholder="e.g. First Term" value={scheduleForm.termLabel}
                    onChange={(e) => setScheduleForm(f => ({ ...f, termLabel: e.target.value }))} />
                </div>
                <div>
                  <label className="form-label">Academic year *</label>
                  <input className="form-input" placeholder="e.g. 2025/2026" value={scheduleForm.academicYear}
                    onChange={(e) => setScheduleForm(f => ({ ...f, academicYear: e.target.value }))} />
                </div>
                <div>
                  <label className="form-label">Grade (optional)</label>
                  <select className="form-input" value={scheduleForm.gradeId}
                    onChange={(e) => setScheduleForm(f => ({ ...f, gradeId: e.target.value, classId: '' }))}>
                    <option value="">All grades</option>
                    {grades.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
                  </select>
                </div>
                <div>
                  <label className="form-label">Class (optional)</label>
                  <select className="form-input" value={scheduleForm.classId}
                    onChange={(e) => setScheduleForm(f => ({ ...f, classId: e.target.value }))}>
                    <option value="">All classes in grade</option>
                    {filteredClasses.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </select>
                </div>
                <div>
                  <label className="form-label">Amount *</label>
                  <input className="form-input" type="number" min="0" step="0.01" placeholder="e.g. 50000"
                    value={scheduleForm.amount}
                    onChange={(e) => setScheduleForm(f => ({ ...f, amount: e.target.value }))} />
                </div>
                <div>
                  <label className="form-label">Description (optional)</label>
                  <input className="form-input" placeholder="e.g. Includes textbooks" value={scheduleForm.description}
                    onChange={(e) => setScheduleForm(f => ({ ...f, description: e.target.value }))} />
                </div>
              </div>
              <div className="form-actions" style={{ marginTop: '0.75rem', gap: '0.5rem', display: 'flex' }}>
                <button type="submit" className="btn-primary-action" disabled={savingSchedule}>
                  {savingSchedule ? 'Saving…' : editingScheduleId ? 'Update schedule' : 'Add schedule'}
                </button>
                {editingScheduleId && (
                  <button type="button" className="btn-primary-action btn-primary-action--ghost"
                    onClick={() => { setScheduleForm(emptySchedule); setEditingScheduleId(null); }}>
                    Cancel
                  </button>
                )}
              </div>
            </form>
          </div>

          <h3 style={{ marginBottom: '0.5rem' }}>Current schedules ({schedules.length})</h3>
          {schedules.length === 0
            ? <p className="empty-state">No fee schedules yet. Add one above.</p>
            : (
              <div className="table-scroll">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Term</th>
                      <th>Year</th>
                      <th>Grade</th>
                      <th>Class</th>
                      <th>Amount</th>
                      <th>Payments</th>
                      <th>Confirmed</th>
                      <th>Status</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {schedules.map(s => (
                      <tr key={s.id}>
                        <td>{s.termLabel}</td>
                        <td>{s.academicYear}</td>
                        <td>{s.gradeName || <em>All</em>}</td>
                        <td>{s.className || <em>All</em>}</td>
                        <td>{fmt(s.amount)}</td>
                        <td>{s.paymentCount}</td>
                        <td>{s.confirmedCount}</td>
                        <td>
                          <span className={`badge ${s.isActive ? 'badge--success' : 'badge--neutral'}`}>
                            {s.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td style={{ whiteSpace: 'nowrap' }}>
                          <button className="btn-icon" title="Edit" onClick={() => editSchedule(s)}>✏️</button>
                          <button className="btn-icon" title="Delete" onClick={() => deleteSchedule(s.id)}>🗑️</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
        </div>
      )}

      {/* ── Bank Details tab ────────────────────────────────────────────── */}
      {!loading && tab === 'bank' && (
        <div className="card" style={{ maxWidth: 600 }}>
          <h3 style={{ marginBottom: '0.75rem' }}>School bank account for fee payments</h3>
          <p style={{ color: 'var(--text-muted)', marginBottom: '1rem', fontSize: '0.9rem' }}>
            These details will be shown to parents when they need to make a payment.
          </p>
          <form onSubmit={saveBank}>
            <label className="form-label">Bank name *</label>
            <input className="form-input" placeholder="e.g. First Bank Nigeria" value={bankForm.bankName}
              onChange={(e) => setBankForm(f => ({ ...f, bankName: e.target.value }))} />

            <label className="form-label" style={{ marginTop: '0.75rem' }}>Account name *</label>
            <input className="form-input" placeholder="e.g. Springfield Academy School Fees" value={bankForm.accountName}
              onChange={(e) => setBankForm(f => ({ ...f, accountName: e.target.value }))} />

            <label className="form-label" style={{ marginTop: '0.75rem' }}>Account number *</label>
            <input className="form-input" placeholder="e.g. 0123456789" value={bankForm.accountNumber}
              onChange={(e) => setBankForm(f => ({ ...f, accountNumber: e.target.value }))} />

            <label className="form-label" style={{ marginTop: '0.75rem' }}>Branch / Sort code (optional)</label>
            <input className="form-input" placeholder="e.g. Victoria Island Branch" value={bankForm.branchOrSortCode}
              onChange={(e) => setBankForm(f => ({ ...f, branchOrSortCode: e.target.value }))} />

            <label className="form-label" style={{ marginTop: '0.75rem' }}>Payment instructions (optional)</label>
            <textarea className="form-input" rows={3}
              placeholder="e.g. Use student's full name and admission number as payment reference"
              value={bankForm.paymentInstructions}
              onChange={(e) => setBankForm(f => ({ ...f, paymentInstructions: e.target.value }))} />

            <div className="form-actions" style={{ marginTop: '1rem' }}>
              <button type="submit" className="btn-primary-action" disabled={savingBank}>
                {savingBank ? 'Saving…' : 'Save bank details'}
              </button>
            </div>
          </form>

          {bank && (
            <div className="access-codes-result" style={{ marginTop: '1.5rem' }}>
              <p><strong>Currently saved:</strong></p>
              <p>Bank: {bank.bankName}</p>
              <p>Account: {bank.accountName} — {bank.accountNumber}</p>
              {bank.branchOrSortCode && <p>Branch: {bank.branchOrSortCode}</p>}
              {bank.paymentInstructions && <p>Instructions: {bank.paymentInstructions}</p>}
            </div>
          )}
        </div>
      )}

      {/* ── Payments Tracker tab ─────────────────────────────────────────── */}
      {tab === 'payments' && (
        <div>
          <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', marginBottom: '1rem', alignItems: 'flex-end' }}>
            <div>
              <label className="form-label">Filter by term</label>
              <select className="form-input" style={{ minWidth: 200 }} value={filterSchedule}
                onChange={(e) => setFilterSchedule(e.target.value)}>
                <option value="">All terms</option>
                {schedules.map(s => <option key={s.id} value={s.id}>{s.termLabel} {s.academicYear}{s.gradeName ? ` — ${s.gradeName}` : ''}{s.className ? ` / ${s.className}` : ''}</option>)}
              </select>
            </div>
            <div>
              <label className="form-label">Filter by status</label>
              <select className="form-input" value={filterStatus} onChange={(e) => setFilterStatus(e.target.value)}>
                <option value="">All</option>
                <option value="Pending">Awaiting receipt</option>
                <option value="ReceiptUploaded">Receipt uploaded</option>
                <option value="InPersonPending">In-person pending</option>
                <option value="Confirmed">Confirmed — Paid</option>
              </select>
            </div>
            <button className="btn-primary-action btn-primary-action--ghost" onClick={loadPayments}>Refresh</button>
          </div>

          {payments.length === 0
            ? <p className="empty-state">No payment records match the filter.</p>
            : (
              <div className="table-scroll">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Student</th>
                      <th>Adm. No.</th>
                      <th>Parent</th>
                      <th>Term</th>
                      <th>Amount</th>
                      <th>Status</th>
                      <th>Receipt</th>
                      <th>Parent note</th>
                      <th>Submitted</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {payments.map(p => (
                      <tr key={p.id}>
                        <td>{p.studentName}</td>
                        <td>{p.admissionNumber || '—'}</td>
                        <td>{p.parentName || '—'}</td>
                        <td style={{ whiteSpace: 'nowrap' }}>{p.termLabel} {p.academicYear}</td>
                        <td>{fmt(p.amount)}</td>
                        <td>
                          <span className={`badge ${STATUS_CLASS[p.status] || 'badge--neutral'}`}>
                            {STATUS_LABELS[p.status] || p.status}
                          </span>
                        </td>
                        <td>
                          {p.receiptFilePath
                            ? <a href={`${apiBase}/api/school-fees/receipts/${p.receiptFilePath}`} target="_blank" rel="noreferrer" className="link">View receipt</a>
                            : '—'}
                        </td>
                        <td style={{ maxWidth: 180, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {p.parentNote || '—'}
                        </td>
                        <td style={{ whiteSpace: 'nowrap' }}>
                          {p.submittedAtUtc ? new Date(p.submittedAtUtc).toLocaleDateString() : '—'}
                        </td>
                        <td>
                          {p.status !== 'Confirmed'
                            ? <button className="btn-primary-action" style={{ padding: '0.25rem 0.75rem', fontSize: '0.8rem' }}
                                onClick={() => confirmPayment(p.id)}>
                                Mark paid
                              </button>
                            : <span style={{ color: 'var(--color-success)', fontWeight: 600 }}>✓ Paid</span>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
        </div>
      )}

      {/* ── Student Roster tab ──────────────────────────────────────────── */}
      {tab === 'roster' && (
        <div>
          <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', marginBottom: '1rem', alignItems: 'flex-end' }}>
            <div>
              <label className="form-label">Select fee schedule</label>
              <select className="form-input" style={{ minWidth: 260 }} value={rosterScheduleId}
                onChange={(e) => setRosterScheduleId(e.target.value)}>
                <option value="">All schedules</option>
                {schedules.map(s => (
                  <option key={s.id} value={s.id}>
                    {s.termLabel} {s.academicYear}{s.gradeName ? ` — ${s.gradeName}` : ''}{s.className ? ` / ${s.className}` : ''}
                  </option>
                ))}
              </select>
            </div>
            <button
              className="btn-primary-action btn-primary-action--ghost"
              onClick={() => loadRoster(rosterScheduleId)}
              disabled={!rosterScheduleId}
            >
              Refresh
            </button>
          </div>

          {!rosterScheduleId && <p className="empty-state">Select a schedule to view the payment roster.</p>}
          {loadingRoster && <p className="empty-state" aria-busy="true">Loading roster...</p>}
          {!loadingRoster && rosterScheduleId && roster.length === 0 && (
            <p className="empty-state">No students found for this schedule.</p>
          )}

          {!loadingRoster && roster.length > 0 && (
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Student</th>
                    <th>Adm. No.</th>
                    <th>Grade</th>
                    <th>Class</th>
                    <th>Status</th>
                    <th>Confirmed</th>
                  </tr>
                </thead>
                <tbody>
                  {roster.map(r => (
                    <tr key={r.studentId}>
                      <td>{r.studentName}</td>
                      <td>{r.admissionNumber || '—'}</td>
                      <td>{r.gradeName || '—'}</td>
                      <td>{r.className || '—'}</td>
                      <td>
                        <span className={`badge ${STATUS_CLASS[r.paymentStatus] || 'badge--neutral'}`}>
                          {STATUS_LABELS[r.paymentStatus] || r.paymentStatus}
                        </span>
                      </td>
                      <td>{r.confirmedAtUtc ? new Date(r.confirmedAtUtc).toLocaleDateString() : '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </PageLayout>
  );
}
