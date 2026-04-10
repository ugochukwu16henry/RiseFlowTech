import { useEffect, useMemo, useState } from 'react';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import TeacherPhoto from '../components/TeacherPhoto';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';
import './StudentPage.css';

function formatDate(value) {
  if (!value) return null;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleDateString();
}

function classmateName(classmate) {
  return [classmate.firstName, classmate.middleName, classmate.lastName].filter(Boolean).join(' ');
}

export default function StudentPage() {
  const [dashboard, setDashboard] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    apiFetch('/api/students/me/dashboard')
      .then(async (res) => {
        if (cancelled) return null;
        if (res.status === 401 || res.status === 403) {
          throw new Error('Your student dashboard is not ready yet. Ask your parent to share your sign-in details from the Parent dashboard.');
        }
        if (!res.ok) {
          throw new Error('Could not load your student dashboard.');
        }
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setDashboard(data || null);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message || 'Could not load your student dashboard.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const schoolLogoUrl = useMemo(() => {
    if (!dashboard?.schoolLogoFileName) return null;
    return `${getApiBase()}/${String(dashboard.schoolLogoFileName).replace(/^\/+/, '')}`;
  }, [dashboard?.schoolLogoFileName]);

  const quickFacts = useMemo(() => {
    if (!dashboard) return [];
    return [
      { label: 'School', value: dashboard.schoolName || '—', sub: 'Your current school' },
      { label: 'Class', value: dashboard.class?.name || '—', sub: 'Your assigned class' },
      { label: 'Teachers', value: dashboard.assignedTeachers?.length ?? 0, sub: 'Teaching or class staff' },
      { label: 'Classmates', value: dashboard.classmates?.length ?? 0, sub: 'Students in your class' },
    ];
  }, [dashboard]);

  const profileFields = useMemo(() => {
    if (!dashboard) return [];
    return [
      ['Admission number', dashboard.admissionNumber],
      ['Grade', dashboard.grade?.name || dashboard.class?.grade?.name],
      ['Date of birth', formatDate(dashboard.dateOfBirth)],
      ['Gender', dashboard.gender],
      ['Nationality', dashboard.nationality],
      ['State / LGA', [dashboard.stateOfOrigin, dashboard.lga].filter(Boolean).join(', ') || null],
      ['Previous school', dashboard.previousSchool],
      ['Previous class', dashboard.previousClass],
      ['Blood group', dashboard.bloodGroup],
      ['Genotype', dashboard.genotype],
      ['Allergies', dashboard.allergies],
      ['Emergency contact', dashboard.emergencyContactName],
      ['Emergency phone', dashboard.emergencyContactPhone],
    ].filter(([, value]) => Boolean(value));
  }, [dashboard]);

  const termResults = Array.isArray(dashboard?.termResults) ? dashboard.termResults : [];
  const teachers = Array.isArray(dashboard?.assignedTeachers) ? dashboard.assignedTeachers : [];
  const parents = Array.isArray(dashboard?.studentParents) ? dashboard.studentParents : [];
  const classmates = Array.isArray(dashboard?.classmates) ? dashboard.classmates : [];

  const nameParts = (dashboard?.fullName || '').split(' ').filter(Boolean);
  const photoFirstName = nameParts[0] || 'Student';
  const photoLastName = nameParts[nameParts.length - 1] || '';
  const schoolInitials = (dashboard?.schoolName || 'School Portal')
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join('') || 'SP';

  return (
    <PageLayout title="Student dashboard" role="student">
      {loading && <p className="empty-state" aria-busy="true">Loading your dashboard…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}

      {!loading && !error && !dashboard && (
        <p className="empty-state">Your student dashboard will appear here once your parent shares your sign-in details.</p>
      )}

      {!loading && !error && dashboard && (
        <>
          <section className="student-dashboard-hero" aria-label="Student welcome banner">
            <div className="student-dashboard-hero__content">
              <span className="student-dashboard-hero__badge">Student portal</span>
              <h2>Welcome, {dashboard.fullName}</h2>
              <p>
                View your school information, teachers, parents who claimed you, and your classmates — all in one safe, read-only space.
              </p>
              <div className="student-dashboard-hero__meta">
                <span>{dashboard.schoolName}</span>
                <span>{dashboard.class?.name || 'Class not assigned yet'}</span>
                <span>{dashboard.currentAveragePercentage != null ? `${dashboard.currentAveragePercentage}% average` : 'No results yet'}</span>
              </div>
            </div>

            <div className="student-dashboard-hero__media">
              <div className="student-dashboard-hero__brand">
                {schoolLogoUrl ? (
                  <img src={schoolLogoUrl} alt={dashboard.schoolName} className="student-dashboard-hero__logo" />
                ) : (
                  <div className="student-dashboard-hero__logo-fallback" aria-hidden="true">
                    {schoolInitials}
                  </div>
                )}
                <div>
                  <strong>{dashboard.schoolName}</strong>
                  <p>{dashboard.class?.grade?.name || dashboard.grade?.name || 'Student space'}</p>
                </div>
              </div>

              <div className="student-dashboard-hero__student">
                <StudentPhoto studentId={dashboard.id} firstName={photoFirstName} lastName={photoLastName} size={56} />
                <span>{dashboard.admissionNumber || 'Admission number will appear here'}</span>
              </div>
            </div>
          </section>

          <section aria-label="Student snapshot">
            <div className="dashboard-grid">
              <article className="dashboard-card dashboard-card--highlight">
                <p className="dashboard-label">Overall average</p>
                <p className="dashboard-value">{dashboard.currentAveragePercentage != null ? `${dashboard.currentAveragePercentage}%` : '—'}</p>
                <p className="dashboard-sub">Across your published results.</p>
              </article>
              {quickFacts.map((fact) => (
                <article key={fact.label} className="dashboard-card">
                  <p className="dashboard-label">{fact.label}</p>
                  <p className="dashboard-value">{fact.value}</p>
                  <p className="dashboard-sub">{fact.sub}</p>
                </article>
              ))}
            </div>
          </section>

          <div className="student-dashboard-layout">
            <section className="student-dashboard-card" aria-label="My information">
              <h3 className="card-title">My information</h3>
              <p className="card-desc">Only the details your parent has chosen to share appear here.</p>
              {profileFields.length === 0 ? (
                <p className="empty-state">No extra personal details are shared right now.</p>
              ) : (
                <dl className="profile-dl">
                  {profileFields.map(([label, value]) => (
                    <div key={label}>
                      <dt>{label}</dt>
                      <dd>{value}</dd>
                    </div>
                  ))}
                </dl>
              )}
            </section>

            <section className="student-dashboard-card" aria-label="Parents who claimed me">
              <h3 className="card-title">Parents who claimed me</h3>
              <p className="card-desc">These are the parents or guardians linked to your profile.</p>
              {parents.length === 0 ? (
                <p className="empty-state">No parent or guardian has been linked yet.</p>
              ) : (
                <ul className="student-dashboard-people-list">
                  {parents.map((parent) => (
                    <li key={parent.parentId} className="student-dashboard-people-item">
                      <strong>{[parent.firstName, parent.lastName].filter(Boolean).join(' ')}</strong>
                      <span>
                        {parent.relationshipToStudent || 'Guardian'}
                        {parent.phone ? ` • ${parent.phone}` : ''}
                        {parent.email ? ` • ${parent.email}` : ''}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>

          <section className="student-dashboard-card" aria-label="Assigned teachers">
            <h3 className="card-title">My teachers</h3>
            <p className="card-desc">Teachers assigned to your class and subjects.</p>
            {teachers.length === 0 ? (
              <p className="empty-state">Your teachers will appear here once they are assigned.</p>
            ) : (
              <div className="student-dashboard-teacher-grid">
                {teachers.map((teacher) => (
                  <article key={`${teacher.teacherId}-${teacher.roleOrSubject || ''}`} className="student-dashboard-teacher-card">
                    <TeacherPhoto teacherId={teacher.teacherId} fullName={teacher.fullName} size={40} />
                    <div>
                      <strong>{teacher.fullName}</strong>
                      <p>{teacher.roleOrSubject || 'Teacher'}</p>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>

          <section className="student-dashboard-card" aria-label="My classmates">
            <h3 className="card-title">My classmates</h3>
            <p className="card-desc">Here are the students in your class.</p>
            {classmates.length === 0 ? (
              <p className="empty-state">No classmates are available to show yet.</p>
            ) : (
              <div className="student-dashboard-classmate-grid">
                {classmates.map((classmate) => (
                  <article key={classmate.studentId} className="student-dashboard-classmate-card">
                    <StudentPhoto
                      studentId={classmate.studentId}
                      firstName={classmate.firstName}
                      lastName={classmate.lastName}
                      size={48}
                    />
                    <span>{classmateName(classmate)}</span>
                  </article>
                ))}
              </div>
            )}
          </section>

          <section className="student-dashboard-card" aria-label="Results by term">
            <h3 className="card-title">My results</h3>
            <p className="card-desc">Published scores and grades from your school records.</p>
            {termResults.length === 0 ? (
              <p className="empty-state">No results have been published yet.</p>
            ) : (
              <div className="student-dashboard-term-grid">
                {termResults.map((term) => (
                  <article key={term.term} className="student-term-card">
                    <div className="student-term-card-header">
                      <strong>{term.term}</strong>
                      <span>{term.averagePercentage}% avg</span>
                    </div>
                    <ul className="student-term-results">
                      {(term.results || []).map((result) => (
                        <li key={result.resultId}>
                          <span>{result.subject}</span>
                          <span>{result.percentage}% {result.gradeLetter ? `(${result.gradeLetter})` : ''}</span>
                        </li>
                      ))}
                    </ul>
                  </article>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </PageLayout>
  );
}
