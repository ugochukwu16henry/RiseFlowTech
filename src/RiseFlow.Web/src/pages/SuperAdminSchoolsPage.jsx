import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';

function buildPublicUrl(relativePath) {
  if (!relativePath) return null;
  if (relativePath.startsWith('http://') || relativePath.startsWith('https://')) return relativePath;
  const normalizedPath = relativePath.replace(/^\/+/, '');
  const base = getApiBase();
  return base ? `${base}/${normalizedPath}` : `/${normalizedPath}`;
}

function formatDate(value) {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
}

export default function SuperAdminSchoolsPage() {
  const [searchParams] = useSearchParams();
  const requestedSchoolId = searchParams.get('schoolId');
  const [schools, setSchools] = useState([]);
  const [selectedSchoolId, setSelectedSchoolId] = useState(requestedSchoolId);
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
        if (cancelled) return;
        const list = Array.isArray(data) ? data : [];
        setSchools(list);
        setSelectedSchoolId((current) => current || requestedSchoolId || list[0]?.id || null);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Failed to load schools');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [requestedSchoolId]);

  const selectedSchool = useMemo(
    () => schools.find((school) => school.id === selectedSchoolId) || null,
    [schools, selectedSchoolId],
  );

  const totals = useMemo(() => schools.reduce((summary, school) => ({
    students: summary.students + Number(school.studentCount || 0),
    teachers: summary.teachers + Number(school.teacherCount || 0),
    parents: summary.parents + Number(school.parentCount || 0),
    active: summary.active + (school.isActive ? 1 : 0),
  }), { students: 0, teachers: 0, parents: 0, active: 0 }), [schools]);

  return (
    <PageLayout title="Super Admin — School Management" role="super">
      <h2 className="section-title">School management</h2>
      <p className="control-room-intro">
        Review every school on RiseFlow, open full school details, and jump quickly to offboarding or revenue operations.
      </p>

      <div className="dashboard-actions" style={{ flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Link to="/super-admin" className="btn-primary-action btn-primary-action--ghost">Back to control room</Link>
        <Link to="/super-admin/revenue" className="btn-primary-action btn-primary-action--ghost">Revenue hub</Link>
        <Link to="/super-admin/affiliates" className="btn-primary-action btn-primary-action--ghost">Affiliates</Link>
        <Link to="/super-admin/data-offboarding" className="btn-primary-action btn-primary-action--ghost">Data offboarding</Link>
      </div>

      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}
      {!loading && !error && schools.length === 0 && <p className="empty-state">No schools found.</p>}

      {!loading && schools.length > 0 && (
        <>
          <div className="dashboard-grid" style={{ marginBottom: '1rem' }}>
            <article className="dashboard-card dashboard-card--highlight">
              <p className="dashboard-label">Schools</p>
              <p className="dashboard-value">{schools.length}</p>
              <p className="dashboard-sub">{totals.active} active schools currently visible.</p>
            </article>
            <article className="dashboard-card">
              <p className="dashboard-label">Students</p>
              <p className="dashboard-value">{totals.students}</p>
              <p className="dashboard-sub">Students linked across all schools.</p>
            </article>
            <article className="dashboard-card">
              <p className="dashboard-label">Teachers</p>
              <p className="dashboard-value">{totals.teachers}</p>
              <p className="dashboard-sub">Teachers currently onboarded.</p>
            </article>
            <article className="dashboard-card">
              <p className="dashboard-label">Parents</p>
              <p className="dashboard-value">{totals.parents}</p>
              <p className="dashboard-sub">Parents connected to student records.</p>
            </article>
          </div>

          {selectedSchool && (
            <section className="dashboard-panel" style={{ marginBottom: '1rem' }}>
              <div className="dashboard-actions" style={{ justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '0.75rem' }}>
                <div>
                  <h3 className="card-title">School detail information</h3>
                  <p className="card-desc">Full profile for <strong>{selectedSchool.name}</strong>.</p>
                </div>
                <div className="dashboard-actions" style={{ flexWrap: 'wrap' }}>
                  <Link className="btn-primary-action" to={`/super-admin/data-offboarding?schoolId=${selectedSchool.id}`}>
                    Open offboarding
                  </Link>
                  {selectedSchool.logoPath && (
                    <a className="btn-primary-action btn-primary-action--ghost" href={buildPublicUrl(selectedSchool.logoPath)} target="_blank" rel="noopener noreferrer">
                      View logo
                    </a>
                  )}
                </div>
              </div>

              <div className="dashboard-grid" style={{ marginTop: '1rem' }}>
                <article className="dashboard-card"><p className="dashboard-label">School</p><p className="dashboard-value" style={{ fontSize: '1.1rem' }}>{selectedSchool.name}</p><p className="dashboard-sub">{selectedSchool.countryName || selectedSchool.countryCode || '—'} • {selectedSchool.currencyCode || 'NGN'}</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Owner email</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedSchool.ownerEmail || '—'}</p><p className="dashboard-sub">School email: {selectedSchool.schoolEmail || '—'}</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Principal</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedSchool.principalName || selectedSchool.ownerName || '—'}</p><p className="dashboard-sub">Phone: {selectedSchool.phone || '—'}</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Address</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedSchool.address || '—'}</p><p className="dashboard-sub">WhatsApp: {selectedSchool.whatsAppNumber || '—'}</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Students / Teachers / Parents</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedSchool.studentCount ?? 0} / {selectedSchool.teacherCount ?? 0} / {selectedSchool.parentCount ?? 0}</p><p className="dashboard-sub">Live counts from the database.</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Created</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{formatDate(selectedSchool.createdAtUtc)}</p><p className="dashboard-sub">CAC: {selectedSchool.cacNumber || '—'}</p></article>
              </div>
            </section>
          )}

          <div className="data-table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>School</th>
                  <th>Owner email</th>
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
                    <td>{s.ownerEmail || s.schoolEmail || '—'}</td>
                    <td>{s.countryName || s.countryCode || '—'}</td>
                    <td>{s.studentCount ?? 0}</td>
                    <td>{s.teacherCount ?? 0}</td>
                    <td>{s.parentCount ?? 0}</td>
                    <td>
                      <span className={s.isActive ? 'pill pill--success' : 'pill pill--muted'}>
                        {s.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td>
                      <div className="dashboard-actions" style={{ flexWrap: 'wrap' }}>
                        <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => setSelectedSchoolId(s.id)}>
                          View details
                        </button>
                        <Link className="btn-primary-action btn-primary-action--ghost" to={`/super-admin/data-offboarding?schoolId=${s.id}`}>
                          Offboard
                        </Link>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </PageLayout>
  );
}