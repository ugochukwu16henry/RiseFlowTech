import { useState, useEffect, useMemo } from 'react';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import { apiFetch } from '../api';
import './RolePages.css';

function formatValue(value) {
  return value == null || value === '' ? '—' : value;
}

export default function StudentPage() {
  const [dashboard, setDashboard] = useState(null);
  const [results, setResults] = useState([]);
  const [assignments, setAssignments] = useState([]);
  const [notices, setNotices] = useState([]);
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.allSettled([
      apiFetch('/api/students/me/dashboard'),
      apiFetch('/api/results'),
      apiFetch('/api/notices?limit=8'),
      apiFetch('/api/events?limit=8'),
    ])
      .then(async ([dashboardResult, resultsResult, noticesResult, eventsResult]) => {
        let dashboardData = null;
        let resultsData = [];
        let assignmentData = [];
        let noticesData = [];
        let eventsData = [];
        let dashboardError = null;

        if (dashboardResult.status === 'fulfilled') {
          const response = dashboardResult.value;
          if (!response.ok) {
            dashboardError = response.status === 403
              ? 'Your student portal is not enabled yet. Ask your parent to share or reactivate your access from the Parent dashboard.'
              : 'Could not load your student dashboard.';
          } else {
            dashboardData = await response.json();
          }
        } else {
          dashboardError = dashboardResult.reason?.message || 'Could not load your student dashboard.';
        }

        if (resultsResult.status === 'fulfilled') {
          const response = resultsResult.value;
          if (response.ok) {
            const payload = await response.json();
            resultsData = Array.isArray(payload) ? payload : [];
            const firstStudentId = resultsData[0]?.studentId;
            const assignmentsRes = await apiFetch(firstStudentId ? `/api/assignments?studentId=${firstStudentId}` : '/api/assignments');
            if (assignmentsRes.ok) {
              const assignmentsPayload = await assignmentsRes.json();
              assignmentData = Array.isArray(assignmentsPayload) ? assignmentsPayload : [];
            }
          }
        }

        if (noticesResult.status === 'fulfilled' && noticesResult.value.ok) {
          const payload = await noticesResult.value.json();
          noticesData = Array.isArray(payload) ? payload : [];
        }

        if (eventsResult.status === 'fulfilled' && eventsResult.value.ok) {
          const payload = await eventsResult.value.json();
          eventsData = Array.isArray(payload) ? payload : [];
        }

        if (!cancelled) {
          setDashboard(dashboardData);
          setResults(resultsData);
          setAssignments(assignmentData);
          setNotices(noticesData);
          setEvents(eventsData);
          if (dashboardError) setError(dashboardError);
        }
      })
      .catch((err) => {
        if (!cancelled) setError(err.message || 'Could not load your student dashboard.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, []);

  const { subjectCount, averagePct } = useMemo(() => {
    if (!results.length) return { subjectCount: 0, averagePct: null };
    const bySubject = {};
    for (const r of results) {
      const name = r.subject?.name || 'Other';
      if (!bySubject[name]) bySubject[name] = { score: 0, max: 0 };
      bySubject[name].score += Number(r.score) || 0;
      bySubject[name].max += Number(r.maxScore) || 0;
    }
    const keys = Object.keys(bySubject);
    const pctList = keys.map((k) => {
      const { score, max } = bySubject[k];
      return max > 0 ? (score / max) * 100 : null;
    }).filter((p) => p != null);
    const avg = pctList.length
      ? Math.round(pctList.reduce((a, b) => a + b, 0) / pctList.length)
      : null;
    return { subjectCount: keys.length, averagePct: avg };
  }, [results]);

  const teachers = dashboard?.teachers || [];
  const parents = dashboard?.parents || [];
  const classmates = dashboard?.classmates || [];

  return (
    <PageLayout title="Student — My dashboard" role="student">
      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && !dashboard && <p className="empty-state empty-state--error">{error}</p>}

      {dashboard && (
        <>
          {error && <p className="empty-state empty-state--error">{error}</p>}

          <section aria-label="Student snapshot">
            <div className="dashboard-grid">
              <article className="dashboard-card dashboard-card--highlight">
                <p className="dashboard-label">School</p>
                <p className="dashboard-value" style={{ fontSize: '1.25rem' }}>{dashboard.schoolName || '—'}</p>
                <p className="dashboard-sub">Your current school.</p>
              </article>
              <article className="dashboard-card">
                <p className="dashboard-label">Class</p>
                <p className="dashboard-value">{dashboard.className || '—'}</p>
                <p className="dashboard-sub">Grade: {dashboard.gradeName || '—'}</p>
              </article>
              <article className="dashboard-card">
                <p className="dashboard-label">Teachers</p>
                <p className="dashboard-value">{teachers.length}</p>
                <p className="dashboard-sub">Assigned to your class.</p>
              </article>
              <article className="dashboard-card">
                <p className="dashboard-label">Classmates</p>
                <p className="dashboard-value">{classmates.length}</p>
                <p className="dashboard-sub">Visible schoolmates in your class.</p>
              </article>
              <article className="dashboard-card">
                <p className="dashboard-label">Subjects</p>
                <p className="dashboard-value">{subjectCount}</p>
                <p className="dashboard-sub">With recorded assessments.</p>
              </article>
              <article className="dashboard-card">
                <p className="dashboard-label">Overall</p>
                <p className="dashboard-value">{averagePct != null ? `${averagePct}%` : '—'}</p>
                <p className="dashboard-sub">Average across your published scores.</p>
              </article>
            </div>
          </section>

          <section className="dashboard-panel" aria-label="My information" style={{ marginTop: '1rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
              <StudentPhoto studentId={dashboard.studentId} firstName={dashboard.fullName} lastName="" size={72} />
              <div>
                <h2 className="section-title" style={{ marginBottom: '0.25rem' }}>My information</h2>
                <p className="card-desc" style={{ marginBottom: 0 }}>View-only details shared with you by your parent or guardian.</p>
              </div>
            </div>
            <div className="data-table-wrap">
              <table className="data-table">
                <tbody>
                  <tr><th>Full name</th><td>{dashboard.fullName}</td></tr>
                  <tr><th>Admission number</th><td>{formatValue(dashboard.admissionNumber)}</td></tr>
                  <tr><th>Class</th><td>{formatValue(dashboard.className)}</td></tr>
                  <tr><th>Grade</th><td>{formatValue(dashboard.gradeName)}</td></tr>
                  <tr><th>Date of birth</th><td>{formatValue(dashboard.dateOfBirth)}</td></tr>
                  <tr><th>Gender</th><td>{formatValue(dashboard.gender)}</td></tr>
                  <tr><th>Nationality</th><td>{formatValue(dashboard.nationality)}</td></tr>
                  <tr><th>State / LGA</th><td>{[dashboard.stateOfOrigin, dashboard.lga].filter(Boolean).join(' / ') || '—'}</td></tr>
                  <tr><th>Previous school</th><td>{formatValue(dashboard.previousSchool)}</td></tr>
                  <tr><th>Blood group / Genotype</th><td>{[dashboard.bloodGroup, dashboard.genotype].filter(Boolean).join(' / ') || '—'}</td></tr>
                  <tr><th>Allergies</th><td>{formatValue(dashboard.allergies)}</td></tr>
                  <tr><th>Emergency contact</th><td>{[dashboard.emergencyContactName, dashboard.emergencyContactPhone].filter(Boolean).join(' • ') || '—'}</td></tr>
                </tbody>
              </table>
            </div>
          </section>

          <section className="dashboard-grid" style={{ marginTop: '1rem' }}>
            <article className="dashboard-card">
              <h3 className="card-title">Parents who claimed me</h3>
              {parents.length === 0 ? (
                <p className="card-desc">No parent or guardian has been linked yet.</p>
              ) : (
                <ul className="student-record-list">
                  {parents.map((parent) => (
                    <li key={parent.parentId}>
                      <strong>{parent.fullName}</strong>
                      <span>
                        {parent.relationship || 'Parent'}
                        {parent.phone ? ` • ${parent.phone}` : ''}
                        {parent.email ? ` • ${parent.email}` : ''}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </article>

            <article className="dashboard-card">
              <h3 className="card-title">My teachers</h3>
              {teachers.length === 0 ? (
                <p className="card-desc">No teachers are linked to your class yet.</p>
              ) : (
                <ul className="student-record-list">
                  {teachers.map((teacher) => (
                    <li key={`${teacher.teacherId}-${teacher.roleOrSubject || ''}`}>
                      <strong>{teacher.fullName}</strong>
                      <span>{teacher.roleOrSubject || 'Teacher'}</span>
                    </li>
                  ))}
                </ul>
              )}
            </article>
          </section>

          <section className="dashboard-panel" style={{ marginTop: '1rem' }} aria-label="My classmates">
            <h2 className="section-title">My classmates</h2>
            {classmates.length === 0 ? (
              <p className="empty-state">No classmates are visible in your class yet.</p>
            ) : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '0.75rem' }}>
                {classmates.map((classmate) => (
                  <div key={classmate.studentId} className="dashboard-card" style={{ textAlign: 'center' }}>
                    <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '0.5rem' }}>
                      <StudentPhoto studentId={classmate.studentId} firstName={classmate.fullName} lastName="" size={52} />
                    </div>
                    <strong style={{ display: 'block' }}>{classmate.fullName}</strong>
                    <span className="dashboard-sub">Classmate</span>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section style={{ marginTop: '1rem' }}>
            <h2 className="section-title">My results</h2>
            {results.length === 0 ? (
              <p className="empty-state">No results yet. Your grades will appear here when your teachers publish them.</p>
            ) : (
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Subject</th>
                      <th>Type</th>
                      <th>Score</th>
                      <th>Grade</th>
                    </tr>
                  </thead>
                  <tbody>
                    {results.map((r) => (
                      <tr key={r.id}>
                        <td>{r.subject?.name || '—'}</td>
                        <td>{r.assessmentType || '—'}</td>
                        <td>{r.score != null ? `${r.score} / ${r.maxScore ?? ''}` : '—'}</td>
                        <td>{r.gradeLetter || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section style={{ marginTop: '1rem' }}>
            <h2 className="section-title">Assignments</h2>
            {assignments.length === 0 ? (
              <p className="empty-state">No assignments published for your class yet.</p>
            ) : (
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Title</th>
                      <th>Subject</th>
                      <th>Term</th>
                      <th>Due</th>
                      <th>File</th>
                    </tr>
                  </thead>
                  <tbody>
                    {assignments.map((a) => (
                      <tr key={a.id}>
                        <td>{a.title}</td>
                        <td>{a.subjectName}</td>
                        <td>{a.termName}</td>
                        <td>{a.dueDateUtc ? new Date(a.dueDateUtc).toLocaleDateString() : '—'}</td>
                        <td><a href={`/api/files/${a.fileAssetId}/download`}>{a.originalFileName}</a></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="dashboard-grid" style={{ marginTop: '1rem' }}>
            <article className="dashboard-card">
              <h3 className="card-title">School notices</h3>
              {notices.length === 0 ? (
                <p className="card-desc">No notices yet.</p>
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
                <p className="card-desc">No upcoming events.</p>
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
        </>
      )}
    </PageLayout>
  );
}
