import { useState, useEffect, useCallback, useRef } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import { apiFetch, getApiBase, STORAGE_TENANT_KEY } from '../api';
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
  const [uploadingId, setUploadingId] = useState(null);
  const fileInputRefs = useRef({});
  const [paying, setPaying] = useState(false);

  const loadData = useCallback(() => {
    setLoading(true);
    setError(null);
    Promise.all([
      apiFetch('/api/schools/dashboard').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/teachers').then((r) => (r.ok ? r.json() : [])),
      apiFetch('/api/students').then((r) => (r.ok ? r.json() : [])),
      apiFetch('/api/billing').then((r) => (r.ok ? r.json() : [])),
    ])
      .then(([dash, tList, sList, bList]) => {
        setDashboard(dash);
        setTeachers(Array.isArray(tList) ? tList : []);
        setStudents(Array.isArray(sList) ? sList : []);
        setBilling(Array.isArray(bList) ? bList : []);
      })
      .catch((err) => setError(err.message || 'Failed to load data'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  const currentBilling = billing.length > 0 ? billing[0] : null;
  const outstanding = currentBilling ? Math.max(0, (currentBilling.amountDue || 0) - (currentBilling.amountPaid || 0)) : 0;

  const handlePayWithPaystack = async () => {
    if (!currentBilling || outstanding <= 0 || paying) return;
    setPaying(true);
    try {
      const res = await apiFetch('/api/billing/initiate-payment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ billingRecordId: currentBilling.id }),
      });
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      if (data.authorizationUrl) {
        window.location.assign(data.authorizationUrl);
      }
    } catch (e) {
      // eslint-disable-next-line no-alert
      alert(e.message || 'Could not start payment. Try again or contact support.');
    } finally {
      setPaying(false);
    }
  };

  const onPhotoFileChange = async (studentId, e) => {
    const file = e.target?.files?.[0];
    if (!file) return;
    setUploadingId(studentId);
    const form = new FormData();
    form.append('file', file);
    try {
      const res = await apiFetch(`/api/students/${studentId}/photo`, { method: 'POST', body: form });
      if (res.ok) loadData();
    } finally {
      setUploadingId(null);
      e.target.value = '';
    }
  };

  if (loading) return <PageLayout title="School Admin"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (error) return <PageLayout title="School Admin"><p className="empty-state empty-state--error">{error}</p></PageLayout>;

  const currencyCode = dashboard?.currencyCode || 'NGN';

  return (
    <PageLayout title="School Admin">
      {outstanding > 0 && (
        <div className="access-codes-result access-codes-result--error" style={{ marginBottom: '1rem' }}>
          <p style={{ margin: 0 }}>
            You have an outstanding balance of <strong>{formatMoney(outstanding, currencyCode)}</strong> for {currentBilling?.periodLabel || 'this period'}.
          </p>
          <button
            type="button"
            className="btn-excel btn-generate"
            style={{ marginTop: '0.5rem' }}
            onClick={handlePayWithPaystack}
            disabled={paying}
          >
            {paying ? 'Redirecting…' : 'Pay with Paystack'}
          </button>
        </div>
      )}
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
      <p className="card-desc">Register students one at a time or import many from Excel — whichever you prefer.</p>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', marginTop: '0.5rem', marginBottom: '0.75rem' }}>
        <Link to="/school/students/add" className="btn-excel btn-download" style={{ display: 'inline-flex' }}>
          Add one student
        </Link>
        <Link to="/school/import" className="btn-excel btn-download" style={{ display: 'inline-flex', background: 'var(--color-neutral-border)', color: 'var(--color-neutral-text)' }}>
          Bulk upload (Excel)
        </Link>
      </div>
      {students.length === 0 ? (
        <p className="empty-state">No students yet. Add one student or bulk upload from Excel.</p>
      ) : (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th style={{ width: '56px' }}>Photo</th>
                <th>Name</th>
                <th>Admission #</th>
                <th>Class</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {students.slice(0, 50).map((s) => (
                <tr key={s.id}>
                  <td>
                    <StudentPhoto studentId={s.id} firstName={s.firstName} lastName={s.lastName} size={40} />
                  </td>
                  <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                  <td>{s.admissionNumber || '—'}</td>
                  <td>{s.class?.name || '—'}</td>
                  <td>
                    <input
                      type="file"
                      accept=".jpg,.jpeg,.png,.gif,.webp"
                      ref={(el) => { fileInputRefs.current[s.id] = el; }}
                      onChange={(e) => onPhotoFileChange(s.id, e)}
                      style={{ display: 'none' }}
                      aria-label={`Upload photo for ${[s.firstName, s.lastName].filter(Boolean).join(' ')}`}
                    />
                    <button
                      type="button"
                      className="btn-upload-photo"
                      onClick={() => fileInputRefs.current[s.id]?.click()}
                      disabled={uploadingId === s.id}
                    >
                      {uploadingId === s.id ? '…' : 'Upload photo'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {students.length > 50 && <p className="card-desc" style={{ marginTop: '0.5rem' }}>Showing first 50 of {students.length} students.</p>}
        </div>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Parent Access Codes</h2>
      <p className="card-desc">Generate unique codes (e.g. RF-8821) for each student. Give the code to the parent so they can claim their child in the app or web.</p>
      <Link to="/school/access-codes" className="btn-excel btn-download" style={{ display: 'inline-flex', marginTop: '0.5rem' }}>
        Manage access codes
      </Link>

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Share with teachers</h2>
      <p className="card-desc">Share this link with teachers so they can sign up directly under your school.</p>
      <TeacherSignupLink />

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Bulk upload</h2>
      <p className="card-desc">Import students from Excel with preview and validation. First 50 students free.</p>
      <Link to="/school/import" className="btn-excel btn-download" style={{ display: 'inline-flex', marginTop: '0.5rem' }}>
        Open Excel import
      </Link>

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

function TeacherSignupLink() {
  const schoolId = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_TENANT_KEY) : null;
  const teacherSignupUrl = schoolId ? `${typeof window !== 'undefined' ? window.location.origin : ''}/teacher/signup?school=${schoolId}` : '';

  const copyTeacherSignup = () => {
    if (teacherSignupUrl) navigator.clipboard.writeText(teacherSignupUrl);
  };

  if (!teacherSignupUrl) {
    return <p className="empty-state">Sign in as School Admin and select your school to see your teacher signup link here.</p>;
  }

  return (
    <div className="parent-signup-link-box" style={{ marginTop: '0.5rem' }}>
      <code className="parent-signup-url">{teacherSignupUrl}</code>
      <button type="button" className="btn-copy" onClick={copyTeacherSignup} title="Copy teacher signup link">
        Copy link
      </button>
    </div>
  );
}

