import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import StudentPhoto from './StudentPhoto';
import { apiFetch } from '../api';

function toInputDate(value) {
  return value ? String(value).slice(0, 10) : '';
}

function formatDate(value) {
  if (!value) return '—';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleDateString();
}

function buildSchoolPayload(detail, form, availableClasses = [], allowAdmissionDateUpdate = true) {
  const selectedClassId = form.classId || detail.class?.id || null;
  const selectedClass = selectedClassId ? availableClasses.find((item) => item.id === selectedClassId) : null;

  return {
    firstName: form.firstName?.trim() || detail.firstName,
    lastName: form.lastName?.trim() || detail.lastName,
    middleName: form.middleName?.trim() || null,
    dateOfBirth: form.dateOfBirth || null,
    gender: form.gender?.trim() || null,
    nationality: form.nationality?.trim() || null,
    stateOfOrigin: form.stateOfOrigin?.trim() || null,
    lga: form.lga?.trim() || null,
    nin: form.nin?.trim() || null,
    nationalIdType: form.nationalIdType?.trim() || null,
    nationalIdNumber: form.nationalIdNumber?.trim() || null,
    admissionNumber: form.admissionNumber?.trim() || null,
    dateOfAdmission: allowAdmissionDateUpdate ? (form.dateOfAdmission || detail.dateOfAdmission || null) : (detail.dateOfAdmission || null),
    classId: selectedClassId,
    gradeId: selectedClass?.gradeId || detail.grade?.id || detail.class?.grade?.id || null,
    previousSchool: form.previousSchool?.trim() || null,
    previousClass: form.previousClass?.trim() || null,
    bloodGroup: form.bloodGroup?.trim() || null,
    genotype: form.genotype?.trim() || null,
    allergies: form.allergies?.trim() || null,
    emergencyContactName: form.emergencyContactName?.trim() || null,
    emergencyContactPhone: form.emergencyContactPhone?.trim() || null,
    isActive: Boolean(detail.isActive),
  };
}

export default function StudentRecordPanel({ studentId, role = 'school', onClose, onSaved }) {
  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [saveMessage, setSaveMessage] = useState(null);
  const [saving, setSaving] = useState(false);
  const [savingVisibility, setSavingVisibility] = useState(false);
  const [form, setForm] = useState({});
  const [visibilityForm, setVisibilityForm] = useState(null);
  const [schoolClasses, setSchoolClasses] = useState([]);
  const canEditRecord = role === 'school' || role === 'teacher' || role === 'parent';
  const [loadingClasses, setLoadingClasses] = useState(canEditRecord);

  useEffect(() => {
    if (!studentId) return undefined;
    let cancelled = false;
    setLoading(true);
    setError(null);
    setSaveMessage(null);

    apiFetch(`/api/students/${studentId}`)
      .then(async (res) => {
        if (!res.ok) throw new Error(await res.text() || 'Could not load student details');
        return res.json();
      })
      .then((data) => {
        if (cancelled) return;
        setDetail(data);
        setForm({
          firstName: data.firstName || '',
          lastName: data.lastName || '',
          middleName: data.middleName || '',
          classId: data.class?.id || '',
          dateOfBirth: toInputDate(data.dateOfBirth),
          gender: data.gender || '',
          nationality: data.nationality || '',
          stateOfOrigin: data.stateOfOrigin || '',
          lga: data.lga || '',
          nin: data.nin || '',
          nationalIdType: data.nationalIdType || '',
          nationalIdNumber: data.nationalIdNumber || '',
          admissionNumber: data.admissionNumber || '',
          dateOfAdmission: toInputDate(data.dateOfAdmission),
          previousSchool: data.previousSchool || '',
          previousClass: data.previousClass || '',
          bloodGroup: data.bloodGroup || '',
          genotype: data.genotype || '',
          allergies: data.allergies || '',
          emergencyContactName: data.emergencyContactName || '',
          emergencyContactPhone: data.emergencyContactPhone || '',
        });
        setVisibilityForm(data.teacherVisibilitySettings || null);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message || 'Could not load student details');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [studentId]);

  useEffect(() => {
    if (!canEditRecord) {
      setSchoolClasses([]);
      setLoadingClasses(false);
      return undefined;
    }

    let cancelled = false;
    setLoadingClasses(true);
    apiFetch('/api/schools/classes')
      .then((res) => (res.ok ? res.json() : []))
      .then((data) => {
        if (!cancelled) setSchoolClasses(Array.isArray(data) ? data : []);
      })
      .catch(() => {
        if (!cancelled) setSchoolClasses([]);
      })
      .finally(() => {
        if (!cancelled) setLoadingClasses(false);
      });

    return () => {
      cancelled = true;
    };
  }, [canEditRecord]);

  const groupedTerms = useMemo(() => (Array.isArray(detail?.termResults) ? detail.termResults : []), [detail]);

  const updateForm = (key, value) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const updateVisibilityForm = (key, value) => {
    setVisibilityForm((prev) => ({ ...(prev || {}), [key]: value }));
  };

  const refreshDetails = async (messageOverride = null) => {
    const res = await apiFetch(`/api/students/${studentId}`);
    if (!res.ok) throw new Error(await res.text() || 'Could not refresh student details');
    const data = await res.json();
    setDetail(data);
    setVisibilityForm(data.teacherVisibilitySettings || null);
    if (messageOverride) setSaveMessage(messageOverride);
    onSaved?.(data);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!detail) return;
    setSaving(true);
    setSaveMessage(null);

    try {
      const isParent = role === 'parent';
      const payload = buildSchoolPayload(detail, form, schoolClasses, !isParent);
      const res = await apiFetch(isParent ? `/api/students/${studentId}/parent-corrections` : `/api/students/${studentId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        throw new Error(data?.message || 'Could not save student changes.');
      }
      await refreshDetails(data?.message || 'Student details updated successfully.');
    } catch (err) {
      setSaveMessage(err.message || 'Could not save student changes.');
    } finally {
      setSaving(false);
    }
  };

  const handleSaveVisibility = async () => {
    if (!detail?.canManageTeacherVisibility || !visibilityForm) return;
    setSavingVisibility(true);
    setSaveMessage(null);
    try {
      const res = await apiFetch('/api/students/profile-visibility-settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(visibilityForm),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) throw new Error(data?.message || 'Could not save teacher visibility settings.');
      setDetail((prev) => ({ ...prev, teacherVisibilitySettings: data }));
      setSaveMessage('Teacher visibility settings updated.');
    } catch (err) {
      setSaveMessage(err.message || 'Could not save teacher visibility settings.');
    } finally {
      setSavingVisibility(false);
    }
  };

  if (!studentId) return null;

  return (
    <section className="student-record-panel" aria-label="Student record details">
      <div className="student-record-header">
        <div>
          <h3 className="card-title" style={{ marginBottom: '0.15rem' }}>Student details</h3>
          <p className="card-desc">Open, review, and update the full student record without leaving this page.</p>
        </div>
        {onClose && (
          <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={onClose}>
            Close
          </button>
        )}
      </div>

      {loading && <p className="empty-state" aria-busy="true">Loading student details…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}

      {!loading && !error && detail && (
        <>
          <div className="student-record-hero">
            <StudentPhoto studentId={detail.id} firstName={detail.firstName} lastName={detail.lastName} size={56} />
            <div>
              <h4 className="student-record-name">{[detail.firstName, detail.middleName, detail.lastName].filter(Boolean).join(' ')}</h4>
              <p className="card-desc">
                Admission #: {detail.admissionNumber || '—'} • Class: {detail.class?.name || '—'} • Grade: {detail.grade?.name || detail.class?.grade?.name || '—'}
              </p>
              <p className="card-desc">
                Current average: {detail.currentAveragePercentage != null ? `${detail.currentAveragePercentage}%` : '—'}
              </p>
            </div>
          </div>

          {detail.parentEditMessage && (
            <p className="student-note">{detail.parentEditMessage}</p>
          )}
          {saveMessage && (
            <p className="student-note student-note--success">{saveMessage}</p>
          )}

          <div className="student-record-grid">
            <div className="student-record-card">
              <h4 className="dashboard-section-title">Profile snapshot</h4>
              <dl className="profile-dl">
                <dt>Date of birth</dt><dd>{formatDate(detail.dateOfBirth)}</dd>
                <dt>Gender</dt><dd>{detail.gender || '—'}</dd>
                <dt>Nationality</dt><dd>{detail.nationality || '—'}</dd>
                <dt>State / LGA</dt><dd>{[detail.stateOfOrigin, detail.lga].filter(Boolean).join(', ') || '—'}</dd>
                <dt>Previous school</dt><dd>{detail.previousSchool || '—'}</dd>
                <dt>Previous class</dt><dd>{detail.previousClass || '—'}</dd>
                <dt>Date of admission</dt><dd>{formatDate(detail.dateOfAdmission)}</dd>
              </dl>
            </div>

            <div className="student-record-card">
              <h4 className="dashboard-section-title">Health & emergency</h4>
              <dl className="profile-dl">
                <dt>Blood group</dt><dd>{detail.bloodGroup || '—'}</dd>
                <dt>Genotype</dt><dd>{detail.genotype || '—'}</dd>
                <dt>Allergies</dt><dd>{detail.allergies || '—'}</dd>
                <dt>Emergency contact</dt><dd>{detail.emergencyContactName || '—'}</dd>
                <dt>Emergency phone</dt><dd>{detail.emergencyContactPhone || '—'}</dd>
              </dl>
            </div>
          </div>

          <div className="student-record-grid">
            <div className="student-record-card">
              <h4 className="dashboard-section-title">Parents / guardians</h4>
              {detail.studentParents?.length ? (
                <ul className="student-record-list">
                  {detail.studentParents.map((parent) => (
                    <li key={parent.parentId}>
                      <strong>{[parent.firstName, parent.lastName].filter(Boolean).join(' ')}</strong>
                      <span>{parent.relationshipToStudent || 'Guardian'} • {parent.phone || parent.email || 'No contact yet'}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="card-desc">No parent contact is visible for this view.</p>
              )}
            </div>

            <div className="student-record-card">
              <h4 className="dashboard-section-title">Assigned teachers</h4>
              {detail.assignedTeachers?.length ? (
                <ul className="student-record-list">
                  {detail.assignedTeachers.map((teacher) => (
                    <li key={`${teacher.teacherId}-${teacher.roleOrSubject || ''}`}>
                      <strong>{teacher.fullName}</strong>
                      <span>{teacher.roleOrSubject || 'Teacher'}{teacher.phone ? ` • ${teacher.phone}` : ''}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="card-desc">No teachers assigned yet for this student’s class.</p>
              )}
            </div>
          </div>

          <div className="student-record-card">
            <h4 className="dashboard-section-title">Term results</h4>
            {groupedTerms.length === 0 ? (
              <p className="card-desc">No term results published yet.</p>
            ) : (
              <div className="student-term-grid">
                {groupedTerms.map((term) => (
                  <article key={term.term} className="student-term-card">
                    <div className="student-term-card-header">
                      <strong>{term.term}</strong>
                      <span>{term.averagePercentage}% avg</span>
                    </div>
                    <ul className="student-term-results">
                      {(term.results || []).map((result) => (
                        <li key={result.resultId}>
                          <span>{result.subject}{result.examName ? ` (${result.examName})` : ''}</span>
                          <span>{result.percentage}% {result.gradeLetter ? `(${result.gradeLetter})` : ''}</span>
                        </li>
                      ))}
                    </ul>
                  </article>
                ))}
              </div>
            )}
          </div>

          {(role === 'school' || role === 'parent') && (
            <div className="student-record-card">
              <h4 className="dashboard-section-title">
                {role === 'parent' ? 'Correct child information' : 'Edit student information'}
              </h4>
              <form onSubmit={handleSubmit} className="student-edit-form">
                <div className="student-edit-grid">
                  <label>
                    <span>First name</span>
                    <input className="form-input" value={form.firstName || ''} onChange={(e) => updateForm('firstName', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Last name</span>
                    <input className="form-input" value={form.lastName || ''} onChange={(e) => updateForm('lastName', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Middle name</span>
                    <input className="form-input" value={form.middleName || ''} onChange={(e) => updateForm('middleName', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Date of birth</span>
                    <input type="date" className="form-input" value={form.dateOfBirth || ''} onChange={(e) => updateForm('dateOfBirth', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Gender</span>
                    <input className="form-input" value={form.gender || ''} onChange={(e) => updateForm('gender', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Nationality</span>
                    <input className="form-input" value={form.nationality || ''} onChange={(e) => updateForm('nationality', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>State of origin</span>
                    <input className="form-input" value={form.stateOfOrigin || ''} onChange={(e) => updateForm('stateOfOrigin', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>LGA</span>
                    <input className="form-input" value={form.lga || ''} onChange={(e) => updateForm('lga', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Previous school</span>
                    <input className="form-input" value={form.previousSchool || ''} onChange={(e) => updateForm('previousSchool', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Previous class</span>
                    <input className="form-input" value={form.previousClass || ''} onChange={(e) => updateForm('previousClass', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Blood group</span>
                    <input className="form-input" value={form.bloodGroup || ''} onChange={(e) => updateForm('bloodGroup', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Genotype</span>
                    <input className="form-input" value={form.genotype || ''} onChange={(e) => updateForm('genotype', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label className="student-edit-grid__wide">
                    <span>Allergies / notes</span>
                    <textarea className="form-input" rows="3" value={form.allergies || ''} onChange={(e) => updateForm('allergies', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Emergency contact name</span>
                    <input className="form-input" value={form.emergencyContactName || ''} onChange={(e) => updateForm('emergencyContactName', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>
                  <label>
                    <span>Emergency contact phone</span>
                    <input className="form-input" value={form.emergencyContactPhone || ''} onChange={(e) => updateForm('emergencyContactPhone', e.target.value)} disabled={saving || (role === 'parent' && !detail.canParentEdit)} />
                  </label>

                  {(role === 'school' || role === 'parent') && (
                    <>
                      <label>
                        <span>Assign to class</span>
                        <select className="form-input" value={form.classId || ''} onChange={(e) => updateForm('classId', e.target.value)} disabled={saving || loadingClasses}>
                          <option value="">— No class assigned —</option>
                          {schoolClasses.map((schoolClass) => (
                            <option key={schoolClass.id} value={schoolClass.id}>
                              {schoolClass.name}{schoolClass.gradeName ? ` (${schoolClass.gradeName})` : ''}
                            </option>
                          ))}
                        </select>
                        {loadingClasses && <span className="card-desc">Loading classes…</span>}
                        {role === 'school' && !loadingClasses && schoolClasses.length === 0 && (
                          <span className="card-desc">
                            No classes yet. <Link to="/school/classes">Create a class first</Link>, then return here to assign this student.
                          </span>
                        )}
                      </label>
                      <label>
                        <span>Admission number</span>
                        <input className="form-input" value={form.admissionNumber || ''} onChange={(e) => updateForm('admissionNumber', e.target.value)} disabled={saving} />
                      </label>
                      {role === 'school' && (
                        <label>
                          <span>Date of admission</span>
                          <input type="date" className="form-input" value={form.dateOfAdmission || ''} onChange={(e) => updateForm('dateOfAdmission', e.target.value)} disabled={saving} />
                        </label>
                      )}
                      <label>
                        <span>NIN</span>
                        <input className="form-input" value={form.nin || ''} onChange={(e) => updateForm('nin', e.target.value)} disabled={saving} />
                      </label>
                      <label>
                        <span>National ID type</span>
                        <input className="form-input" value={form.nationalIdType || ''} onChange={(e) => updateForm('nationalIdType', e.target.value)} disabled={saving} />
                      </label>
                      <label className="student-edit-grid__wide">
                        <span>National ID number</span>
                        <input className="form-input" value={form.nationalIdNumber || ''} onChange={(e) => updateForm('nationalIdNumber', e.target.value)} disabled={saving} />
                      </label>
                    </>
                  )}
                </div>

                <div className="form-actions" style={{ marginTop: '0.85rem' }}>
                  <button type="submit" className="btn-primary-action" disabled={saving || (role === 'parent' && !detail.canParentEdit)}>
                    {saving ? 'Saving…' : (role === 'parent' ? 'Save correction' : 'Save changes')}
                  </button>
                  {role === 'parent' && !detail.canParentEdit && (
                    <span className="card-desc">Next parent edit window: {formatDate(detail.parentEditLockedUntilUtc)}</span>
                  )}
                </div>
              </form>
            </div>
          )}

          {detail.canManageTeacherVisibility && visibilityForm && (
            <div className="student-record-card">
              <h4 className="dashboard-section-title">Teacher visibility controls</h4>
              <p className="card-desc" style={{ marginBottom: '0.75rem' }}>
                Decide what teachers in this school are allowed to view when they open a student record.
              </p>
              <div className="student-visibility-grid">
                {[
                  ['showDateOfBirthToTeachers', 'Allow teachers to see date of birth'],
                  ['showLocationDetailsToTeachers', 'Allow teachers to see nationality, state and LGA'],
                  ['showHealthDetailsToTeachers', 'Allow teachers to see health and emergency details'],
                  ['showParentContactsToTeachers', 'Allow teachers to see parent contacts'],
                  ['showAcademicHistoryToTeachers', 'Allow teachers to see academic history'],
                  ['showPreviousRecordToTeachers', 'Allow teachers to see previous school/class'],
                ].map(([key, label]) => (
                  <label key={key} className="student-visibility-item">
                    <input
                      type="checkbox"
                      checked={Boolean(visibilityForm[key])}
                      onChange={(e) => updateVisibilityForm(key, e.target.checked)}
                      disabled={savingVisibility}
                    />
                    <span>{label}</span>
                  </label>
                ))}
              </div>
              <div className="form-actions" style={{ marginTop: '0.85rem' }}>
                <button type="button" className="btn-primary-action" onClick={handleSaveVisibility} disabled={savingVisibility}>
                  {savingVisibility ? 'Saving…' : 'Save teacher visibility'}
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </section>
  );
}
