import { useEffect, useState, useCallback } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';

const MONTHS = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

function fmtDate(d) {
  if (!d) return '—';
  const date = new Date(d + 'T00:00:00');
  return `${date.getDate()} ${MONTHS[date.getMonth()]} ${date.getFullYear()}`;
}

function termColor(index) {
  const colors = [
    { bg: '#dbeafe', border: '#3b82f6', text: '#1e40af' },
    { bg: '#dcfce7', border: '#22c55e', text: '#15803d' },
    { bg: '#fef9c3', border: '#eab308', text: '#854d0e' },
    { bg: '#f3e8ff', border: '#a855f7', text: '#6b21a8' },
    { bg: '#ffedd5', border: '#f97316', text: '#9a3412' },
  ];
  return colors[index % colors.length];
}

export default function SchoolTermsPage() {
  const [terms, setTerms] = useState([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState(null);
  const [editingId, setEditingId] = useState(null);
  const [saving, setSaving] = useState(false);

  const emptyForm = {
    name: '', academicYear: '', startDate: '', endDate: '',
    midtermBreakStart: '', midtermBreakEnd: '', description: '',
    sortOrder: '', setAsCurrent: false,
  };
  const [form, setForm] = useState(emptyForm);

  const loadTerms = useCallback(async () => {
    setLoading(true);
    try {
      const res = await apiFetch('/api/academicterms');
      setTerms(res.ok ? await res.json() : []);
    } catch {
      setMessage('Could not load terms.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadTerms(); }, [loadTerms]);

  const setField = (key, value) => setForm(f => ({ ...f, [key]: value }));

  const saveTerm = async (e) => {
    e.preventDefault();
    if (!form.name.trim() || !form.academicYear.trim() || !form.startDate || !form.endDate) {
      return setMessage('Name, academic year, start date and end date are required.');
    }
    setSaving(true);
    setMessage(null);
    try {
      const body = {
        name: form.name.trim(),
        academicYear: form.academicYear.trim(),
        startDate: form.startDate,
        endDate: form.endDate,
        midtermBreakStart: form.midtermBreakStart || null,
        midtermBreakEnd: form.midtermBreakEnd || null,
        description: form.description.trim() || null,
        sortOrder: Number(form.sortOrder) || 0,
        setAsCurrent: form.setAsCurrent,
      };
      const url = editingId ? `/api/academicterms/${editingId}` : '/api/academicterms';
      const method = editingId ? 'PUT' : 'POST';
      const res = await apiFetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      if (!res.ok) throw new Error(await res.text());
      setForm(emptyForm);
      setEditingId(null);
      setMessage(editingId ? 'Term updated.' : 'Term created.');
      await loadTerms();
    } catch (err) {
      setMessage(err.message || 'Could not save term.');
    } finally {
      setSaving(false);
    }
  };

  const editTerm = (t) => {
    setForm({
      name: t.name,
      academicYear: t.academicYear,
      startDate: t.startDate,
      endDate: t.endDate,
      midtermBreakStart: t.midtermBreakStart || '',
      midtermBreakEnd: t.midtermBreakEnd || '',
      description: t.description || '',
      sortOrder: String(t.sortOrder ?? 0),
      setAsCurrent: !!t.isCurrent,
    });
    setEditingId(t.id);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const deleteTerm = async (id) => {
    if (!window.confirm('Delete this term? This cannot be undone.')) return;
    try {
      const res = await apiFetch(`/api/academicterms/${id}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(await res.text());
      setMessage('Term deleted.');
      await loadTerms();
    } catch (err) {
      setMessage(err.message || 'Could not delete term.');
    }
  };

  const groupedByYear = terms.reduce((acc, t) => {
    const yr = t.academicYear;
    if (!acc[yr]) acc[yr] = [];
    acc[yr].push(t);
    return acc;
  }, {});

  return (
    <PageLayout title="Term Calendar" role="school">
      <h2 className="section-title">Academic Terms &amp; Calendar</h2>

      <div className="dashboard-actions" style={{ flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Link to="/school" className="btn-primary-action btn-primary-action--ghost">Dashboard</Link>
      </div>

      {message && (
        <p className={`empty-state${message.startsWith('Could not') || message.includes('required') ? ' empty-state--error' : ''}`}>
          {message}
        </p>
      )}

      {/* ── Add / Edit form ──────────────────────────────────────────────── */}
      <div className="card" style={{ maxWidth: 760, marginBottom: '2rem' }}>
        <h3 style={{ marginBottom: '0.75rem' }}>{editingId ? 'Edit term' : 'Add academic term'}</h3>
        <form onSubmit={saveTerm}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
            <div>
              <label className="form-label">Term name *</label>
              <input className="form-input" placeholder="e.g. First Term" value={form.name}
                onChange={(e) => setField('name', e.target.value)} />
            </div>
            <div>
              <label className="form-label">Academic year *</label>
              <input className="form-input" placeholder="e.g. 2025/2026" value={form.academicYear}
                onChange={(e) => setField('academicYear', e.target.value)} />
            </div>
            <div>
              <label className="form-label">Term start date *</label>
              <input type="date" className="form-input" value={form.startDate}
                onChange={(e) => setField('startDate', e.target.value)} />
            </div>
            <div>
              <label className="form-label">Term end date *</label>
              <input type="date" className="form-input" value={form.endDate}
                onChange={(e) => setField('endDate', e.target.value)} />
            </div>
            <div>
              <label className="form-label">Midterm break start (optional)</label>
              <input type="date" className="form-input" value={form.midtermBreakStart}
                onChange={(e) => setField('midtermBreakStart', e.target.value)} />
            </div>
            <div>
              <label className="form-label">Midterm break end (optional)</label>
              <input type="date" className="form-input" value={form.midtermBreakEnd}
                onChange={(e) => setField('midtermBreakEnd', e.target.value)} />
            </div>
            <div>
              <label className="form-label">Sort order (optional)</label>
              <input type="number" min="0" className="form-input" placeholder="e.g. 1" value={form.sortOrder}
                onChange={(e) => setField('sortOrder', e.target.value)} />
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: '0.5rem', paddingBottom: '0.25rem' }}>
              <input type="checkbox" id="setAsCurrent" checked={form.setAsCurrent}
                onChange={(e) => setField('setAsCurrent', e.target.checked)} />
              <label htmlFor="setAsCurrent" style={{ cursor: 'pointer' }}>Mark as current term</label>
            </div>
            <div style={{ gridColumn: '1 / -1' }}>
              <label className="form-label">Description (optional)</label>
              <input className="form-input" placeholder="e.g. End-of-session exams in Week 12"
                value={form.description} onChange={(e) => setField('description', e.target.value)} />
            </div>
          </div>
          <div className="form-actions" style={{ marginTop: '0.75rem', gap: '0.5rem', display: 'flex' }}>
            <button type="submit" className="btn-primary-action" disabled={saving}>
              {saving ? 'Saving…' : editingId ? 'Update term' : 'Create term'}
            </button>
            {editingId && (
              <button type="button" className="btn-primary-action btn-primary-action--ghost"
                onClick={() => { setForm(emptyForm); setEditingId(null); }}>
                Cancel
              </button>
            )}
          </div>
        </form>
      </div>

      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}

      {/* ── Calendar view per academic year ──────────────────────────────── */}
      {!loading && terms.length === 0 && (
        <p className="empty-state">No academic terms yet. Add the first one above.</p>
      )}

      {!loading && Object.entries(groupedByYear)
        .sort(([a], [b]) => b.localeCompare(a))
        .map(([year, yearTerms]) => (
          <div key={year} style={{ marginBottom: '2.5rem' }}>
            <h3 style={{ marginBottom: '1rem', color: 'var(--text-primary)' }}>
              Academic Year {year}
            </h3>

            {/* Visual timeline */}
            <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap', marginBottom: '1.25rem' }}>
              {[...yearTerms]
                .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.name.localeCompare(b.name))
                .map((t, i) => {
                  const c = termColor(i);
                  return (
                    <div key={t.id} style={{
                      flex: '1 1 220px',
                      minWidth: 220,
                      background: c.bg,
                      border: `2px solid ${c.border}`,
                      borderRadius: 10,
                      padding: '0.75rem 1rem',
                      position: 'relative',
                    }}>
                      {t.isCurrent && (
                        <span className="badge badge--success" style={{ position: 'absolute', top: 8, right: 8, fontSize: '0.7rem' }}>
                          Current
                        </span>
                      )}
                      <p style={{ margin: '0 0 0.3rem', fontWeight: 700, color: c.text, fontSize: '1rem' }}>
                        {t.name}
                      </p>
                      <p style={{ margin: '0.15rem 0', fontSize: '0.85rem', color: 'var(--text-secondary, #555)' }}>
                        {fmtDate(t.startDate)} — {fmtDate(t.endDate)}
                      </p>
                      {(t.midtermBreakStart || t.midtermBreakEnd) && (
                        <p style={{ margin: '0.3rem 0 0', fontSize: '0.8rem', color: 'var(--text-muted, #888)', background: 'rgba(0,0,0,0.05)', borderRadius: 4, padding: '0.2rem 0.4rem' }}>
                          Midterm: {fmtDate(t.midtermBreakStart)} — {fmtDate(t.midtermBreakEnd)}
                        </p>
                      )}
                      {t.description && (
                        <p style={{ margin: '0.3rem 0 0', fontSize: '0.8rem', color: 'var(--text-muted, #777)', fontStyle: 'italic' }}>
                          {t.description}
                        </p>
                      )}
                      <div style={{ marginTop: '0.6rem', display: 'flex', gap: '0.4rem' }}>
                        <button className="btn-icon" title="Edit" onClick={() => editTerm(t)}>✏️</button>
                        <button className="btn-icon" title="Delete" onClick={() => deleteTerm(t.id)}>🗑️</button>
                      </div>
                    </div>
                  );
                })}
            </div>

            {/* Table view */}
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Term</th>
                    <th>Start</th>
                    <th>End</th>
                    <th>Midterm break</th>
                    <th>Status</th>
                    <th>Description</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {[...yearTerms]
                    .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.name.localeCompare(b.name))
                    .map((t) => (
                      <tr key={t.id}>
                        <td><strong>{t.name}</strong></td>
                        <td>{fmtDate(t.startDate)}</td>
                        <td>{fmtDate(t.endDate)}</td>
                        <td style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
                          {t.midtermBreakStart ? `${fmtDate(t.midtermBreakStart)} – ${fmtDate(t.midtermBreakEnd)}` : '—'}
                        </td>
                        <td>
                          {t.isCurrent
                            ? <span className="badge badge--success">Current</span>
                            : <span className="badge badge--neutral">Past / future</span>}
                        </td>
                        <td style={{ maxWidth: 200 }}>{t.description || '—'}</td>
                        <td style={{ whiteSpace: 'nowrap' }}>
                          <button className="btn-icon" title="Edit" onClick={() => editTerm(t)}>✏️</button>
                          <button className="btn-icon" title="Delete" onClick={() => deleteTerm(t.id)}>🗑️</button>
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
          </div>
        ))}
    </PageLayout>
  );
}
