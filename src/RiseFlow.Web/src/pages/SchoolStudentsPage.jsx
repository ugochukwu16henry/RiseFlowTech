import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import { apiFetch } from '../api';
import './RolePages.css';

export default function SchoolStudentsPage() {
  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    apiFetch('/api/students')
      .then((res) => {
        if (cancelled) return null;
        if (!res.ok) throw new Error('Could not load students');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setStudents(Array.isArray(data) ? data : []);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Failed to load students');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  return (
    <PageLayout title="School Admin — Teachers & Students">
      <h2 className="section-title">Students</h2>
      <div className="form-actions" style={{ marginBottom: '0.75rem' }}>
        <Link to="/school/students/add" className="btn-primary-action">Add student</Link>
        <Link to="/school/import" className="btn-primary-action btn-primary-action--ghost">Bulk import</Link>
      </div>
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}
      {!loading && !error && students.length === 0 && <p className="empty-state">No students found.</p>}
      {!loading && students.length > 0 && (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th style={{ width: '48px' }}>Photo</th>
                <th>Name</th>
                <th>Admission #</th>
                <th>Class</th>
              </tr>
            </thead>
            <tbody>
              {students.map((s) => (
                <tr key={s.id}>
                  <td><StudentPhoto studentId={s.id} firstName={s.firstName} lastName={s.lastName} size={36} /></td>
                  <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                  <td>{s.admissionNumber || '—'}</td>
                  <td>{s.class?.name || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </PageLayout>
  );
}
