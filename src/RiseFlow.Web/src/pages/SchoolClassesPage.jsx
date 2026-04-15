import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';
import './SchoolClassesPage.css';

const FALLBACK_GRADE_TEMPLATES = [
  { label: 'Nursery', name: 'Nursery', levelOrder: 5 },
  { label: 'Primary 1', name: 'Primary 1', levelOrder: 10 },
  { label: 'Primary 6', name: 'Primary 6', levelOrder: 15 },
  { label: 'JSS 1', name: 'JSS 1', levelOrder: 30 },
  { label: 'JSS 3', name: 'JSS 3', levelOrder: 32 },
  { label: 'SS1', name: 'SS1', levelOrder: 40 },
  { label: 'SS3', name: 'SS3', levelOrder: 42 },
];

const CLASS_CATEGORY_OPTIONS = [
  { key: 'pre_nursery', label: 'Pre-nursery' },
  { key: 'primary', label: 'Primary' },
  { key: 'secondary', label: 'Secondary' },
  { key: 'custom', label: 'Custom' },
];

function toDistinctNonEmpty(values) {
  const seen = new Set();
  const output = [];
  (values || []).forEach((value) => {
    const normalized = String(value || '').trim();
    if (!normalized) return;
    const key = normalized.toLowerCase();
    if (seen.has(key)) return;
    seen.add(key);
    output.push(normalized);
  });
  return output;
}

function classifyLevelByCategory(levelName) {
  const text = String(levelName || '').trim().toLowerCase();
  if (!text) return null;

  const isPreNursery = /creche|daycare|playgroup|pre[-\s]?nursery|nursery|kg|kindergarten|reception|pp\d|maternelle|petite section|moyenne section|grande section/.test(text);
  if (isPreNursery) return 'pre_nursery';

  const isPrimary = /primary|\bgrade\s*[1-6]\b|\bcp\d\b|\bce\d\b|\bcm\d\b/.test(text);
  if (isPrimary) return 'primary';

  const isSecondary = /jss|jhs|junior secondary|\bss\s*\d\b|\bshs\s*\d\b|senior secondary|college|lycee|\bform\s*\d\b/.test(text);
  if (isSecondary) return 'secondary';

  return null;
}

function matchesAny(text, patterns) {
  return patterns.some((pattern) => pattern.test(text));
}

function inferCategoryByCountry(countryCode, levelName) {
  const text = String(levelName || '').trim().toLowerCase();
  if (!text) return null;

  const normalizedCode = String(countryCode || '').trim().toUpperCase();

  const commonPrimary = [/\bprimary\b/, /\bgrade\s*[1-6]\b/, /\bcp\d\b/, /\bce\d\b/, /\bcm\d\b/];
  const commonSecondary = [/\bjss\b/, /\bjhs\b/, /\bss\s*\d\b/, /\bshs\s*\d\b/, /junior secondary/, /senior secondary/, /college/, /lycee/, /\bform\s*\d\b/];

  const byCountry = {
    NG: {
      primary: [/\bprimary\b/],
      secondary: [/\bjss\b/, /\bss\s*\d\b/],
    },
    GH: {
      primary: [/\bprimary\b/],
      secondary: [/\bjhs\b/, /\bshs\b/],
    },
    KE: {
      primary: [/\bgrade\s*[1-6]\b/],
      secondary: [/junior secondary/, /senior secondary/, /\bform\s*\d\b/],
    },
    SN: {
      primary: [/\bcp\d\b/, /\bce\d\b/, /\bcm\d\b/],
      secondary: [/college/, /lycee/],
    },
    CI: {
      primary: [/\bcp\d\b/, /\bce\d\b/, /\bcm\d\b/],
      secondary: [/college/, /lycee/],
    },
    MA: {
      primary: [/\bcp\d\b/, /\bce\d\b/, /\bcm\d\b/],
      secondary: [/college/, /lycee/],
    },
  };

  const rules = byCountry[normalizedCode];
  if (rules) {
    if (matchesAny(text, rules.primary)) return 'primary';
    if (matchesAny(text, rules.secondary)) return 'secondary';
  }

  if (matchesAny(text, commonPrimary)) return 'primary';
  if (matchesAny(text, commonSecondary)) return 'secondary';
  return null;
}

function buildCountryCategoryTemplates(countryCode, prePrimaryStages, countryLevels) {
  const allLevels = toDistinctNonEmpty(countryLevels);
  const prePrimaryNames = new Set(
    toDistinctNonEmpty((prePrimaryStages || []).map((stage) => stage?.levelName))
      .map((name) => name.toLowerCase()),
  );

  const mapped = {
    pre_nursery: [],
    primary: [],
    secondary: [],
  };

  allLevels.forEach((levelName, index) => {
    const normalizedLevel = String(levelName || '').trim().toLowerCase();
    const category = prePrimaryNames.has(normalizedLevel)
      ? 'pre_nursery'
      : (inferCategoryByCountry(countryCode, levelName) || classifyLevelByCategory(levelName));

    if (!category || !mapped[category]) return;
    mapped[category].push({
      label: levelName,
      name: levelName,
      levelOrder: (index + 1) * 5,
    });
  });

  return mapped;
}

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
  const [profileInfo, setProfileInfo] = useState({ profileCode: 'NG_6334', profileName: 'Nigeria 6-3-3-4' });
  const [quickGradeTemplates, setQuickGradeTemplates] = useState(FALLBACK_GRADE_TEMPLATES);
  const [classCategory, setClassCategory] = useState('pre_nursery');
  const [countryCode, setCountryCode] = useState('');
  const [countryName, setCountryName] = useState('');
  const [countryClassTemplates, setCountryClassTemplates] = useState({
    pre_nursery: [],
    primary: [],
    secondary: [],
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [gRes, cRes, pRes, oRes] = await Promise.all([
        apiFetch('/api/schools/grades'),
        apiFetch('/api/schools/classes'),
        apiFetch('/api/schools/profile'),
        apiFetch('/api/schools/onboarding-options'),
      ]);
      const tRes = await apiFetch('/api/schools/grade-templates');
      if (gRes.status === 401 || gRes.status === 403 || cRes.status === 401 || cRes.status === 403 || pRes.status === 401 || pRes.status === 403) {
        throw new Error('Your session expired or your school access is missing. Please sign in again as School Admin.');
      }
      if (!gRes.ok) throw new Error(await gRes.text().catch(() => 'Could not load grades.'));
      if (!cRes.ok) throw new Error(await cRes.text().catch(() => 'Could not load classes.'));
      if (!pRes.ok) throw new Error(await pRes.text().catch(() => 'Could not load school profile.'));
      const gData = await gRes.json();
      const cData = await cRes.json();
      const pData = await pRes.json();
      const oData = oRes.ok ? await oRes.json().catch(() => null) : null;
      const tData = tRes.ok ? await tRes.json().catch(() => null) : null;

      const countries = Array.isArray(oData?.countries) ? oData.countries : [];
      const profileCountryCode = String(pData?.countryCode || '').trim().toUpperCase();
      const selectedCountry = countries.find((country) => String(country.countryCode || '').trim().toUpperCase() === profileCountryCode) || null;
      const categoryTemplates = buildCountryCategoryTemplates(
        profileCountryCode,
        selectedCountry?.prePrimaryStages || [],
        selectedCountry?.defaultClassLevels || [],
      );

      setGrades(Array.isArray(gData) ? gData : []);
      setClasses(Array.isArray(cData) ? cData : []);
      setCountryCode(profileCountryCode);
      setCountryName(selectedCountry?.countryName || 'your country');
      setCountryClassTemplates(categoryTemplates);

      setClassCategory((current) => {
        if (current === 'custom') return current;
        const hasOptions = Array.isArray(categoryTemplates[current]) && categoryTemplates[current].length > 0;
        if (hasOptions) return current;
          const firstWithOptions = ['pre_nursery', 'primary', 'secondary'].find((key) => Array.isArray(categoryTemplates[key]) && categoryTemplates[key].length > 0);
        return firstWithOptions || 'custom';
      });

      if (tData && Array.isArray(tData.templates) && tData.templates.length > 0) {
        setQuickGradeTemplates(tData.templates);
        setProfileInfo({
          profileCode: tData.profileCode || 'NG_6334',
          profileName: tData.profileName || 'Academic system profile',
        });
      } else {
        setQuickGradeTemplates(FALLBACK_GRADE_TEMPLATES);
      }
    } catch (e) {
      setError(e.message || 'Failed to load.');
    } finally {
      setLoading(false);
    }
  }, []);

  const activeCategoryTemplates = classCategory === 'custom'
    ? []
    : (countryClassTemplates[classCategory] || []);

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
        <p className="card-desc">
          Quick add ({profileInfo.profileName}). You can still type any custom name below.
        </p>
        <div className="quick-grade-chips">
          {quickGradeTemplates.map((t) => (
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

        <p className="card-desc" style={{ marginTop: '0.5rem' }}>
          Country-specific references ({countryName}{countryCode ? ` - ${countryCode}` : ''}): choose a group to show recommended class levels.
        </p>
        <div className="quick-grade-chips">
          {CLASS_CATEGORY_OPTIONS.map((option) => (
            <button
              key={option.key}
              type="button"
              className={`chip-btn ${classCategory === option.key ? 'chip-btn--active' : ''}`}
              onClick={() => setClassCategory(option.key)}
            >
              {option.label}
            </button>
          ))}
        </div>

        {classCategory !== 'custom' && activeCategoryTemplates.length > 0 && (
          <div className="quick-grade-chips">
            {activeCategoryTemplates.map((template) => (
              <button
                key={`${classCategory}-${template.name}`}
                type="button"
                className="chip-btn"
                disabled={savingGrade}
                onClick={() => addQuickGrade(template)}
              >
                {template.label}
              </button>
            ))}
          </div>
        )}
        {classCategory !== 'custom' && activeCategoryTemplates.length === 0 && (
          <p className="card-desc" style={{ marginTop: '0.5rem' }}>
            No country-specific entries found for this group. Use Custom to type your own.
          </p>
        )}
        {classCategory === 'custom' && (
          <p className="card-desc" style={{ marginTop: '0.5rem' }}>
            Custom selected: type any class or grade level name your school uses.
          </p>
        )}

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
