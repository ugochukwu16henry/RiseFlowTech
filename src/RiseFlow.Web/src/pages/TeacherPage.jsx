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
  const [selectedClassId, setSelectedClassId] = useState('');
  const [selectedDate, setSelectedDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [attendance, setAttendance] = useState({});
  const [savingAttendance, setSavingAttendance] = useState(false);
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
        const arr = Array.isArray(list) ? list : [];
        setStudents(arr);
        if (arr.length > 0 && !selectedClassId) {
          setSelectedClassId(arr[0].classId);
        }
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

  const classes = Array.from(
    new Map(students.map((s) => [s.classId, s.className || 'Unnamed class'])).entries(),
  ).map(([id, name]) => ({ id, name }));

  const loadAttendance = async () => {
    if (!selectedClassId || !selectedDate) return;
    try {
      const res = await apiFetch(`/api/attendance/class/${selectedClassId}?date=${selectedDate}`);
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      const next = {};
      (data.students || data.Students || []).forEach((s) => {
        const att = s.attendance || s.Attendance;
        next[s.id || s.Id] = att?.status || att?.Status || '';
      });
      setAttendance(next);
    } catch (err) {
      // eslint-disable-next-line no-alert
      alert(err.message || 'Could not load attendance.');
    }
  };

  const handleAttendanceChange = (studentId, value) => {
    setAttendance((prev) => ({ ...prev, [studentId]: value }));
  };

  const saveAttendance = async () => {
    if (!selectedClassId || !selectedDate) return;
    const items = students
      .filter((s) => s.classId === selectedClassId)
      .map((s) => ({
        studentId: s.studentId,
        date: selectedDate,
        status: attendance[s.studentId] || 'Present',
        period: null,
        note: null,
        sourceDeviceId: 'web-teacher',
        clientTimestampUtc: new Date().toISOString(),
      }));
    setSavingAttendance(true);
    try {
      const res = await apiFetch('/api/attendance/batch', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ items }),
      });
      if (!res.ok) throw new Error(await res.text());
      // eslint-disable-next-line no-alert
      alert('Attendance saved.');
      await loadAttendance();
    } catch (err) {
      // eslint-disable-next-line no-alert
      alert(err.message || 'Could not save attendance.');
    } finally {
      setSavingAttendance(false);
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
                <th>Today&apos;s attendance</th>
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
                  <td>
                    <select
                      value={attendance[s.studentId] || ''}
                      onChange={(e) => handleAttendanceChange(s.studentId, e.target.value)}
                    >
                      <option value="">—</option>
                      <option value="Present">Present</option>
                      <option value="Absent">Absent</option>
                      <option value="Late">Late</option>
                      <option value="Excused">Excused</option>
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {classes.length > 0 && (
        <section style={{ marginTop: '1.5rem' }} aria-label="Quick attendance capture">
          <h2 className="section-title">Quick attendance (online)</h2>
          <p className="card-desc">
            Choose a class and date, load any existing attendance, then update each student&apos;s status.
          </p>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', margin: '0.5rem 0 1rem' }}>
            <label style={{ fontSize: '0.875rem' }}>
              Class:&nbsp;
              <select
                value={selectedClassId}
                onChange={(e) => setSelectedClassId(e.target.value)}
              >
                {classes.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </label>
            <label style={{ fontSize: '0.875rem' }}>
              Date:&nbsp;
              <input
                type="date"
                value={selectedDate}
                onChange={(e) => setSelectedDate(e.target.value)}
              />
            </label>
            <button
              type="button"
              className="btn-excel btn-download"
              onClick={loadAttendance}
            >
              Load attendance
            </button>
            <button
              type="button"
              className="btn-excel btn-generate"
              onClick={saveAttendance}
              disabled={savingAttendance}
            >
              {savingAttendance ? 'Saving…' : 'Save attendance'}
            </button>
          </div>
        </section>
      )}
    </PageLayout>
  );
}
