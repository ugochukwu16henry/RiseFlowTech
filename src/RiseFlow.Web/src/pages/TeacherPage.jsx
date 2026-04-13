import { useState, useEffect, useRef } from 'react';
import { Link, useLocation } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import TeacherPhoto from '../components/TeacherPhoto';
import StudentRecordPanel from '../components/StudentRecordPanel';
import { apiFetch } from '../api';
import './RolePages.css';

export default function TeacherPage() {
  const location = useLocation();
  const [me, setMe] = useState(null);
  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [selectedClassId, setSelectedClassId] = useState('');
  const [selectedDate, setSelectedDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [selectedStudentId, setSelectedStudentId] = useState(null);
  const [attendance, setAttendance] = useState({});
  const [savingAttendance, setSavingAttendance] = useState(false);
  const [activeView, setActiveView] = useState('overview');
  const [showProfileDetails, setShowProfileDetails] = useState(false);
  const [savingProfile, setSavingProfile] = useState(false);
  const [terms, setTerms] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [exams, setExams] = useState([]);
  const [loadingGrading, setLoadingGrading] = useState(false);
  const [savingExam, setSavingExam] = useState(false);
  const [savingResult, setSavingResult] = useState(false);
  const [gradingMessage, setGradingMessage] = useState(null);
  const [submissionWindow, setSubmissionWindow] = useState(null);
  const [notices, setNotices] = useState([]);
  const [events, setEvents] = useState([]);
  const [examForm, setExamForm] = useState({ name: '', classId: '', subjectId: '', termId: '', startDateUtc: '', endDateUtc: '' });
  const [resultForm, setResultForm] = useState({ studentId: '', subjectId: '', termId: '', examId: '', assessmentType: 'Exam', score: '', maxScore: '100', gradeLetter: '', comment: '' });
  const [fieldSettings, setFieldSettings] = useState([]);
  const [customFieldValues, setCustomFieldValues] = useState({});
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
    if (location.pathname.startsWith('/teacher/grading')) setActiveView('grading');
  }, [location.pathname]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    Promise.all([
      apiFetch('/api/teachers/me').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/teachers/my-students').then((r) => (r.ok ? r.json() : [])),
    ])
      .then(([profileConfig, list]) => {
        if (cancelled) return;
        const profile = profileConfig?.teacher || profileConfig || null;
        setMe(profile);
        setFieldSettings(Array.isArray(profileConfig?.fieldSettings) ? profileConfig.fieldSettings : []);
        setCustomFieldValues(profileConfig?.customFields && typeof profileConfig.customFields === 'object' ? profileConfig.customFields : {});
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
    let cancelled = false;
    Promise.all([
      apiFetch('/api/notices?limit=6').then((r) => (r.ok ? r.json() : [])),
      apiFetch('/api/events?limit=6').then((r) => (r.ok ? r.json() : [])),
    ])
      .then(([noticeList, eventList]) => {
        if (cancelled) return;
        setNotices(Array.isArray(noticeList) ? noticeList : []);
        setEvents(Array.isArray(eventList) ? eventList : []);
      })
      .catch(() => {
        if (cancelled) return;
        setNotices([]);
        setEvents([]);
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

  useEffect(() => {
    if (activeView !== 'grading') return;
    let cancelled = false;
    setLoadingGrading(true);
    setGradingMessage(null);

    Promise.all([
      apiFetch('/api/academicterms').then((r) => (r.ok ? r.json() : [])),
      apiFetch('/api/subjects').then((r) => (r.ok ? r.json() : [])),
      apiFetch('/api/exams').then((r) => (r.ok ? r.json() : [])),
    ])
      .then(([termList, subjectList, examList]) => {
        if (cancelled) return;
        const safeTerms = Array.isArray(termList) ? termList : [];
        const safeSubjects = Array.isArray(subjectList) ? subjectList : [];
        const safeExams = Array.isArray(examList) ? examList : [];
        setTerms(safeTerms);
        setSubjects(safeSubjects);
        setExams(safeExams);

        if (safeTerms.length > 0) {
          const termId = safeTerms[0].id;
          setExamForm((prev) => ({ ...prev, termId: prev.termId || termId }));
          setResultForm((prev) => ({ ...prev, termId: prev.termId || termId }));
          apiFetch(`/api/exams/submission-window?termId=${termId}`)
            .then(async (res) => {
              if (!res.ok) return null;
              return res.json();
            })
            .then((data) => {
              if (!cancelled) setSubmissionWindow(data);
            })
            .catch(() => {
              if (!cancelled) setSubmissionWindow(null);
            });
        }

        if (safeSubjects.length > 0) {
          const subjectId = safeSubjects[0].id;
          setExamForm((prev) => ({ ...prev, subjectId: prev.subjectId || subjectId }));
          setResultForm((prev) => ({ ...prev, subjectId: prev.subjectId || subjectId }));
        }
      })
      .catch((e) => {
        if (!cancelled) setGradingMessage(e.message || 'Could not load grading workspace.');
      })
      .finally(() => {
        if (!cancelled) setLoadingGrading(false);
      });

    return () => { cancelled = true; };
  }, [activeView]);

  const settingByKey = (fieldKey) => fieldSettings.find((s) => s.fieldKey === fieldKey);
  const isFieldVisible = (fieldKey) => {
    const s = settingByKey(fieldKey);
    return s ? !!s.isVisibleToTeacher : true;
  };
  const isFieldEditable = (fieldKey) => {
    const s = settingByKey(fieldKey);
    return s ? (!!s.isEditableByTeacher && !!s.isVisibleToTeacher && !s.isAdminOnly) : true;
  };

  const visibleCustomFieldSettings = fieldSettings
    .filter((s) => s.isCustom && s.isVisibleToTeacher)
    .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));

  const setCustomFieldValue = (fieldKey, value) => {
    setCustomFieldValues((prev) => ({ ...prev, [fieldKey]: value }));
  };

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
        customFields: Object.fromEntries(
          visibleCustomFieldSettings
            .filter((s) => isFieldEditable(s.fieldKey))
            .map((s) => [s.fieldKey, (customFieldValues[s.fieldKey] || '').trim() || null]),
        ),
      };
      const res = await apiFetch('/api/teachers/me', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error(await res.text());
      const updated = await res.json();
      setMe(updated?.teacher || updated);
      setFieldSettings(Array.isArray(updated?.fieldSettings) ? updated.fieldSettings : fieldSettings);
      setCustomFieldValues(updated?.customFields && typeof updated.customFields === 'object' ? updated.customFields : customFieldValues);
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

  const studentsInSelectedResultClass = students.filter((s) => s.classId === (resultForm.classId || selectedClassId));

  const refreshExams = async () => {
    const res = await apiFetch('/api/exams');
    if (!res.ok) throw new Error(await res.text());
    const list = await res.json();
    setExams(Array.isArray(list) ? list : []);
  };

  const loadSubmissionWindow = async (termId) => {
    if (!termId) return;
    const res = await apiFetch(`/api/exams/submission-window?termId=${termId}`);
    if (!res.ok) return;
    const data = await res.json();
    setSubmissionWindow(data);
  };

  const createExam = async () => {
    if (!examForm.name.trim() || !examForm.classId || !examForm.subjectId || !examForm.termId) {
      setGradingMessage('Exam name, class, subject, and term are required.');
      return;
    }

    setSavingExam(true);
    setGradingMessage(null);
    try {
      const res = await apiFetch('/api/exams', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: examForm.name.trim(),
          classId: examForm.classId,
          subjectId: examForm.subjectId,
          termId: examForm.termId,
          startDateUtc: examForm.startDateUtc || null,
          endDateUtc: examForm.endDateUtc || null,
        }),
      });
      if (!res.ok) throw new Error(await res.text());
      await refreshExams();
      setExamForm((prev) => ({ ...prev, name: '', startDateUtc: '', endDateUtc: '' }));
      setGradingMessage('Exam created.');
    } catch (e) {
      setGradingMessage(e.message || 'Could not create exam.');
    } finally {
      setSavingExam(false);
    }
  };

  const submitResult = async () => {
    if (!resultForm.studentId || !resultForm.subjectId || !resultForm.termId || resultForm.score === '' || resultForm.maxScore === '') {
      setGradingMessage('Student, subject, term, score and max score are required.');
      return;
    }

    setSavingResult(true);
    setGradingMessage(null);
    try {
      const payload = {
        studentId: resultForm.studentId,
        subjectId: resultForm.subjectId,
        termId: resultForm.termId,
        assessmentType: resultForm.assessmentType || 'Exam',
        score: Number(resultForm.score),
        maxScore: Number(resultForm.maxScore),
        gradeLetter: resultForm.gradeLetter?.trim() || null,
        comment: resultForm.comment?.trim() || null,
        examId: resultForm.examId || null,
      };

      const res = await apiFetch('/api/results', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error(await res.text());
      setResultForm((prev) => ({ ...prev, score: '', gradeLetter: '', comment: '' }));
      setGradingMessage('Result submitted successfully.');
    } catch (e) {
      setGradingMessage(e.message || 'Could not submit result.');
    } finally {
      setSavingResult(false);
    }
  };

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
          <button type="button" className={`school-admin-nav-btn ${activeView === 'grading' ? 'is-active' : ''}`} onClick={() => setActiveView('grading')}>
            Grading
          </button>
          <Link to="/teacher/assignments" className="school-admin-nav-btn school-admin-nav-link">
            Assignments
          </Link>
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
                <Link to="/teacher/assignments" className="btn-primary-action btn-primary-action--ghost">
                  Assignments
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

              <section className="dashboard-grid" style={{ marginTop: '1rem' }} aria-label="School communications">
                <article className="dashboard-card">
                  <h3 className="card-title">Latest notices</h3>
                  {notices.length === 0 ? (
                    <p className="card-desc">No notices shared with teachers yet.</p>
                  ) : (
                    <ul className="student-record-list">
                      {notices.map((notice) => (
                        <li key={notice.id}>
                          <strong>{notice.title}</strong>
                          <span>{notice.body}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </article>
                <article className="dashboard-card">
                  <h3 className="card-title">Upcoming events</h3>
                  {events.length === 0 ? (
                    <p className="card-desc">No upcoming events yet.</p>
                  ) : (
                    <ul className="student-record-list">
                      {events.map((event) => (
                        <li key={event.id}>
                          <strong>{event.title}</strong>
                          <span>{event.startAtUtc ? new Date(event.startAtUtc).toLocaleString() : 'Date pending'}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </article>
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
                          <p className="card-desc">Email: {me.email || '—'} • Phone: {isFieldVisible('phone') ? (me.phone || '—') : 'Hidden by school admin'}</p>
                          <p className="card-desc">Highest qualification: {isFieldVisible('highestQualification') ? (me.highestQualification || '—') : 'Hidden by school admin'} • Religion: {isFieldVisible('religion') ? (me.religion || '—') : 'Hidden by school admin'}</p>
                        </>
                      )}
                    </div>
                  </div>
                  {showProfileDetails && (
                    <div className="form-grid" style={{ marginTop: '0.75rem' }}>
                      {isFieldVisible('firstName') && (<label className="form-field">First name
                        <input className="form-input" value={profileForm.firstName} onChange={(e) => updateProfileField('firstName', e.target.value)} disabled={!isFieldEditable('firstName')} />
                      </label>)}
                      {isFieldVisible('middleName') && (<label className="form-field">Middle name
                        <input className="form-input" value={profileForm.middleName} onChange={(e) => updateProfileField('middleName', e.target.value)} disabled={!isFieldEditable('middleName')} />
                      </label>)}
                      {isFieldVisible('lastName') && (<label className="form-field">Last name
                        <input className="form-input" value={profileForm.lastName} onChange={(e) => updateProfileField('lastName', e.target.value)} disabled={!isFieldEditable('lastName')} />
                      </label>)}
                      {isFieldVisible('phone') && (<label className="form-field">Phone
                        <input className="form-input" value={profileForm.phone} onChange={(e) => updateProfileField('phone', e.target.value)} disabled={!isFieldEditable('phone')} />
                      </label>)}
                      {isFieldVisible('whatsAppNumber') && (<label className="form-field">WhatsApp number
                        <input className="form-input" value={profileForm.whatsAppNumber} onChange={(e) => updateProfileField('whatsAppNumber', e.target.value)} disabled={!isFieldEditable('whatsAppNumber')} />
                      </label>)}
                      {isFieldVisible('dateOfBirth') && (<label className="form-field">Date of birth
                        <input type="date" className="form-input" value={profileForm.dateOfBirth || ''} onChange={(e) => updateProfileField('dateOfBirth', e.target.value)} disabled={!isFieldEditable('dateOfBirth')} />
                      </label>)}
                      {isFieldVisible('gender') && (<label className="form-field">Gender
                        <input className="form-input" value={profileForm.gender} onChange={(e) => updateProfileField('gender', e.target.value)} disabled={!isFieldEditable('gender')} />
                      </label>)}
                      {isFieldVisible('nationality') && (<label className="form-field">Nationality
                        <input className="form-input" value={profileForm.nationality} onChange={(e) => updateProfileField('nationality', e.target.value)} disabled={!isFieldEditable('nationality')} />
                      </label>)}
                      {isFieldVisible('stateOfOrigin') && (<label className="form-field">State
                        <input className="form-input" value={profileForm.stateOfOrigin} onChange={(e) => updateProfileField('stateOfOrigin', e.target.value)} disabled={!isFieldEditable('stateOfOrigin')} />
                      </label>)}
                      {isFieldVisible('lga') && (<label className="form-field">LGA
                        <input className="form-input" value={profileForm.lga} onChange={(e) => updateProfileField('lga', e.target.value)} disabled={!isFieldEditable('lga')} />
                      </label>)}
                      {isFieldVisible('religion') && (<label className="form-field">Religion
                        <input className="form-input" value={profileForm.religion} onChange={(e) => updateProfileField('religion', e.target.value)} disabled={!isFieldEditable('religion')} />
                      </label>)}
                      {isFieldVisible('yearsOfExperience') && (<label className="form-field">Years of experience
                        <input type="number" min="0" className="form-input" value={profileForm.yearsOfExperience} onChange={(e) => updateProfileField('yearsOfExperience', e.target.value)} disabled={!isFieldEditable('yearsOfExperience')} />
                      </label>)}
                      {isFieldVisible('residentialAddress') && (<label className="form-field" style={{ gridColumn: '1 / -1' }}>Residential address
                        <input className="form-input" value={profileForm.residentialAddress} onChange={(e) => updateProfileField('residentialAddress', e.target.value)} disabled={!isFieldEditable('residentialAddress')} />
                      </label>)}
                      {isFieldVisible('subjectSpecialization') && (<label className="form-field">Subject specialization
                        <input className="form-input" value={profileForm.subjectSpecialization} onChange={(e) => updateProfileField('subjectSpecialization', e.target.value)} disabled={!isFieldEditable('subjectSpecialization')} />
                      </label>)}
                      {isFieldVisible('highestQualification') && (<label className="form-field">Highest qualification
                        <input className="form-input" value={profileForm.highestQualification} onChange={(e) => updateProfileField('highestQualification', e.target.value)} disabled={!isFieldEditable('highestQualification')} />
                      </label>)}
                      {isFieldVisible('fieldOfStudy') && (<label className="form-field">Field of study
                        <input className="form-input" value={profileForm.fieldOfStudy} onChange={(e) => updateProfileField('fieldOfStudy', e.target.value)} disabled={!isFieldEditable('fieldOfStudy')} />
                      </label>)}
                      {isFieldVisible('previousSchools') && (<label className="form-field">Previous schools
                        <input className="form-input" value={profileForm.previousSchools} onChange={(e) => updateProfileField('previousSchools', e.target.value)} disabled={!isFieldEditable('previousSchools')} />
                      </label>)}
                      {isFieldVisible('professionalBodies') && (<label className="form-field">Professional bodies
                        <input className="form-input" value={profileForm.professionalBodies} onChange={(e) => updateProfileField('professionalBodies', e.target.value)} disabled={!isFieldEditable('professionalBodies')} />
                      </label>)}
                      {visibleCustomFieldSettings.map((setting) => (
                        <label key={setting.fieldKey} className="form-field">
                          {setting.displayName}
                          <input
                            className="form-input"
                            value={customFieldValues[setting.fieldKey] || ''}
                            onChange={(e) => setCustomFieldValue(setting.fieldKey, e.target.value)}
                            disabled={!isFieldEditable(setting.fieldKey)}
                          />
                        </label>
                      ))}
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
                        <th>Record</th>
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
                          <td>
                            <button
                              type="button"
                              className="btn-primary-action btn-primary-action--ghost"
                              onClick={() => setSelectedStudentId(s.studentId)}
                            >
                              View details
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

          {activeView === 'grading' && (
            <section aria-label="Grading workspace">
              <h2 className="section-title">Grading workspace</h2>
              <p className="card-desc">Create exams, monitor submission window status, and submit marks for your assigned students.</p>
              {gradingMessage && <p className="student-note student-note--success">{gradingMessage}</p>}
              {loadingGrading && <p className="empty-state" aria-busy="true">Loading grading workspace…</p>}

              <div className="student-record-grid">
                <div className="student-record-card">
                  <h4 className="dashboard-section-title">Mark submission window</h4>
                  <label className="form-field">
                    <span>Term</span>
                    <select
                      className="form-input"
                      value={resultForm.termId}
                      onChange={(e) => {
                        const termId = e.target.value;
                        setResultForm((prev) => ({ ...prev, termId }));
                        setExamForm((prev) => ({ ...prev, termId }));
                        loadSubmissionWindow(termId);
                      }}
                    >
                      <option value="">Select term</option>
                      {terms.map((term) => (
                        <option key={term.id} value={term.id}>{term.name} {term.academicYear || ''}</option>
                      ))}
                    </select>
                  </label>
                  <p className="card-desc" style={{ marginTop: '0.5rem' }}>
                    Current status: <strong>{submissionWindow?.isOpen ? 'Open' : 'Closed'}</strong>
                  </p>
                </div>

                <div className="student-record-card">
                  <h4 className="dashboard-section-title">Create exam</h4>
                  <div className="student-edit-grid">
                    <label>
                      <span>Name</span>
                      <input className="form-input" value={examForm.name} onChange={(e) => setExamForm((prev) => ({ ...prev, name: e.target.value }))} />
                    </label>
                    <label>
                      <span>Class</span>
                      <select className="form-input" value={examForm.classId} onChange={(e) => setExamForm((prev) => ({ ...prev, classId: e.target.value }))}>
                        <option value="">Select class</option>
                        {classes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                      </select>
                    </label>
                    <label>
                      <span>Subject</span>
                      <select className="form-input" value={examForm.subjectId} onChange={(e) => setExamForm((prev) => ({ ...prev, subjectId: e.target.value }))}>
                        <option value="">Select subject</option>
                        {subjects.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                      </select>
                    </label>
                    <label>
                      <span>Start date</span>
                      <input type="date" className="form-input" value={examForm.startDateUtc} onChange={(e) => setExamForm((prev) => ({ ...prev, startDateUtc: e.target.value }))} />
                    </label>
                    <label>
                      <span>End date</span>
                      <input type="date" className="form-input" value={examForm.endDateUtc} onChange={(e) => setExamForm((prev) => ({ ...prev, endDateUtc: e.target.value }))} />
                    </label>
                  </div>
                  <div className="form-actions" style={{ marginTop: '0.6rem' }}>
                    <button type="button" className="btn-primary-action" onClick={createExam} disabled={savingExam}>{savingExam ? 'Saving…' : 'Create exam'}</button>
                  </div>
                </div>
              </div>

              <div className="student-record-card" style={{ marginTop: '1rem' }}>
                <h4 className="dashboard-section-title">Submit student marks</h4>
                <div className="student-edit-grid">
                  <label>
                    <span>Class</span>
                    <select className="form-input" value={resultForm.classId || selectedClassId} onChange={(e) => setResultForm((prev) => ({ ...prev, classId: e.target.value, studentId: '' }))}>
                      <option value="">Select class</option>
                      {classes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                    </select>
                  </label>
                  <label>
                    <span>Student</span>
                    <select className="form-input" value={resultForm.studentId} onChange={(e) => setResultForm((prev) => ({ ...prev, studentId: e.target.value }))}>
                      <option value="">Select student</option>
                      {studentsInSelectedResultClass.map((s) => (
                        <option key={s.studentId} value={s.studentId}>{[s.firstName, s.lastName].filter(Boolean).join(' ')}</option>
                      ))}
                    </select>
                  </label>
                  <label>
                    <span>Subject</span>
                    <select className="form-input" value={resultForm.subjectId} onChange={(e) => setResultForm((prev) => ({ ...prev, subjectId: e.target.value }))}>
                      <option value="">Select subject</option>
                      {subjects.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                    </select>
                  </label>
                  <label>
                    <span>Exam</span>
                    <select className="form-input" value={resultForm.examId} onChange={(e) => setResultForm((prev) => ({ ...prev, examId: e.target.value }))}>
                      <option value="">No named exam</option>
                      {exams
                        .filter((e) => !resultForm.termId || e.termId === resultForm.termId)
                        .map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}
                    </select>
                  </label>
                  <label>
                    <span>Assessment type</span>
                    <input className="form-input" value={resultForm.assessmentType} onChange={(e) => setResultForm((prev) => ({ ...prev, assessmentType: e.target.value }))} />
                  </label>
                  <label>
                    <span>Score</span>
                    <input type="number" className="form-input" value={resultForm.score} onChange={(e) => setResultForm((prev) => ({ ...prev, score: e.target.value }))} />
                  </label>
                  <label>
                    <span>Max score</span>
                    <input type="number" className="form-input" value={resultForm.maxScore} onChange={(e) => setResultForm((prev) => ({ ...prev, maxScore: e.target.value }))} />
                  </label>
                  <label>
                    <span>Manual grade (optional)</span>
                    <input className="form-input" value={resultForm.gradeLetter} onChange={(e) => setResultForm((prev) => ({ ...prev, gradeLetter: e.target.value }))} />
                  </label>
                  <label className="student-edit-grid__wide">
                    <span>Comment</span>
                    <textarea className="form-input" rows="3" value={resultForm.comment} onChange={(e) => setResultForm((prev) => ({ ...prev, comment: e.target.value }))} />
                  </label>
                </div>
                <div className="form-actions" style={{ marginTop: '0.6rem' }}>
                  <button type="button" className="btn-primary-action" onClick={submitResult} disabled={savingResult}>{savingResult ? 'Submitting…' : 'Submit result'}</button>
                </div>
              </div>
            </section>
          )}
        </section>
      </div>
    </PageLayout>
  );
}
