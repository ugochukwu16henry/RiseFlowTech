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

function dedupeCaseInsensitive(values) {
  const seen = new Set();
  const output = [];
  (values || []).forEach((value) => {
    const normalized = String(value || '').trim();
    if (!normalized) return;
    const key = normalized.toLowerCase();
    if (seen.has(key)) return;
    seen.add(key);
    output.push(normalized);
  });
  return output;
}

function subjectCodeFromName(name) {
  const cleaned = String(name || '').toUpperCase().replace(/[^A-Z0-9]+/g, '');
  if (!cleaned) return 'SUBJ';
  return cleaned.slice(0, 10);
}

function getTermNamingOptions(termCount) {
  if (termCount === 2) return ['Semester 1', 'Semester 2'];
  if (termCount === 4) return ['Quarter 1', 'Quarter 2', 'Quarter 3', 'Quarter 4'];
  return ['First Term', 'Second Term', 'Third Term'];
}

function defaultTermsByCountry(countryCode) {
  const normalized = String(countryCode || '').trim().toUpperCase();
  if (normalized === 'MA') return 2;
  if (normalized === 'EG' || normalized === 'TN' || normalized === 'DZ') return 2;
  return 3;
}

export default function SchoolTermsPage() {
  const [terms, setTerms] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [schoolProfile, setSchoolProfile] = useState(null);
  const [academicProfiles, setAcademicProfiles] = useState([]);
  const [onboardingCountries, setOnboardingCountries] = useState([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState(null);
  const [editingId, setEditingId] = useState(null);
  const [saving, setSaving] = useState(false);
  const [selectedTermsPerYear, setSelectedTermsPerYear] = useState(3);
  const [savingTermsPreference, setSavingTermsPreference] = useState(false);
  const [selectedPresetSubjects, setSelectedPresetSubjects] = useState([]);
  const [customSubjects, setCustomSubjects] = useState([]);
  const [customSubjectInput, setCustomSubjectInput] = useState('');
  const [savingSubjects, setSavingSubjects] = useState(false);

  const emptyForm = {
    name: '', academicYear: '', startDate: '', endDate: '',
    midtermBreakStart: '', midtermBreakEnd: '', description: '',
    sortOrder: '', setAsCurrent: false,
  };
  const [form, setForm] = useState(emptyForm);

  const currentCountry = onboardingCountries.find((country) =>
    String(country.countryCode || '').toUpperCase() === String(schoolProfile?.countryCode || '').toUpperCase());

  const selectedAcademicProfile = academicProfiles.find((profile) => profile.id === schoolProfile?.academicSystemProfileId);

  const suggestedTermsFromProfile = Number(selectedAcademicProfile?.suggestedTermsPerYear || 0) || null;
  const suggestedTermsFromCountry = defaultTermsByCountry(schoolProfile?.countryCode);
  const savedTermsPreference = Number(schoolProfile?.termsPerYear || 0) || null;
  const effectiveSuggestedTerms = suggestedTermsFromProfile || suggestedTermsFromCountry;

  const termOptions = dedupeCaseInsensitive([
    String(effectiveSuggestedTerms),
    String(savedTermsPreference || ''),
    '2',
    '3',
    '4',
  ]).map((item) => Number(item)).filter((item) => Number.isFinite(item));

  const availablePresetSubjects = dedupeCaseInsensitive(currentCountry?.defaultSubjects || []);

  const termNameOptions = getTermNamingOptions(selectedTermsPerYear);

  const loadTerms = useCallback(async () => {
    setLoading(true);
    try {
      const [termsRes, profileRes, profileOptionsRes, countriesRes, subjectsRes] = await Promise.all([
        apiFetch('/api/academicterms'),
        apiFetch('/api/schools/profile'),
        apiFetch('/api/schools/academic-system-profiles'),
        apiFetch('/api/schools/onboarding-options'),
        apiFetch('/api/subjects'),
      ]);

      const termsData = termsRes.ok ? await termsRes.json() : [];
      const profileData = profileRes.ok ? await profileRes.json() : null;
      const profileOptionsData = profileOptionsRes.ok ? await profileOptionsRes.json() : [];
      const countriesDataRaw = countriesRes.ok ? await countriesRes.json() : {};
      const subjectsData = subjectsRes.ok ? await subjectsRes.json() : [];

      const countriesData = Array.isArray(countriesDataRaw?.countries) ? countriesDataRaw.countries : [];

      setTerms(Array.isArray(termsData) ? termsData : []);
      setSubjects(Array.isArray(subjectsData) ? subjectsData : []);
      setSchoolProfile(profileData);
      setAcademicProfiles(Array.isArray(profileOptionsData) ? profileOptionsData : []);
      setOnboardingCountries(countriesData);

      const selectedProfile = (Array.isArray(profileOptionsData) ? profileOptionsData : [])
        .find((item) => item.id === profileData?.academicSystemProfileId);
      const profileSuggested = Number(selectedProfile?.suggestedTermsPerYear || 0);
      const schoolSelectedTerms = Number(profileData?.termsPerYear || 0);
      if (schoolSelectedTerms > 0) {
        setSelectedTermsPerYear(schoolSelectedTerms);
      } else if (profileSuggested > 0) {
        setSelectedTermsPerYear(profileSuggested);
      } else {
        setSelectedTermsPerYear(defaultTermsByCountry(profileData?.countryCode));
      }

      const countryMatch = countriesData.find((country) =>
        String(country.countryCode || '').toUpperCase() === String(profileData?.countryCode || '').toUpperCase());
      const defaultSubjects = dedupeCaseInsensitive(countryMatch?.defaultSubjects || []);
      setSelectedPresetSubjects(defaultSubjects);
    } catch {
      setMessage('Could not load terms.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadTerms(); }, [loadTerms]);

  const setField = (key, value) => setForm(f => ({ ...f, [key]: value }));

  const applySuggestedTermName = () => {
    const scopedTerms = terms
      .filter((term) => String(term.academicYear || '').trim() === String(form.academicYear || '').trim())
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));

    const nextIndex = scopedTerms.length;
    const names = getTermNamingOptions(selectedTermsPerYear);
    const suggestedName = names[nextIndex] || `Term ${nextIndex + 1}`;

    setForm((current) => ({
      ...current,
      name: suggestedName,
      sortOrder: String(nextIndex + 1),
    }));
  };

  const addCustomSubject = () => {
    const normalized = customSubjectInput.trim();
    if (!normalized) return;
    const combined = dedupeCaseInsensitive([...customSubjects, normalized]);
    setCustomSubjects(combined);
    setCustomSubjectInput('');
  };

  const removeCustomSubject = (name) => {
    setCustomSubjects((current) => current.filter((item) => item.toLowerCase() !== String(name || '').toLowerCase()));
  };

  const togglePresetSubject = (name) => {
    setSelectedPresetSubjects((current) => (
      current.some((item) => item.toLowerCase() === String(name || '').toLowerCase())
        ? current.filter((item) => item.toLowerCase() !== String(name || '').toLowerCase())
        : [...current, name]
    ));
  };

  const createSelectedSubjects = async () => {
    if (savingSubjects) return;
    setSavingSubjects(true);
    setMessage(null);

    try {
      const existingNames = new Set((subjects || []).map((subject) => String(subject.name || '').trim().toLowerCase()));
      const pending = dedupeCaseInsensitive([...selectedPresetSubjects, ...customSubjects])
        .filter((name) => !existingNames.has(name.toLowerCase()));

      if (pending.length === 0) {
        setMessage('All selected subjects already exist.');
        return;
      }

      for (const subjectName of pending) {
        const res = await apiFetch('/api/subjects', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ name: subjectName, code: subjectCodeFromName(subjectName) }),
        });

        if (!res.ok) {
          const text = await res.text().catch(() => '');
          throw new Error(text || `Could not create subject: ${subjectName}`);
        }
      }

      setMessage(`Created ${pending.length} subject${pending.length === 1 ? '' : 's'}.`);
      setCustomSubjects([]);
      await loadTerms();
    } catch (err) {
      setMessage(err.message || 'Could not create selected subjects.');
    } finally {
      setSavingSubjects(false);
    }
  };

  const saveTermsPerYearPreference = async () => {
    if (savingTermsPreference) return;
    setSavingTermsPreference(true);
    setMessage(null);

    try {
      const res = await apiFetch('/api/schools/profile/terms-per-year', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ termsPerYear: selectedTermsPerYear }),
      });

      if (!res.ok) {
        throw new Error(await res.text());
      }

      const updated = await res.json();
      setSchoolProfile(updated);
      setMessage('Terms-per-year preference saved.');
    } catch (err) {
      setMessage(err.message || 'Could not save terms-per-year preference.');
    } finally {
      setSavingTermsPreference(false);
    }
  };

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

      <section className="dashboard-panel" style={{ marginBottom: '1rem' }}>
        <h3 className="card-title">African term structure guide</h3>
        <p className="card-desc">
          Most African school systems use <strong>3 terms per year</strong> (trimester model). Some systems use <strong>2 terms</strong> (semester model), and a few private calendars run <strong>4 quarters</strong>.
        </p>
        <p className="card-desc" style={{ marginTop: '0.35rem' }}>
          Suggested for your school: <strong>{effectiveSuggestedTerms}</strong> term(s) per year
          {selectedAcademicProfile?.name ? ` from ${selectedAcademicProfile.name}` : ''}
          {schoolProfile?.countryCode ? ` (${schoolProfile.countryCode})` : ''}.
        </p>
        <div className="form-grid" style={{ marginTop: '0.75rem' }}>
          <label className="form-field">Terms per year
            <select
              className="form-input"
              value={selectedTermsPerYear}
              onChange={(e) => setSelectedTermsPerYear(Number(e.target.value) || 3)}
            >
              {termOptions.sort((a, b) => a - b).map((option) => (
                <option key={option} value={option}>{option} term(s) per year</option>
              ))}
            </select>
          </label>
        </div>
        <div className="form-actions" style={{ marginTop: '0.65rem' }}>
          <button
            type="button"
            className="btn-primary-action btn-primary-action--ghost"
            onClick={saveTermsPerYearPreference}
            disabled={savingTermsPreference}
          >
            {savingTermsPreference ? 'Saving preference…' : 'Save terms-per-year preference'}
          </button>
          {savedTermsPreference ? (
            <span className="card-desc" style={{ alignSelf: 'center' }}>
              Saved preference: {savedTermsPreference} term(s)
            </span>
          ) : null}
        </div>
      </section>

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
          <div className="form-actions" style={{ marginBottom: '0.6rem', gap: '0.5rem', display: 'flex', flexWrap: 'wrap' }}>
            <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={applySuggestedTermName}>
              Use next suggested term name
            </button>
            <span className="card-desc" style={{ alignSelf: 'center' }}>
              Suggested names: {termNameOptions.join(', ')}
            </span>
          </div>
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

      <section className="dashboard-panel" style={{ marginTop: '1.5rem' }}>
        <h3 className="card-title">Subjects setup (preset + custom)</h3>
        <p className="card-desc">
          Start with recommended subjects for your country/profile, then add custom subjects unique to your school.
        </p>

        {availablePresetSubjects.length > 0 && (
          <div style={{ marginTop: '0.75rem' }}>
            <p className="card-desc"><strong>Recommended subjects</strong></p>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))', gap: '0.45rem', marginTop: '0.35rem' }}>
              {availablePresetSubjects.map((subject) => {
                const checked = selectedPresetSubjects.some((item) => item.toLowerCase() === subject.toLowerCase());
                return (
                  <label key={subject} style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', border: '1px solid var(--border-color)', borderRadius: 8, padding: '0.45rem 0.55rem' }}>
                    <input type="checkbox" checked={checked} onChange={() => togglePresetSubject(subject)} />
                    <span>{subject}</span>
                  </label>
                );
              })}
            </div>
          </div>
        )}

        <div style={{ marginTop: '0.85rem' }}>
          <p className="card-desc"><strong>Custom subjects</strong></p>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: '0.5rem', marginTop: '0.35rem' }}>
            <input
              className="form-input"
              value={customSubjectInput}
              onChange={(e) => setCustomSubjectInput(e.target.value)}
              placeholder="e.g. Robotics, French, Music Theory"
            />
            <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={addCustomSubject}>Add custom</button>
          </div>
          {customSubjects.length > 0 && (
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.4rem', marginTop: '0.5rem' }}>
              {customSubjects.map((subject) => (
                <button
                  key={subject}
                  type="button"
                  className="btn-primary-action btn-primary-action--ghost"
                  style={{ padding: '0.2rem 0.55rem' }}
                  onClick={() => removeCustomSubject(subject)}
                >
                  {subject} ×
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="form-actions" style={{ marginTop: '0.8rem' }}>
          <button type="button" className="btn-primary-action" onClick={createSelectedSubjects} disabled={savingSubjects}>
            {savingSubjects ? 'Saving subjects…' : 'Create selected subjects'}
          </button>
        </div>
      </section>
    </PageLayout>
  );
}
