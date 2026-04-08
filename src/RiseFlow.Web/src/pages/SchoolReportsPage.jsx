import { useEffect, useMemo, useState } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

function formatMoney(amount, currencyCode) {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: currencyCode || 'NGN', maximumFractionDigits: 0 }).format(n);
}

function getGradeName(student) {
  return student?.grade?.name || student?.class?.grade?.name || 'Unassigned';
}

export default function SchoolReportsPage() {
  const [dashboard, setDashboard] = useState(null);
  const [students, setStudents] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [parents, setParents] = useState([]);
  const [billing, setBilling] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.all([
      apiFetch('/api/schools/dashboard'),
      apiFetch('/api/students'),
      apiFetch('/api/teachers'),
      apiFetch('/api/parents'),
      apiFetch('/api/billing'),
    ])
      .then(async ([dashRes, studentsRes, teachersRes, parentsRes, billingRes]) => {
        const responses = [dashRes, studentsRes, teachersRes, parentsRes, billingRes];
        const forbidden = responses.find((res) => res.status === 401 || res.status === 403);
        if (forbidden) {
          throw new Error('Your session expired or your school access is missing. Please sign in again as School Admin.');
        }
        const failed = responses.find((res) => !res.ok);
        if (failed) {
          throw new Error(await failed.text().catch(() => 'Could not load school reports.'));
        }

        const [dash, studentList, teacherList, parentList, billingList] = await Promise.all(responses.map((res) => res.json()));
        if (cancelled) return;

        setDashboard(dash || null);
        setStudents(Array.isArray(studentList) ? studentList : []);
        setTeachers(Array.isArray(teacherList) ? teacherList : []);
        setParents(Array.isArray(parentList) ? parentList : []);
        setBilling(Array.isArray(billingList) ? billingList : []);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message || 'Failed to load school reports.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, []);

  const classBreakdown = useMemo(() => {
    const map = new Map();
    students.forEach((student) => {
      const className = student?.class?.name || 'Unassigned';
      const gradeName = getGradeName(student);
      const current = map.get(className) || { className, gradeName, count: 0 };
      current.count += 1;
      map.set(className, current);
    });
    return Array.from(map.values()).sort((a, b) => b.count - a.count || a.className.localeCompare(b.className));
  }, [students]);

  const gradeBreakdown = useMemo(() => {
    const map = new Map();
    students.forEach((student) => {
      const gradeName = getGradeName(student);
      map.set(gradeName, (map.get(gradeName) || 0) + 1);
    });
    return Array.from(map.entries())
      .map(([gradeName, count]) => ({ gradeName, count }))
      .sort((a, b) => b.count - a.count || a.gradeName.localeCompare(b.gradeName));
  }, [students]);

  const currencyCode = dashboard?.currencyCode || billing[0]?.currencyCode || 'NGN';
  const activeStudents = dashboard?.studentCount ?? dashboard?.activeStudentCount ?? students.length;
  const activeTeachers = dashboard?.teacherCount ?? teachers.filter((teacher) => teacher?.isActive !== false).length;
  const activeParents = parents.filter((parent) => parent?.isActive !== false).length;
  const pendingResults = dashboard?.pendingResultsCount ?? 0;
  const unpaidFees = dashboard?.unpaidFeesTotal ?? 0;
  const currentBilling = billing[0] || null;
  const outstanding = currentBilling ? Math.max(0, Number(currentBilling.amountDue || 0) - Number(currentBilling.amountPaid || 0)) : 0;

  return (
    <PageLayout title="School Admin — Reports" role="school">
      <h2 className="section-title">Reports</h2>
      <p className="card-desc">This page now pulls live school information from your database for quick reporting and admin review.</p>

      {loading && <p className="empty-state" aria-busy="true">Loading report data…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}

      {!loading && !error && (
        <>
          <div style={{ display: 'grid', gap: '1rem', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))' }}>
            <div className="card">
              <p className="card-title">Students</p>
              <p className="card-desc"><strong>{activeStudents}</strong> enrolled</p>
            </div>
            <div className="card">
              <p className="card-title">Teachers</p>
              <p className="card-desc"><strong>{activeTeachers}</strong> active staff</p>
            </div>
            <div className="card">
              <p className="card-title">Parents</p>
              <p className="card-desc"><strong>{activeParents}</strong> linked profiles</p>
            </div>
            <div className="card">
              <p className="card-title">Pending results</p>
              <p className="card-desc"><strong>{pendingResults}</strong> awaiting upload/review</p>
            </div>
          </div>

          <div style={{ display: 'grid', gap: '1rem', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', marginTop: '1rem' }}>
            <section className="card">
              <p className="card-title">Billing snapshot</p>
              <p className="card-desc">Outstanding this period: <strong>{formatMoney(outstanding, currencyCode)}</strong></p>
              <p className="card-desc">Total unpaid tracked by dashboard: <strong>{formatMoney(unpaidFees, currencyCode)}</strong></p>
              {currentBilling && (
                <p className="card-desc">Current period: <strong>{currentBilling.periodLabel || 'Active cycle'}</strong></p>
              )}
            </section>

            <section className="card">
              <p className="card-title">Coverage check</p>
              <ul>
                <li>Student records loaded: <strong>{students.length}</strong></li>
                <li>Teacher records loaded: <strong>{teachers.length}</strong></li>
                <li>Parent records loaded: <strong>{parents.length}</strong></li>
                <li>Billing records loaded: <strong>{billing.length}</strong></li>
              </ul>
            </section>
          </div>

          <section style={{ marginTop: '1rem' }}>
            <h3 className="section-title">Enrollment by class</h3>
            {classBreakdown.length === 0 ? (
              <p className="empty-state">No class-based enrollment data is available yet.</p>
            ) : (
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Class</th>
                      <th>Grade</th>
                      <th>Students</th>
                    </tr>
                  </thead>
                  <tbody>
                    {classBreakdown.map((row) => (
                      <tr key={`${row.className}-${row.gradeName}`}>
                        <td>{row.className}</td>
                        <td>{row.gradeName}</td>
                        <td>{row.count}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section style={{ marginTop: '1rem' }}>
            <h3 className="section-title">Enrollment by grade</h3>
            {gradeBreakdown.length === 0 ? (
              <p className="empty-state">No grade-level data is available yet.</p>
            ) : (
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Grade</th>
                      <th>Students</th>
                    </tr>
                  </thead>
                  <tbody>
                    {gradeBreakdown.map((row) => (
                      <tr key={row.gradeName}>
                        <td>{row.gradeName}</td>
                        <td>{row.count}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
    </PageLayout>
  );
}
