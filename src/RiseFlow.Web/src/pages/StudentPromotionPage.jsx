import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

export default function StudentPromotionPage() {
  const [classes, setClasses] = useState([]);
  const [students, setStudents] = useState([]);
  const [terms, setTerms] = useState([]);
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [form, setForm] = useState({
    fromClassId: '',
    toClassId: '',
    fromTermId: '',
    promotionSessionLabel: '',
    notes: '',
    studentIds: [],
  });

  const loadData = async () => {
    setLoading(true);
    setMessage(null);
    try {
      const [classRes, studentRes, termRes, historyRes] = await Promise.all([
        apiFetch('/api/schools/classes'),
        apiFetch('/api/students'),
        apiFetch('/api/academicterms'),
        apiFetch('/api/promotions/history'),
      ]);

      const [classData, studentData, termData, historyData] = await Promise.all([
        classRes.ok ? classRes.json() : [],
        studentRes.ok ? studentRes.json() : [],
        termRes.ok ? termRes.json() : [],
        historyRes.ok ? historyRes.json() : [],
      ]);

      setClasses(Array.isArray(classData) ? classData : []);
      setStudents(Array.isArray(studentData) ? studentData : []);
      setTerms(Array.isArray(termData) ? termData : []);
      setHistory(Array.isArray(historyData) ? historyData : []);
    } catch (err) {
      setMessage(err.message || 'Could not load promotion data.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const classStudents = students.filter((s) => s.classId === form.fromClassId);

  useEffect(() => {
    setForm((prev) => ({
      ...prev,
      studentIds: prev.studentIds.filter((id) => classStudents.some((s) => s.id === id)),
    }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form.fromClassId, students.length]);

  const toggleStudent = (id) => {
    setForm((prev) => {
      const exists = prev.studentIds.includes(id);
      return {
        ...prev,
        studentIds: exists ? prev.studentIds.filter((x) => x !== id) : [...prev.studentIds, id],
      };
    });
  };

  const selectAll = () => {
    setForm((prev) => ({ ...prev, studentIds: classStudents.map((s) => s.id) }));
  };

  const clearAll = () => {
    setForm((prev) => ({ ...prev, studentIds: [] }));
  };

  const promote = async () => {
    if (!form.fromClassId || !form.toClassId || form.studentIds.length === 0) {
      setMessage('Source class, destination class, and at least one student are required.');
      return;
    }

    setSaving(true);
    setMessage(null);
    try {
      const res = await apiFetch('/api/promotions/bulk', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fromClassId: form.fromClassId,
          toClassId: form.toClassId,
          fromTermId: form.fromTermId || null,
          promotionSessionLabel: form.promotionSessionLabel || null,
          studentIds: form.studentIds,
          notes: form.notes || null,
        }),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) throw new Error(data?.message || 'Could not promote students.');
      setMessage(data?.message || 'Promotion completed.');
      setForm((prev) => ({ ...prev, studentIds: [], notes: '' }));
      await loadData();
    } catch (err) {
      setMessage(err.message || 'Could not promote students.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageLayout title="Student Promotion" role="school">
      <div className="school-admin-shell">
        <aside className="school-admin-nav">
          <Link to="/school" className="school-admin-nav-btn school-admin-nav-link">Back to dashboard</Link>
          <Link to="/school/students" className="school-admin-nav-btn school-admin-nav-link">Students</Link>
        </aside>

        <section className="school-admin-view">
          <h2 className="section-title">Promote students</h2>
          <p className="card-desc">Move students from one class to another with a tracked promotion history.</p>
          {message && <p className="student-note student-note--success">{message}</p>}
          {loading && <p className="empty-state" aria-busy="true">Loading…</p>}

          <div className="student-record-card" style={{ marginBottom: '1rem' }}>
            <h4 className="dashboard-section-title">Promotion setup</h4>
            <div className="student-edit-grid">
              <label>
                <span>From class</span>
                <select className="form-input" value={form.fromClassId} onChange={(e) => setForm((p) => ({ ...p, fromClassId: e.target.value }))}>
                  <option value="">Select class</option>
                  {classes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </label>
              <label>
                <span>To class</span>
                <select className="form-input" value={form.toClassId} onChange={(e) => setForm((p) => ({ ...p, toClassId: e.target.value }))}>
                  <option value="">Select class</option>
                  {classes.filter((c) => c.id !== form.fromClassId).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </label>
              <label>
                <span>Term (optional)</span>
                <select className="form-input" value={form.fromTermId} onChange={(e) => setForm((p) => ({ ...p, fromTermId: e.target.value }))}>
                  <option value="">All terms</option>
                  {terms.map((t) => <option key={t.id} value={t.id}>{t.name} {t.academicYear || ''}</option>)}
                </select>
              </label>
              <label>
                <span>Session label (optional)</span>
                <input className="form-input" value={form.promotionSessionLabel} onChange={(e) => setForm((p) => ({ ...p, promotionSessionLabel: e.target.value }))} placeholder="e.g. 2026-2027" />
              </label>
              <label className="student-edit-grid__wide">
                <span>Notes</span>
                <textarea className="form-input" rows="2" value={form.notes} onChange={(e) => setForm((p) => ({ ...p, notes: e.target.value }))} />
              </label>
            </div>
          </div>

          <div className="student-record-card" style={{ marginBottom: '1rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
              <h4 className="dashboard-section-title" style={{ margin: 0 }}>Select students ({form.studentIds.length})</h4>
              <div className="form-actions" style={{ margin: 0 }}>
                <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={selectAll}>Select all</button>
                <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={clearAll}>Clear</button>
              </div>
            </div>
            {classStudents.length === 0 ? (
              <p className="card-desc">No students in selected source class.</p>
            ) : (
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Select</th>
                      <th>Name</th>
                      <th>Admission #</th>
                      <th>Class</th>
                    </tr>
                  </thead>
                  <tbody>
                    {classStudents.map((s) => (
                      <tr key={s.id}>
                        <td><input type="checkbox" checked={form.studentIds.includes(s.id)} onChange={() => toggleStudent(s.id)} /></td>
                        <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                        <td>{s.admissionNumber || '—'}</td>
                        <td>{s.class?.name || s.className || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <div className="form-actions" style={{ marginTop: '0.75rem' }}>
              <button type="button" className="btn-primary-action" onClick={promote} disabled={saving}>{saving ? 'Promoting…' : 'Promote selected students'}</button>
            </div>
          </div>

          <div className="student-record-card">
            <h4 className="dashboard-section-title">Promotion history</h4>
            {history.length === 0 ? (
              <p className="card-desc">No promotions recorded yet.</p>
            ) : (
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Student</th>
                      <th>From</th>
                      <th>To</th>
                      <th>Session</th>
                      <th>Date</th>
                    </tr>
                  </thead>
                  <tbody>
                    {history.map((h) => (
                      <tr key={h.id}>
                        <td>{h.studentName}</td>
                        <td>{h.fromClassName}</td>
                        <td>{h.toClassName}</td>
                        <td>{h.promotionSessionLabel || '—'}</td>
                        <td>{new Date(h.promotedAtUtc).toLocaleDateString()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </section>
      </div>
    </PageLayout>
  );
}
