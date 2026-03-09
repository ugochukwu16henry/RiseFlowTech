import { useState, useEffect, useCallback } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch, STORAGE_TENANT_KEY } from '../api';
import './RolePages.css';
import './AccessCodesPage.css';

export default function AccessCodesPage() {
  const [list, setList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [generating, setGenerating] = useState(false);
  const [lastResult, setLastResult] = useState(null);

  const load = useCallback(async () => {
    const res = await apiFetch('/api/students/with-access-codes');
    if (!res.ok) throw new Error(await res.text());
    const data = await res.json();
    setList(Array.isArray(data) ? data : []);
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    load()
      .then(() => { if (!cancelled) setLoading(false); })
      .catch((e) => { if (!cancelled) { setError(e.message); setLoading(false); } });
    return () => { cancelled = true; };
  }, [load]);

  const handleGenerateAll = async () => {
    setGenerating(true);
    setLastResult(null);
    try {
      const res = await apiFetch('/api/students/generate-access-codes', { method: 'POST' });
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      setLastResult(data);
      await load();
    } catch (e) {
      setLastResult({ error: e.message });
    } finally {
      setGenerating(false);
    }
  };

  const copyCode = (code) => {
    if (!code) return;
    navigator.clipboard.writeText(code).then(() => { /* optional toast */ });
  };

  const withoutCode = list.filter((s) => !s.parentAccessCode || s.parentAccessCode === '');
  const withCode = list.filter((s) => s.parentAccessCode);

  const schoolId = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_TENANT_KEY) : null;
  const parentSignupUrl = schoolId ? `${typeof window !== 'undefined' ? window.location.origin : ''}/parent/signup?school=${schoolId}` : '';
  const copyParentSignupLink = () => {
    if (parentSignupUrl) navigator.clipboard.writeText(parentSignupUrl);
  };

  if (loading) return <PageLayout title="Parent Access Codes"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (error) return <PageLayout title="Parent Access Codes"><p className="empty-state empty-state--error">{error}</p></PageLayout>;

  return (
    <PageLayout title="Parent Access Codes">
      <p className="card-desc">
        Each student has a unique code (e.g. <strong>RF-8821</strong>). Give this code to the parent so they can open the RiseFlow app or web and &quot;Claim&quot; their child&apos;s profile.
      </p>

      <section className="parent-signup-link-section" aria-label="Parent signup link">
        <h2 className="section-title">Share with parents</h2>
        <p className="card-desc">
          Parents who don&apos;t have an account can sign up using this link. After signup they sign in and enter the access code to link their child.
        </p>
        {parentSignupUrl ? (
          <div className="parent-signup-link-box">
            <code className="parent-signup-url">{parentSignupUrl}</code>
            <button type="button" className="btn-copy" onClick={copyParentSignupLink} title="Copy parent signup link">
              Copy link
            </button>
          </div>
        ) : (
          <p className="empty-state">Sign in as School Admin and select your school to see your parent signup link here.</p>
        )}
      </section>

      <div className="access-codes-actions">
        <button
          type="button"
          className="btn-excel btn-generate"
          onClick={handleGenerateAll}
          disabled={generating || withoutCode.length === 0}
        >
          {generating ? 'Generating…' : `Generate codes for ${withoutCode.length} student(s) without code`}
        </button>
      </div>
      {lastResult && (
        <div className={`access-codes-result ${lastResult.error ? 'access-codes-result--error' : ''}`}>
          {lastResult.error
            ? lastResult.error
            : `Generated ${lastResult.generatedCount} code(s). ${lastResult.studentsWithCode} of ${lastResult.totalStudents} students now have a code.`}
        </div>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Students and their codes</h2>
      {list.length === 0 ? (
        <p className="empty-state">No students yet. Import students first, then generate codes.</p>
      ) : (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Student</th>
                <th>Admission #</th>
                <th>Class</th>
                <th>Parent Access Code</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {list.map((s) => (
                <tr key={s.id}>
                  <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                  <td>{s.admissionNumber || '—'}</td>
                  <td>{s.className || '—'}</td>
                  <td>
                    <code className="access-code-value">{s.parentAccessCode || '—'}</code>
                  </td>
                  <td>
                    {s.parentAccessCode && (
                      <button
                        type="button"
                        className="btn-copy"
                        onClick={() => copyCode(s.parentAccessCode)}
                        title="Copy code"
                      >
                        Copy
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </PageLayout>
  );
}
