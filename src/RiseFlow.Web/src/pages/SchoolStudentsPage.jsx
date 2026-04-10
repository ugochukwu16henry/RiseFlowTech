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
  const [error, setError] = useState(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [classFilter, setClassFilter] = useState('');
  const [gradeFilter, setGradeFilter] = useState('');
  const [selectedStudentId, setSelectedStudentId] = useState(null);
  const [savingClassId, setSavingClassId] = useState(null);

  const loadStudents = async (cancelledRef) => {
    setLoading(true);
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
      if (!cancelledRef?.cancelled) setError(e.message || 'Failed to load students');
    } finally {
      if (!cancelledRef?.cancelled) setLoading(false);
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

  const handleQuickAssignClass = async (student, nextClassId) => {
    if (!student?.id || savingClassId === student.id) return;
    setSavingClassId(student.id);
    setError(null);

    try {
      const selectedClass = nextClassId ? classes.find((item) => item.id === nextClassId) : null;
      const payload = {
        firstName: student.firstName,
        lastName: student.lastName,
        middleName: student.middleName || null,
        dateOfBirth: student.dateOfBirth || null,
        gender: student.gender || null,
        nationality: student.nationality || null,
        stateOfOrigin: student.stateOfOrigin || null,
        lga: student.lga || null,
        nin: student.nin || null,
        nationalIdType: student.nationalIdType || null,
        nationalIdNumber: student.nationalIdNumber || null,
        admissionNumber: student.admissionNumber || null,
        dateOfAdmission: student.dateOfAdmission || null,
        classId: nextClassId || null,
        gradeId: selectedClass?.gradeId || student.grade?.id || student.class?.grade?.id || null,
        previousSchool: student.previousSchool || null,
        previousClass: student.previousClass || null,
        bloodGroup: student.bloodGroup || null,
        genotype: student.genotype || null,
        allergies: student.allergies || null,
        emergencyContactName: student.emergencyContactName || null,
        emergencyContactPhone: student.emergencyContactPhone || null,
      };

      const res = await apiFetch(`/api/students/${student.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      const text = await res.text();
      if (!res.ok) throw new Error(text || 'Could not assign class.');

      await loadStudents();
    } catch (e) {
      setError(e.message || 'Failed to assign class.');
    } finally {
      setSavingClassId(null);
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
        <p className="card-desc">Showing {filteredStudents.length} of {students.length} students. Click “Open record” on any row to assign the student to a class, view full details, teachers, results, and other edit controls.</p>
      )}
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
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
                      disabled={savingClassId === s.id}
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
