import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
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
  const [showProfileDetails, setShowProfileDetails] = useState(false);
  const [savingProfile, setSavingProfile] = useState(false);
  const [profileForm, setProfileForm] = useState({
    firstName: '',
    lastName: '',
    middleName: '',
    phone: '',
    whatsAppNumber: '',
    dateOfBirth: '',
    gender: '',
    nationality: '',
    stateOfOrigin: '',
    lga: '',
    religion: '',
    residentialAddress: '',
    subjectSpecialization: '',
    highestQualification: '',
    fieldOfStudy: '',
    yearsOfExperience: '',
    previousSchools: '',
    professionalBodies: '',
  });
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

  useEffect(() => {
    if (!me) return;
    setProfileForm({
      firstName: me.firstName || '',
      lastName: me.lastName || '',
      middleName: me.middleName || '',
      phone: me.phone || '',
      whatsAppNumber: me.whatsAppNumber || '',
      dateOfBirth: me.dateOfBirth || '',
      gender: me.gender || '',
      nationality: me.nationality || '',
      stateOfOrigin: me.stateOfOrigin || '',
      lga: me.lga || '',
      religion: me.religion || '',
      residentialAddress: me.residentialAddress || '',
      subjectSpecialization: me.subjectSpecialization || '',
      highestQualification: me.highestQualification || '',
      fieldOfStudy: me.fieldOfStudy || '',
      yearsOfExperience: me.yearsOfExperience ?? '',
      previousSchools: me.previousSchools || '',
      professionalBodies: me.professionalBodies || '',
    });
  }, [me]);

  const updateProfileField = (field, value) => {
    setProfileForm((prev) => ({ ...prev, [field]: value }));
  };

  const saveProfile = async () => {
    if (!me) return;
    setSavingProfile(true);
    try {
      const payload = {
        firstName: profileForm.firstName.trim(),
        lastName: profileForm.lastName.trim(),
        middleName: profileForm.middleName.trim() || null,
        phone: profileForm.phone.trim() || null,
        whatsAppNumber: profileForm.whatsAppNumber.trim() || null,
        dateOfBirth: profileForm.dateOfBirth || null,
        gender: profileForm.gender.trim() || null,
        nationality: profileForm.nationality.trim() || null,
        stateOfOrigin: profileForm.stateOfOrigin.trim() || null,
        lga: profileForm.lga.trim() || null,
        religion: profileForm.religion.trim() || null,
        residentialAddress: profileForm.residentialAddress.trim() || null,
        subjectSpecialization: profileForm.subjectSpecialization.trim() || null,
        highestQualification: profileForm.highestQualification.trim() || null,
        fieldOfStudy: profileForm.fieldOfStudy.trim() || null,
        yearsOfExperience: profileForm.yearsOfExperience === '' ? null : Number(profileForm.yearsOfExperience),
        previousSchools: profileForm.previousSchools.trim() || null,
        professionalBodies: profileForm.professionalBodies.trim() || null,
      };
      const res = await apiFetch('/api/teachers/me', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error(await res.text());
      const updated = await res.json();
      setMe(updated);
      // eslint-disable-next-line no-alert
      alert('Profile updated successfully. School Admin can now see your latest details.');
    } catch (err) {
      // eslint-disable-next-line no-alert
      alert(err.message || 'Could not update profile right now.');
    } finally {
      setSavingProfile(false);
    }
  };

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
              <div className="dashboard-actions" style={{ flexWrap: 'wrap', marginBottom: '1rem' }}>
                <button type="button" className="btn-primary-action" onClick={() => setActiveView('students')}>
                  Open my students
                </button>
                <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => setActiveView('attendance')} disabled={classes.length === 0}>
                  Attendance
                </button>
                <Link to="/teacher/grading" className="btn-primary-action btn-primary-action--ghost">
                  Grading workspace
                </Link>
              </div>
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
                      <p className="card-desc">Role: {me.roleTitle || 'Teacher'} • Department: {me.department || '—'}</p>
                      {!showProfileDetails ? (
                        <p className="card-desc">Your personal contact details are hidden until you click <strong>View details</strong>.</p>
                      ) : (
                        <>
                          <p className="card-desc">Email: {me.email || '—'} • Phone: {me.phone || '—'}</p>
                          <p className="card-desc">Highest qualification: {me.highestQualification || '—'} • Religion: {me.religion || '—'}</p>
                        </>
                      )}
                    </div>
                  </div>
                  {showProfileDetails && (
                    <div className="form-grid" style={{ marginTop: '0.75rem' }}>
                      <label className="form-field">First name
                        <input className="form-input" value={profileForm.firstName} onChange={(e) => updateProfileField('firstName', e.target.value)} />
                      </label>
                      <label className="form-field">Middle name
                        <input className="form-input" value={profileForm.middleName} onChange={(e) => updateProfileField('middleName', e.target.value)} />
                      </label>
                      <label className="form-field">Last name
                        <input className="form-input" value={profileForm.lastName} onChange={(e) => updateProfileField('lastName', e.target.value)} />
                      </label>
                      <label className="form-field">Phone
                        <input className="form-input" value={profileForm.phone} onChange={(e) => updateProfileField('phone', e.target.value)} />
                      </label>
                      <label className="form-field">WhatsApp number
                        <input className="form-input" value={profileForm.whatsAppNumber} onChange={(e) => updateProfileField('whatsAppNumber', e.target.value)} />
                      </label>
                      <label className="form-field">Date of birth
                        <input type="date" className="form-input" value={profileForm.dateOfBirth || ''} onChange={(e) => updateProfileField('dateOfBirth', e.target.value)} />
                      </label>
                      <label className="form-field">Gender
                        <input className="form-input" value={profileForm.gender} onChange={(e) => updateProfileField('gender', e.target.value)} />
                      </label>
                      <label className="form-field">Nationality
                        <input className="form-input" value={profileForm.nationality} onChange={(e) => updateProfileField('nationality', e.target.value)} />
                      </label>
                      <label className="form-field">State
                        <input className="form-input" value={profileForm.stateOfOrigin} onChange={(e) => updateProfileField('stateOfOrigin', e.target.value)} />
                      </label>
                      <label className="form-field">LGA
                        <input className="form-input" value={profileForm.lga} onChange={(e) => updateProfileField('lga', e.target.value)} />
                      </label>
                      <label className="form-field">Religion
                        <input className="form-input" value={profileForm.religion} onChange={(e) => updateProfileField('religion', e.target.value)} />
                      </label>
                      <label className="form-field">Years of experience
                        <input type="number" min="0" className="form-input" value={profileForm.yearsOfExperience} onChange={(e) => updateProfileField('yearsOfExperience', e.target.value)} />
                      </label>
                      <label className="form-field" style={{ gridColumn: '1 / -1' }}>Residential address
                        <input className="form-input" value={profileForm.residentialAddress} onChange={(e) => updateProfileField('residentialAddress', e.target.value)} />
                      </label>
                      <label className="form-field">Subject specialization
                        <input className="form-input" value={profileForm.subjectSpecialization} onChange={(e) => updateProfileField('subjectSpecialization', e.target.value)} />
                      </label>
                      <label className="form-field">Highest qualification
                        <input className="form-input" value={profileForm.highestQualification} onChange={(e) => updateProfileField('highestQualification', e.target.value)} />
                      </label>
                      <label className="form-field">Field of study
                        <input className="form-input" value={profileForm.fieldOfStudy} onChange={(e) => updateProfileField('fieldOfStudy', e.target.value)} />
                      </label>
                      <label className="form-field">Previous schools
                        <input className="form-input" value={profileForm.previousSchools} onChange={(e) => updateProfileField('previousSchools', e.target.value)} />
                      </label>
                      <label className="form-field">Professional bodies
                        <input className="form-input" value={profileForm.professionalBodies} onChange={(e) => updateProfileField('professionalBodies', e.target.value)} />
                      </label>
                    </div>
                  )}
                  <div className="form-actions" style={{ marginTop: '0.5rem' }}>
                    <button
                      type="button"
                      className="btn-primary-action btn-primary-action--ghost"
                      onClick={() => setShowProfileDetails((current) => !current)}
                    >
                      {showProfileDetails ? 'Hide details' : 'View details'}
                    </button>
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
                    {showProfileDetails && (
                      <button
                        type="button"
                        className="btn-primary-action"
                        onClick={saveProfile}
                        disabled={savingProfile || !profileForm.firstName.trim() || !profileForm.lastName.trim()}
                      >
                        {savingProfile ? 'Saving…' : 'Save profile'}
                      </button>
                    )}
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
