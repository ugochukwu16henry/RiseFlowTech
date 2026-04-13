import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';

export default function TeacherAssignmentsPage() {
  const [classes, setClasses] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [terms, setTerms] = useState([]);
  const [assignments, setAssignments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);
  const [file, setFile] = useState(null);
  const [form, setForm] = useState({ classId: '', subjectId: '', termId: '', title: '', description: '', dueDateUtc: '' });

  const loadData = async () => {
    setLoading(true);
    try {
      const [classesRes, subjectsRes, termsRes, assignmentsRes] = await Promise.all([
        apiFetch('/api/schools/classes'),
        apiFetch('/api/subjects'),
        apiFetch('/api/academicterms'),
        apiFetch('/api/assignments'),
      ]);
      const [classData, subjectData, termData, assignmentData] = await Promise.all([
        classesRes.ok ? classesRes.json() : [],
        subjectsRes.ok ? subjectsRes.json() : [],
        termsRes.ok ? termsRes.json() : [],
        assignmentsRes.ok ? assignmentsRes.json() : [],
      ]);
      const safeClasses = Array.isArray(classData) ? classData : [];
      const safeSubjects = Array.isArray(subjectData) ? subjectData : [];
      const safeTerms = Array.isArray(termData) ? termData : [];
      setClasses(safeClasses);
      setSubjects(safeSubjects);
      setTerms(safeTerms);
      setAssignments(Array.isArray(assignmentData) ? assignmentData : []);
      setForm((prev) => ({
        ...prev,
        classId: prev.classId || safeClasses[0]?.id || '',
        subjectId: prev.subjectId || safeSubjects[0]?.id || '',
        termId: prev.termId || safeTerms[0]?.id || '',
      }));
    } catch (err) {
      setMessage(err.message || 'Could not load assignments workspace.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const uploadAndCreate = async () => {
    if (!file || !form.classId || !form.subjectId || !form.termId || !form.title.trim()) {
      setMessage('Class, subject, term, title, and file are required.');
      return;
    }

    setSaving(true);
    setMessage(null);
    try {
      const fileForm = new FormData();
      fileForm.append('file', file);
      fileForm.append('category', 'assignment');

      const uploadRes = await apiFetch('/api/files/upload', { method: 'POST', body: fileForm });
      if (!uploadRes.ok) throw new Error(await uploadRes.text());
      const asset = await uploadRes.json();

      const createRes = await apiFetch('/api/assignments', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          classId: form.classId,
          subjectId: form.subjectId,
          termId: form.termId,
          title: form.title.trim(),
          description: form.description.trim() || null,
          fileAssetId: asset.id,
          dueDateUtc: form.dueDateUtc ? new Date(form.dueDateUtc).toISOString() : null,
        }),
      });
      if (!createRes.ok) throw new Error(await createRes.text());

      setFile(null);
      setForm((prev) => ({ ...prev, title: '', description: '', dueDateUtc: '' }));
      await loadData();
      setMessage('Assignment published.');
    } catch (err) {
      setMessage(err.message || 'Could not publish assignment.');
    } finally {
      setSaving(false);
    }
  };

  const deleteAssignment = async (id) => {
    setSaving(true);
    setMessage(null);
    try {
      const res = await apiFetch(`/api/assignments/${id}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(await res.text());
      await loadData();
      setMessage('Assignment removed.');
    } catch (err) {
      setMessage(err.message || 'Could not remove assignment.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <PageLayout title="Teacher Assignments" role="teacher">
      <div className="school-admin-shell">
        <aside className="school-admin-nav">
          <Link to="/teacher" className="school-admin-nav-btn school-admin-nav-link">Back to teacher dashboard</Link>
          <Link to="/teacher/grading" className="school-admin-nav-btn school-admin-nav-link">Grading</Link>
        </aside>

        <section className="school-admin-view">
          <h2 className="section-title">Assignments</h2>
          <p className="card-desc">Upload files and publish assignments for your classes.</p>
          {message && <p className="student-note student-note--success">{message}</p>}
          {loading && <p className="empty-state" aria-busy="true">Loading…</p>}

          <div className="student-record-card" style={{ marginBottom: '1rem' }}>
            <h4 className="dashboard-section-title">Publish assignment</h4>
            <div className="student-edit-grid">
              <label>
                <span>Class</span>
                <select className="form-input" value={form.classId} onChange={(e) => setForm((p) => ({ ...p, classId: e.target.value }))}>
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
                <span>Term</span>
                <select className="form-input" value={form.termId} onChange={(e) => setForm((p) => ({ ...p, termId: e.target.value }))}>
                  <option value="">Select term</option>
                  {terms.map((t) => <option key={t.id} value={t.id}>{t.name} {t.academicYear || ''}</option>)}
                </select>
              </label>
              <label>
                <span>Title</span>
                <input className="form-input" value={form.title} onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))} />
              </label>
              <label>
                <span>Due date</span>
                <input type="date" className="form-input" value={form.dueDateUtc} onChange={(e) => setForm((p) => ({ ...p, dueDateUtc: e.target.value }))} />
              </label>
              <label className="student-edit-grid__wide">
                <span>Description</span>
                <textarea className="form-input" rows="2" value={form.description} onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))} />
              </label>
              <label className="student-edit-grid__wide">
                <span>Assignment file</span>
                <input type="file" className="form-input" onChange={(e) => setFile(e.target.files?.[0] || null)} />
              </label>
            </div>
            <div className="form-actions" style={{ marginTop: '0.6rem' }}>
              <button type="button" className="btn-primary-action" onClick={uploadAndCreate} disabled={saving}>{saving ? 'Publishing…' : 'Publish assignment'}</button>
            </div>
          </div>

          <div className="student-record-card">
            <h4 className="dashboard-section-title">Published assignments</h4>
            {assignments.length === 0 ? (
              <p className="card-desc">No assignments published yet.</p>
            ) : (
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Title</th>
                      <th>Class</th>
                      <th>Subject</th>
                      <th>Term</th>
                      <th>Due</th>
                      <th>File</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {assignments.map((a) => (
                      <tr key={a.id}>
                        <td>{a.title}</td>
                        <td>{a.className}</td>
                        <td>{a.subjectName}</td>
                        <td>{a.termName}</td>
                        <td>{a.dueDateUtc ? new Date(a.dueDateUtc).toLocaleDateString() : '—'}</td>
                        <td>
                          <a href={`${getApiBase()}/api/files/${a.fileAssetId}/download`} target="_blank" rel="noopener noreferrer">{a.originalFileName}</a>
                        </td>
                        <td>
                          <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => deleteAssignment(a.id)} disabled={saving}>Delete</button>
                        </td>
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
