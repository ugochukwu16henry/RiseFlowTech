import { useState, useEffect } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

function whatsAppUrl(whatsAppNumber, phone) {
  const raw = (whatsAppNumber || phone || '').replace(/\D/g, '');
  if (!raw) return null;
  return `https://wa.me/${raw}`;
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
  const [results, setResults] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [loadingResults, setLoadingResults] = useState(true);
  const [loadingTeachers, setLoadingTeachers] = useState(true);
  const [errorResults, setErrorResults] = useState(null);
  const [errorTeachers, setErrorTeachers] = useState(null);

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
      .catch((err) => {
        if (!cancelled) setErrorResults(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoadingResults(false);
      });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoadingTeachers(true);
    setErrorTeachers(null);
    apiFetch('/api/contacts/teachers')
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
      .finally(() => {
        if (!cancelled) setLoadingTeachers(false);
      });
    return () => { cancelled = true; };
  }, []);

  const progress = mapResultsToProgress(results);
  const overallPct = progress.length
    ? Math.round(progress.reduce((s, p) => s + p.value, 0) / progress.length)
    : 0;

  return (
    <PageLayout title="Parent — My children">
      <h2 className="section-title">Recent results (from database)</h2>
      <section className="progress-section">
        {loadingResults && <p className="empty-state" aria-busy="true">Loading results…</p>}
        {errorResults && <p className="empty-state empty-state--error">{errorResults}</p>}
        {!loadingResults && !errorResults && results.length === 0 && (
          <p className="empty-state">Sign in as a parent to see your child’s results, or no results yet.</p>
        )}
        {!loadingResults && !errorResults && progress.length > 0 && (
          <>
            <div className="progress-item progress-overall">
              <div className="progress-header">
                <span className="progress-label">Overall</span>
                <span className="progress-value">{overallPct}%</span>
              </div>
              <div className="progress-track">
                <div className="progress-fill progress-overall-fill" style={{ width: `${overallPct}%` }} />
              </div>
            </div>
            <ul className="progress-list">
              {progress.map(({ subject, value }) => (
                <li key={subject}>
                  <ProgressBar label={subject} value={value} />
                </li>
              ))}
            </ul>
          </>
        )}
      </section>

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Teacher contacts (from database)</h2>
      <section className="progress-section">
        {loadingTeachers && <p className="empty-state" aria-busy="true">Loading teachers…</p>}
        {errorTeachers && <p className="empty-state empty-state--error">{errorTeachers}</p>}
        {!loadingTeachers && !errorTeachers && teachers.length === 0 && (
          <p className="empty-state">Sign in as a parent to see your children’s teachers.</p>
        )}
        {!loadingTeachers && teachers.length > 0 && (
          <ul className="teacher-list">
            {teachers.map((t) => {
              const wa = whatsAppUrl(t.whatsAppNumber, t.phone);
              return (
                <li key={t.teacherId} className="teacher-item">
                  <span className="teacher-name">{t.fullName}</span>
                  {wa ? (
                    <a href={wa} target="_blank" rel="noopener noreferrer" className="btn-whatsapp">WhatsApp</a>
                  ) : (
                    <span className="teacher-no-wa">No WhatsApp</span>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </PageLayout>
  );
}
