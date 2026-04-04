import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';
import './SchoolClassesPage.css';

/** Nigerian / WAEC-style examples — schools can use any names; these are shortcuts. */
const QUICK_GRADE_TEMPLATES = [
  { label: 'Nursery', name: 'Nursery', levelOrder: 5 },
  { label: 'Primary 1', name: 'Primary 1', levelOrder: 10 },
  { label: 'Primary 6', name: 'Primary 6', levelOrder: 15 },
  { label: 'JSS 1', name: 'JSS 1', levelOrder: 30 },
  { label: 'JSS 3', name: 'JSS 3', levelOrder: 32 },
  { label: 'SS1', name: 'SS1', levelOrder: 40 },
  { label: 'SS3', name: 'SS3', levelOrder: 42 },
];

export default function SchoolClassesPage() {
  const [grades, setGrades] = useState([]);
  const [classes, setClasses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [gradeName, setGradeName] = useState('');
  const [gradeLevelOrder, setGradeLevelOrder] = useState('');
  const [savingGrade, setSavingGrade] = useState(false);
  const [className, setClassName] = useState('');
  const [classGradeId, setClassGradeId] = useState('');
  const [academicYear, setAcademicYear] = useState('');
  const [savingClass, setSavingClass] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [gRes, cRes] = await Promise.all([
        apiFetch('/api/schools/grades'),
        apiFetch('/api/schools/classes'),
      ]);
      if (gRes.status === 401 || gRes.status === 403 || cRes.status === 401 || cRes.status === 403) {
        throw new Error('Your session expired or your school access is missing. Please sign in again as School Admin.');
      }
      if (!gRes.ok) throw new Error(await gRes.text().catch(() => 'Could not load grades.'));
      if (!cRes.ok) throw new Error(await cRes.text().catch(() => 'Could not load classes.'));
      const gData = await gRes.json();
      const cData = await cRes.json();
      setGrades(Array.isArray(gData) ? gData : []);
      setClasses(Array.isArray(cData) ? cData : []);
    } catch (e) {
      setError(e.message || 'Failed to load.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    if (grades.length > 0 && !classGradeId) {
      setClassGradeId(grades[0].id);
    }
  }, [grades, classGradeId]);

  const addGrade = async (e) => {
    e.preventDefault();
    const name = gradeName.trim();
    if (!name) return;
    setSavingGrade(true);
    setError(null);
    try {
      const body = { name };
      const lo = parseInt(gradeLevelOrder, 10);
      if (!Number.isNaN(lo) && lo > 0) body.levelOrder = lo;
      const res = await apiFetch('/api/schools/grades', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      const text = await res.text();
      if (!res.ok) throw new Error(text || 'Could not create grade.');
      setGradeName('');
      setGradeLevelOrder('');
      await load();
    } catch (err) {
      setError(err.message || 'Failed to add grade.');
    } finally {
      setSavingGrade(false);
    }
  };

  const addQuickGrade = async (template) => {
    setSavingGrade(true);
    setError(null);
    try {
      const res = await apiFetch('/api/schools/grades', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: template.name, levelOrder: template.levelOrder }),
      });
      const text = await res.text();
      if (res.status === 409) {
        setError(`Grade "${template.name}" may already exist.`);
        await load();
        return;
      }
      if (!res.ok) throw new Error(text || 'Could not create grade.');
      await load();
    } catch (err) {
      setError(err.message || 'Failed to add grade.');
    } finally {
      setSavingGrade(false);
    }
  };

  const addClass = async (e) => {
    e.preventDefault();
    const name = className.trim();
    if (!name || !classGradeId) return;
    setSavingClass(true);
    setError(null);
    try {
      const res = await apiFetch('/api/schools/classes', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name,
          gradeId: classGradeId,
          academicYear: academicYear.trim() || null,
        }),
      });
      const text = await res.text();
      if (!res.ok) throw new Error(text || 'Could not create class.');
      setClassName('');
      setAcademicYear('');
      await load();
    } catch (err) {
      setError(err.message || 'Failed to add class.');
    } finally {
      setSavingClass(false);
    }
  };

  if (loading) {
    return (
      <PageLayout title="Grades & classes" role="school">
        <p className="empty-state" aria-busy="true">Loading…</p>
      </PageLayout>
    );
  }

  return (
    <PageLayout title="Grades & classes" role="school">
      <p className="card-desc school-classes-intro">
        Define your own structure: <strong>Nursery</strong>, <strong>Primary 1–6</strong>, <strong>JSS1–JSS3</strong>,{' '}
        <strong>SS1–SS3</strong> (Senior Secondary / high school), or any names your school uses. Add a{' '}
        <em>grade level</em> first, then add <em>classes</em> under it (e.g. JSS 1A, SS2 Science).
      </p>

      {error && <p className="empty-state empty-state--error" style={{ marginBottom: '1rem' }}>{error}</p>}

      <section className="school-classes-section" aria-labelledby="grades-heading">
        <h2 id="grades-heading" className="section-title">1. Grade levels (programmes)</h2>
        <p className="card-desc">Quick add (Nigeria / common labels). You can still type any custom name below.</p>
        <div className="quick-grade-chips">
          {QUICK_GRADE_TEMPLATES.map((t) => (
            <button
              key={t.name}
              type="button"
              className="chip-btn"
              disabled={savingGrade}
              onClick={() => addQuickGrade(t)}
            >
              {t.label}
            </button>
          ))}
        </div>

        <form onSubmit={addGrade} className="school-classes-form">
          <label htmlFor="newGradeName" className="form-label">Custom grade name</label>
          <input
            id="newGradeName"
            className="form-input"
            value={gradeName}
            onChange={(e) => setGradeName(e.target.value)}
            placeholder="e.g. Primary 4, JSS 2"
            maxLength={64}
          />
          <label htmlFor="levelOrder" className="form-label">Sort order (optional)</label>
          <input
            id="levelOrder"
            type="number"
            min={1}
            className="form-input"
            value={gradeLevelOrder}
            onChange={(e) => setGradeLevelOrder(e.target.value)}
            placeholder="Lower numbers appear first; leave blank to auto-append"
          />
          <button type="submit" className="btn-excel btn-download" disabled={savingGrade || !gradeName.trim()}>
            {savingGrade ? 'Saving…' : 'Add grade level'}
          </button>
        </form>

        {grades.length > 0 && (
          <ul className="grade-list">
            {grades.map((g) => (
              <li key={g.id}>
                <strong>{g.name}</strong>
                <span className="grade-meta"> order {g.levelOrder}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="school-classes-section" aria-labelledby="classes-heading">
        <h2 id="classes-heading" className="section-title">2. Classes (arms / streams)</h2>
        <p className="card-desc">Pick a grade, then name the class (e.g. JSS 1A, Primary 3 Red, SS1 Science).</p>

        <form onSubmit={addClass} className="school-classes-form">
          <label htmlFor="gradeSelect" className="form-label">Grade level</label>
          <select
            id="gradeSelect"
            className="form-input"
            value={classGradeId}
            onChange={(e) => setClassGradeId(e.target.value)}
            required
          >
            <option value="">— Select grade —</option>
            {grades.map((g) => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </select>

          <label htmlFor="newClassName" className="form-label">Class name</label>
          <input
            id="newClassName"
            className="form-input"
            value={className}
            onChange={(e) => setClassName(e.target.value)}
            placeholder="e.g. JSS 1A, SS2 Arts"
            maxLength={64}
            required
          />

          <label htmlFor="academicYear" className="form-label">Academic year (optional)</label>
          <input
            id="academicYear"
            className="form-input"
            value={academicYear}
            onChange={(e) => setAcademicYear(e.target.value)}
            placeholder="e.g. 2025/2026"
            maxLength={16}
          />

          <button type="submit" className="btn-excel btn-download" disabled={savingClass || !classGradeId || !className.trim()}>
            {savingClass ? 'Saving…' : 'Add class'}
          </button>
        </form>

        {classes.length === 0 && grades.length > 0 && (
          <p className="empty-state">No classes yet. Add at least one class under a grade so students can be assigned.</p>
        )}
        {grades.length === 0 && (
          <p className="empty-state">Add at least one grade level above before creating classes.</p>
        )}

        {classes.length > 0 && (
          <div className="data-table-wrap" style={{ marginTop: '1rem' }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Class</th>
                  <th>Grade level</th>
                  <th>Year</th>
                </tr>
              </thead>
              <tbody>
                {classes.map((c) => (
                  <tr key={c.id}>
                    <td>{c.name}</td>
                    <td>{c.gradeName ?? '—'}</td>
                    <td>{c.academicYear ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <p style={{ marginTop: '1.5rem' }}>
        <Link to="/school" className="header-link">← Back to School Admin</Link>
        {' · '}
        <Link to="/school/students/add" className="header-link">Add students</Link>
        {' · '}
        <Link to="/school/import" className="header-link">Bulk import Excel</Link>
      </p>
    </PageLayout>
  );
}
