import { useEffect, useMemo, useRef, useState } from 'react';
import PageLayout from '../components/PageLayout';
import TeacherPhoto from '../components/TeacherPhoto';
import { apiFetch } from '../api';
import './RolePages.css';

function percentComplete(profile) {
  if (!profile) return 0;
  const checks = [
    profile.firstName,
    profile.lastName,
    profile.email,
    profile.phone,
    profile.roleTitle,
    profile.department,
    profile.residentialAddress,
  ];
  const filled = checks.filter((v) => String(v || '').trim().length > 0).length;
  return Math.round((filled / checks.length) * 100);
}

function toFriendlyDate(value) {
  if (!value) return 'No date';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'No date';
  return date.toLocaleDateString();
}

export default function StaffPage() {
  const [metrics, setMetrics] = useState(null);
  const [me, setMe] = useState(null);
  const [rolePermissions, setRolePermissions] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [error, setError] = useState(null);
  const [notices, setNotices] = useState([]);
  const [events, setEvents] = useState([]);
  const [activeView, setActiveView] = useState('overview');
  const [form, setForm] = useState({
    firstName: '',
    lastName: '',
    phone: '',
    whatsAppNumber: '',
    residentialAddress: '',
  });
  const photoInputRef = useRef(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.all([
      apiFetch('/api/teachers/me').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/schools/staff/dashboard-metrics').then((r) => (r.ok ? r.json() : null)),
      apiFetch('/api/notices?limit=5').then((r) => (r.ok ? r.json() : [])),
      apiFetch('/api/events?limit=5').then((r) => (r.ok ? r.json() : [])),
    ])
      .then(([profileConfig, metricsPayload, noticeList, eventList]) => {
        if (cancelled) return;
        const profile = profileConfig?.teacher || profileConfig || null;
        setMe(profile);
        setRolePermissions(profileConfig?.permissions || null);
        setMetrics(metricsPayload);
        setNotices(Array.isArray(noticeList) ? noticeList : []);
        setEvents(Array.isArray(eventList) ? eventList : []);
      })
      .catch((e) => {
        if (!cancelled) setError(e.message || 'Failed to load staff dashboard.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!me) return;
    setForm({
      firstName: me.firstName || '',
      lastName: me.lastName || '',
      phone: me.phone || '',
      whatsAppNumber: me.whatsAppNumber || '',
      residentialAddress: me.residentialAddress || '',
    });
  }, [me]);

  const profileCompletion = useMemo(() => percentComplete(me), [me]);
  const can = (key, fallback = true) => {
    if (!rolePermissions || typeof rolePermissions[key] !== 'boolean') return fallback;
    return !!rolePermissions[key];
  };

  const canViewApprovals = can('canApproveResults', true) || can('canManageFees', true);
  const canViewOfficeQueue = can('canManageTeachers', true) || can('canAssignClasses', true) || can('canManageFees', true);
  const canViewCommunications = can('canSendParentBroadcasts', true);

  const currentView = (() => {
    if (activeView === 'approvals' && canViewApprovals) return 'approvals';
    if (activeView === 'operations' && canViewOfficeQueue) return 'operations';
    if (activeView === 'communications' && canViewCommunications) return 'communications';
    if (activeView === 'profile') return 'profile';
    return 'overview';
  })();

  const updateField = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const saveProfile = async () => {
    if (!me) return;
    setSaving(true);
    try {
      const payload = {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        middleName: me.middleName || null,
        phone: form.phone.trim() || null,
        whatsAppNumber: form.whatsAppNumber.trim() || null,
        dateOfBirth: me.dateOfBirth || null,
        gender: me.gender || null,
        nationality: me.nationality || null,
        stateOfOrigin: me.stateOfOrigin || null,
        lga: me.lga || null,
        religion: me.religion || null,
        residentialAddress: form.residentialAddress.trim() || null,
        subjectSpecialization: me.subjectSpecialization || null,
        highestQualification: me.highestQualification || null,
        fieldOfStudy: me.fieldOfStudy || null,
        yearsOfExperience: me.yearsOfExperience ?? null,
        previousSchools: me.previousSchools || null,
        professionalBodies: me.professionalBodies || null,
        customFields: null,
      };

      const res = await apiFetch('/api/teachers/me', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error(await res.text());

      const updated = await res.json();
      const profile = updated?.teacher || updated;
      setMe(profile);
      setRolePermissions(updated?.permissions || rolePermissions);
      // eslint-disable-next-line no-alert
      alert('Staff profile updated successfully.');
    } catch (e) {
      // eslint-disable-next-line no-alert
      alert(e.message || 'Could not save profile right now.');
    } finally {
      setSaving(false);
    }
  };

  const handlePhotoChange = async (e) => {
    const file = e.target?.files?.[0];
    if (!file || !me?.id) return;

    if (file.size > 5 * 1024 * 1024) {
      // eslint-disable-next-line no-alert
      alert('Photo is too large. Please choose an image up to 5 MB.');
      e.target.value = '';
      return;
    }

    setUploadingPhoto(true);
    const formData = new FormData();
    formData.append('file', file);

    try {
      const res = await apiFetch(`/api/teachers/${me.id}/photo`, {
        method: 'POST',
        body: formData,
      });
      if (!res.ok) throw new Error(await res.text());

      setMe((prev) => (prev ? { ...prev } : prev));
    } catch (err) {
      // eslint-disable-next-line no-alert
      alert(err.message || 'Could not upload photo.');
    } finally {
      setUploadingPhoto(false);
      e.target.value = '';
    }
  };

  return (
    <PageLayout title="Staff dashboard" role="staff">
      <div className="school-admin-shell">
        <aside className="school-admin-nav" aria-label="Staff sections">
          <button type="button" className={`school-admin-nav-btn ${currentView === 'overview' ? 'is-active' : ''}`} onClick={() => setActiveView('overview')}>
            Overview
          </button>
          <button type="button" className={`school-admin-nav-btn ${currentView === 'profile' ? 'is-active' : ''}`} onClick={() => setActiveView('profile')}>
            Profile
          </button>
          {canViewApprovals && (
            <button type="button" className={`school-admin-nav-btn ${currentView === 'approvals' ? 'is-active' : ''}`} onClick={() => setActiveView('approvals')}>
              Approvals
            </button>
          )}
          {canViewOfficeQueue && (
            <button type="button" className={`school-admin-nav-btn ${currentView === 'operations' ? 'is-active' : ''}`} onClick={() => setActiveView('operations')}>
              Operations
            </button>
          )}
          {canViewCommunications && (
            <button type="button" className={`school-admin-nav-btn ${currentView === 'communications' ? 'is-active' : ''}`} onClick={() => setActiveView('communications')}>
              Communications
            </button>
          )}
        </aside>

        <main className="main school-admin-view">
          {currentView === 'overview' && (
            <section className="dashboard-grid">
              <article className="dashboard-card dashboard-card--warning">
                <p className="dashboard-label">Tasks</p>
                <p className="dashboard-value">{metrics?.tasksCount ?? 0}</p>
                <p className="dashboard-sub">
                  {metrics?.personalAssignmentsCount ?? 0} personal assignments, {metrics?.pendingPromotionRequestsCount ?? 0} promotion requests.
                </p>
              </article>

              {canViewApprovals && (
                <article className="dashboard-card">
                  <p className="dashboard-label">Pending approvals</p>
                  <p className="dashboard-value">{metrics?.pendingApprovalsCount ?? 0}</p>
                  <p className="dashboard-sub">
                    {metrics?.pendingFeeVerificationsCount ?? 0} fee verifications, {metrics?.pendingResultEntriesCount ?? 0} result entries.
                  </p>
                </article>
              )}

              {canViewOfficeQueue && (
                <article className="dashboard-card">
                  <p className="dashboard-label">Office queue</p>
                  <p className="dashboard-value">{metrics?.officeQueueCount ?? 0}</p>
                  <p className="dashboard-sub">Includes {metrics?.recentDeniedAttemptsCount ?? 0} denied attempts in the last 7 days.</p>
                </article>
              )}

              <article className="dashboard-card dashboard-card--highlight">
                <p className="dashboard-label">Profile completion</p>
                <p className="dashboard-value">{profileCompletion}%</p>
                <p className="dashboard-sub">Role: {me?.roleTitle || 'Staff'} • Department: {me?.department || 'Not set'}</p>
              </article>
            </section>
          )}

          {loading && <p className="card-desc">Loading staff workspace...</p>}
          {error && <p className="card-desc" style={{ color: '#b91c1c' }}>{error}</p>}

          {!loading && me && currentView === 'profile' && (
            <section className="card" style={{ marginBottom: '1rem' }}>
              <h3 className="section-title">My staff profile</h3>
              <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', alignItems: 'center' }}>
                <div style={{ display: 'grid', gap: '0.5rem', justifyItems: 'center' }}>
                  <TeacherPhoto
                    teacherId={me.id}
                    firstName={me.firstName}
                    lastName={me.lastName}
                    profilePhotoFileName={me.profilePhotoFileName}
                    size={84}
                  />
                  <button
                    type="button"
                    className="btn-upload-photo"
                    onClick={() => photoInputRef.current?.click()}
                    disabled={uploadingPhoto}
                  >
                    {uploadingPhoto ? 'Uploading...' : 'Upload photo'}
                  </button>
                  <input
                    ref={photoInputRef}
                    type="file"
                    accept="image/png,image/jpeg,image/jpg,image/gif,image/webp"
                    style={{ display: 'none' }}
                    onChange={handlePhotoChange}
                  />
                </div>

                <div style={{ flex: '1 1 420px', display: 'grid', gap: '0.75rem' }}>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(220px,1fr))', gap: '0.75rem' }}>
                    <label>
                      <span className="card-desc">First name</span>
                      <input className="claim-input signup-input" value={form.firstName} onChange={(e) => updateField('firstName', e.target.value)} />
                    </label>
                    <label>
                      <span className="card-desc">Last name</span>
                      <input className="claim-input signup-input" value={form.lastName} onChange={(e) => updateField('lastName', e.target.value)} />
                    </label>
                    <label>
                      <span className="card-desc">Phone</span>
                      <input className="claim-input signup-input" value={form.phone} onChange={(e) => updateField('phone', e.target.value)} />
                    </label>
                    <label>
                      <span className="card-desc">WhatsApp</span>
                      <input className="claim-input signup-input" value={form.whatsAppNumber} onChange={(e) => updateField('whatsAppNumber', e.target.value)} />
                    </label>
                  </div>

                  <label>
                    <span className="card-desc">Residential address</span>
                    <textarea
                      className="claim-input signup-input"
                      rows={2}
                      value={form.residentialAddress}
                      onChange={(e) => updateField('residentialAddress', e.target.value)}
                    />
                  </label>

                  <div className="dashboard-actions">
                    <button type="button" className="btn-primary-action" onClick={saveProfile} disabled={saving}>
                      {saving ? 'Saving...' : 'Save profile'}
                    </button>
                  </div>
                </div>
              </div>
            </section>
          )}

          {!loading && me && currentView === 'approvals' && canViewApprovals && (
            <section className="card" style={{ marginBottom: '1rem' }}>
              <h3 className="section-title">Approvals workspace</h3>
              <p className="card-desc">Pending approvals requiring your role permissions.</p>
              <div className="dashboard-grid" style={{ marginTop: '0.75rem' }}>
                <article className="dashboard-card">
                  <p className="dashboard-label">Total pending approvals</p>
                  <p className="dashboard-value">{metrics?.pendingApprovalsCount ?? 0}</p>
                </article>
                <article className="dashboard-card">
                  <p className="dashboard-label">Fee verifications</p>
                  <p className="dashboard-value">{metrics?.pendingFeeVerificationsCount ?? 0}</p>
                </article>
                <article className="dashboard-card">
                  <p className="dashboard-label">Result entries</p>
                  <p className="dashboard-value">{metrics?.pendingResultEntriesCount ?? 0}</p>
                </article>
              </div>
            </section>
          )}

          {!loading && me && currentView === 'operations' && canViewOfficeQueue && (
            <section className="card" style={{ marginBottom: '1rem' }}>
              <h3 className="section-title">Operations queue</h3>
              <p className="card-desc">Operational backlog and governance queue in your scope.</p>
              <div className="dashboard-grid" style={{ marginTop: '0.75rem' }}>
                <article className="dashboard-card">
                  <p className="dashboard-label">Office queue</p>
                  <p className="dashboard-value">{metrics?.officeQueueCount ?? 0}</p>
                </article>
                <article className="dashboard-card">
                  <p className="dashboard-label">Denied attempts (7 days)</p>
                  <p className="dashboard-value">{metrics?.recentDeniedAttemptsCount ?? 0}</p>
                </article>
                <article className="dashboard-card">
                  <p className="dashboard-label">Promotion requests</p>
                  <p className="dashboard-value">{metrics?.pendingPromotionRequestsCount ?? 0}</p>
                </article>
              </div>
            </section>
          )}

          {!loading && me && currentView === 'communications' && canViewCommunications && (
            <>
              <section className="card" style={{ marginBottom: '1rem' }}>
                <h3 className="section-title">Recent notices</h3>
                {notices.length === 0 ? (
                  <p className="card-desc">No notices yet.</p>
                ) : (
                  <ul className="card-list">
                    {notices.map((notice) => (
                      <li key={notice.id}>
                        <p className="card-title" style={{ marginBottom: '0.2rem' }}>{notice.title || 'Notice'}</p>
                        <p className="card-desc">{notice.message || 'No details provided.'}</p>
                        <p className="card-desc" style={{ marginTop: '0.3rem' }}>{toFriendlyDate(notice.createdAtUtc || notice.createdAt)}</p>
                      </li>
                    ))}
                  </ul>
                )}
              </section>

              <section className="card">
                <h3 className="section-title">Upcoming events</h3>
                {events.length === 0 ? (
                  <p className="card-desc">No events available.</p>
                ) : (
                  <ul className="card-list">
                    {events.map((event) => (
                      <li key={event.id}>
                        <p className="card-title" style={{ marginBottom: '0.2rem' }}>{event.title || event.name || 'Event'}</p>
                        <p className="card-desc">{event.description || 'No description.'}</p>
                        <p className="card-desc" style={{ marginTop: '0.3rem' }}>
                          {toFriendlyDate(event.startDateUtc || event.startsAtUtc || event.startDate)}
                        </p>
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            </>
          )}

          {!loading && me && currentView === 'overview' && (
            <p className="card-desc" style={{ marginTop: '1rem' }}>
              Use the left menu to access only the sections your role is allowed to use.
            </p>
          )}
        </main>
      </div>
    </PageLayout>
  );
}
