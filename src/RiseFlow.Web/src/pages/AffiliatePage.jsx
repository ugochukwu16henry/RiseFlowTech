import { useEffect, useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';
import './AffiliatePage.css';

function formatMoney(amount, currency = 'NGN') {
  const value = Number(amount || 0);
  return new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);
}

function buildPublicUrl(relativePath) {
  if (!relativePath) return null;
  if (relativePath.startsWith('http://') || relativePath.startsWith('https://')) return relativePath;
  const normalizedPath = relativePath.replace(/^\/+/, '');
  const base = getApiBase();
  return base ? `${base}/${normalizedPath}` : `/${normalizedPath}`;
}

export default function AffiliatePage() {
  const location = useLocation();
  const [dashboard, setDashboard] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [saveState, setSaveState] = useState({ type: null, message: null });
  const [settingsForm, setSettingsForm] = useState({ bankName: '', accountNumber: '', accountName: '', countryCode: 'NG', phoneNumber: '' });

  const view = useMemo(() => {
    if (location.pathname.includes('/training')) return 'training';
    if (location.pathname.includes('/payouts')) return 'payouts';
    if (location.pathname.includes('/schools')) return 'schools';
    return 'dashboard';
  }, [location.pathname]);

  const pageMeta = useMemo(() => {
    switch (view) {
      case 'schools':
        return {
          title: 'My referred schools',
          subtitle: 'Track each school, student count, and commission performance.',
        };
      case 'payouts':
        return {
          title: 'Affiliate payouts',
          subtitle: 'Manage your bank details and review payout history.',
        };
      case 'training':
        return {
          title: 'Training academy',
          subtitle: 'Watch affiliate onboarding and growth videos from RiseFlow.',
        };
      default:
        return {
          title: 'Affiliate partner dashboard',
          subtitle: 'Monitor referrals, earnings, and your latest partner activity at a glance.',
        };
    }
  }, [view]);

  const loadDashboard = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await apiFetch('/api/affiliates/me/dashboard');
      if (res.status === 401 || res.status === 403) {
        throw new Error('Please sign in again with your affiliate account to continue.');
      }
      if (res.status === 404) {
        throw new Error('Your affiliate profile is still being prepared. Refresh shortly or contact support.');
      }
      if (!res.ok) {
        const text = await res.text().catch(() => '');
        throw new Error(text || 'Could not load affiliate dashboard.');
      }
      const data = await res.json();
      setDashboard(data);
      setSettingsForm({
        bankName: data?.payoutSettings?.bankName || '',
        accountNumber: data?.payoutSettings?.accountNumber || '',
        accountName: data?.payoutSettings?.accountName || '',
        countryCode: data?.payoutSettings?.countryCode || 'NG',
        phoneNumber: data?.payoutSettings?.phoneNumber || '',
      });
    } catch (e) {
      const message = e?.message === 'Failed to fetch'
        ? 'We could not reach the affiliate service right now. Please refresh and try again.'
        : (e?.message || 'Could not load affiliate dashboard.');
      setError(message);
      setDashboard(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadDashboard();
  }, []);

  const copyLink = async () => {
    if (!dashboard?.referralUrl) return;
    try {
      await navigator.clipboard.writeText(dashboard.referralUrl);
      setSaveState({ type: 'success', message: 'Referral link copied.' });
    } catch {
      setSaveState({ type: 'error', message: 'Could not copy your referral link.' });
    }
  };

  const handleSettingsChange = (event) => {
    const { name, value } = event.target;
    setSettingsForm((current) => ({ ...current, [name]: value }));
  };

  const handleSettingsSave = async (event) => {
    event.preventDefault();
    setSaveState({ type: null, message: null });

    const normalizedCountry = (() => {
      const value = (settingsForm.countryCode || '').trim();
      if (!value) return '';
      if (value.toUpperCase() === 'NIGERIA') return 'NG';
      return value.toUpperCase();
    })();

    const payload = {
      ...settingsForm,
      countryCode: normalizedCountry,
    };

    try {
      const res = await apiFetch('/api/affiliates/me/payout-settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      const raw = await res.text().catch(() => '');
      let data = null;
      if (raw) {
        try {
          data = JSON.parse(raw);
        } catch {
          data = raw;
        }
      }

      if (!res.ok) {
        const message = typeof data === 'string'
          ? data
          : (data?.message || data?.title || 'Could not save payout settings.');
        setSaveState({ type: 'error', message });
        return;
      }

      setDashboard((current) => current ? { ...current, payoutSettings: data } : current);
      setSettingsForm((current) => ({ ...current, countryCode: normalizedCountry || current.countryCode }));
      setSaveState({ type: 'success', message: 'Payout settings saved successfully.' });
    } catch {
      setSaveState({ type: 'error', message: 'Network error while saving payout settings.' });
    }
  };

  const handleHeadshotUpload = async (event) => {
    const file = event.target.files?.[0];
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file);

    setSaveState({ type: null, message: null });
    try {
      const res = await apiFetch('/api/affiliates/me/headshot', { method: 'POST', body: formData });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        setSaveState({ type: 'error', message: data || 'Could not upload headshot.' });
        return;
      }
      setDashboard((current) => current ? { ...current, headshotPath: data?.headshotPath || current.headshotPath } : current);
      setSaveState({ type: 'success', message: 'Headshot uploaded.' });
    } catch {
      setSaveState({ type: 'error', message: 'Network error while uploading your headshot.' });
    }
  };

  const headshotUrl = buildPublicUrl(dashboard?.headshotPath || dashboard?.payoutSettings?.headshotPath);

  return (
    <PageLayout title={pageMeta.title} role="affiliate">
      <section className="progress-section">
        <h2 className="section-title">{pageMeta.title}</h2>
        <p className="card-desc">{pageMeta.subtitle}</p>
      </section>

      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}

      {!loading && dashboard && (
        <>
          {saveState.message && (
            <p className={saveState.type === 'error' ? 'empty-state empty-state--error' : 'affiliate-note'}>
              {saveState.message}
            </p>
          )}

          {view === 'dashboard' && (
            <>
              <div className="affiliate-profile-hero progress-section">
                <div className="affiliate-profile-identity">
                  {headshotUrl ? (
                    <img className="affiliate-headshot" src={headshotUrl} alt={dashboard.fullName} loading="lazy" />
                  ) : (
                    <div className="affiliate-headshot affiliate-headshot--placeholder" aria-hidden="true">
                      {(dashboard.fullName || 'A').trim().charAt(0).toUpperCase()}
                    </div>
                  )}
                  <div>
                    <p className="dashboard-label">Affiliate ID</p>
                    <h3 className="card-title">{dashboard.fullName}</h3>
                    <p className="card-desc">{dashboard.email} • {dashboard.uniqueCode}</p>
                  </div>
                </div>
                <label className="btn-primary-action btn-primary-action--ghost affiliate-upload-label">
                  Upload headshot
                  <input type="file" accept=".png,.jpg,.jpeg,.webp" onChange={handleHeadshotUpload} hidden />
                </label>
              </div>

              <div className="summary-cards">
                <div className="summary-card">
                  <span className="summary-value">{dashboard.totalReferredSchools}</span>
                  <span className="summary-label">Referred schools</span>
                </div>
                <div className="summary-card">
                  <span className="summary-value">{dashboard.totalBillableStudents}</span>
                  <span className="summary-label">Billable students</span>
                </div>
                <div className="summary-card">
                  <span className="summary-value">{formatMoney(dashboard.currentMonthEarnings)}</span>
                  <span className="summary-label">Current month earnings</span>
                </div>
                <div className="summary-card">
                  <span className="summary-value">{formatMoney(dashboard.pendingPayoutAmount)}</span>
                  <span className="summary-label">Pending payout</span>
                </div>
              </div>

              <section className="progress-section">
                <div className="affiliate-link-box">
                  <div>
                    <p className="dashboard-label">My referral link</p>
                    <p className="affiliate-link-value">{dashboard.referralUrl}</p>
                  </div>
                  <button type="button" className="btn-primary-action" onClick={copyLink}>
                    Copy link
                  </button>
                </div>
              </section>

              <section className="progress-section">
                <h3 className="section-title">Referred schools overview</h3>
                <div className="data-table-wrap">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>School</th>
                        <th>Total students</th>
                        <th>Billable students</th>
                        <th>Last paid</th>
                        <th>Your share</th>
                      </tr>
                    </thead>
                    <tbody>
                      {dashboard.referredSchools.map((school) => (
                        <tr key={school.schoolId}>
                          <td>{school.schoolName}</td>
                          <td>{school.totalStudents}</td>
                          <td>{school.billableStudents}</td>
                          <td>{school.latestPaidAtUtc ? new Date(school.latestPaidAtUtc).toLocaleDateString() : 'Not paid yet'}</td>
                          <td>{formatMoney(school.lifetimeCommission)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>

              <section className="progress-section">
                <h3 className="section-title">Recent notifications</h3>
                {dashboard.notifications.length === 0 ? (
                  <p className="empty-state">No notifications yet.</p>
                ) : (
                  <ul className="card-list">
                    {dashboard.notifications.map((item) => (
                      <li key={item.id}>
                        <p className="card-title">{item.title}</p>
                        <p className="card-desc">{item.message}</p>
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            </>
          )}

          {view === 'schools' && (
            <>
              <section className="progress-section">
                <div className="affiliate-link-box">
                  <div>
                    <p className="dashboard-label">Referral link</p>
                    <p className="affiliate-link-value">{dashboard.referralUrl}</p>
                  </div>
                  <button type="button" className="btn-primary-action" onClick={copyLink}>
                    Copy link
                  </button>
                </div>
              </section>

              <section className="progress-section">
                <h3 className="section-title">All referred schools</h3>
                {dashboard.referredSchools.length === 0 ? (
                  <p className="empty-state">You have not referred any schools yet.</p>
                ) : (
                  <div className="data-table-wrap">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>School</th>
                          <th>Total students</th>
                          <th>Billable students</th>
                          <th>Pending</th>
                          <th>Paid to date</th>
                        </tr>
                      </thead>
                      <tbody>
                        {dashboard.referredSchools.map((school) => (
                          <tr key={school.schoolId}>
                            <td>{school.schoolName}</td>
                            <td>{school.totalStudents}</td>
                            <td>{school.billableStudents}</td>
                            <td>{formatMoney(school.pendingCommission)}</td>
                            <td>{formatMoney(school.paidCommission)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            </>
          )}

          {view === 'payouts' && (
            <>
              <section className="progress-section">
                <h3 className="section-title">Payout settings</h3>
                <form className="affiliate-form-grid" onSubmit={handleSettingsSave}>
                  <label>
                    <span className="dashboard-label">Bank name</span>
                    <input className="form-input" name="bankName" value={settingsForm.bankName} onChange={handleSettingsChange} placeholder="e.g. Access Bank" />
                  </label>
                  <label>
                    <span className="dashboard-label">Account number</span>
                    <input className="form-input" name="accountNumber" value={settingsForm.accountNumber} onChange={handleSettingsChange} placeholder="0123456789" />
                  </label>
                  <label>
                    <span className="dashboard-label">Account name</span>
                    <input className="form-input" name="accountName" value={settingsForm.accountName} onChange={handleSettingsChange} placeholder="Your bank account name" />
                  </label>
                  <label>
                    <span className="dashboard-label">Country</span>
                    <input className="form-input" name="countryCode" value={settingsForm.countryCode} onChange={handleSettingsChange} placeholder="NG" />
                  </label>
                  <label>
                    <span className="dashboard-label">Phone number</span>
                    <input className="form-input" name="phoneNumber" value={settingsForm.phoneNumber} onChange={handleSettingsChange} placeholder="0800 000 0000" />
                  </label>
                  <div className="affiliate-form-grid__wide dashboard-actions">
                    <button type="submit" className="btn-primary-action">Save payout settings</button>
                  </div>
                </form>
              </section>

              <section className="progress-section">
                <h3 className="section-title">Payout history</h3>
                {dashboard.payoutHistory.length === 0 ? (
                  <p className="empty-state">No payouts have been created yet.</p>
                ) : (
                  <div className="data-table-wrap">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>Period</th>
                          <th>Amount</th>
                          <th>Status</th>
                          <th>Reference</th>
                        </tr>
                      </thead>
                      <tbody>
                        {dashboard.payoutHistory.map((payout) => (
                          <tr key={payout.id}>
                            <td>{new Date(payout.periodStartUtc).toLocaleDateString()} – {new Date(payout.periodEndUtc).toLocaleDateString()}</td>
                            <td>{formatMoney(payout.amount, payout.currencyCode)}</td>
                            <td>{payout.status}</td>
                            <td>{payout.paystackTransferReference || '—'}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            </>
          )}

          {view === 'training' && (
            <section className="progress-section">
              <h3 className="section-title">Training academy</h3>
              {dashboard.trainingVideos.length === 0 ? (
                <p className="empty-state">No training videos have been published yet.</p>
              ) : (
                <div className="affiliate-video-grid">
                  {dashboard.trainingVideos.map((video) => (
                    <article key={video.id} className="affiliate-video-card">
                      <iframe
                        className="affiliate-video-frame"
                        src={video.youtubeUrl}
                        title={video.title}
                        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                        allowFullScreen
                      />
                      <h4 className="card-title">{video.title}</h4>
                      <p className="card-desc">{video.topic || 'Training'} • {video.description || 'Affiliate onboarding and coaching from the RiseFlow team.'}</p>
                    </article>
                  ))}
                </div>
              )}
            </section>
          )}
        </>
      )}
    </PageLayout>
  );
}
