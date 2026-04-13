import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

const weekdayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export default function TimetablePage() {
  const [classes, setClasses] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [routines, setRoutines] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [selectedClassId, setSelectedClassId] = useState('');
  const [form, setForm] = useState({ subjectId: '', teacherId: '', weekday: 1, startTime: '08:00', endTime: '09:00', room: '' });

  const loadBase = async () => {
    setLoading(true);
    setMessage(null);
    try {
      const [classesRes, subjectsRes, teachersRes] = await Promise.all([
        apiFetch('/api/schools/classes'),
        apiFetch('/api/subjects'),
        apiFetch('/api/teachers'),
      ]);
      const [classData, subjectData, teacherData] = await Promise.all([
        classesRes.ok ? classesRes.json() : [],
        subjectsRes.ok ? subjectsRes.json() : [],
        teachersRes.ok ? teachersRes.json() : [],
      ]);
      const safeClasses = Array.isArray(classData) ? classData : [];
      const safeSubjects = Array.isArray(subjectData) ? subjectData : [];
      const safeTeachers = Array.isArray(teacherData) ? teacherData : [];

      setClasses(safeClasses);
      setSubjects(safeSubjects);
      setTeachers(safeTeachers);

      const firstClassId = safeClasses[0]?.id || '';
      setSelectedClassId((prev) => prev || firstClassId);
      setForm((prev) => ({ ...prev, subjectId: prev.subjectId || (safeSubjects[0]?.id || ''), teacherId: prev.teacherId || (safeTeachers[0]?.id || '') }));
    } catch (err) {
      setMessage(err.message || 'Could not load timetable references.');
    } finally {
      setLoading(false);
    }
  };

  const loadRoutines = async (classId) => {
    if (!classId) {
      setRoutines([]);
      return;
    }
    try {
      const res = await apiFetch(`/api/routines?classId=${classId}`);
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      setRoutines(Array.isArray(data) ? data : []);
    } catch (err) {
      setMessage(err.message || 'Could not load class routine.');
      setRoutines([]);
    }
  };

  useEffect(() => {
    loadBase();
  }, []);

  useEffect(() => {
    if (selectedClassId) loadRoutines(selectedClassId);
  }, [selectedClassId]);

  const grouped = useMemo(() => {
    const map = new Map();
    routines.forEach((r) => {
      const key = Number(r.weekday);
      if (!map.has(key)) map.set(key, []);
      map.get(key).push(r);
    });
    for (const [k, arr] of map.entries()) {
      arr.sort((a, b) => String(a.startTime).localeCompare(String(b.startTime)));
      map.set(k, arr);
    }
    return map;
  }, [routines]);

  const createSlot = async () => {
    if (!selectedClassId || !form.subjectId || form.startTime >= form.endTime) {
      setMessage('Class, subject and valid time range are required.');
      return;
    }

    setSaving(true);
    setMessage(null);
    try {
      const res = await apiFetch('/api/routines', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          classId: selectedClassId,
          subjectId: form.subjectId,
          teacherId: form.teacherId || null,
          weekday: Number(form.weekday),
          startTime: form.startTime,
          endTime: form.endTime,
          room: form.room?.trim() || null,
        }),
      });
      if (!res.ok) throw new Error(await res.text());
      await loadRoutines(selectedClassId);
      setMessage('Routine slot created.');
    } catch (err) {
      setMessage(err.message || 'Could not create routine slot.');
    } finally {
      setSaving(false);
    }
  };

  const deleteSlot = async (id) => {
    setSaving(true);
    setMessage(null);
    try {
      const res = await apiFetch(`/api/routines/${id}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(await res.text());
      await loadRoutines(selectedClassId);
      setMessage('Routine slot deleted.');
    } catch (err) {
      setMessage(err.message || 'Could not delete routine slot.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageLayout title="Class Timetable" role="school">
      <div className="school-admin-shell">
        <aside className="school-admin-nav">
          <Link to="/school" className="school-admin-nav-btn school-admin-nav-link">Back to dashboard</Link>
          <Link to="/school/classes" className="school-admin-nav-btn school-admin-nav-link">Classes</Link>
        </aside>

        <section className="school-admin-view">
          <h2 className="section-title">Class timetable</h2>
          <p className="card-desc">Create and manage weekly class routines with subject and teacher mapping.</p>
          {message && <p className="student-note student-note--success">{message}</p>}
          {loading && <p className="empty-state" aria-busy="true">Loading…</p>}

          <div className="student-record-card" style={{ marginBottom: '1rem' }}>
            <h4 className="dashboard-section-title">Add timetable slot</h4>
            <div className="student-edit-grid">
              <label>
                <span>Class</span>
                <select className="form-input" value={selectedClassId} onChange={(e) => setSelectedClassId(e.target.value)}>
                  <option value="">Select class</option>
                  {classes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </label>
              <label>
                <span>Subject</span>
                <select className="form-input" value={form.subjectId} onChange={(e) => setForm((p) => ({ ...p, subjectId: e.target.value }))}>
                  <option value="">Select subject</option>
                  {subjects.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                </select>
              </label>
              <label>
                <span>Teacher (optional)</span>
                <select className="form-input" value={form.teacherId} onChange={(e) => setForm((p) => ({ ...p, teacherId: e.target.value }))}>
                  <option value="">Unassigned</option>
                  {teachers.map((t) => <option key={t.id} value={t.id}>{[t.firstName, t.lastName].filter(Boolean).join(' ')}</option>)}
                </select>
              </label>
              <label>
                <span>Weekday</span>
                <select className="form-input" value={form.weekday} onChange={(e) => setForm((p) => ({ ...p, weekday: Number(e.target.value) }))}>
                  {weekdayNames.map((d, i) => <option key={d} value={i}>{d}</option>)}
                </select>
              </label>
              <label>
                <span>Start</span>
                <input type="time" className="form-input" value={form.startTime} onChange={(e) => setForm((p) => ({ ...p, startTime: e.target.value }))} />
              </label>
              <label>
                <span>End</span>
                <input type="time" className="form-input" value={form.endTime} onChange={(e) => setForm((p) => ({ ...p, endTime: e.target.value }))} />
              </label>
              <label>
                <span>Room (optional)</span>
                <input className="form-input" value={form.room} onChange={(e) => setForm((p) => ({ ...p, room: e.target.value }))} />
              </label>
            </div>
            <div className="form-actions" style={{ marginTop: '0.6rem' }}>
              <button type="button" className="btn-primary-action" onClick={createSlot} disabled={saving}>{saving ? 'Saving…' : 'Add slot'}</button>
            </div>
          </div>

          <div className="student-record-card">
            <h4 className="dashboard-section-title">Weekly view</h4>
            {selectedClassId === '' ? (
              <p className="card-desc">Select a class to view timetable.</p>
            ) : (
              <div className="student-term-grid">
                {weekdayNames.map((name, weekday) => (
                  <article key={name} className="student-term-card">
                    <div className="student-term-card-header">
                      <strong>{name}</strong>
                      <span>{(grouped.get(weekday) || []).length} slots</span>
                    </div>
                    <ul className="student-term-results">
                      {(grouped.get(weekday) || []).map((slot) => (
                        <li key={slot.id}>
                          <span>{slot.startTime} - {slot.endTime} • {slot.subject?.name || 'Subject'}</span>
                          <span>
                            {slot.teacher ? `${slot.teacher.firstName || ''} ${slot.teacher.lastName || ''}`.trim() : 'Unassigned'}
                            {' '}
                            <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => deleteSlot(slot.id)} disabled={saving} style={{ padding: '0.2rem 0.45rem', marginLeft: '0.35rem' }}>Remove</button>
                          </span>
                        </li>
                      ))}
                    </ul>
                  </article>
                ))}
              </div>
            )}
          </div>
        </section>
      </div>
    </PageLayout>
  );
}
