import { useState, useEffect, useCallback, useRef } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import { apiFetch, getApiBase, STORAGE_ONBOARDING_KEY, STORAGE_TENANT_KEY } from '../api';
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
  const [parents, setParents] = useState([]);
  const [billing, setBilling] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [uploadingId, setUploadingId] = useState(null);
  const [uploadingAsset, setUploadingAsset] = useState(false);
  const fileInputRefs = useRef({});
  const schoolFileInputRef = useRef(null);
  const [paying, setPaying] = useState(false);
  const [activeView, setActiveView] = useState('overview');
  const [onboardingSummary, setOnboardingSummary] = useState(() => {
    try {
      const raw = localStorage.getItem(STORAGE_ONBOARDING_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  });

  const readJsonOrThrow = async (response, fallbackMessage) => {
    if (response.status === 401 || response.status === 403) {
      throw new Error('Your session expired or your school access is missing. Please sign in again as School Admin.');
    }
    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(text || fallbackMessage);
    }
    return response.json();
  };

  const loadData = useCallback(() => {
    setLoading(true);
    setError(null);
    Promise.allSettled([
      apiFetch('/api/schools/dashboard').then((r) => readJsonOrThrow(r, 'Failed to load school dashboard.')),
      apiFetch('/api/teachers').then((r) => readJsonOrThrow(r, 'Failed to load teachers.')),
      apiFetch('/api/students').then((r) => readJsonOrThrow(r, 'Failed to load students.')),
      apiFetch('/api/parents').then((r) => readJsonOrThrow(r, 'Failed to load parents.')),
      apiFetch('/api/billing').then((r) => readJsonOrThrow(r, 'Failed to load billing records.')),
    ])
      .then((results) => {
        const [dashResult, teacherResult, studentResult, parentResult, billingResult] = results;
        const dash = dashResult.status === 'fulfilled' ? dashResult.value : null;

        if (!dash) {
          const failure = results.find((result) => result.status === 'rejected');
          throw new Error(failure?.reason?.message || 'Failed to load school dashboard.');
        }

        setDashboard(dash);
        setTeachers(teacherResult.status === 'fulfilled' && Array.isArray(teacherResult.value) ? teacherResult.value : []);
        setStudents(studentResult.status === 'fulfilled' && Array.isArray(studentResult.value) ? studentResult.value : []);
        setParents(parentResult.status === 'fulfilled' && Array.isArray(parentResult.value) ? parentResult.value : []);
        setBilling(billingResult.status === 'fulfilled' && Array.isArray(billingResult.value) ? billingResult.value : []);
      })
      .catch((err) => {
        const message = /blocked or unreachable|failed to fetch|networkerror/i.test(String(err?.message || ''))
          ? 'The live school dashboard is syncing right now. Please refresh again shortly.'
          : (err.message || 'Failed to load data');
        setError(message);
      })
      .finally(() => setLoading(false));
  }, [readJsonOrThrow]);

  useEffect(() => { loadData(); }, [loadData]);

  // Keep X-Tenant-Id in sync with the signed-in school (fixes teacher/parent share links if localStorage was cleared).
  useEffect(() => {
    const id = dashboard?.schoolId;
    if (!id || typeof localStorage === 'undefined') return;
    try {
      localStorage.setItem(STORAGE_TENANT_KEY, id);
    } catch {
      // ignore
    }
  }, [dashboard?.schoolId]);

  useEffect(() => {
    if (!onboardingSummary) return;

    // Hide after 24h
    const createdAt = onboardingSummary.createdAtUtc ? Date.parse(onboardingSummary.createdAtUtc) : NaN;
    const expired = Number.isFinite(createdAt) && (Date.now() - createdAt) > (24 * 60 * 60 * 1000);

    // Hide once first student exists (imported or added manually)
    const hasStudents = (dashboard?.studentCount ?? dashboard?.activeStudentCount ?? 0) > 0 || students.length > 0;

    if (expired || hasStudents) {
      try {
        localStorage.removeItem(STORAGE_ONBOARDING_KEY);
      } catch {
        // ignore
      }
      setOnboardingSummary(null);
    }
  }, [onboardingSummary, dashboard?.activeStudentCount, students.length]);

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

  const onSchoolFileChange = async (e) => {
    const file = e.target?.files?.[0];
    if (!file || uploadingAsset) return;
    setUploadingAsset(true);
    const form = new FormData();
    form.append('file', file);
    form.append('category', 'school-document');
    try {
      const res = await apiFetch('/api/files/upload', { method: 'POST', body: form });
      if (!res.ok) {
        // eslint-disable-next-line no-alert
        alert('Could not upload file. Please try again.');
      } else {
        // eslint-disable-next-line no-alert
        alert('File uploaded successfully.');
      }
    } catch {
      // eslint-disable-next-line no-alert
      alert('Could not upload file. Please try again.');
    } finally {
      setUploadingAsset(false);
      if (e.target) e.target.value = '';
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

  if (loading) return <PageLayout title="School Admin" role="school"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (error) return <PageLayout title="School Admin" role="school"><p className="empty-state empty-state--error">{error}</p></PageLayout>;

  const currencyCode = dashboard?.currencyCode || 'NGN';
  const activeStudents = dashboard?.studentCount ?? dashboard?.activeStudentCount ?? students.length;
  const unpaidFees = dashboard?.unpaidFeesTotal ?? 0;
  const buildPublicUrl = (relativePath) => {
    if (!relativePath) return null;
    if (relativePath.startsWith('http://') || relativePath.startsWith('https://')) return relativePath;
    const normalizedPath = relativePath.replace(/^\/+/, '');
    const base = getApiBase();
    if (!base) return `/${normalizedPath}`;
    return `${base}/${normalizedPath}`;
  };
  const dismissOnboardingSummary = () => {
    setOnboardingSummary(null);
    try {
      localStorage.removeItem(STORAGE_ONBOARDING_KEY);
    } catch {
      // ignore
    }
  };

  return (
    <PageLayout title="School Admin" role="school">
      <div className="school-admin-shell">
        <aside className="school-admin-nav">
          <button type="button" className={`school-admin-nav-btn ${activeView === 'overview' ? 'is-active' : ''}`} onClick={() => setActiveView('overview')}>
            Overview
          </button>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'people' ? 'is-active' : ''}`} onClick={() => setActiveView('people')}>
            People
          </button>
          <Link to="/school/classes" className="school-admin-nav-btn school-admin-nav-link">
            Grades &amp; classes
          </Link>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'operations' ? 'is-active' : ''}`} onClick={() => setActiveView('operations')}>
            Operations
          </button>
        </aside>

        <section className="school-admin-view">
      {onboardingSummary?.schoolName && (
        <section className="school-welcome-panel" aria-label="School setup complete">
          <button type="button" className="school-welcome-close" onClick={dismissOnboardingSummary} aria-label="Dismiss welcome panel">×</button>
          <h2 className="school-welcome-title">Congratulations, {onboardingSummary.schoolName} is now live!</h2>
          <p className="school-welcome-sub">Your setup is complete. Welcome to RiseFlow.</p>

          <div className="school-id-box">
            <span className="school-id-label">RiseFlow ID</span>
            <strong className="school-id-value">{onboardingSummary.schoolId || 'Generated'}</strong>
          </div>

          {onboardingSummary.logoPath && (
            <div className="logo-preview-box">
              <span className="school-id-label">School Logo Preview</span>
              <a
                href={buildPublicUrl(onboardingSummary.logoPath)}
                target="_blank"
                rel="noopener noreferrer"
                className="logo-preview-link"
              >
                <img
                  src={buildPublicUrl(onboardingSummary.logoPath)}
                  alt={`${onboardingSummary.schoolName} logo`}
                  className="logo-preview-image"
                  loading="lazy"
                />
              </a>
            </div>
          )}

          {(onboardingSummary.logoPath || onboardingSummary.cacDocumentPath) && (
            <div className="school-files-box">
              <span className="school-id-label">Uploaded Files</span>
              <div className="school-files-list">
                {onboardingSummary.logoPath && (
                  <a href={buildPublicUrl(onboardingSummary.logoPath)} target="_blank" rel="noopener noreferrer">View School Logo</a>
                )}
                {onboardingSummary.cacDocumentPath && (
                  <a href={buildPublicUrl(onboardingSummary.cacDocumentPath)} target="_blank" rel="noopener noreferrer">View CAC Document</a>
                )}
              </div>
            </div>
          )}

          <div className="success-actions">
            <Link to="/school/import" className="action-card">
              <h3>Import Students</h3>
              <p>Upload your student list to go live faster.</p>
            </Link>
            <Link to="/teacher/signup" className="action-card">
              <h3>Add Teachers</h3>
              <p>Create teacher accounts and assign classes.</p>
            </Link>
            <Link to="/school/classes" className="action-card">
              <h3>Set up grades &amp; classes</h3>
              <p>Nursery, Primary, JSS, SS — add the levels and classes your school uses.</p>
            </Link>
          </div>

          <div className="next-checklist">
            <p className="next-checklist-title">Next Steps</p>
            <ul>
              <li><a href={`${getApiBase()}/api/public/teacher-quick-start`} target="_blank" rel="noopener noreferrer">Download the Teacher Guide</a></li>
              <li><Link to="/school/classes">Add your first grade &amp; class</Link></li>
              <li><Link to="/school/access-codes">Print Parent Access Codes</Link></li>
            </ul>
          </div>
        </section>
      )}

      {activeView === 'overview' && (
        <>
      <div className="dashboard-actions" style={{ flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Link to="/school/students" className="btn-primary-action">Students</Link>
        <Link to="/school/classes" className="btn-primary-action btn-primary-action--ghost">Grades & classes</Link>
        <Link to="/school/billing" className="btn-primary-action btn-primary-action--ghost">Billing</Link>
        <Link to="/school/reports" className="btn-primary-action btn-primary-action--ghost">Reports</Link>
        <Link to="/school/import" className="btn-primary-action btn-primary-action--ghost">Import</Link>
        <Link to="/school/access-codes" className="btn-primary-action btn-primary-action--ghost">Access codes</Link>
      </div>
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
            <span className="summary-value">{activeStudents}</span>
            <span className="summary-label">Active students</span>
          </div>
          <div className="summary-card">
            <span className="summary-value">{teachers.length}</span>
            <span className="summary-label">Teachers</span>
          </div>
          <div className="summary-card">
            <span className="summary-value">{parents.length}</span>
            <span className="summary-label">Parents</span>
          </div>
          <div className="summary-card summary-card--warning">
            <span className="summary-value">{formatMoney(unpaidFees, currencyCode)}</span>
            <span className="summary-label">Unpaid fees</span>
          </div>
        </div>
      )}
        </>
      )}

      {activeView === 'people' && (
        <>
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

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Parents</h2>
      {parents.length === 0 ? (
        <p className="empty-state">No parents have signed up yet. Share your parent signup link and access codes so families can join.</p>
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
              {parents.map((p) => (
                <tr key={p.id}>
                  <td>{[p.firstName, p.middleName, p.lastName].filter(Boolean).join(' ')}</td>
                  <td>{p.email || '—'}</td>
                  <td>{p.phone || p.whatsAppNumber || '—'}</td>
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
      <TeacherSignupLink schoolIdFromApi={dashboard?.schoolId} />

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Share with parents</h2>
      <p className="card-desc">Parents create an account and link to their child using the access code you provide.</p>
      <ParentSignupLink schoolIdFromApi={dashboard?.schoolId} />
        </>
      )}

      {activeView === 'operations' && (
        <>
      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Grades &amp; classes</h2>
      <p className="card-desc">Create your school&apos;s programme levels (Nursery, Primary 1–6, JSS, SS1–SS3, etc.) and classes for each level.</p>
      <Link to="/school/classes" className="btn-excel btn-download" style={{ display: 'inline-flex', marginTop: '0.5rem' }}>
        Open grades &amp; classes setup
      </Link>

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>School files &amp; documents</h2>
      <p className="card-desc">
        Upload photos or documents (e.g. letterhead, logo variations) so they are stored safely in your RiseFlow account.
      </p>
      <input
        type="file"
        ref={schoolFileInputRef}
        style={{ display: 'none' }}
        onChange={onSchoolFileChange}
        accept=".jpg,.jpeg,.png,.gif,.webp,.pdf,.doc,.docx"
      />
      <button
        type="button"
        className="btn-excel btn-download"
        style={{ display: 'inline-flex', marginTop: '0.5rem' }}
        onClick={() => schoolFileInputRef.current?.click()}
        disabled={uploadingAsset}
      >
        {uploadingAsset ? 'Uploading…' : 'Upload a file'}
      </button>

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Bulk upload</h2>
      <p className="card-desc">Import students from Excel with preview and validation. First 50 students free after you register your school.</p>
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
        </>
      )}
        </section>
      </div>
    </PageLayout>
  );
}

function resolveSchoolId(schoolIdFromApi) {
  if (schoolIdFromApi) return schoolIdFromApi;
  try {
    return typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_TENANT_KEY) : null;
  } catch {
    return null;
  }
}

function TeacherSignupLink({ schoolIdFromApi }) {
  const schoolId = resolveSchoolId(schoolIdFromApi);
  const teacherSignupUrl = schoolId ? `${typeof window !== 'undefined' ? window.location.origin : ''}/teacher/signup?school=${encodeURIComponent(schoolId)}` : '';

  const copyTeacherSignup = () => {
    if (teacherSignupUrl) navigator.clipboard.writeText(teacherSignupUrl);
  };

  if (!teacherSignupUrl) {
    return <p className="empty-state">Loading your school link… If this persists, sign out and sign in again as School Admin.</p>;
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

function ParentSignupLink({ schoolIdFromApi }) {
  const schoolId = resolveSchoolId(schoolIdFromApi);
  const parentSignupUrl = schoolId ? `${typeof window !== 'undefined' ? window.location.origin : ''}/parent/signup?school=${encodeURIComponent(schoolId)}` : '';

  const copyParentSignup = () => {
    if (parentSignupUrl) navigator.clipboard.writeText(parentSignupUrl);
  };

  if (!parentSignupUrl) {
    return <p className="empty-state">Loading your school link… If this persists, sign out and sign in again as School Admin.</p>;
  }

  return (
    <div className="parent-signup-link-box" style={{ marginTop: '0.5rem' }}>
      <code className="parent-signup-url">{parentSignupUrl}</code>
      <button type="button" className="btn-copy" onClick={copyParentSignup} title="Copy parent signup link">
        Copy link
      </button>
    </div>
  );
}

