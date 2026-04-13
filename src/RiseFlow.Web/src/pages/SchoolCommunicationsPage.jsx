import { useEffect, useState } from 'react';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

export default function SchoolCommunicationsPage() {
  const [notices, setNotices] = useState([]);
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState(null);
  const [savingNotice, setSavingNotice] = useState(false);
  const [savingEvent, setSavingEvent] = useState(false);
  const [noticeForm, setNoticeForm] = useState({ title: '', body: '', targetRolesCsv: 'All', expiresAtUtc: '', isActive: true });
  const [eventForm, setEventForm] = useState({ title: '', description: '', startAtUtc: '', endAtUtc: '', colorHex: '#1f7a8c' });

  const loadData = async () => {
    setLoading(true);
    setMessage(null);
    try {
      const [noticeRes, eventRes] = await Promise.all([
        apiFetch('/api/notices?limit=50'),
        apiFetch('/api/events?limit=100'),
      ]);

      const noticeData = noticeRes.ok ? await noticeRes.json() : [];
      const eventData = eventRes.ok ? await eventRes.json() : [];
      setNotices(Array.isArray(noticeData) ? noticeData : []);
      setEvents(Array.isArray(eventData) ? eventData : []);
    } catch {
      setMessage('Could not load communication data right now.');
      setNotices([]);
      setEvents([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const createNotice = async () => {
    if (!noticeForm.title.trim() || !noticeForm.body.trim()) {
      setMessage('Notice title and body are required.');
      return;
    }

    setSavingNotice(true);
    setMessage(null);
    try {
      const payload = {
        title: noticeForm.title.trim(),
        body: noticeForm.body.trim(),
        targetRolesCsv: noticeForm.targetRolesCsv || 'All',
        expiresAtUtc: noticeForm.expiresAtUtc ? new Date(noticeForm.expiresAtUtc).toISOString() : null,
        isActive: noticeForm.isActive,
      };

      const res = await apiFetch('/api/notices', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error(await res.text());

      setNoticeForm({ title: '', body: '', targetRolesCsv: 'All', expiresAtUtc: '', isActive: true });
      setMessage('Notice published.');
      await loadData();
    } catch (e) {
      setMessage(e.message || 'Could not publish notice.');
    } finally {
      setSavingNotice(false);
    }
  };

  const createEvent = async () => {
    if (!eventForm.title.trim() || !eventForm.startAtUtc || !eventForm.endAtUtc) {
      setMessage('Event title, start, and end are required.');
      return;
    }

    setSavingEvent(true);
    setMessage(null);
    try {
      const payload = {
        title: eventForm.title.trim(),
        description: eventForm.description.trim() || null,
        startAtUtc: new Date(eventForm.startAtUtc).toISOString(),
        endAtUtc: new Date(eventForm.endAtUtc).toISOString(),
        colorHex: eventForm.colorHex || null,
      };

      const res = await apiFetch('/api/events', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) throw new Error(await res.text());

      setEventForm({ title: '', description: '', startAtUtc: '', endAtUtc: '', colorHex: '#1f7a8c' });
      setMessage('Event saved.');
      await loadData();
    } catch (e) {
      setMessage(e.message || 'Could not save event.');
    } finally {
      setSavingEvent(false);
    }
  };

  const removeNotice = async (id) => {
    const res = await apiFetch(`/api/notices/${id}`, { method: 'DELETE' });
    if (res.ok) {
      setNotices((current) => current.filter((n) => n.id !== id));
    }
  };

  const removeEvent = async (id) => {
    const res = await apiFetch(`/api/events/${id}`, { method: 'DELETE' });
    if (res.ok) {
      setEvents((current) => current.filter((n) => n.id !== id));
    }
  };

  return (
    <PageLayout title="School Communications" role="school">
      <section aria-label="Communications workspace">
        <h2 className="section-title">Notices and events</h2>
        <p className="card-desc">Publish school-wide updates and calendar events for teachers, parents, and students.</p>
        {message && <p className="student-note student-note--success">{message}</p>}
      </section>

      {loading ? (
        <p className="empty-state" aria-busy="true">Loading communications…</p>
      ) : (
        <>
          <section className="student-record-grid" style={{ marginTop: '1rem' }}>
            <article className="student-record-card">
              <h3 className="dashboard-section-title">Publish notice</h3>
              <div className="student-edit-grid">
                <label>
                  <span>Title</span>
                  <input className="form-input" value={noticeForm.title} onChange={(e) => setNoticeForm((p) => ({ ...p, title: e.target.value }))} />
                </label>
                <label>
                  <span>Target roles</span>
                  <input className="form-input" placeholder="All or Teacher,Parent" value={noticeForm.targetRolesCsv} onChange={(e) => setNoticeForm((p) => ({ ...p, targetRolesCsv: e.target.value }))} />
                </label>
                <label>
                  <span>Expires at (optional)</span>
                  <input type="datetime-local" className="form-input" value={noticeForm.expiresAtUtc} onChange={(e) => setNoticeForm((p) => ({ ...p, expiresAtUtc: e.target.value }))} />
                </label>
                <label className="student-edit-grid__wide">
                  <span>Body</span>
                  <textarea className="form-input" rows="4" value={noticeForm.body} onChange={(e) => setNoticeForm((p) => ({ ...p, body: e.target.value }))} />
                </label>
              </div>
              <div className="form-actions" style={{ marginTop: '0.5rem' }}>
                <button type="button" className="btn-primary-action" onClick={createNotice} disabled={savingNotice}>{savingNotice ? 'Publishing…' : 'Publish notice'}</button>
              </div>
            </article>

            <article className="student-record-card">
              <h3 className="dashboard-section-title">Create event</h3>
              <div className="student-edit-grid">
                <label>
                  <span>Title</span>
                  <input className="form-input" value={eventForm.title} onChange={(e) => setEventForm((p) => ({ ...p, title: e.target.value }))} />
                </label>
                <label>
                  <span>Start</span>
                  <input type="datetime-local" className="form-input" value={eventForm.startAtUtc} onChange={(e) => setEventForm((p) => ({ ...p, startAtUtc: e.target.value }))} />
                </label>
                <label>
                  <span>End</span>
                  <input type="datetime-local" className="form-input" value={eventForm.endAtUtc} onChange={(e) => setEventForm((p) => ({ ...p, endAtUtc: e.target.value }))} />
                </label>
                <label>
                  <span>Color</span>
                  <input type="color" className="form-input" value={eventForm.colorHex} onChange={(e) => setEventForm((p) => ({ ...p, colorHex: e.target.value }))} />
                </label>
                <label className="student-edit-grid__wide">
                  <span>Description (optional)</span>
                  <textarea className="form-input" rows="4" value={eventForm.description} onChange={(e) => setEventForm((p) => ({ ...p, description: e.target.value }))} />
                </label>
              </div>
              <div className="form-actions" style={{ marginTop: '0.5rem' }}>
                <button type="button" className="btn-primary-action" onClick={createEvent} disabled={savingEvent}>{savingEvent ? 'Saving…' : 'Save event'}</button>
              </div>
            </article>
          </section>

          <section style={{ marginTop: '1rem' }}>
            <h3 className="section-title">Recent notices</h3>
            {notices.length === 0 ? (
              <p className="empty-state">No notices yet.</p>
            ) : (
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Title</th>
                      <th>Roles</th>
                      <th>Published</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {notices.map((notice) => (
                      <tr key={notice.id}>
                        <td>
                          <strong>{notice.title}</strong>
                          <div className="card-desc" style={{ margin: 0 }}>{notice.body}</div>
                        </td>
                        <td>{notice.targetRolesCsv || 'All'}</td>
                        <td>{notice.publishedAtUtc ? new Date(notice.publishedAtUtc).toLocaleString() : '—'}</td>
                        <td>
                          <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => removeNotice(notice.id)}>Delete</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section style={{ marginTop: '1rem' }}>
            <h3 className="section-title">Upcoming events</h3>
            {events.length === 0 ? (
              <p className="empty-state">No events yet.</p>
            ) : (
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Event</th>
                      <th>Schedule</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {events.map((event) => (
                      <tr key={event.id}>
                        <td>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                            <span style={{ width: 12, height: 12, borderRadius: 999, background: event.colorHex || '#1f7a8c', display: 'inline-block' }} />
                            <strong>{event.title}</strong>
                          </div>
                          <div className="card-desc" style={{ margin: 0 }}>{event.description || 'No description'}</div>
                        </td>
                        <td>
                          {event.startAtUtc ? new Date(event.startAtUtc).toLocaleString() : '—'}
                          {' - '}
                          {event.endAtUtc ? new Date(event.endAtUtc).toLocaleString() : '—'}
                        </td>
                        <td>
                          <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => removeEvent(event.id)}>Delete</button>
                        </td>
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
