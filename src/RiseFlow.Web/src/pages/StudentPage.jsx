import { useState, useEffect } from 'react';
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

  return (
    <PageLayout title="Student — My results">
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
