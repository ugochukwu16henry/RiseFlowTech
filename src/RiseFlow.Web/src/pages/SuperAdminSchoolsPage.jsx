import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

export default function SuperAdminSchoolsPage() {
  const [schools, setSchools] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    apiFetch('/api/superadmin/schools', { skipTenantHeader: true })
      .then((res) => {
        if (cancelled) return null;
        if (!res.ok) throw new Error('Could not load schools');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setSchools(Array.isArray(data) ? data : []);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Failed to load schools');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  return (
    <PageLayout title="Super Admin — School Management" role="super">
      <h2 className="section-title">School management</h2>
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}
      {!loading && !error && schools.length === 0 && <p className="empty-state">No schools found.</p>}

      {!loading && schools.length > 0 && (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>School</th>
                <th>Country</th>
                <th>Students</th>
                <th>Teachers</th>
                <th>Parents</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {schools.map((s) => (
                <tr key={s.id}>
                  <td>{s.name}</td>
                  <td>{s.countryCode || '—'}</td>
                  <td>{s.studentCount ?? 0}</td>
                  <td>{s.teacherCount ?? 0}</td>
                  <td>{s.parentCount ?? 0}</td>
                  <td>
                    <span className={s.isActive ? 'pill pill--success' : 'pill pill--muted'}>
                      {s.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>
                    <Link className="btn-primary-action btn-primary-action--ghost" to={`/super-admin/data-offboarding?schoolId=${s.id}`}>
                      Offboard
                    </Link>
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