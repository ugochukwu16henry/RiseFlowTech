import { useState, useEffect, useMemo } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

export default function StudentPage() {
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    apiFetch('/api/results')
      .then((r) => {
        if (cancelled) return null;
        if (!r.ok) throw new Error('Could not load results');
        return r.json();
      })
      .then((data) => {
        if (!cancelled) setResults(Array.isArray(data) ? data : []);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  const { subjectCount, averagePct } = useMemo(() => {
    if (!results.length) return { subjectCount: 0, averagePct: null };
    const bySubject = {};
    for (const r of results) {
      const name = r.subject?.name || 'Other';
      if (!bySubject[name]) bySubject[name] = { score: 0, max: 0 };
      bySubject[name].score += Number(r.score) || 0;
      bySubject[name].max += Number(r.maxScore) || 0;
    }
    const keys = Object.keys(bySubject);
    const pctList = keys.map((k) => {
      const { score, max } = bySubject[k];
      return max > 0 ? (score / max) * 100 : null;
    }).filter((p) => p != null);
    const avg = pctList.length
      ? Math.round(pctList.reduce((a, b) => a + b, 0) / pctList.length)
      : null;
    return { subjectCount: keys.length, averagePct: avg };
  }, [results]);

  return (
    <PageLayout title="Student — My results" role="student">
      <section aria-label="Results snapshot">
        <div className="dashboard-grid">
          <article className="dashboard-card dashboard-card--highlight">
            <p className="dashboard-label">Subjects</p>
            <p className="dashboard-value">{loading ? '—' : subjectCount}</p>
            <p className="dashboard-sub">With recorded assessments.</p>
          </article>
          <article className="dashboard-card">
            <p className="dashboard-label">Overall (approx.)</p>
            <p className="dashboard-value">
              {loading ? '—' : (averagePct != null ? `${averagePct}%` : '—')}
            </p>
            <p className="dashboard-sub">Average across subjects with scores.</p>
          </article>
          <article className="dashboard-card">
            <p className="dashboard-label">Records</p>
            <p className="dashboard-value">{loading ? '—' : results.length}</p>
            <p className="dashboard-sub">Assessment rows in your gradebook.</p>
          </article>
        </div>
      </section>

      <h2 className="section-title">My results (from database)</h2>
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}
      {!loading && !error && results.length === 0 && (
        <p className="empty-state">No results yet. Sign in as a student to see your grades when teachers upload them.</p>
      )}
      {!loading && results.length > 0 && (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Subject</th>
                <th>Type</th>
                <th>Score</th>
                <th>Grade</th>
              </tr>
            </thead>
            <tbody>
              {results.map((r) => (
                <tr key={r.id}>
                  <td>{r.subject?.name || '—'}</td>
                  <td>{r.assessmentType || '—'}</td>
                  <td>{r.score != null ? `${r.score} / ${r.maxScore ?? ''}` : '—'}</td>
                  <td>{r.gradeLetter || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </PageLayout>
  );
}
