import { useState, useEffect, useCallback, useRef } from 'react';
import { Link, useLocation } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import TeacherPhoto from '../components/TeacherPhoto';
import { apiFetch, getApiBase, STORAGE_ONBOARDING_KEY, STORAGE_TENANT_KEY } from '../api';
import './RolePages.css';

function formatMoney(amount, currencyCode) {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: currencyCode || 'NGN', maximumFractionDigits: 0 }).format(n);
}

function getSchoolAdminViewFromHash(hash) {
  switch ((hash || '').replace(/^#/, '').toLowerCase()) {
    case 'people':
      return 'people';
    case 'operations':
      return 'operations';
    default:
      return 'overview';
  }
}

export default function SchoolAdminPage() {
  const location = useLocation();
  const [dashboard, setDashboard] = useState(null);
  const [teachers, setTeachers] = useState([]);
  const [students, setStudents] = useState([]);
  const [parents, setParents] = useState([]);
  const [billing, setBilling] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [uploadingId, setUploadingId] = useState(null);
  const [uploadingAsset, setUploadingAsset] = useState(false);
  const [selectedTeacherId, setSelectedTeacherId] = useState(null);
  const fileInputRefs = useRef({});
  const schoolFileInputRef = useRef(null);
  const [paying, setPaying] = useState(false);
  const [activeView, setActiveView] = useState(() => getSchoolAdminViewFromHash(typeof window !== 'undefined' ? window.location.hash : ''));
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
    Promise.all([
      apiFetch('/api/schools/dashboard').then((r) => readJsonOrThrow(r, 'Failed to load school dashboard.')),
      apiFetch('/api/teachers').then((r) => readJsonOrThrow(r, 'Failed to load teachers.')),
      apiFetch('/api/students').then((r) => readJsonOrThrow(r, 'Failed to load students.')),
      apiFetch('/api/parents').then((r) => readJsonOrThrow(r, 'Failed to load parents.')),
      apiFetch('/api/billing').then((r) => readJsonOrThrow(r, 'Failed to load billing records.')),
    ])
      .then(([dash, tList, sList, pList, bList]) => {
        setDashboard(dash || null);
        setTeachers(Array.isArray(tList) ? tList : []);
        setStudents(Array.isArray(sList) ? sList : []);
        setParents(Array.isArray(pList) ? pList : []);
        setBilling(Array.isArray(bList) ? bList : []);
      })
      .catch((err) => setError(err.message || 'Failed to load data'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  useEffect(() => {
    if (teachers.length === 0) {
      setSelectedTeacherId(null);
      return;
    }

    if (selectedTeacherId && !teachers.some((teacher) => teacher.id === selectedTeacherId)) {
      setSelectedTeacherId(null);
    }
  }, [teachers, selectedTeacherId]);

  useEffect(() => {
    setActiveView(getSchoolAdminViewFromHash(location.hash));
  }, [location.hash]);

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
  const selectedTeacher = teachers.find((teacher) => teacher.id === selectedTeacherId) || null;

  const switchView = (view) => {
    setActiveView(view);
    if (typeof window !== 'undefined') {
      const nextHash = view === 'overview' ? '#overview' : `#${view}`;
      window.history.replaceState(null, '', `${window.location.pathname}${nextHash}`);
    }
  };

  return (
    <PageLayout title="School Admin" role="school">
      <div className="school-admin-shell">
        <aside className="school-admin-nav">
          <button type="button" className={`school-admin-nav-btn ${activeView === 'overview' ? 'is-active' : ''}`} onClick={() => switchView('overview')}>
            Overview
          </button>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'people' ? 'is-active' : ''}`} onClick={() => switchView('people')}>
            People
          </button>
          <Link to="/school/classes" className="school-admin-nav-btn school-admin-nav-link">
            Grades &amp; classes
          </Link>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'operations' ? 'is-active' : ''}`} onClick={() => switchView('operations')}>
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
            <Link to={onboardingSummary?.schoolId ? `/teacher/signup?school=${encodeURIComponent(onboardingSummary.schoolId)}` : '/teacher'} className="action-card">
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

      <section aria-labelledby="school-admin-actions-heading" style={{ marginTop: '1.5rem' }}>
        <h3 id="school-admin-actions-heading" className="section-title">Quick setup actions</h3>
        <p className="card-desc">Create classes, assign teachers, and share invite links directly from the dashboard.</p>
        <div className="success-actions" style={{ marginTop: '0.75rem' }}>
          <Link to="/school/classes" className="action-card">
            <h3>Create grades &amp; classes</h3>
            <p>Set up Nursery, Primary, JSS, SS and the class arms your school uses.</p>
          </Link>
          <Link to="/school/classes#teacher-assignments" className="action-card">
            <h3>Assign classes to teachers</h3>
            <p>Link each teacher to a class so results and student lists show correctly.</p>
          </Link>
          <Link to="/school/access-codes" className="action-card">
            <h3>Open sharing links</h3>
            <p>Copy the teacher invite link and manage parent access codes in one place.</p>
          </Link>
        </div>

        <div style={{ marginTop: '1rem' }}>
          <h3 className="section-title">Teacher signup link</h3>
          <p className="card-desc">Send this link to your teachers so they can join your school account.</p>
          <TeacherSignupLink schoolIdFromApi={dashboard?.schoolId} />
        </div>
      </section>
        </>
      )}

      {activeView === 'people' && (
        <>
      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Teachers</h2>
      {teachers.length === 0 ? (
        <p className="empty-state">No teachers yet.</p>
      ) : (
        <>
          <div className="data-table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Phone</th>
                  <th>Role</th>
                  <th>Classes</th>
                  <th>Students</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {teachers.map((t) => (
                  <tr key={t.id} style={selectedTeacherId === t.id ? { background: 'rgba(59, 130, 246, 0.08)' } : undefined}>
                    <td>{[t.firstName, t.middleName, t.lastName].filter(Boolean).join(' ')}</td>
                    <td>{t.email || '—'}</td>
                    <td>{t.phone || '—'}</td>
                    <td>{t.roleTitle || 'Teacher'}</td>
                    <td>{t.assignedClassCount ?? t.teacherClasses?.length ?? 0}</td>
                    <td>{t.assignedStudentCount ?? 0}</td>
                    <td>
                      <button
                        type="button"
                        className="btn-primary-action btn-primary-action--ghost"
                        onClick={() => setSelectedTeacherId((prev) => (prev === t.id ? null : t.id))}
                        aria-expanded={selectedTeacherId === t.id}
                      >
                        {selectedTeacherId === t.id ? 'Hide details' : 'View details'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {selectedTeacher ? (
            <TeacherDetailsPanel
              teacher={selectedTeacher}
              onClose={() => setSelectedTeacherId(null)}
              onTeacherUpdated={(updatedTeacher) => {
                setTeachers((prev) => prev.map((item) => (item.id === updatedTeacher.id ? updatedTeacher : item)));
              }}
            />
          ) : (
            <p className="card-desc" style={{ marginTop: '0.75rem' }}>
              Teacher details stay hidden until you click <strong>View details</strong>.
            </p>
          )}
        </>
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
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', marginTop: '0.5rem' }}>
        <Link to="/school/classes" className="btn-excel btn-download" style={{ display: 'inline-flex' }}>
          Open grades &amp; classes setup
        </Link>
        <Link to="/school/classes#teacher-assignments" className="btn-excel btn-download" style={{ display: 'inline-flex' }}>
          Assign teachers to classes
        </Link>
      </div>

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

const MANAGED_TEACHER_FIELDS = [
  { fieldKey: 'baseSalaryAmount', displayName: 'Base salary' },
  { fieldKey: 'allowancesNote', displayName: 'Allowances' },
  { fieldKey: 'recognitions', displayName: 'Recognitions' },
];

function buildTeacherAdminFormState(teacher) {
  const fieldSettings = Array.isArray(teacher?.fieldSettings)
    ? teacher.fieldSettings.map((field) => ({ ...field }))
    : [];

  MANAGED_TEACHER_FIELDS.forEach((field, index) => {
    if (!fieldSettings.some((item) => item.fieldKey === field.fieldKey)) {
      fieldSettings.push({
        fieldKey: field.fieldKey,
        displayName: field.displayName,
        isCustom: false,
        isVisibleToTeacher: false,
        isEditableByTeacher: false,
        isAdminOnly: true,
        sortOrder: index,
      });
    }
  });

  return {
    baseSalaryAmount: teacher?.baseSalaryAmount ?? '',
    baseSalaryCurrency: teacher?.baseSalaryCurrency || 'NGN',
    allowancesNote: teacher?.allowancesNote || '',
    recognitions: teacher?.recognitions || '',
    fieldSettings,
    customFields: Array.isArray(teacher?.customFields) ? teacher.customFields.map((field) => ({ ...field })) : [],
    newCustomFieldName: '',
  };
}

function TeacherDetailsPanel({ teacher, onTeacherUpdated, onClose }) {
  const fullName = [teacher.firstName, teacher.middleName, teacher.lastName].filter(Boolean).join(' ') || 'Teacher';
  const [adminForm, setAdminForm] = useState(() => buildTeacherAdminFormState(teacher));
  const [savingSettings, setSavingSettings] = useState(false);
  const [settingsMessage, setSettingsMessage] = useState(null);

  useEffect(() => {
    setAdminForm(buildTeacherAdminFormState(teacher));
    setSettingsMessage(null);
  }, [teacher]);

  const updateFieldSetting = (fieldKey, changes, fallbackDisplayName) => {
    setAdminForm((prev) => {
      const existing = prev.fieldSettings.find((field) => field.fieldKey === fieldKey);
      const nextField = {
        fieldKey,
        displayName: fallbackDisplayName || existing?.displayName || fieldKey,
        isCustom: existing?.isCustom ?? fieldKey.startsWith('custom-'),
        isVisibleToTeacher: existing?.isVisibleToTeacher ?? true,
        isEditableByTeacher: existing?.isEditableByTeacher ?? (!fieldKey.startsWith('baseSalary') && !fieldKey.startsWith('allowances') && !fieldKey.startsWith('recognitions')),
        isAdminOnly: existing?.isAdminOnly ?? false,
        sortOrder: existing?.sortOrder ?? prev.fieldSettings.length,
        ...changes,
      };

      return {
        ...prev,
        fieldSettings: existing
          ? prev.fieldSettings.map((field) => (field.fieldKey === fieldKey ? nextField : field))
          : [...prev.fieldSettings, nextField],
      };
    });
    setSettingsMessage(null);
  };

  const updateCustomField = (fieldKey, changes) => {
    setAdminForm((prev) => ({
      ...prev,
      customFields: prev.customFields.map((field) => (
        field.fieldKey === fieldKey ? { ...field, ...changes } : field
      )),
    }));
    setSettingsMessage(null);
  };

  const handleAddCustomField = () => {
    const displayName = adminForm.newCustomFieldName.trim();
    if (!displayName) return;

    const baseKey = displayName.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'custom-field';
    let fieldKey = `custom-${baseKey}`;
    let suffix = 2;
    const usedKeys = new Set(adminForm.customFields.map((field) => field.fieldKey));
    while (usedKeys.has(fieldKey)) {
      fieldKey = `custom-${baseKey}-${suffix}`;
      suffix += 1;
    }

    const sortOrder = adminForm.customFields.length + 10;
    const newField = {
      fieldKey,
      displayName,
      value: '',
      isVisibleToTeacher: true,
      isEditableByTeacher: true,
      isAdminOnly: false,
      sortOrder,
    };

    setAdminForm((prev) => ({
      ...prev,
      customFields: [...prev.customFields, newField],
      fieldSettings: [
        ...prev.fieldSettings,
        {
          fieldKey,
          displayName,
          isCustom: true,
          isVisibleToTeacher: true,
          isEditableByTeacher: true,
          isAdminOnly: false,
          sortOrder,
        },
      ],
      newCustomFieldName: '',
    }));
    setSettingsMessage(null);
  };

  const handleSaveAdminControls = async () => {
    setSavingSettings(true);
    setSettingsMessage(null);

    try {
      const payload = {
        firstName: teacher.firstName || '',
        lastName: teacher.lastName || '',
        middleName: teacher.middleName || null,
        email: teacher.email || null,
        phone: teacher.phone || null,
        whatsAppNumber: teacher.whatsAppNumber || null,
        staffId: teacher.staffId || null,
        subjectSpecialization: teacher.subjectSpecialization || null,
        dateOfBirth: teacher.dateOfBirth || null,
        gender: teacher.gender || null,
        nationality: teacher.nationality || null,
        stateOfOrigin: teacher.stateOfOrigin || null,
        lga: teacher.lga || null,
        religion: teacher.religion || null,
        nin: teacher.nin || null,
        nationalIdType: teacher.nationalIdType || null,
        nationalIdNumber: teacher.nationalIdNumber || null,
        trcnNumber: teacher.trcnNumber || null,
        residentialAddress: teacher.residentialAddress || null,
        highestQualification: teacher.highestQualification || null,
        fieldOfStudy: teacher.fieldOfStudy || null,
        yearsOfExperience: teacher.yearsOfExperience ?? null,
        previousSchools: teacher.previousSchools || null,
        professionalBodies: teacher.professionalBodies || null,
        dateEmployed: teacher.dateEmployed || null,
        employmentType: teacher.employmentType || null,
        roleTitle: teacher.roleTitle || null,
        department: teacher.department || null,
        baseSalaryAmount: adminForm.baseSalaryAmount === '' ? null : Number(adminForm.baseSalaryAmount),
        baseSalaryCurrency: adminForm.baseSalaryCurrency || 'NGN',
        allowancesNote: adminForm.allowancesNote.trim() || null,
        promotionHistory: teacher.promotionHistory || null,
        recognitions: adminForm.recognitions.trim() || null,
        isActive: teacher.isActive ?? true,
        fieldSettings: adminForm.fieldSettings.map((field) => ({
          fieldKey: field.fieldKey,
          displayName: field.displayName,
          isCustom: !!field.isCustom,
          isVisibleToTeacher: !!field.isVisibleToTeacher,
          isEditableByTeacher: !!field.isEditableByTeacher,
          isAdminOnly: !!field.isAdminOnly,
          sortOrder: field.sortOrder || 0,
        })),
        customFields: adminForm.customFields.map((field) => ({
          fieldKey: field.fieldKey,
          displayName: field.displayName,
          value: field.value || null,
          isVisibleToTeacher: !!field.isVisibleToTeacher,
          isEditableByTeacher: !!field.isEditableByTeacher,
          isAdminOnly: !!field.isAdminOnly,
          sortOrder: field.sortOrder || 0,
        })),
      };

      const response = await apiFetch(`/api/teachers/${teacher.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        throw new Error(await response.text().catch(() => 'Could not save teacher settings.'));
      }

      const updatedTeacher = await response.json();
      onTeacherUpdated?.(updatedTeacher);
      setAdminForm(buildTeacherAdminFormState(updatedTeacher));
      setSettingsMessage({ type: 'success', text: 'Teacher access rules updated.' });
    } catch (error) {
      setSettingsMessage({ type: 'error', text: error.message || 'Could not save teacher settings.' });
    } finally {
      setSavingSettings(false);
    }
  };

  const infoItems = [
    ['Email', teacher.email],
    ['Phone', teacher.phone],
    ['WhatsApp', teacher.whatsAppNumber],
    ['Staff ID', teacher.staffId],
    ['Subject specialization', teacher.subjectSpecialization],
    ['Highest qualification', teacher.highestQualification],
    ['Field of study', teacher.fieldOfStudy],
    ['Years of experience', teacher.yearsOfExperience != null ? `${teacher.yearsOfExperience} year(s)` : '—'],
    ['Date of birth', formatTeacherDate(teacher.dateOfBirth)],
    ['Gender', teacher.gender],
    ['Nationality', teacher.nationality],
    ['State of origin', teacher.stateOfOrigin],
    ['LGA', teacher.lga],
    ['Religion', teacher.religion],
    ['Residential address', teacher.residentialAddress],
    ['NIN', teacher.nin],
    ['National ID type', teacher.nationalIdType],
    ['National ID number', teacher.nationalIdNumber],
    ['TRCN number', teacher.trcnNumber],
    ['Employment type', teacher.employmentType],
    ['Date employed', formatTeacherDate(teacher.dateEmployed)],
    ['Base salary', teacher.baseSalaryAmount != null ? formatMoney(teacher.baseSalaryAmount, teacher.baseSalaryCurrency || 'NGN') : '—'],
    ['Allowances', teacher.allowancesNote],
    ['Promotion history', teacher.promotionHistory],
    ['Recognitions', teacher.recognitions],
    ['Created', formatTeacherDate(teacher.createdAtUtc)],
    ['Last updated', formatTeacherDate(teacher.updatedAtUtc)],
  ];

  return (
    <section className="progress-section" aria-label="Teacher details" style={{ marginTop: '1rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem', alignItems: 'flex-start', flexWrap: 'wrap', marginBottom: '1rem' }}>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <TeacherPhoto teacherId={teacher.id} fullName={fullName} size={56} />
          <div>
            <h3 className="card-title" style={{ margin: 0 }}>{fullName}</h3>
            <p className="card-desc">Role: {teacher.roleTitle || 'Teacher'} • Department: {teacher.department || '—'} • Status: {teacher.isActive ? 'Active' : 'Inactive'}</p>
            <p className="card-desc">Classes handled: {teacher.assignedClassCount ?? teacher.teacherClasses?.length ?? 0} • Students handled: {teacher.assignedStudentCount ?? 0}</p>
          </div>
        </div>
        {onClose && (
          <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={onClose}>
            Hide details
          </button>
        )}
      </div>

      {Array.isArray(teacher.teacherClasses) && teacher.teacherClasses.length > 0 && (
        <div style={{ marginBottom: '1rem' }}>
          <p className="dashboard-label">Assigned classes</p>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginTop: '0.35rem' }}>
            {teacher.teacherClasses.map((assignedClass) => (
              <span
                key={`${teacher.id}-${assignedClass.classId}-${assignedClass.roleInClass || 'teacher'}`}
                style={{ padding: '0.3rem 0.6rem', borderRadius: '999px', background: '#eef2ff', color: '#3730a3', fontSize: '0.85rem' }}
              >
                {assignedClass.className}
                {assignedClass.roleInClass ? ` • ${assignedClass.roleInClass}` : ''}
              </span>
            ))}
          </div>
        </div>
      )}

      <div className="dashboard-card" style={{ marginBottom: '1rem' }}>
        <h4 className="card-title" style={{ marginBottom: '0.35rem' }}>Admin-managed teacher profile controls</h4>
        <p className="card-desc" style={{ marginBottom: '0.85rem' }}>Choose what this teacher can see, keep salary details admin-only, and add custom profile fields.</p>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '0.75rem' }}>
          <label>
            <span className="dashboard-label">Base salary</span>
            <input
              type="number"
              min="0"
              className="form-input"
              value={adminForm.baseSalaryAmount}
              onChange={(e) => setAdminForm((prev) => ({ ...prev, baseSalaryAmount: e.target.value }))}
            />
          </label>
          <label>
            <span className="dashboard-label">Currency</span>
            <input
              className="form-input"
              value={adminForm.baseSalaryCurrency}
              onChange={(e) => setAdminForm((prev) => ({ ...prev, baseSalaryCurrency: e.target.value.toUpperCase() }))}
              maxLength={6}
            />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>
            <span className="dashboard-label">Allowances</span>
            <textarea
              className="form-input"
              rows={2}
              value={adminForm.allowancesNote}
              onChange={(e) => setAdminForm((prev) => ({ ...prev, allowancesNote: e.target.value }))}
            />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>
            <span className="dashboard-label">Recognitions</span>
            <textarea
              className="form-input"
              rows={2}
              value={adminForm.recognitions}
              onChange={(e) => setAdminForm((prev) => ({ ...prev, recognitions: e.target.value }))}
            />
          </label>
        </div>

        <div style={{ marginTop: '1rem', display: 'grid', gap: '0.75rem' }}>
          {MANAGED_TEACHER_FIELDS.map((field) => {
            const fieldSetting = adminForm.fieldSettings.find((item) => item.fieldKey === field.fieldKey) || {};
            return (
              <div key={field.fieldKey} style={{ border: '1px solid var(--color-neutral-border)', borderRadius: '12px', padding: '0.75rem' }}>
                <p className="dashboard-label" style={{ marginBottom: '0.35rem' }}>{field.displayName}</p>
                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.35rem' }}>
                  <input
                    type="checkbox"
                    checked={!!fieldSetting.isVisibleToTeacher}
                    onChange={(e) => updateFieldSetting(field.fieldKey, {
                      displayName: field.displayName,
                      isVisibleToTeacher: e.target.checked,
                      isEditableByTeacher: false,
                      isAdminOnly: true,
                      isCustom: false,
                    }, field.displayName)}
                  />
                  <span className="card-desc">Show to teacher</span>
                </label>
                <p className="card-desc">This item stays admin-only for editing.</p>
              </div>
            );
          })}
        </div>

        <div style={{ marginTop: '1rem' }}>
          <h5 className="card-title" style={{ marginBottom: '0.35rem' }}>Custom fields</h5>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
            <input
              className="form-input"
              style={{ flex: '1 1 220px' }}
              placeholder="e.g. House duty, Mentor group"
              value={adminForm.newCustomFieldName}
              onChange={(e) => setAdminForm((prev) => ({ ...prev, newCustomFieldName: e.target.value }))}
            />
            <button type="button" className="btn-excel btn-download" onClick={handleAddCustomField}>
              Add field
            </button>
          </div>

          {adminForm.customFields.length === 0 ? (
            <p className="card-desc">No custom fields added yet.</p>
          ) : (
            <div style={{ display: 'grid', gap: '0.75rem' }}>
              {adminForm.customFields.map((field) => (
                <div key={field.fieldKey} style={{ border: '1px solid var(--color-neutral-border)', borderRadius: '12px', padding: '0.75rem' }}>
                  <label style={{ display: 'grid', gap: '0.35rem' }}>
                    <span className="dashboard-label">{field.displayName}</span>
                    <input
                      className="form-input"
                      value={field.value || ''}
                      onChange={(e) => updateCustomField(field.fieldKey, { value: e.target.value })}
                    />
                  </label>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.85rem', marginTop: '0.65rem' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                      <input
                        type="checkbox"
                        checked={!!field.isVisibleToTeacher}
                        onChange={(e) => {
                          updateCustomField(field.fieldKey, { isVisibleToTeacher: e.target.checked });
                          updateFieldSetting(field.fieldKey, { isVisibleToTeacher: e.target.checked }, field.displayName);
                        }}
                      />
                      <span className="card-desc">Visible to teacher</span>
                    </label>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                      <input
                        type="checkbox"
                        checked={!!field.isEditableByTeacher}
                        onChange={(e) => {
                          updateCustomField(field.fieldKey, { isEditableByTeacher: e.target.checked, isAdminOnly: !e.target.checked });
                          updateFieldSetting(field.fieldKey, { isEditableByTeacher: e.target.checked, isAdminOnly: !e.target.checked, isCustom: true }, field.displayName);
                        }}
                      />
                      <span className="card-desc">Teacher can edit</span>
                    </label>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="form-actions" style={{ marginTop: '1rem', alignItems: 'center' }}>
          <button type="button" className="btn-excel btn-generate" onClick={handleSaveAdminControls} disabled={savingSettings}>
            {savingSettings ? 'Saving…' : 'Save teacher access rules'}
          </button>
          <span className="card-desc">Teachers only see fields you allow and can edit only unlocked items.</span>
        </div>

        {settingsMessage && (
          <p className={`access-codes-result ${settingsMessage.type === 'error' ? 'access-codes-result--error' : 'access-codes-result--success'}`} style={{ marginTop: '0.75rem' }}>
            {settingsMessage.text}
          </p>
        )}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '0.75rem' }}>
        {infoItems.map(([label, value]) => (
          <div key={label} className="dashboard-card" style={{ padding: '0.85rem 1rem' }}>
            <p className="dashboard-label">{label}</p>
            <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>{value || '—'}</p>
          </div>
        ))}
      </div>
    </section>
  );
}

function formatTeacherDate(value) {
  if (!value) return '—';
  try {
    return new Date(value).toLocaleDateString();
  } catch {
    return String(value);
  }
}

