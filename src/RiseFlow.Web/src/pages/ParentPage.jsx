import { useState, useEffect, useCallback, useRef } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import TeacherPhoto from '../components/TeacherPhoto';
import StudentRecordPanel from '../components/StudentRecordPanel';
import { apiFetch } from '../api';
import './RolePages.css';
import './ParentPage.css';

function whatsAppUrl(whatsAppNumber, phone) {
  const raw = (whatsAppNumber || phone || '').replace(/\D/g, '');
  if (!raw) return null;
  return `https://wa.me/${raw}`;
}

function displayName(child) {
  return [child.firstName, child.middleName, child.lastName].filter(Boolean).join(' ');
}

function mapResultsToProgress(results) {
  if (!Array.isArray(results) || results.length === 0) return [];
  const bySubject = {};
  for (const r of results) {
    const name = r.subject?.name || 'Other';
    if (!bySubject[name]) bySubject[name] = { totalScore: 0, totalMax: 0 };
    bySubject[name].totalScore += Number(r.score) || 0;
    bySubject[name].totalMax += Number(r.maxScore) || 0;
  }
  return Object.entries(bySubject).map(([subject, { totalScore, totalMax }]) => ({
    subject,
    value: totalMax > 0 ? Math.round((totalScore / totalMax) * 100) : 0,
  })).sort((a, b) => a.subject.localeCompare(b.subject));
}

function ProgressBar({ label, value }) {
  const pct = Math.min(100, Math.max(0, Number(value) || 0));
  return (
    <div className="progress-item">
      <div className="progress-header">
        <span className="progress-label">{label}</span>
        <span className="progress-value">{pct}%</span>
      </div>
      <div className="progress-track">
        <div className="progress-fill" style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

export default function ParentPage() {
  const [children, setChildren] = useState([]);
  const [selectedChildId, setSelectedChildId] = useState(null);
  const [results, setResults] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [loadingChildren, setLoadingChildren] = useState(true);
  const [loadingResults, setLoadingResults] = useState(true);
  const [loadingTeachers, setLoadingTeachers] = useState(false);
  const [errorChildren, setErrorChildren] = useState(null);
  const [errorResults, setErrorResults] = useState(null);
  const [errorTeachers, setErrorTeachers] = useState(null);
  const [activeView, setActiveView] = useState('overview');
  const [uploadingChildPhoto, setUploadingChildPhoto] = useState(false);
  const [photoMessage, setPhotoMessage] = useState(null);
  const [photoVersions, setPhotoVersions] = useState({});
  const [portalAccess, setPortalAccess] = useState(null);
  const [loadingPortalAccess, setLoadingPortalAccess] = useState(false);
  const [savingPortalAccess, setSavingPortalAccess] = useState(false);
  const [resettingPortalPassword, setResettingPortalPassword] = useState(false);
  const [portalMessage, setPortalMessage] = useState(null);
  const childPhotoInputRef = useRef(null);

  const loadChildren = useCallback(async () => {
    const res = await apiFetch('/api/parents/my-children');
    if (res.status === 401) return [];
    if (!res.ok) throw new Error('Could not load children');
    const data = await res.json();
    return Array.isArray(data) ? data : [];
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoadingChildren(true);
    setErrorChildren(null);
    loadChildren()
      .then((data) => {
        if (cancelled) return;
        setChildren(data);
        if (data.length > 0 && !selectedChildId) setSelectedChildId(data[0].studentId);
        if (data.length === 0) setSelectedChildId(null);
      })
      .catch((e) => { if (!cancelled) setErrorChildren(e.message); })
      .finally(() => { if (!cancelled) setLoadingChildren(false); });
    return () => { cancelled = true; };
  }, [loadChildren]);

  useEffect(() => {
    if (children.length > 0 && !selectedChildId) setSelectedChildId(children[0].studentId);
  }, [children, selectedChildId]);

  useEffect(() => {
    let cancelled = false;
    setLoadingResults(true);
    setErrorResults(null);
    apiFetch('/api/results/my-children')
      .then((res) => {
        if (cancelled) return null;
        if (res.status === 401) return [];
        if (!res.ok) throw new Error('Could not load results');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setResults(Array.isArray(data) ? data : []);
      })
      .catch((err) => { if (!cancelled) setErrorResults(err.message); })
      .finally(() => { if (!cancelled) setLoadingResults(false); });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!selectedChildId) {
      setTeachers([]);
      setLoadingTeachers(false);
      return;
    }
    let cancelled = false;
    setLoadingTeachers(true);
    setErrorTeachers(null);
    apiFetch(`/api/contacts/teachers?studentId=${selectedChildId}`)
      .then((res) => {
        if (cancelled) return [];
        if (res.status === 401) return [];
        if (!res.ok) throw new Error('Could not load teachers');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setTeachers(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        if (!cancelled) setErrorTeachers(err.message);
      })
      .finally(() => { if (!cancelled) setLoadingTeachers(false); });
    return () => { cancelled = true; };
  }, [selectedChildId]);

  useEffect(() => {
    if (!selectedChildId) {
      setPortalAccess(null);
      setLoadingPortalAccess(false);
      return;
    }

    let cancelled = false;
    setLoadingPortalAccess(true);

    apiFetch(`/api/parents/student-portal-access/${selectedChildId}`)
      .then((res) => {
        if (cancelled) return null;
        if (res.status === 401) return null;
        if (!res.ok) throw new Error('Could not load student access details');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setPortalAccess(data || null);
      })
      .catch((err) => {
        if (!cancelled) setPortalMessage(err.message || 'Could not load student access details.');
      })
      .finally(() => {
        if (!cancelled) setLoadingPortalAccess(false);
      });

    return () => { cancelled = true; };
  }, [selectedChildId]);

  const selectedChild = children.find((c) => c.studentId === selectedChildId);
  const resultsForChild = selectedChildId ? results.filter((r) => r.studentId === selectedChildId) : [];

  const handleChildPhotoChange = async (e) => {
    const file = e.target?.files?.[0];
    if (!file || !selectedChild) return;
    setUploadingChildPhoto(true);
    setPhotoMessage(null);
    const form = new FormData();
    form.append('file', file);
    try {
      const res = await apiFetch(`/api/students/${selectedChild.studentId}/photo`, {
        method: 'POST',
        body: form,
      });
      const payload = await res.json().catch(() => null);
      if (!res.ok) {
        throw new Error(payload?.message || 'Could not update child photo.');
      }
      setPhotoVersions((prev) => ({ ...prev, [selectedChild.studentId]: Date.now() }));
      setPhotoMessage('Child photo updated successfully.');
    } catch (err) {
      setPhotoMessage(err.message || 'Could not update child photo.');
    } finally {
      setUploadingChildPhoto(false);
      if (e.target) e.target.value = '';
    }
  };

  const updatePortalVisibility = (key, value) => {
    setPortalAccess((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        visibility: {
          ...(prev.visibility || {}),
          [key]: value,
        },
      };
    });
  };

  const copyPortalValue = async (value, label) => {
    if (!value) return;
    try {
      await navigator.clipboard.writeText(value);
      setPortalMessage(`${label} copied.`);
    } catch {
      setPortalMessage(`Could not copy ${label.toLowerCase()}.`);
    }
  };

  const handleSavePortalAccess = async () => {
    if (!selectedChild || !portalAccess) return;
    setSavingPortalAccess(true);
    setPortalMessage(null);
    try {
      const res = await apiFetch(`/api/parents/student-portal-access/${selectedChild.studentId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          isEnabled: Boolean(portalAccess.isEnabled),
          showDateOfBirth: Boolean(portalAccess.visibility?.showDateOfBirth),
          showLocationDetails: Boolean(portalAccess.visibility?.showLocationDetails),
          showHealthDetails: Boolean(portalAccess.visibility?.showHealthDetails),
          showEmergencyContacts: Boolean(portalAccess.visibility?.showEmergencyContacts),
          showParentContactDetails: Boolean(portalAccess.visibility?.showParentContactDetails),
          showPreviousSchoolDetails: Boolean(portalAccess.visibility?.showPreviousSchoolDetails),
        }),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        throw new Error(data?.message || 'Could not save student access settings.');
      }
      setPortalAccess(data);
      setPortalMessage('Student access settings saved.');
    } catch (err) {
      setPortalMessage(err.message || 'Could not save student access settings.');
    } finally {
      setSavingPortalAccess(false);
    }
  };

  const handleResetPortalPassword = async () => {
    if (!selectedChild) return;
    setResettingPortalPassword(true);
    setPortalMessage(null);
    try {
      const res = await apiFetch(`/api/parents/student-portal-access/${selectedChild.studentId}/reset-password`, {
        method: 'POST',
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        throw new Error(data?.message || 'Could not generate a new temporary password.');
      }
      setPortalAccess(data);
      setPortalMessage('A new temporary password is ready to share.');
    } catch (err) {
      setPortalMessage(err.message || 'Could not generate a new temporary password.');
    } finally {
      setResettingPortalPassword(false);
    }
  };

  const progress = mapResultsToProgress(resultsForChild);
  const overallPct = progress.length
    ? Math.round(progress.reduce((s, p) => s + p.value, 0) / progress.length)
    : selectedChild?.termAverage ?? null;

  return (
    <PageLayout title="Family View — My children" role="parent">
      <div className="school-admin-shell">
        <aside className="school-admin-nav" aria-label="Family sections">
          <button type="button" className={`school-admin-nav-btn ${activeView === 'overview' ? 'is-active' : ''}`} onClick={() => setActiveView('overview')}>
            Overview
          </button>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'progress' ? 'is-active' : ''}`} onClick={() => setActiveView('progress')} disabled={!selectedChild}>
            Academic progress
          </button>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'teachers' ? 'is-active' : ''}`} onClick={() => setActiveView('teachers')} disabled={!selectedChild}>
            Teachers
          </button>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'access' ? 'is-active' : ''}`} onClick={() => setActiveView('access')} disabled={!selectedChild}>
            Student access
          </button>
        </aside>

        <section className="school-admin-view">
          {activeView === 'overview' && children.length > 0 && (
            <section aria-label="Family snapshot">
              <div className="dashboard-grid">
                <article className="dashboard-card dashboard-card--highlight">
                  <p className="dashboard-label">Children linked</p>
                  <p className="dashboard-value">{children.length}</p>
                  <p className="dashboard-sub">Students tied to your parent account.</p>
                </article>
                <article className="dashboard-card">
                  <p className="dashboard-label">Term average (selected)</p>
                  <p className="dashboard-value">
                    {selectedChild?.termAverage != null ? `${selectedChild.termAverage}%` : (overallPct != null ? `${overallPct}%` : '—')}
                  </p>
                  <p className="dashboard-sub">From school records or calculated from results.</p>
                </article>
                <article className="dashboard-card">
                  <p className="dashboard-label">Subjects tracked</p>
                  <p className="dashboard-value">{progress.length || '—'}</p>
                  <p className="dashboard-sub">With published results this term.</p>
                </article>
              </div>
            </section>
          )}

          {/* Top bar: My Children + child switcher + Add another child */}
          <div className="family-view-top">
            <h2 className="section-title">My Children</h2>
            <div className="family-view-actions">
              {children.length > 0 && (
                <div className="child-switcher" role="tablist" aria-label="Select child">
                  {children.map((child) => (
                    <button
                      key={child.studentId}
                      type="button"
                      role="tab"
                      aria-selected={selectedChildId === child.studentId}
                      aria-label={displayName(child)}
                      title={displayName(child)}
                      className={`child-avatar ${selectedChildId === child.studentId ? 'child-avatar--selected' : ''}`}
                      onClick={() => setSelectedChildId(child.studentId)}
                    >
                      <StudentPhoto
                        studentId={child.studentId}
                        firstName={child.firstName}
                        lastName={child.lastName}
                        size={48}
                        cacheBust={photoVersions[child.studentId]}
                      />
                    </button>
                  ))}
                </div>
              )}
              <Link to="/parent/claim" className="btn-add-child">
                Add another child
              </Link>
            </div>
          </div>

          {loadingChildren && <p className="empty-state" aria-busy="true">Loading…</p>}
          {errorChildren && <p className="empty-state empty-state--error">{errorChildren}</p>}

          {!loadingChildren && children.length === 0 && (
            <div className="family-view-empty">
              <p className="card-desc">You haven’t linked any children yet. Use the Parent Access Code from your school to claim your child.</p>
              <Link to="/parent/claim" className="btn-claim-child-cta">Claim your child</Link>
            </div>
          )}

          {!loadingChildren && selectedChild && activeView === 'overview' && (
            <section className="family-view-card family-view-profile" aria-label="Student profile">
              <div className="family-view-profile-header">
                <StudentPhoto
                  studentId={selectedChild.studentId}
                  firstName={selectedChild.firstName}
                  lastName={selectedChild.lastName}
                  size={56}
                  cacheBust={photoVersions[selectedChild.studentId]}
                />
                <h3 className="card-title" style={{ marginBottom: 0 }}>{displayName(selectedChild)}</h3>
              </div>
              <input
                type="file"
                accept=".jpg,.jpeg,.png,.gif,.webp"
                ref={childPhotoInputRef}
                onChange={handleChildPhotoChange}
                style={{ display: 'none' }}
                aria-label="Upload child photo"
              />
              <div className="form-actions" style={{ marginBottom: '0.75rem' }}>
                <button
                  type="button"
                  className="btn-upload-photo"
                  onClick={() => childPhotoInputRef.current?.click()}
                  disabled={uploadingChildPhoto}
                >
                  {uploadingChildPhoto ? 'Uploading…' : 'Upload / change child photo'}
                </button>
                <span className="card-desc">Parents and the school can update this photo anytime.</span>
              </div>
              {photoMessage && <p className="card-desc">{photoMessage}</p>}
              <dl className="profile-dl">
                <dt>Class</dt>
                <dd>{selectedChild.className || '—'}</dd>
                <dt>Attendance</dt>
                <dd>—</dd>
                <dt>Current term average</dt>
                <dd>{selectedChild.termAverage != null ? `${selectedChild.termAverage}%` : '—'}</dd>
              </dl>
            </section>
          )}

          {!loadingChildren && selectedChild && activeView === 'overview' && (
            <StudentRecordPanel studentId={selectedChild.studentId} role="parent" />
          )}

          {!loadingChildren && selectedChild && activeView === 'teachers' && (
            <section className="family-view-card family-view-teachers" aria-label="Assigned teachers">
              <h3 className="card-title">Assigned Teachers</h3>
              {loadingTeachers && <p className="empty-state" aria-busy="true">Loading teachers…</p>}
              {errorTeachers && <p className="empty-state empty-state--error">{errorTeachers}</p>}
              {!loadingTeachers && !errorTeachers && teachers.length === 0 && (
                <p className="empty-state">No teachers assigned for this class yet.</p>
              )}
              {!loadingTeachers && teachers.length > 0 && (
                <ul className="teacher-cards">
                  {teachers.map((t) => {
                    const wa = whatsAppUrl(t.whatsAppNumber, t.phone);
                    const tel = (t.phone || '').replace(/\D/g, '').length >= 10 ? `tel:${t.phone}` : null;
                    const mail = t.email ? `mailto:${t.email}` : null;
                    return (
                      <li key={`${t.teacherId}-${t.subject || ''}`} className="teacher-card">
                        <div className="teacher-card-header">
                          <TeacherPhoto teacherId={t.teacherId} fullName={t.fullName} size={40} />
                          <div>
                            <span className="teacher-card-name">{t.fullName}</span>
                            {t.subject && <span className="teacher-card-subject">{t.subject}</span>}
                          </div>
                        </div>
                        <div className="teacher-card-actions">
                          {tel && (
                            <a href={tel} className="btn-teacher-action btn-call" aria-label={`Call ${t.fullName}`}>Call</a>
                          )}
                          {wa ? (
                            <a href={wa} target="_blank" rel="noopener noreferrer" className="btn-teacher-action btn-whatsapp" aria-label={`WhatsApp ${t.fullName}`}>WhatsApp</a>
                          ) : (
                            <span className="teacher-no-wa">No WhatsApp</span>
                          )}
                          {mail && (
                            <a href={mail} className="btn-teacher-action btn-email" aria-label={`Email ${t.fullName}`}>Email</a>
                          )}
                        </div>
                      </li>
                    );
                  })}
                </ul>
              )}
            </section>
          )}

          {!loadingChildren && selectedChild && activeView === 'access' && (
            <section className="family-view-card student-portal-card" aria-label="Student dashboard access">
              <div className="student-portal-card__header">
                <div>
                  <h3 className="card-title">Student dashboard access</h3>
                  <p className="card-desc">
                    Only parents can share these sign-in details with {selectedChild.firstName}. The student dashboard stays view-only for the child.
                  </p>
                </div>
                <span className={`student-portal-status ${portalAccess?.isEnabled ? 'is-active' : 'is-paused'}`}>
                  {portalAccess?.isEnabled ? 'Access on' : 'Access paused'}
                </span>
              </div>

              {loadingPortalAccess && <p className="empty-state" aria-busy="true">Loading student access…</p>}
              {portalMessage && <p className="card-desc">{portalMessage}</p>}

              {!loadingPortalAccess && portalAccess && (
                <>
                  <div className="student-portal-share-grid">
                    <article className="student-portal-share-item">
                      <span className="dashboard-label">Student login ID</span>
                      <code>{portalAccess.loginId}</code>
                      <button type="button" className="btn-primary-action" onClick={() => copyPortalValue(portalAccess.loginId, 'Login ID')}>
                        Copy login ID
                      </button>
                    </article>

                    <article className="student-portal-share-item">
                      <span className="dashboard-label">Sign-in link</span>
                      <code>{`${window.location.origin}${portalAccess.loginPath || '/login'}`}</code>
                      <button type="button" className="btn-primary-action" onClick={() => copyPortalValue(`${window.location.origin}${portalAccess.loginPath || '/login'}`, 'Sign-in link')}>
                        Copy link
                      </button>
                    </article>

                    <article className="student-portal-share-item student-portal-share-item--wide">
                      <span className="dashboard-label">Temporary password</span>
                      <code>{portalAccess.temporaryPassword || 'Hidden until first creation or password reset'}</code>
                      <p className="card-desc" style={{ marginBottom: '0.75rem' }}>
                        For security, the password only shows when it is first created or when you generate a fresh temporary one.
                      </p>
                      {portalAccess.temporaryPassword && (
                        <button type="button" className="btn-primary-action" onClick={() => copyPortalValue(portalAccess.temporaryPassword, 'Temporary password')}>
                          Copy temporary password
                        </button>
                      )}
                    </article>
                  </div>

                  <div className="student-visibility-grid" style={{ marginTop: '1rem' }}>
                    <label className="student-visibility-item">
                      <input
                        type="checkbox"
                        checked={Boolean(portalAccess.isEnabled)}
                        onChange={(e) => setPortalAccess((prev) => ({ ...prev, isEnabled: e.target.checked }))}
                        disabled={savingPortalAccess}
                      />
                      <span>Allow this child to sign in to the student dashboard</span>
                    </label>
                    {[
                      ['showDateOfBirth', 'Show date of birth'],
                      ['showLocationDetails', 'Show nationality, state and LGA'],
                      ['showHealthDetails', 'Show health details'],
                      ['showEmergencyContacts', 'Show emergency contact details'],
                      ['showParentContactDetails', 'Show parent contact details'],
                      ['showPreviousSchoolDetails', 'Show previous school history'],
                    ].map(([key, label]) => (
                      <label key={key} className="student-visibility-item">
                        <input
                          type="checkbox"
                          checked={Boolean(portalAccess.visibility?.[key])}
                          onChange={(e) => updatePortalVisibility(key, e.target.checked)}
                          disabled={savingPortalAccess}
                        />
                        <span>{label}</span>
                      </label>
                    ))}
                  </div>

                  <div className="form-actions" style={{ marginTop: '0.85rem' }}>
                    <button type="button" className="btn-primary-action" onClick={handleSavePortalAccess} disabled={savingPortalAccess}>
                      {savingPortalAccess ? 'Saving…' : 'Save student access settings'}
                    </button>
                    <button type="button" className="btn-primary-action" onClick={handleResetPortalPassword} disabled={resettingPortalPassword}>
                      {resettingPortalPassword ? 'Generating…' : 'Generate new temporary password'}
                    </button>
                  </div>
                </>
              )}
            </section>
          )}

          {!loadingChildren && selectedChild && activeView === 'progress' && (
            <section className="family-view-results" aria-label="Performance snapshot">
              <h3 className="card-title">Performance snapshot</h3>
              {loadingResults && <p className="empty-state" aria-busy="true">Loading results…</p>}
              {errorResults && <p className="empty-state empty-state--error">{errorResults}</p>}
              {!loadingResults && !errorResults && progress.length === 0 && (
                <p className="empty-state">No results yet for this term.</p>
              )}
              {!loadingResults && progress.length > 0 && (
                <>
                  {overallPct != null && (
                    <div className="progress-item progress-overall">
                      <div className="progress-header">
                        <span className="progress-label">Overall</span>
                        <span className="progress-value">{overallPct}%</span>
                      </div>
                      <div className="progress-track">
                        <div className="progress-fill progress-overall-fill" style={{ width: `${overallPct}%` }} />
                      </div>
                    </div>
                  )}
                  <ul className="progress-list">
                    {progress.map(({ subject, value }) => (
                      <li key={subject}>
                        <ProgressBar label={subject} value={value} />
                      </li>
                    ))}
                  </ul>
                </>
              )}
              <p className="card-desc" style={{ marginTop: '1rem' }}>
                <button type="button" className="btn-download-pdf" disabled>Download PDF report (coming soon)</button>
              </p>
            </section>
          )}
        </section>
      </div>
    </PageLayout>
  );
}
