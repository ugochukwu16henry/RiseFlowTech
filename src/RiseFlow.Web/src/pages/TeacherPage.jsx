import { useState, useEffect, useRef } from 'react';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import TeacherPhoto from '../components/TeacherPhoto';
import { apiFetch } from '../api';
import './RolePages.css';

export default function TeacherPage() {
  const [me, setMe] = useState(null);
  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const photoInputRef = useRef(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    Promise.all([
      apiFetch('/api/teachers/me').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/teachers/my-students').then((r) => (r.ok ? r.json() : [])),
    ])
      .then(([profile, list]) => {
        if (cancelled) return;
        setMe(profile);
        setStudents(Array.isArray(list) ? list : []);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Failed to load teacher data');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  const handlePhotoChange = async (e) => {
    const file = e.target?.files?.[0];
    if (!file || !me?.id) return;
    setUploadingPhoto(true);
    const form = new FormData();
    form.append('file', file);
    try {
      const res = await apiFetch(`/api/teachers/${me.id}/photo`, { method: 'POST', body: form });
      if (!res.ok) throw new Error(await res.text());
    } catch (err) {
      // eslint-disable-next-line no-alert
      alert(err.message || 'Could not upload photo.');
    } finally {
      setUploadingPhoto(false);
      e.target.value = '';
    }
  };

  if (loading) return <PageLayout title="Teacher"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (error) return <PageLayout title="Teacher"><p className="empty-state empty-state--error">{error}</p></PageLayout>;

  return (
    <PageLayout title="Teacher">
      <h2 className="section-title">My profile</h2>
      {!me && (
        <p className="empty-state">No teacher profile found. Sign in through your school&apos;s teacher login.</p>
      )}
      {me && (
        <section className="progress-section" aria-label="Teacher profile">
          <div style={{ display: 'flex', gap: '1rem', alignItems: 'center', marginBottom: '0.75rem' }}>
            <TeacherPhoto teacherId={me.id} fullName={`${me.firstName} ${me.lastName}`} size={56} />
            <div>
              <h3 className="card-title" style={{ margin: 0 }}>{[me.firstName, me.middleName, me.lastName].filter(Boolean).join(' ')}</h3>
              <p className="card-desc">Email: {me.email || '—'} • Phone: {me.phone || '—'}</p>
              <p className="card-desc">Role: {me.roleTitle || 'Teacher'} • Department: {me.department || '—'}</p>
              <p className="card-desc">Highest qualification: {me.highestQualification || '—'}</p>
            </div>
          </div>
          <div className="form-actions" style={{ marginTop: '0.5rem' }}>
            <input
              type="file"
              accept=".jpg,.jpeg,.png,.gif,.webp"
              ref={photoInputRef}
              onChange={handlePhotoChange}
              style={{ display: 'none' }}
              aria-label="Upload teacher photo"
            />
            <button
              type="button"
              className="btn-upload-photo"
              onClick={() => photoInputRef.current?.click()}
              disabled={uploadingPhoto}
            >
              {uploadingPhoto ? 'Uploading…' : 'Upload / change photo'}
            </button>
          </div>
        </section>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>My students</h2>
      {students.length === 0 ? (
        <p className="empty-state">No classes or students assigned yet. Your School Admin will assign your classes and subjects.</p>
      ) : (
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
                <tr key={s.studentId}>
                  <td><StudentPhoto studentId={s.studentId} firstName={s.firstName} lastName={s.lastName} size={40} /></td>
                  <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                  <td>{s.admissionNumber || '—'}</td>
                  <td>{s.className || '—'}</td>
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
