import { useState, useEffect, useMemo, useRef } from 'react';
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
  const [activeView, setActiveView] = useState('overview');
  const [studentSearch, setStudentSearch] = useState('');
  const [studentClassFilter, setStudentClassFilter] = useState('');
  const [studentGradeFilter, setStudentGradeFilter] = useState('');
  const photoInputRef = useRef(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    Promise.all([
      apiFetch('/api/teachers/me').then(async (r) => {
        if (r.status === 204) return null;
        return r.ok ? r.json() : null;
      }),
      apiFetch('/api/teachers/my-students').then(async (r) => {
        if (r.status === 204) return [];
        return r.ok ? r.json() : [];
      }),
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
  const gradeOptions = Array.from(new Set(students.map((s) => s.gradeName).filter(Boolean))).sort();
  const filteredStudents = useMemo(() => {
    const query = studentSearch.trim().toLowerCase();
    return students.filter((student) => {
      const fullName = [student.firstName, student.middleName, student.lastName].filter(Boolean).join(' ').toLowerCase();
      const className = (student.className || '').toLowerCase();
      const gradeName = (student.gradeName || '').toLowerCase();
      const admissionNumber = (student.admissionNumber || '').toLowerCase();
      const matchesSearch = !query
        || fullName.includes(query)
        || className.includes(query)
        || gradeName.includes(query)
        || admissionNumber.includes(query);
      const matchesClass = !studentClassFilter || student.classId === studentClassFilter;
      const matchesGrade = !studentGradeFilter || student.gradeName === studentGradeFilter;
      return matchesSearch && matchesClass && matchesGrade;
    });
  }, [students, studentSearch, studentClassFilter, studentGradeFilter]);

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

  if (loading) return <PageLayout title="Teacher" role="teacher"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (error) return <PageLayout title="Teacher" role="teacher"><p className="empty-state empty-state--error">{error}</p></PageLayout>;

  const classCount = classes.length;

  return (
    <PageLayout title="Teacher" role="teacher">
      <div className="school-admin-shell">
        <aside className="school-admin-nav" aria-label="Teacher sections">
          <button type="button" className={`school-admin-nav-btn ${activeView === 'overview' ? 'is-active' : ''}`} onClick={() => setActiveView('overview')}>
            Overview
          </button>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'students' ? 'is-active' : ''}`} onClick={() => setActiveView('students')}>
            My students
          </button>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'attendance' ? 'is-active' : ''}`} onClick={() => setActiveView('attendance')} disabled={classes.length === 0}>
            Attendance
          </button>
        </aside>

        <section className="school-admin-view">
          {activeView === 'overview' && (
            <>
              <section aria-label="Classroom snapshot">
                <div className="dashboard-grid">
                  <article className="dashboard-card dashboard-card--highlight">
                    <p className="dashboard-label">Students you teach</p>
                    <p className="dashboard-value">{students.length}</p>
                    <p className="dashboard-sub">Across your assigned classes (tenant-scoped).</p>
                  </article>
                  <article className="dashboard-card">
                    <p className="dashboard-label">Classes</p>
                    <p className="dashboard-value">{classCount}</p>
                    <p className="dashboard-sub">Distinct classes on your timetable.</p>
                  </article>
                  <article className="dashboard-card">
                    <p className="dashboard-label">Profile</p>
                    <p className="dashboard-value">{me ? 'Complete' : '—'}</p>
                    <p className="dashboard-sub">Photo and contact details.</p>
                  </article>
                </div>
              </section>

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
            </>
          )}

          {activeView === 'students' && (
            <>
              <h2 className="section-title">My students</h2>
              {students.length === 0 ? (
                <p className="empty-state">No classes or students assigned yet. Your School Admin will assign your classes and subjects.</p>
              ) : (
                <>
                  <div className="form-actions" style={{ marginBottom: '0.75rem', flexWrap: 'wrap' }}>
                    <input
                      type="search"
                      className="form-input"
                      style={{ maxWidth: '260px' }}
                      placeholder="Search by name, admission no, class or grade"
                      value={studentSearch}
                      onChange={(e) => setStudentSearch(e.target.value)}
                    />
                    <select className="form-input" style={{ maxWidth: '180px' }} value={studentClassFilter} onChange={(e) => setStudentClassFilter(e.target.value)}>
                      <option value="">All classes</option>
                      {classes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                    </select>
                    <select className="form-input" style={{ maxWidth: '180px' }} value={studentGradeFilter} onChange={(e) => setStudentGradeFilter(e.target.value)}>
                      <option value="">All grades</option>
                      {gradeOptions.map((grade) => <option key={grade} value={grade}>{grade}</option>)}
                    </select>
                  </div>
                  <p className="card-desc">Showing {filteredStudents.length} of {students.length} students.</p>
                  {filteredStudents.length === 0 ? (
                    <p className="empty-state">No students match your current search or filters.</p>
                  ) : (
                    <div className="data-table-wrap">
                      <table className="data-table">
                        <thead>
                          <tr>
                            <th style={{ width: '48px' }}>Photo</th>
                            <th>Name</th>
                            <th>Admission #</th>
                            <th>Class</th>
                            <th>Grade</th>
                            <th>Gender</th>
                            <th>Today&apos;s attendance</th>
                          </tr>
                        </thead>
                        <tbody>
                          {filteredStudents.map((s) => (
                            <tr key={s.studentId}>
                              <td><StudentPhoto studentId={s.studentId} firstName={s.firstName} lastName={s.lastName} size={40} /></td>
                              <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                              <td>{s.admissionNumber || '—'}</td>
                              <td>{s.className || '—'}</td>
                              <td>{s.gradeName || '—'}</td>
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
                </>
              )}
            </>
          )}

          {activeView === 'attendance' && classes.length > 0 && (
            <section aria-label="Quick attendance capture">
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
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {students.filter((s) => s.classId === selectedClassId).map((s) => (
                      <tr key={s.studentId}>
                        <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
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
            </section>
          )}
        </section>
      </div>
    </PageLayout>
  );
}
