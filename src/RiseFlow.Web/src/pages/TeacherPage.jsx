import { useState, useEffect, useMemo, useRef } from 'react';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import TeacherPhoto from '../components/TeacherPhoto';
import StudentRecordPanel from '../components/StudentRecordPanel';
import { apiFetch } from '../api';
import './RolePages.css';

const EMPTY_PROFILE_FORM = {
  firstName: '',
  lastName: '',
  middleName: '',
  phone: '',
  whatsAppNumber: '',
  staffId: '',
  subjectSpecialization: '',
  dateOfBirth: '',
  gender: '',
  nationality: '',
  stateOfOrigin: '',
  lga: '',
  religion: '',
  nin: '',
  nationalIdType: '',
  nationalIdNumber: '',
  trcnNumber: '',
  residentialAddress: '',
  highestQualification: '',
  fieldOfStudy: '',
  yearsOfExperience: '',
  previousSchools: '',
  professionalBodies: '',
};

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
  const [selectedStudentId, setSelectedStudentId] = useState(null);
  const [profileForm, setProfileForm] = useState(EMPTY_PROFILE_FORM);
  const [savingProfile, setSavingProfile] = useState(false);
  const [profileMessage, setProfileMessage] = useState(null);
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

  useEffect(() => {
    if (!me) {
      setProfileForm(EMPTY_PROFILE_FORM);
      return;
    }

    setProfileForm({
      firstName: me.firstName || '',
      lastName: me.lastName || '',
      middleName: me.middleName || '',
      phone: me.phone || '',
      whatsAppNumber: me.whatsAppNumber || '',
      staffId: me.staffId || '',
      subjectSpecialization: me.subjectSpecialization || '',
      dateOfBirth: me.dateOfBirth || '',
      gender: me.gender || '',
      nationality: me.nationality || '',
      stateOfOrigin: me.stateOfOrigin || '',
      lga: me.lga || '',
      religion: me.religion || '',
      nin: me.nin || '',
      nationalIdType: me.nationalIdType || '',
      nationalIdNumber: me.nationalIdNumber || '',
      trcnNumber: me.trcnNumber || '',
      residentialAddress: me.residentialAddress || '',
      highestQualification: me.highestQualification || '',
      fieldOfStudy: me.fieldOfStudy || '',
      yearsOfExperience: me.yearsOfExperience ?? '',
      previousSchools: me.previousSchools || '',
      professionalBodies: me.professionalBodies || '',
    });
    setProfileMessage(null);
  }, [me]);

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

  const handleProfileFieldChange = (e) => {
    const { name, value } = e.target;
    setProfileForm((prev) => ({ ...prev, [name]: value }));
    setProfileMessage(null);
  };

  const saveProfileSettings = async (e) => {
    e.preventDefault();
    const firstName = profileForm.firstName.trim();
    const lastName = profileForm.lastName.trim();

    if (!firstName || !lastName) {
      setProfileMessage({ type: 'error', text: 'First name and last name are required.' });
      return;
    }

    setSavingProfile(true);
    setProfileMessage(null);
    try {
      const payload = {
        firstName,
        lastName,
        middleName: profileForm.middleName.trim() || null,
        phone: profileForm.phone.trim() || null,
        whatsAppNumber: profileForm.whatsAppNumber.trim() || null,
        staffId: profileForm.staffId.trim() || null,
        subjectSpecialization: profileForm.subjectSpecialization.trim() || null,
        dateOfBirth: profileForm.dateOfBirth || null,
        gender: profileForm.gender.trim() || null,
        nationality: profileForm.nationality.trim() || null,
        stateOfOrigin: profileForm.stateOfOrigin.trim() || null,
        lga: profileForm.lga.trim() || null,
        religion: profileForm.religion.trim() || null,
        nin: profileForm.nin.trim() || null,
        nationalIdType: profileForm.nationalIdType.trim() || null,
        nationalIdNumber: profileForm.nationalIdNumber.trim() || null,
        trcnNumber: profileForm.trcnNumber.trim() || null,
        residentialAddress: profileForm.residentialAddress.trim() || null,
        highestQualification: profileForm.highestQualification.trim() || null,
        fieldOfStudy: profileForm.fieldOfStudy.trim() || null,
        yearsOfExperience: profileForm.yearsOfExperience === '' ? null : Number(profileForm.yearsOfExperience),
        previousSchools: profileForm.previousSchools.trim() || null,
        professionalBodies: profileForm.professionalBodies.trim() || null,
      };

      const res = await apiFetch('/api/teachers/me/profile', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      const data = await res.json().catch(() => null);
      if (!res.ok) {
        throw new Error(data?.message || data?.title || 'Could not save your profile settings.');
      }

      setMe(data);
      setProfileMessage({ type: 'success', text: 'Profile settings updated successfully.' });
    } catch (err) {
      setProfileMessage({ type: 'error', text: err.message || 'Could not save your profile settings.' });
    } finally {
      setSavingProfile(false);
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
                  <div style={{ display: 'flex', gap: '1rem', alignItems: 'center', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
                    <TeacherPhoto teacherId={me.id} fullName={`${me.firstName} ${me.lastName}`} size={56} />
                    <div>
                      <h3 className="card-title" style={{ margin: 0 }}>{[me.firstName, me.middleName, me.lastName].filter(Boolean).join(' ')}</h3>
                      <p className="card-desc">Email: {me.email || '—'} • Phone: {me.phone || '—'}</p>
                      <p className="card-desc">Role: {me.roleTitle || 'Teacher'} • Department: {me.department || '—'}</p>
                      <p className="card-desc">Highest qualification: {me.highestQualification || '—'} • Experience: {me.yearsOfExperience ?? '—'} year(s)</p>
                    </div>
                  </div>
                  <div className="form-actions" style={{ marginTop: '0.5rem', marginBottom: '1rem' }}>
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

                  <div className="dashboard-grid" style={{ marginBottom: '1rem' }}>
                    <article className="dashboard-card">
                      <p className="dashboard-label">Classes assigned</p>
                      <p className="dashboard-value">{me.assignedClassCount ?? classCount}</p>
                      <p className="dashboard-sub">Classes currently linked to your profile.</p>
                    </article>
                    <article className="dashboard-card">
                      <p className="dashboard-label">Students handled</p>
                      <p className="dashboard-value">{me.assignedStudentCount ?? students.length}</p>
                      <p className="dashboard-sub">Students visible in your teacher dashboard.</p>
                    </article>
                    <article className="dashboard-card">
                      <p className="dashboard-label">Assigned classes</p>
                      <p className="dashboard-sub">
                        {Array.isArray(me.teacherClasses) && me.teacherClasses.length > 0
                          ? me.teacherClasses.map((c) => c.className).join(', ')
                          : 'No classes assigned yet.'}
                      </p>
                    </article>
                  </div>

                  <h3 className="card-title" style={{ marginBottom: '0.35rem' }}>Profile settings</h3>
                  <p className="card-desc" style={{ marginBottom: '0.75rem' }}>Update your information here. School Admin will see the latest details.</p>

                  {profileMessage && (
                    <p
                      className={profileMessage.type === 'error' ? 'empty-state empty-state--error' : 'card-desc'}
                      style={profileMessage.type === 'success' ? { color: '#166534', marginBottom: '0.75rem' } : undefined}
                    >
                      {profileMessage.text}
                    </p>
                  )}

                  <form onSubmit={saveProfileSettings}>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '0.75rem' }}>
                      <label>
                        <span className="dashboard-label">First name</span>
                        <input className="form-input" name="firstName" value={profileForm.firstName} onChange={handleProfileFieldChange} required />
                      </label>
                      <label>
                        <span className="dashboard-label">Middle name</span>
                        <input className="form-input" name="middleName" value={profileForm.middleName} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Last name</span>
                        <input className="form-input" name="lastName" value={profileForm.lastName} onChange={handleProfileFieldChange} required />
                      </label>
                      <label>
                        <span className="dashboard-label">Phone</span>
                        <input className="form-input" name="phone" value={profileForm.phone} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">WhatsApp</span>
                        <input className="form-input" name="whatsAppNumber" value={profileForm.whatsAppNumber} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Staff ID</span>
                        <input className="form-input" name="staffId" value={profileForm.staffId} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Subject specialization</span>
                        <input className="form-input" name="subjectSpecialization" value={profileForm.subjectSpecialization} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Date of birth</span>
                        <input className="form-input" type="date" name="dateOfBirth" value={profileForm.dateOfBirth} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Gender</span>
                        <input className="form-input" name="gender" value={profileForm.gender} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Nationality</span>
                        <input className="form-input" name="nationality" value={profileForm.nationality} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">State of origin</span>
                        <input className="form-input" name="stateOfOrigin" value={profileForm.stateOfOrigin} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">LGA</span>
                        <input className="form-input" name="lga" value={profileForm.lga} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Religion</span>
                        <input className="form-input" name="religion" value={profileForm.religion} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">TRCN number</span>
                        <input className="form-input" name="trcnNumber" value={profileForm.trcnNumber} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">NIN</span>
                        <input className="form-input" name="nin" value={profileForm.nin} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">National ID type</span>
                        <input className="form-input" name="nationalIdType" value={profileForm.nationalIdType} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">National ID number</span>
                        <input className="form-input" name="nationalIdNumber" value={profileForm.nationalIdNumber} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Highest qualification</span>
                        <input className="form-input" name="highestQualification" value={profileForm.highestQualification} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Field of study</span>
                        <input className="form-input" name="fieldOfStudy" value={profileForm.fieldOfStudy} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Years of experience</span>
                        <input className="form-input" type="number" min="0" name="yearsOfExperience" value={profileForm.yearsOfExperience} onChange={handleProfileFieldChange} />
                      </label>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '0.75rem', marginTop: '0.75rem' }}>
                      <label>
                        <span className="dashboard-label">Residential address</span>
                        <textarea className="form-input" name="residentialAddress" rows="2" value={profileForm.residentialAddress} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Previous schools</span>
                        <textarea className="form-input" name="previousSchools" rows="2" value={profileForm.previousSchools} onChange={handleProfileFieldChange} />
                      </label>
                      <label>
                        <span className="dashboard-label">Professional bodies</span>
                        <textarea className="form-input" name="professionalBodies" rows="2" value={profileForm.professionalBodies} onChange={handleProfileFieldChange} />
                      </label>
                    </div>

                    <div className="form-actions" style={{ marginTop: '0.85rem', alignItems: 'center' }}>
                      <button type="submit" className="btn-excel btn-generate" disabled={savingProfile}>
                        {savingProfile ? 'Saving…' : 'Save profile settings'}
                      </button>
                      <span className="card-desc">School-managed items like account status, salary, and official role remain read-only.</span>
                    </div>
                  </form>
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
                  <p className="card-desc">Showing {filteredStudents.length} of {students.length} students. Open a student record to see the information your School Admin has allowed teachers to view.</p>
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
                            <th>Record</th>
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
                              <td>
                                <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => setSelectedStudentId(s.studentId)}>
                                  Open record
                                </button>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                  {selectedStudentId && (
                    <StudentRecordPanel
                      studentId={selectedStudentId}
                      role="teacher"
                      onClose={() => setSelectedStudentId(null)}
                    />
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
