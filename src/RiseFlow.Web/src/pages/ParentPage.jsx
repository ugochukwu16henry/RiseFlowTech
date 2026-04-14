import { useState, useEffect, useCallback } from 'react';
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
  const [assignments, setAssignments] = useState([]);
  const [notices, setNotices] = useState([]);
  const [events, setEvents] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [portalAccesses, setPortalAccesses] = useState([]);
  const [portalForm, setPortalForm] = useState(null);
  const [passwordResetResult, setPasswordResetResult] = useState(null);
  const [portalNotice, setPortalNotice] = useState(null);
  const [loadingChildren, setLoadingChildren] = useState(true);
  const [loadingResults, setLoadingResults] = useState(true);
  const [loadingTeachers, setLoadingTeachers] = useState(false);
  const [loadingPortalAccess, setLoadingPortalAccess] = useState(false);
  const [portalAccessLoaded, setPortalAccessLoaded] = useState(false);
  const [savingPortalAccess, setSavingPortalAccess] = useState(false);
  const [resettingPortalPassword, setResettingPortalPassword] = useState(false);
  const [errorChildren, setErrorChildren] = useState(null);
  const [errorResults, setErrorResults] = useState(null);
  const [errorTeachers, setErrorTeachers] = useState(null);
  const [errorPortalAccess, setErrorPortalAccess] = useState(null);
  const [activeView, setActiveView] = useState('overview');
  const [showStudentDetails, setShowStudentDetails] = useState(false);
  const [uploadingChildPhoto, setUploadingChildPhoto] = useState(false);
  const [childPhotoUploadError, setChildPhotoUploadError] = useState(null);
  const [childPhotoCacheKey, setChildPhotoCacheKey] = useState('');

  const loadChildren = useCallback(async () => {
    const res = await apiFetch('/api/parents/my-children');
    if (res.status === 401) return [];
    if (!res.ok) throw new Error('Could not load children');
    const data = await res.json();
    return Array.isArray(data) ? data : [];
  }, []);

  const loadPortalAccess = useCallback(async () => {
    const res = await apiFetch('/api/parents/student-portal-access');
    if (res.status === 401 || res.status === 403) return [];
    if (!res.ok) throw new Error('Could not load student access details');
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
  }, [loadChildren, selectedChildId]);

  useEffect(() => {
    if (activeView !== 'access' || children.length === 0 || portalAccessLoaded) {
      return undefined;
    }

    let cancelled = false;
    setLoadingPortalAccess(true);
    setErrorPortalAccess(null);
    loadPortalAccess()
      .then((data) => {
        if (!cancelled) {
          setPortalAccesses(data);
          setPortalAccessLoaded(true);
        }
      })
      .catch((e) => {
        if (!cancelled) {
          const message = /blocked or unreachable|failed to fetch|networkerror/i.test(String(e?.message || ''))
            ? 'Student access controls are syncing with the live API. Please retry shortly.'
            : (e.message || 'Could not load student access details');
          setErrorPortalAccess(message);
        }
      })
      .finally(() => {
        if (!cancelled) setLoadingPortalAccess(false);
      });
    return () => { cancelled = true; };
  }, [activeView, children.length, loadPortalAccess, portalAccessLoaded]);

  useEffect(() => {
    if (children.length > 0 && !selectedChildId) setSelectedChildId(children[0].studentId);
  }, [children, selectedChildId]);

  useEffect(() => {
    setShowStudentDetails(false);
  }, [selectedChildId]);

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
      setAssignments([]);
      return undefined;
    }
    let cancelled = false;
    apiFetch(`/api/assignments?studentId=${selectedChildId}`)
      .then(async (res) => {
        if (cancelled) return [];
        if (res.status === 401 || res.status === 403) return [];
        if (!res.ok) throw new Error('Could not load assignments');
        const data = await res.json();
        return Array.isArray(data) ? data : [];
      })
      .then((data) => {
        if (!cancelled) setAssignments(data);
      })
      .catch(() => {
        if (!cancelled) setAssignments([]);
      });
    return () => { cancelled = true; };
  }, [selectedChildId]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      apiFetch('/api/notices?limit=8').then((r) => (r.ok ? r.json() : [])),
      apiFetch('/api/events?limit=8').then((r) => (r.ok ? r.json() : [])),
    ])
      .then(([noticeList, eventList]) => {
        if (cancelled) return;
        setNotices(Array.isArray(noticeList) ? noticeList : []);
        setEvents(Array.isArray(eventList) ? eventList : []);
      })
      .catch(() => {
        if (cancelled) return;
        setNotices([]);
        setEvents([]);
      });

    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!selectedChildId) {
      setTeachers([]);
      setLoadingTeachers(false);
      return undefined;
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

  const selectedChild = children.find((c) => c.studentId === selectedChildId);
  const selectedPortalAccess = portalAccesses.find((item) => item.studentId === selectedChildId) || null;
  const selectedChildUploadInputId = selectedChild ? `parent-child-photo-${selectedChild.studentId}` : 'parent-child-photo';

  useEffect(() => {
    if (!selectedPortalAccess) {
      setPortalForm(null);
      return;
    }
    setPortalForm({
      isEnabled: Boolean(selectedPortalAccess.isEnabled),
      showDateOfBirth: Boolean(selectedPortalAccess.showDateOfBirth),
      showLocationDetails: Boolean(selectedPortalAccess.showLocationDetails),
      showHealthDetails: Boolean(selectedPortalAccess.showHealthDetails),
      showEmergencyContacts: Boolean(selectedPortalAccess.showEmergencyContacts),
      showParentContactDetails: Boolean(selectedPortalAccess.showParentContactDetails),
      showPreviousSchoolDetails: Boolean(selectedPortalAccess.showPreviousSchoolDetails),
    });
  }, [selectedPortalAccess]);

  const resultsForChild = selectedChildId ? results.filter((r) => r.studentId === selectedChildId) : [];
  const assignmentsForChild = selectedChild?.classId ? assignments.filter((a) => a.classId === selectedChild.classId) : assignments;
  const progress = mapResultsToProgress(resultsForChild);
  const overallPct = progress.length
    ? Math.round(progress.reduce((s, p) => s + p.value, 0) / progress.length)
    : selectedChild?.termAverage ?? null;
  const studentSignInUrl = typeof window !== 'undefined' ? `${window.location.origin}/login` : '/login';
  const visiblePassword = passwordResetResult?.studentId === selectedChildId
    ? passwordResetResult.temporaryPassword
    : (selectedPortalAccess?.temporaryPassword || null);

  const handlePortalToggle = (key, value) => {
    setPortalForm((current) => (current ? { ...current, [key]: value } : current));
  };

  const copyText = async (text, label) => {
    if (!text || !navigator?.clipboard) return;
    try {
      await navigator.clipboard.writeText(text);
      setPortalNotice(`${label} copied.`);
    } catch {
      setPortalNotice(`Could not copy ${label.toLowerCase()} automatically.`);
    }
  };

  const handleSavePortalSettings = async () => {
    if (!selectedChildId || !portalForm) return;
    setSavingPortalAccess(true);
    setErrorPortalAccess(null);
    setPortalNotice(null);
    try {
      const res = await apiFetch(`/api/parents/student-portal-access/${selectedChildId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(portalForm),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) throw new Error(data?.message || 'Could not save student access settings.');
      setPortalAccesses((current) => current.map((item) => (item.studentId === selectedChildId ? { ...item, ...data } : item)));
      setPortalNotice('Student visibility settings saved.');
    } catch (err) {
      const message = /blocked or unreachable|failed to fetch|networkerror/i.test(String(err?.message || ''))
        ? 'Student access controls are syncing with the live API. Please retry shortly.'
        : (err.message || 'Could not save student access settings.');
      setErrorPortalAccess(message);
    } finally {
      setSavingPortalAccess(false);
    }
  };

  const handleResetPortalPassword = async () => {
    if (!selectedChildId) return;
    setResettingPortalPassword(true);
    setErrorPortalAccess(null);
    setPortalNotice(null);
    try {
      const res = await apiFetch(`/api/parents/student-portal-access/${selectedChildId}/reset-password`, {
        method: 'POST',
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) throw new Error(data?.message || 'Could not reset the student password.');
      setPasswordResetResult({ ...data, studentId: selectedChildId });
      setPortalAccesses((current) => current.map((item) => (
        item.studentId === selectedChildId
          ? { ...item, loginId: data.loginId, isEnabled: true }
          : item
      )));
      setPortalNotice(data?.message || 'Student sign-in password reset.');
    } catch (err) {
      const message = /blocked or unreachable|failed to fetch|networkerror/i.test(String(err?.message || ''))
        ? 'Student access controls are syncing with the live API. Please retry shortly.'
        : (err.message || 'Could not reset the student password.');
      setErrorPortalAccess(message);
    } finally {
      setResettingPortalPassword(false);
    }
  };

  const handleChildPhotoUpload = async (studentId, event) => {
    const file = event?.target?.files?.[0];
    if (!studentId || !file || uploadingChildPhoto) {
      if (event?.target) event.target.value = '';
      return;
    }

    setUploadingChildPhoto(true);
    setChildPhotoUploadError(null);

    const formData = new FormData();
    formData.append('file', file);

    try {
      const res = await apiFetch(`/api/students/${studentId}/photo`, { method: 'POST', body: formData });
      const text = await res.text().catch(() => '');

      if (!res.ok) {
        let message = text || 'Could not upload child photo.';
        try {
          const parsed = text ? JSON.parse(text) : null;
          message = parsed?.message || parsed?.title || parsed?.error || message;
        } catch {
          // Keep text fallback when response is not JSON.
        }
        throw new Error(message);
      }

      setChildPhotoCacheKey(String(Date.now()));
      const refreshedChildren = await loadChildren();
      setChildren(refreshedChildren);
    } catch (error) {
      setChildPhotoUploadError(error?.message || 'Could not upload child photo.');
    } finally {
      setUploadingChildPhoto(false);
      if (event?.target) event.target.value = '';
    }
  };

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
                <article className="dashboard-card">
                  <p className="dashboard-label">Student portal</p>
                  <p className="dashboard-value">{selectedPortalAccess?.isEnabled ? 'On' : 'Off'}</p>
                  <p className="dashboard-sub">Only parents can share this login with the child.</p>
                </article>
                <article className="dashboard-card">
                  <p className="dashboard-label">Latest notice</p>
                  <p className="dashboard-value" style={{ fontSize: '1rem' }}>{notices[0]?.title || '—'}</p>
                  <p className="dashboard-sub">School announcements for families.</p>
                </article>
                <article className="dashboard-card">
                  <p className="dashboard-label">Next event</p>
                  <p className="dashboard-value" style={{ fontSize: '1rem' }}>{events[0]?.title || '—'}</p>
                  <p className="dashboard-sub">{events[0]?.startAtUtc ? new Date(events[0].startAtUtc).toLocaleDateString() : 'No upcoming date yet.'}</p>
                </article>
              </div>
            </section>
          )}

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
                      <StudentPhoto studentId={child.studentId} firstName={child.firstName} lastName={child.lastName} size={48} cacheKey={childPhotoCacheKey} />
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
                <StudentPhoto studentId={selectedChild.studentId} firstName={selectedChild.firstName} lastName={selectedChild.lastName} size={56} cacheKey={childPhotoCacheKey} />
                <h3 className="card-title" style={{ marginBottom: 0 }}>{displayName(selectedChild)}</h3>
              </div>
              <dl className="profile-dl">
                <dt>Class</dt>
                <dd>{selectedChild.className || '—'}</dd>
                <dt>Attendance</dt>
                <dd>—</dd>
                <dt>Current term average</dt>
                <dd>{selectedChild.termAverage != null ? `${selectedChild.termAverage}%` : '—'}</dd>
                <dt>Student sign-in</dt>
                <dd>{selectedPortalAccess?.loginId || 'Generated once claimed'}</dd>
              </dl>
              <div className="dashboard-actions" style={{ marginTop: '0.75rem', flexWrap: 'wrap' }}>
                <label htmlFor={selectedChildUploadInputId} className="btn-primary-action btn-primary-action--ghost" style={{ cursor: uploadingChildPhoto ? 'not-allowed' : 'pointer', opacity: uploadingChildPhoto ? 0.7 : 1 }}>
                  {uploadingChildPhoto ? 'Uploading photo…' : 'Upload child photo'}
                </label>
                <input
                  id={selectedChildUploadInputId}
                  type="file"
                  accept=".jpg,.jpeg,.png,.gif,.webp,image/*"
                  onChange={(e) => handleChildPhotoUpload(selectedChild.studentId, e)}
                  disabled={uploadingChildPhoto}
                  style={{ display: 'none' }}
                />
                <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => setActiveView('access')}>
                  Open student access controls
                </button>
                <button type="button" className="btn-primary-action" onClick={() => setShowStudentDetails((current) => !current)}>
                  {showStudentDetails ? 'Hide details' : 'View details'}
                </button>
              </div>

              {childPhotoUploadError && <p className="empty-state empty-state--error" style={{ marginTop: '0.75rem' }}>{childPhotoUploadError}</p>}

              {showStudentDetails && (
                <div style={{ marginTop: '1rem' }}>
                  <StudentRecordPanel
                    studentId={selectedChild.studentId}
                    role="parent"
                    onClose={() => setShowStudentDetails(false)}
                  />
                </div>
              )}
            </section>
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

              <div className="student-record-card" style={{ marginTop: '1rem' }}>
                <h4 className="dashboard-section-title">Assignments</h4>
                {assignmentsForChild.length === 0 ? (
                  <p className="card-desc">No assignments published for this child’s class.</p>
                ) : (
                  <ul className="student-record-list">
                    {assignmentsForChild.map((a) => (
                      <li key={a.id}>
                        <strong>{a.title}</strong>
                        <span>
                          {a.subjectName} • {a.termName}
                          {a.dueDateUtc ? ` • Due ${new Date(a.dueDateUtc).toLocaleDateString()}` : ''}
                          {' • '}
                          <a href={`/api/files/${a.fileAssetId}/download`}>{a.originalFileName}</a>
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </section>
          )}

          {!loadingChildren && selectedChild && activeView === 'access' && (
            <section className="family-view-results" aria-label="Student access controls">
              <h3 className="card-title">Student access & privacy</h3>
              <p className="card-desc">Only parents can share this sign-in with the child. Use these settings to decide what sensitive details appear on the student dashboard.</p>

              {loadingPortalAccess && <p className="empty-state" aria-busy="true">Loading student access…</p>}
              {errorPortalAccess && (
                <div>
                  <p className="empty-state empty-state--error">{errorPortalAccess}</p>
                  <button
                    type="button"
                    className="btn-primary-action btn-primary-action--ghost"
                    onClick={() => {
                      setPortalAccessLoaded(false);
                      setErrorPortalAccess(null);
                    }}
                  >
                    Retry student access sync
                  </button>
                </div>
              )}
              {portalNotice && <p className="card-desc" style={{ color: 'var(--color-primary)' }}>{portalNotice}</p>}

              {visiblePassword && (
                <div className="claim-success" role="status">
                  <p className="claim-success-title">Temporary student password</p>
                  <p className="claim-success-msg"><strong>{visiblePassword}</strong></p>
                  <p className="card-desc">Share this only with {displayName(selectedChild)}. You can reset it again at any time.</p>
                </div>
              )}

              {selectedPortalAccess && portalForm && (
                <>
                  <div className="dashboard-grid" style={{ marginTop: '0.75rem' }}>
                    <article className="dashboard-card">
                      <p className="dashboard-label">Portal status</p>
                      <p className="dashboard-value">{portalForm.isEnabled ? 'Enabled' : 'Paused'}</p>
                      <p className="dashboard-sub">Pause access without deleting the child account.</p>
                    </article>
                    <article className="dashboard-card">
                      <p className="dashboard-label">Student login ID</p>
                      <p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedPortalAccess.loginId}</p>
                      <p className="dashboard-sub">Use this in the email field on the sign-in page.</p>
                    </article>
                    <article className="dashboard-card">
                      <p className="dashboard-label">Student sign-in link</p>
                      <p className="dashboard-value" style={{ fontSize: '1rem' }}>RiseFlow login</p>
                      <p className="dashboard-sub">{studentSignInUrl}</p>
                    </article>
                  </div>

                  <div className="dashboard-actions" style={{ marginTop: '0.75rem', flexWrap: 'wrap' }}>
                    <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => copyText(studentSignInUrl, 'Sign-in link')}>
                      Copy sign-in link
                    </button>
                    <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => copyText(selectedPortalAccess.loginId, 'Login ID')}>
                      Copy login ID
                    </button>
                    <button type="button" className="btn-primary-action" onClick={handleResetPortalPassword} disabled={resettingPortalPassword}>
                      {resettingPortalPassword ? 'Resetting…' : 'Reset student password'}
                    </button>
                  </div>

                  <div style={{ display: 'grid', gap: '0.75rem', marginTop: '1rem' }}>
                    {[
                      ['isEnabled', 'Allow this child to sign in to the student dashboard'],
                      ['showDateOfBirth', 'Show date of birth'],
                      ['showLocationDetails', 'Show nationality, state, and LGA'],
                      ['showHealthDetails', 'Show blood group, genotype, and allergies'],
                      ['showEmergencyContacts', 'Show emergency contact name and phone'],
                      ['showParentContactDetails', 'Show parent/guardian contact details'],
                      ['showPreviousSchoolDetails', 'Show previous school history'],
                    ].map(([key, label]) => (
                      <label key={key} style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                        <input
                          type="checkbox"
                          checked={Boolean(portalForm[key])}
                          onChange={(e) => handlePortalToggle(key, e.target.checked)}
                        />
                        <span>{label}</span>
                      </label>
                    ))}
                  </div>

                  <div className="dashboard-actions" style={{ marginTop: '1rem' }}>
                    <button type="button" className="btn-primary-action" onClick={handleSavePortalSettings} disabled={savingPortalAccess}>
                      {savingPortalAccess ? 'Saving…' : 'Save student privacy settings'}
                    </button>
                  </div>
                </>
              )}
            </section>
          )}
        </section>
      </div>
    </PageLayout>
  );
}
