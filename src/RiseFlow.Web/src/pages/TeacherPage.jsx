import { useState, useEffect } from 'react';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import { apiFetch } from '../api';
import './RolePages.css';

export default function TeacherPage() {
  const [teachers, setTeachers] = useState([]);
  const [students, setStudents] = useState([]);
  const [loadingTeachers, setLoadingTeachers] = useState(true);
  const [loadingStudents, setLoadingStudents] = useState(true);
  const [errorTeachers, setErrorTeachers] = useState(null);
  const [errorStudents, setErrorStudents] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoadingTeachers(true);
    apiFetch('/api/teachers')
      .then((r) => (r.ok ? r.json() : []))
      .then((data) => { if (!cancelled) setTeachers(Array.isArray(data) ? data : []); })
      .catch((e) => { if (!cancelled) setErrorTeachers(e.message); })
      .finally(() => { if (!cancelled) setLoadingTeachers(false); });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoadingStudents(true);
    apiFetch('/api/students')
      .then((r) => (r.ok ? r.json() : []))
      .then((data) => { if (!cancelled) setStudents(Array.isArray(data) ? data : []); })
      .catch((e) => { if (!cancelled) setErrorStudents(e.message); })
      .finally(() => { if (!cancelled) setLoadingStudents(false); });
    return () => { cancelled = true; };
  }, []);

  return (
    <PageLayout title="Teacher">
      <h2 className="section-title">Teachers (from database)</h2>
      {loadingTeachers && <p className="empty-state" aria-busy="true">Loading…</p>}
      {errorTeachers && <p className="empty-state empty-state--error">{errorTeachers}</p>}
      {!loadingTeachers && teachers.length === 0 && <p className="empty-state">No teachers in this school.</p>}
      {!loadingTeachers && teachers.length > 0 && (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Phone</th>
                <th>Specialization</th>
              </tr>
            </thead>
            <tbody>
              {teachers.map((t) => (
                <tr key={t.id}>
                  <td>{[t.firstName, t.middleName, t.lastName].filter(Boolean).join(' ')}</td>
                  <td>{t.email || '—'}</td>
                  <td>{t.phone || '—'}</td>
                  <td>{t.subjectSpecialization || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Students (from database)</h2>
      {loadingStudents && <p className="empty-state" aria-busy="true">Loading…</p>}
      {errorStudents && <p className="empty-state empty-state--error">{errorStudents}</p>}
      {!loadingStudents && students.length === 0 && <p className="empty-state">No students in this school.</p>}
      {!loadingStudents && students.length > 0 && (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th style={{ width: '48px' }}>Photo</th>
                <th>Name</th>
                <th>Admission #</th>
                <th>Class</th>
                <th>Gender</th>
              </tr>
            </thead>
            <tbody>
              {students.map((s) => (
                <tr key={s.id}>
                  <td><StudentPhoto studentId={s.id} firstName={s.firstName} lastName={s.lastName} size={40} /></td>
                  <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                  <td>{s.admissionNumber || '—'}</td>
                  <td>{s.class?.name || '—'}</td>
                  <td>{s.gender || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </PageLayout>
  );
}
