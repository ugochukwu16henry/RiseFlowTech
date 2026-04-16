import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import StudentRecordPanel from '../components/StudentRecordPanel';
import { apiFetch } from '../api';
import './RolePages.css';

function getGradeName(student) {
  return student?.grade?.name || student?.class?.grade?.name || '—';
}

function studentListHttpError(status, bodySnippet) {
  if (status === 401) {
    return 'Not signed in or session expired. Sign in again, then open Students.';
  }
  if (status === 403) {
    return 'Access denied (no school context). Sign out and sign in again as a school admin, or contact support if your account should be linked to a school.';
  }
  if (status === 502 || status === 503 || status === 504) {
    return 'The API is not reachable. For local dev: run RiseFlow.Api on port 5221 and use the Vite dev server with an empty VITE_API_URL so /api is proxied.';
  }
  if (status >= 500) {
    return 'The student directory is syncing with the live API. Please refresh shortly.';
  }
  const hint = (bodySnippet || '').trim().slice(0, 200);
  return hint ? `Could not load students: ${hint}` : `Could not load students (HTTP ${status}).`;
}

function studentListNetworkError(raw) {
  const s = String(raw || '');
  if (import.meta.env.DEV && s.length > 20) return s;
  if (/blocked or unreachable|failed to fetch|networkerror/i.test(s)) {
    return 'Could not reach the API. For local dev: run RiseFlow.Api on port 5221, leave VITE_API_URL empty (use the Vite proxy), restart the dev server, and sign in from this same origin.';
  }
  return s || 'Failed to load students.';
}

export default function SchoolStudentsPage() {
  const [students, setStudents] = useState([]);
  const [classes, setClasses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [classesUnavailable, setClassesUnavailable] = useState(false);
  const [error, setError] = useState(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [classFilter, setClassFilter] = useState('');
  const [gradeFilter, setGradeFilter] = useState('');
  const [selectedStudentId, setSelectedStudentId] = useState(null);
  const [savingClassId, setSavingClassId] = useState(null);
  const [selectedStudentIds, setSelectedStudentIds] = useState([]);
  const [bulkClassId, setBulkClassId] = useState('');
  const [bulkAssigning, setBulkAssigning] = useState(false);
  const [processingLifecycleId, setProcessingLifecycleId] = useState(null);

  const loadStudents = async (cancelledRef, options = {}) => {
    const { background = false } = options;
    if (background) {
      if (!cancelledRef?.cancelled) setRefreshing(true);
    } else {
      setLoading(true);
    }
    setError(null);
    setClassesUnavailable(false);
    try {
      const [studentsResult, classesResult] = await Promise.allSettled([
        apiFetch('/api/students'),
        apiFetch('/api/schools/classes'),
      ]);

      let nextStudents = [];
      let nextClasses = [];
      let studentFailure = null;
      let classesFailed = false;

      if (studentsResult.status === 'fulfilled') {
        const studentsRes = studentsResult.value;
        if (!studentsRes.ok) {
          const errBody = await studentsRes.text().catch(() => '');
          studentFailure = studentListHttpError(studentsRes.status, errBody);
        } else {
          try {
            const studentsData = await studentsRes.json();
            nextStudents = Array.isArray(studentsData) ? studentsData : [];
          } catch {
            studentFailure = 'Invalid response when loading students (not JSON). Check that the API is RiseFlow.Api.';
          }
        }
      } else {
        studentFailure = studentListNetworkError(studentsResult.reason?.message || 'Failed to load students');
      }

      if (classesResult.status === 'fulfilled') {
        const classesRes = classesResult.value;
        if (classesRes.ok) {
          const classesData = await classesRes.json();
          nextClasses = Array.isArray(classesData) ? classesData : [];
        } else {
          classesFailed = true;
        }
      } else {
        classesFailed = true;
      }

      if (!cancelledRef?.cancelled) {
        setStudents(nextStudents);
        setClasses(nextClasses);
        setClassesUnavailable(classesFailed);

        if (studentFailure) {
          setError(studentFailure);
        }
      }
    } catch (e) {
      if (!cancelledRef?.cancelled) {
        setError(studentListNetworkError(e?.message || e));
      }
    } finally {
      if (!cancelledRef?.cancelled) {
        if (!background) setLoading(false);
        setRefreshing(false);
      }
    }
  };

  useEffect(() => {
    const cancelledRef = { cancelled: false };
    loadStudents(cancelledRef);
    return () => { cancelledRef.cancelled = true; };
  }, []);

  const classOptions = useMemo(
    () => Array.from(new Set(students.map((s) => s.class?.name).filter(Boolean))).sort(),
    [students],
  );
  const gradeOptions = useMemo(
    () => Array.from(new Set(students.map((s) => getGradeName(s)).filter((g) => g && g !== '—'))).sort(),
    [students],
  );

  const filteredStudents = useMemo(() => {
    const query = searchTerm.trim().toLowerCase();
    return students.filter((student) => {
      const fullName = [student.firstName, student.middleName, student.lastName].filter(Boolean).join(' ').toLowerCase();
      const className = (student.class?.name || '').toLowerCase();
      const gradeName = getGradeName(student).toLowerCase();
      const admissionNumber = (student.admissionNumber || '').toLowerCase();
      const matchesSearch = !query
        || fullName.includes(query)
        || className.includes(query)
        || gradeName.includes(query)
        || admissionNumber.includes(query);
      const matchesClass = !classFilter || student.class?.name === classFilter;
      const matchesGrade = !gradeFilter || getGradeName(student) === gradeFilter;
      return matchesSearch && matchesClass && matchesGrade;
    });
  }, [students, searchTerm, classFilter, gradeFilter]);

  const filteredStudentIds = useMemo(() => filteredStudents.map((student) => student.id), [filteredStudents]);
  const allFilteredSelected = filteredStudentIds.length > 0 && filteredStudentIds.every((id) => selectedStudentIds.includes(id));

  const toggleStudentSelection = (studentId, isChecked) => {
    setSelectedStudentIds((prev) => {
      if (isChecked) return prev.includes(studentId) ? prev : [...prev, studentId];
      return prev.filter((id) => id !== studentId);
    });
  };

  const toggleSelectAllFiltered = (isChecked) => {
    setSelectedStudentIds((prev) => {
      if (isChecked) {
        return Array.from(new Set([...prev, ...filteredStudentIds]));
      }
      return prev.filter((id) => !filteredStudentIds.includes(id));
    });
  };

  const saveStudentClassAssignment = async (studentId, nextClassId) => {
    const res = await apiFetch(`/api/students/${studentId}/class-assignment`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ classId: nextClassId || null }),
    });
    const text = await res.text();
    if (!res.ok) throw new Error(text || 'Could not assign class.');
  };

  const handleQuickAssignClass = async (student, nextClassId) => {
    if (!student?.id || savingClassId === student.id) return;
    setSavingClassId(student.id);
    setError(null);

    try {
      await saveStudentClassAssignment(student.id, nextClassId);
      await loadStudents(undefined, { background: true });
    } catch (e) {
      setError(e.message || 'Failed to assign class.');
    } finally {
      setSavingClassId(null);
    }
  };

  const handleBulkAssignClass = async () => {
    if (!bulkClassId) {
      setError('Select a class to assign first.');
      return;
    }
    if (selectedStudentIds.length === 0) {
      setError('Select at least one student first.');
      return;
    }

    setBulkAssigning(true);
    setError(null);
    try {
      for (const studentId of selectedStudentIds) {
        await saveStudentClassAssignment(studentId, bulkClassId);
      }
      setSelectedStudentIds([]);
      setBulkClassId('');
      await loadStudents(undefined, { background: true });
    } catch (e) {
      setError(e.message || 'Failed to bulk assign class.');
    } finally {
      setBulkAssigning(false);
    }
  };

  const closeStudent = async (student) => {
    if (!student?.id || processingLifecycleId) return;
    const reason = window.prompt('Reason for closing this student record (e.g. transferred, dropped out):', 'Student left school');
    if (reason == null) return;
    setProcessingLifecycleId(student.id);
    setError(null);
    try {
      const res = await apiFetch(`/api/students/${student.id}/lifecycle/close`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason }),
      });
      const text = await res.text();
      if (!res.ok) throw new Error(text || 'Could not close student record.');
      await loadStudents(undefined, { background: true });
    } catch (e) {
      setError(e.message || 'Could not close student record.');
    } finally {
      setProcessingLifecycleId(null);
    }
  };

  const graduateStudent = async (student) => {
    if (!student?.id || processingLifecycleId) return;
    const notes = window.prompt('Graduation notes (optional):', '');
    if (notes == null) return;
    setProcessingLifecycleId(student.id);
    setError(null);
    try {
      const res = await apiFetch(`/api/students/${student.id}/lifecycle/graduate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ notes, issuedToName: [student.firstName, student.lastName].filter(Boolean).join(' ') }),
      });
      const payload = await res.json().catch(() => null);
      if (!res.ok) throw new Error(payload?.message || 'Could not mark student as graduated.');
      if (payload?.verificationUrl) {
        window.alert(`Graduate verification QR link created:\n${payload.verificationUrl}`);
      }
      await loadStudents(undefined, { background: true });
    } catch (e) {
      setError(e.message || 'Could not mark student as graduated.');
    } finally {
      setProcessingLifecycleId(null);
    }
  };

  return (
    <PageLayout title="School Admin — Teachers & Students" role="school">
      <h2 className="section-title">Students</h2>
      <div className="form-actions" style={{ marginBottom: '0.75rem', flexWrap: 'wrap' }}>
        <Link to="/school/students/add" className="btn-primary-action">Add student</Link>
        <Link to="/school/import" className="btn-primary-action btn-primary-action--ghost">Bulk import</Link>
        <Link to="/school/classes" className="btn-primary-action btn-primary-action--ghost">Grades & classes</Link>
        <input
          type="search"
          className="form-input"
          style={{ maxWidth: '260px' }}
          placeholder="Search by name, admission no, class or grade"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
        <select className="form-input" style={{ maxWidth: '180px' }} value={classFilter} onChange={(e) => setClassFilter(e.target.value)}>
          <option value="">All classes</option>
          {classOptions.map((name) => <option key={name} value={name}>{name}</option>)}
        </select>
        <select className="form-input" style={{ maxWidth: '180px' }} value={gradeFilter} onChange={(e) => setGradeFilter(e.target.value)}>
          <option value="">All grades</option>
          {gradeOptions.map((name) => <option key={name} value={name}>{name}</option>)}
        </select>
      </div>
      {!loading && !error && students.length > 0 && (
        <p className="card-desc">Showing {filteredStudents.length} of {students.length} students. Use the quick class dropdown for one-by-one changes, or select multiple students below and bulk assign them to a class.</p>
      )}
      {!loading && !error && filteredStudents.length > 0 && classesUnavailable && (
        <p className="card-desc">Student records are available. Class filters and quick assignment are reconnecting and will appear again automatically.</p>
      )}
      {!loading && !error && filteredStudents.length > 0 && classes.length > 0 && (
        <div className="form-actions" style={{ marginBottom: '0.75rem', flexWrap: 'wrap' }}>
          <span className="card-desc"><strong>{selectedStudentIds.length}</strong> selected</span>
          <select
            className="form-input"
            style={{ minWidth: '220px' }}
            value={bulkClassId}
            onChange={(e) => setBulkClassId(e.target.value)}
            disabled={bulkAssigning}
          >
            <option value="">— Bulk assign to class —</option>
            {classes.map((schoolClass) => (
              <option key={schoolClass.id} value={schoolClass.id}>
                {schoolClass.name}{schoolClass.gradeName ? ` (${schoolClass.gradeName})` : ''}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="btn-primary-action"
            onClick={handleBulkAssignClass}
            disabled={bulkAssigning || !bulkClassId || selectedStudentIds.length === 0}
          >
            {bulkAssigning ? 'Assigning…' : `Assign selected (${selectedStudentIds.length})`}
          </button>
          <button
            type="button"
            className="btn-primary-action btn-primary-action--ghost"
            onClick={() => toggleSelectAllFiltered(!allFilteredSelected)}
            disabled={bulkAssigning}
          >
            {allFilteredSelected ? 'Clear selection' : 'Select all shown'}
          </button>
        </div>
      )}
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {refreshing && !loading && !error && <p className="card-desc" aria-live="polite">Refreshing student directory…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}
      {!loading && !error && students.length === 0 && <p className="empty-state">No students found.</p>}
      {!loading && !error && students.length > 0 && filteredStudents.length === 0 && (
        <p className="empty-state">No students match your current search or filters.</p>
      )}
      {!loading && filteredStudents.length > 0 && (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th style={{ width: '36px' }}>
                  <input
                    type="checkbox"
                    checked={allFilteredSelected}
                    onChange={(e) => toggleSelectAllFiltered(e.target.checked)}
                    aria-label="Select all visible students"
                    disabled={bulkAssigning}
                  />
                </th>
                <th style={{ width: '48px' }}>Photo</th>
                <th>Name</th>
                <th>Admission #</th>
                <th>Class</th>
                <th>Quick assign</th>
                <th>Grade</th>
                <th>Status</th>
                <th>Record</th>
              </tr>
            </thead>
            <tbody>
              {filteredStudents.map((s) => (
                <tr key={s.id}>
                  <td>
                    <input
                      type="checkbox"
                      checked={selectedStudentIds.includes(s.id)}
                      onChange={(e) => toggleStudentSelection(s.id, e.target.checked)}
                      aria-label={`Select ${[s.firstName, s.lastName].filter(Boolean).join(' ')}`}
                      disabled={bulkAssigning}
                    />
                  </td>
                  <td><StudentPhoto studentId={s.id} firstName={s.firstName} lastName={s.lastName} size={36} /></td>
                  <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                  <td>{s.admissionNumber || '—'}</td>
                  <td>{s.class?.name || '—'}</td>
                  <td>
                    <select
                      className="form-input"
                      style={{ minWidth: '180px' }}
                      value={s.class?.id || ''}
                      onChange={(e) => handleQuickAssignClass(s, e.target.value)}
                      disabled={savingClassId === s.id || bulkAssigning}
                    >
                      <option value="">— No class —</option>
                      {classes.map((schoolClass) => (
                        <option key={schoolClass.id} value={schoolClass.id}>
                          {schoolClass.name}{schoolClass.gradeName ? ` (${schoolClass.gradeName})` : ''}
                        </option>
                      ))}
                    </select>
                    {savingClassId === s.id && <span className="form-hint">Saving…</span>}
                  </td>
                  <td>{getGradeName(s)}</td>
                  <td>{s.enrollmentStatus || (s.isActive ? 'Active' : 'Closed')}</td>
                  <td>
                    <div className="form-actions" style={{ gap: '0.35rem', flexWrap: 'wrap' }}>
                      <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => setSelectedStudentId(s.id)}>
                        Open record
                      </button>
                      <button
                        type="button"
                        className="btn-primary-action btn-primary-action--ghost"
                        onClick={() => closeStudent(s)}
                        disabled={processingLifecycleId === s.id || (s.enrollmentStatus || '').toLowerCase() === 'closed'}
                      >
                        {processingLifecycleId === s.id ? 'Saving…' : 'Offboard'}
                      </button>
                      <button
                        type="button"
                        className="btn-primary-action btn-primary-action--ghost"
                        onClick={() => graduateStudent(s)}
                        disabled={processingLifecycleId === s.id || (s.enrollmentStatus || '').toLowerCase() === 'graduated'}
                      >
                        {processingLifecycleId === s.id ? 'Saving…' : 'Graduate'}
                      </button>
                    </div>
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
          role="school"
          onClose={() => setSelectedStudentId(null)}
          onSaved={() => loadStudents()}
        />
      )}
    </PageLayout>
  );
}
