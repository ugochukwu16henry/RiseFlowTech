import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

export default function GradingSystemPage() {
  const [systems, setSystems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState(null);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({ name: '', classId: '', termId: '', isActive: true });
  const [ruleForms, setRuleForms] = useState({});
  const [classes, setClasses] = useState([]);
  const [terms, setTerms] = useState([]);

  const loadData = async () => {
    setLoading(true);
    try {
      const [systemsRes, classesRes, termsRes] = await Promise.all([
        apiFetch('/api/grading-systems'),
        apiFetch('/api/schools/classes'),
        apiFetch('/api/academicterms'),
      ]);
      if (!systemsRes.ok) throw new Error(await systemsRes.text());
      const [systemsData, classData, termData] = await Promise.all([
        systemsRes.json(),
        classesRes.ok ? classesRes.json() : [],
        termsRes.ok ? termsRes.json() : [],
      ]);
      setSystems(Array.isArray(systemsData) ? systemsData : []);
      setClasses(Array.isArray(classData) ? classData : []);
      setTerms(Array.isArray(termData) ? termData : []);
    } catch (err) {
      setMessage(err.message || 'Could not load grading systems.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const createSystem = async () => {
    if (!form.name.trim()) {
      setMessage('System name is required.');
      return;
    }

    setSaving(true);
    setMessage(null);
    try {
      const res = await apiFetch('/api/grading-systems', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: form.name.trim(),
          classId: form.classId || null,
          termId: form.termId || null,
          isActive: !!form.isActive,
        }),
      });
      if (!res.ok) throw new Error(await res.text());
      setForm({ name: '', classId: '', termId: '', isActive: true });
      await loadData();
      setMessage('Grading system created.');
    } catch (err) {
      setMessage(err.message || 'Could not create grading system.');
    } finally {
      setSaving(false);
    }
  };

  const addRule = async (systemId) => {
    const rf = ruleForms[systemId] || { gradeLetter: '', minPercent: '', maxPercent: '', gradePoint: '', remarks: '' };
    if (!rf.gradeLetter || rf.minPercent === '' || rf.maxPercent === '') {
      setMessage('Grade letter, minimum, and maximum percent are required.');
      return;
    }

    setSaving(true);
    setMessage(null);
    try {
      const res = await apiFetch(`/api/grading-systems/${systemId}/rules`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          gradeLetter: rf.gradeLetter.trim(),
          minPercent: Number(rf.minPercent),
          maxPercent: Number(rf.maxPercent),
          gradePoint: rf.gradePoint === '' ? null : Number(rf.gradePoint),
          remarks: rf.remarks?.trim() || null,
        }),
      });
      if (!res.ok) throw new Error(await res.text());
      setRuleForms((prev) => ({ ...prev, [systemId]: { gradeLetter: '', minPercent: '', maxPercent: '', gradePoint: '', remarks: '' } }));
      await loadData();
      setMessage('Grade rule added.');
    } catch (err) {
      setMessage(err.message || 'Could not add rule.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageLayout title="Grading Systems" role="school">
      <div className="school-admin-shell">
        <aside className="school-admin-nav">
          <Link to="/school" className="school-admin-nav-btn school-admin-nav-link">Back to dashboard</Link>
          <Link to="/school/classes" className="school-admin-nav-btn school-admin-nav-link">Classes</Link>
        </aside>

        <section className="school-admin-view">
          <h2 className="section-title">Grading systems</h2>
          <p className="card-desc">Define grade boundaries once, then let result entry auto-resolve grade letters.</p>
          {message && <p className="student-note student-note--success">{message}</p>}
          {loading && <p className="empty-state" aria-busy="true">Loading…</p>}

          <div className="student-record-card" style={{ marginBottom: '1rem' }}>
            <h4 className="dashboard-section-title">Create grading system</h4>
            <div className="student-edit-grid">
              <label>
                <span>Name</span>
                <input className="form-input" value={form.name} onChange={(e) => setForm((p) => ({ ...p, name: e.target.value }))} />
              </label>
              <label>
                <span>Class scope (optional)</span>
                <select className="form-input" value={form.classId} onChange={(e) => setForm((p) => ({ ...p, classId: e.target.value }))}>
                  <option value="">All classes</option>
                  {classes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </label>
              <label>
                <span>Term scope (optional)</span>
                <select className="form-input" value={form.termId} onChange={(e) => setForm((p) => ({ ...p, termId: e.target.value }))}>
                  <option value="">All terms</option>
                  {terms.map((t) => <option key={t.id} value={t.id}>{t.name} {t.academicYear || ''}</option>)}
                </select>
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '1.6rem' }}>
                <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))} />
                <span>Active</span>
              </label>
            </div>
            <div className="form-actions" style={{ marginTop: '0.6rem' }}>
              <button type="button" className="btn-primary-action" onClick={createSystem} disabled={saving}>{saving ? 'Saving…' : 'Create system'}</button>
            </div>
          </div>

          {systems.length === 0 ? (
            <p className="empty-state">No grading systems yet.</p>
          ) : (
            <div className="student-term-grid">
              {systems.map((system) => {
                const localRule = ruleForms[system.id] || { gradeLetter: '', minPercent: '', maxPercent: '', gradePoint: '', remarks: '' };
                return (
                  <article key={system.id} className="student-term-card">
                    <div className="student-term-card-header">
                      <strong>{system.name}</strong>
                      <span>{system.isActive ? 'Active' : 'Inactive'}</span>
                    </div>
                    <p className="card-desc">{system.class?.name || 'All classes'} • {system.term?.name || 'All terms'}</p>

                    <ul className="student-term-results">
                      {(system.rules || []).sort((a, b) => Number(b.minPercent) - Number(a.minPercent)).map((rule) => (
                        <li key={rule.id}>
                          <span>{rule.gradeLetter}</span>
                          <span>{rule.minPercent} - {rule.maxPercent}%</span>
                        </li>
                      ))}
                    </ul>

                    <div className="student-edit-grid" style={{ marginTop: '0.75rem' }}>
                      <label><span>Grade</span><input className="form-input" value={localRule.gradeLetter} onChange={(e) => setRuleForms((prev) => ({ ...prev, [system.id]: { ...localRule, gradeLetter: e.target.value } }))} /></label>
                      <label><span>Min %</span><input type="number" className="form-input" value={localRule.minPercent} onChange={(e) => setRuleForms((prev) => ({ ...prev, [system.id]: { ...localRule, minPercent: e.target.value } }))} /></label>
                      <label><span>Max %</span><input type="number" className="form-input" value={localRule.maxPercent} onChange={(e) => setRuleForms((prev) => ({ ...prev, [system.id]: { ...localRule, maxPercent: e.target.value } }))} /></label>
                    </div>
                    <div className="form-actions" style={{ marginTop: '0.5rem' }}>
                      <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => addRule(system.id)} disabled={saving}>Add rule</button>
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>
    </PageLayout>
  );
}
