import { Fragment, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';

function buildPublicUrl(relativePath) {
  if (!relativePath) return null;
  if (relativePath.startsWith('http://') || relativePath.startsWith('https://')) return relativePath;
  const normalizedPath = relativePath.replace(/^\/+/, '');
  const base = getApiBase();
  return base ? `${base}/${normalizedPath}` : `/${normalizedPath}`;
}

function buildWhatsAppUrl(phone, schoolName) {
  const raw = (phone || '').replace(/\D/g, '');
  if (raw.length < 10) return null;
  const text = encodeURIComponent(`Hello ${schoolName}, this is the RiseFlow Super Admin team reaching out.`);
  return `https://wa.me/${raw}?text=${text}`;
}

function buildMailtoUrl(email, schoolName) {
  if (!email) return null;
  const [firstEmail] = String(email).split(',').map((value) => value.trim()).filter(Boolean);
  if (!firstEmail) return null;
  return `mailto:${firstEmail}?subject=${encodeURIComponent(`RiseFlow support for ${schoolName}`)}`;
}

function formatOnboarding(dateValue) {
  const parsed = dateValue ? new Date(dateValue) : null;
  if (!parsed || Number.isNaN(parsed.getTime())) {
    return { full: '—', day: '—', year: '—' };
  }

  return {
    full: parsed.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }),
    day: parsed.toLocaleDateString(undefined, { weekday: 'long' }),
    year: String(parsed.getFullYear()),
  };
}

export default function SuperAdminSchoolsPage() {
  const [schools, setSchools] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [expandedSchoolId, setExpandedSchoolId] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    apiFetch('/api/superadmin/schools', { skipTenantHeader: true })
      .then((res) => {
        if (cancelled) return null;
        if (!res.ok) throw new Error('Could not load schools');
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setSchools(Array.isArray(data) ? data : []);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Failed to load schools');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const totals = schools.reduce((summary, school) => {
    summary.students += school.studentCount ?? 0;
    summary.teachers += school.teacherCount ?? 0;
    summary.parents += school.parentCount ?? 0;
    if (school.isActive) summary.active += 1;
    return summary;
  }, { students: 0, teachers: 0, parents: 0, active: 0 });

  return (
    <PageLayout title="Super Admin — School Management" role="super">
      <h2 className="section-title">School management</h2>
      <p className="control-room-intro">
        View school contact details, onboarding history, logos, registration files, and direct contact actions in one place.
      </p>

      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}
      {!loading && !error && schools.length === 0 && <p className="empty-state">No schools found.</p>}

      {!loading && !error && schools.length > 0 && (
        <>
          <div className="summary-cards">
            <div className="summary-card">
              <span className="summary-value">{schools.length}</span>
              <span className="summary-label">Total schools</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{totals.active}</span>
              <span className="summary-label">Active schools</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{totals.students}</span>
              <span className="summary-label">Students</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{totals.teachers}</span>
              <span className="summary-label">Teachers</span>
            </div>
          </div>

          <div className="data-table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>School</th>
                  <th>Owner / principal</th>
                  <th>Country</th>
                  <th>Students</th>
                  <th>Teachers</th>
                  <th>Contact</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {schools.map((s) => {
                  const isExpanded = expandedSchoolId === s.id;
                  const onboarding = formatOnboarding(s.createdAtUtc);
                  const contactEmail = s.schoolEmail || s.ownerEmail;
                  const mailtoUrl = buildMailtoUrl(contactEmail, s.name);
                  const whatsAppUrl = buildWhatsAppUrl(s.whatsAppNumber || s.phone, s.name);
                  const telLink = s.phone ? `tel:${s.phone}` : null;
                  const logoUrl = buildPublicUrl(s.logoPath);
                  const registrationDocUrl = buildPublicUrl(s.registrationDocumentPath);

                  return (
                    <Fragment key={s.id}>
                      <tr>
                        <td>
                          <div className="sa-school-cell">
                            {logoUrl ? (
                              <img className="sa-school-table-logo" src={logoUrl} alt={`${s.name} logo`} loading="lazy" />
                            ) : (
                              <div className="sa-school-logo-placeholder" aria-hidden="true">
                                {(s.name || 'S').trim().charAt(0).toUpperCase()}
                              </div>
                            )}
                            <div>
                              <strong>{s.name}</strong>
                              <span className="sa-school-secondary">{s.schoolEmail || 'No school email yet'}</span>
                            </div>
                          </div>
                        </td>
                        <td>
                          <strong>{s.ownerName || s.principalName || '—'}</strong>
                          <span className="sa-school-secondary">Onboarded {onboarding.full}</span>
                        </td>
                        <td>{s.countryName || s.countryCode || '—'}</td>
                        <td>{s.studentCount ?? 0}</td>
                        <td>{s.teacherCount ?? 0}</td>
                        <td>
                          <div className="sa-school-contact">
                            <span>{contactEmail || 'No email saved'}</span>
                            <span>{s.phone || 'No phone saved'}</span>
                          </div>
                        </td>
                        <td>
                          <span className={s.isActive ? 'pill pill--success' : 'pill pill--muted'}>
                            {s.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td>
                          <div className="sa-school-action-stack">
                            <button
                              type="button"
                              className="btn-primary-action btn-primary-action--ghost"
                              onClick={() => setExpandedSchoolId((current) => (current === s.id ? null : s.id))}
                            >
                              {isExpanded ? 'Hide details' : 'View details'}
                            </button>
                            <Link className="btn-primary-action btn-primary-action--ghost" to={`/super-admin/data-offboarding?schoolId=${s.id}`}>
                              Offboard
                            </Link>
                          </div>
                        </td>
                      </tr>
                      {isExpanded && (
                        <tr className="sa-school-details-row">
                          <td colSpan={8}>
                            <div className="sa-school-detail-panel">
                              <div className="sa-school-media">
                                {logoUrl ? (
                                  <a href={logoUrl} target="_blank" rel="noopener noreferrer">
                                    <img className="sa-school-detail-logo" src={logoUrl} alt={`${s.name} logo`} loading="lazy" />
                                  </a>
                                ) : (
                                  <div className="sa-school-logo-placeholder sa-school-logo-placeholder--large">
                                    {(s.name || 'S').trim().charAt(0).toUpperCase()}
                                  </div>
                                )}
                                <div className="sa-school-media-actions">
                                  {logoUrl && (
                                    <a className="btn-primary-action btn-primary-action--ghost" href={logoUrl} target="_blank" rel="noopener noreferrer">
                                      View logo
                                    </a>
                                  )}
                                  {registrationDocUrl && (
                                    <a className="btn-primary-action btn-primary-action--ghost" href={registrationDocUrl} target="_blank" rel="noopener noreferrer">
                                      View registration doc
                                    </a>
                                  )}
                                </div>
                              </div>

                              <div className="sa-school-detail-body">
                                <div className="sa-school-detail-grid">
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">School email</span>
                                    <p className="sa-detail-value">{s.schoolEmail || '—'}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Phone</span>
                                    <p className="sa-detail-value">{s.phone || '—'}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">WhatsApp</span>
                                    <p className="sa-detail-value">{s.whatsAppNumber || s.phone || '—'}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Address</span>
                                    <p className="sa-detail-value">{s.address || '—'}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Country</span>
                                    <p className="sa-detail-value">{s.countryName || s.countryCode || '—'}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Owner / principal</span>
                                    <p className="sa-detail-value">{s.ownerName || s.principalName || '—'}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Owner email</span>
                                    <p className="sa-detail-value">{s.ownerEmail || '—'}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Registration no.</span>
                                    <p className="sa-detail-value">{s.cacNumber || '—'}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Onboarded date</span>
                                    <p className="sa-detail-value">{onboarding.full}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Onboarded day</span>
                                    <p className="sa-detail-value">{onboarding.day}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Onboarded year</span>
                                    <p className="sa-detail-value">{onboarding.year}</p>
                                  </div>
                                  <div className="sa-school-detail-card">
                                    <span className="dashboard-label">Community</span>
                                    <p className="sa-detail-value">
                                      {`${s.studentCount ?? 0} students • ${s.teacherCount ?? 0} teachers • ${s.parentCount ?? 0} parents`}
                                    </p>
                                  </div>
                                </div>

                                <div className="dashboard-actions">
                                  {mailtoUrl && (
                                    <a className="btn-primary-action" href={mailtoUrl}>
                                      Email school
                                    </a>
                                  )}
                                  {whatsAppUrl && (
                                    <a className="btn-whatsapp" href={whatsAppUrl} target="_blank" rel="noopener noreferrer">
                                      WhatsApp
                                    </a>
                                  )}
                                  {telLink && (
                                    <a className="btn-primary-action btn-primary-action--ghost" href={telLink}>
                                      Call school
                                    </a>
                                  )}
                                  <Link className="btn-primary-action btn-primary-action--ghost" to={`/super-admin/data-offboarding?schoolId=${s.id}`}>
                                    Offboard school
                                  </Link>
                                </div>
                              </div>
                            </div>
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
    </PageLayout>
  );
}