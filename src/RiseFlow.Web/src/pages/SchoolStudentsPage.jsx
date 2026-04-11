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

export default function SchoolStudentsPage() {
  const [students, setStudents] = useState([]);
  const [classes, setClasses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [classFilter, setClassFilter] = useState('');
  const [gradeFilter, setGradeFilter] = useState('');
  const [selectedStudentId, setSelectedStudentId] = useState(null);
  const [savingClassId, setSavingClassId] = useState(null);
  const [selectedStudentIds, setSelectedStudentIds] = useState([]);
  const [bulkClassId, setBulkClassId] = useState('');
  const [bulkAssigning, setBulkAssigning] = useState(false);

  const loadStudents = async (cancelledRef, options = {}) => {
    const { background = false } = options;
    if (background) {
      if (!cancelledRef?.cancelled) setRefreshing(true);
    } else {
      setLoading(true);
    }
    setError(null);
    try {
      const [studentsRes, classesRes] = await Promise.all([
        apiFetch('/api/students'),
        apiFetch('/api/schools/classes'),
      ]);
      if (!studentsRes.ok) throw new Error('Could not load students');
      const studentsData = await studentsRes.json();
      const classesData = classesRes.ok ? await classesRes.json() : [];
      if (!cancelledRef?.cancelled) {
        setStudents(Array.isArray(studentsData) ? studentsData : []);
        setClasses(Array.isArray(classesData) ? classesData : []);
      }
    } catch (e) {
      if (!cancelledRef?.cancelled) {
        const message = /blocked or unreachable|failed to fetch|networkerror/i.test(String(e?.message || ''))
          ? 'The student directory is syncing with the live API. Please refresh shortly.'
          : (e.message || 'Failed to load students');
        setError(message);
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
                  <td>
                    <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => setSelectedStudentId(s.id)}>
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
          role="school"
          onClose={() => setSelectedStudentId(null)}
          onSaved={() => loadStudents()}
        />
      )}
    </PageLayout>
  );
}
