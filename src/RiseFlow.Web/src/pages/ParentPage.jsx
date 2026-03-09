import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
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

function initials(child) {
  const first = (child.firstName || '').charAt(0).toUpperCase();
  const last = (child.lastName || '').charAt(0).toUpperCase();
  return last ? `${first}${last}` : first || '?';
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

  const selectedChild = children.find((c) => c.studentId === selectedChildId);
  const resultsForChild = selectedChildId ? results.filter((r) => r.studentId === selectedChildId) : [];
  const progress = mapResultsToProgress(resultsForChild);
  const overallPct = progress.length
    ? Math.round(progress.reduce((s, p) => s + p.value, 0) / progress.length)
    : selectedChild?.termAverage ?? null;

  return (
    <PageLayout title="Family View — My children">
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
                  <StudentPhoto studentId={child.studentId} firstName={child.firstName} lastName={child.lastName} size={48} />
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

      {!loadingChildren && selectedChild && (
        <>
          <div className="family-view-grid">
            {/* Left: Student profile */}
            <section className="family-view-card family-view-profile" aria-label="Student profile">
              <div className="family-view-profile-header">
                <StudentPhoto studentId={selectedChild.studentId} firstName={selectedChild.firstName} lastName={selectedChild.lastName} size={56} />
                <h3 className="card-title" style={{ marginBottom: 0 }}>{displayName(selectedChild)}</h3>
              </div>
              <dl className="profile-dl">
                <dt>Class</dt>
                <dd>{selectedChild.className || '—'}</dd>
                <dt>Attendance</dt>
                <dd>—</dd>
                <dt>Current term average</dt>
                <dd>{selectedChild.termAverage != null ? `${selectedChild.termAverage}%` : '—'}</dd>
              </dl>
            </section>

            {/* Right: Teacher directory */}
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
                          <span className="teacher-card-name">{t.fullName}</span>
                          {t.subject && <span className="teacher-card-subject">{t.subject}</span>}
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
          </div>

          {/* Bottom: Performance snapshot + Download PDF */}
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
        </>
      )}
    </PageLayout>
  );
}
