import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './RolePages.css';
import './AffiliatePage.css';

function formatMoney(amount, currency = 'NGN') {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency, maximumFractionDigits: 0 }).format(Number(amount || 0));
}

function buildPublicUrl(relativePath) {
  if (!relativePath) return null;
  if (relativePath.startsWith('http://') || relativePath.startsWith('https://')) return relativePath;
  const normalizedPath = relativePath.replace(/^\/+/, '');
  const base = getApiBase();
  return base ? `${base}/${normalizedPath}` : `/${normalizedPath}`;
}

const emptyVideoForm = {
  title: '',
  topic: '',
  description: '',
  youtubeUrl: '',
  isPublished: true,
  sortOrder: 0,
};

export default function SuperAdminAffiliatesPage() {
  const location = useLocation();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [actionMessage, setActionMessage] = useState(null);
  const [affiliates, setAffiliates] = useState([]);
  const [requests, setRequests] = useState([]);
  const [payouts, setPayouts] = useState([]);
  const [videos, setVideos] = useState([]);
  const [videoForm, setVideoForm] = useState(emptyVideoForm);
  const [editingVideoId, setEditingVideoId] = useState(null);
  const [selectedAffiliateDetail, setSelectedAffiliateDetail] = useState(null);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [adminReplyMessage, setAdminReplyMessage] = useState('');

  const view = useMemo(() => {
    if (location.pathname.includes('/affiliate-requests')) return 'requests';
    if (location.pathname.includes('/affiliate-payouts')) return 'payouts';
    if (location.pathname.includes('/affiliate-training')) return 'training';
    return 'manager';
  }, [location.pathname]);

  const loadAffiliates = async () => {
    const res = await apiFetch('/api/superadmin/affiliates', { skipTenantHeader: true });
    if (!res.ok) throw new Error('Could not load affiliates.');
    const data = await res.json();
    setAffiliates(Array.isArray(data) ? data : []);
  };

  const loadOverview = async () => {
    await Promise.all([loadAffiliates(), loadRequests()]);
  };

  const loadRequests = async () => {
    const res = await apiFetch('/api/superadmin/affiliate-requests', { skipTenantHeader: true });
    if (!res.ok) throw new Error('Could not load affiliate requests.');
    const data = await res.json();
    setRequests(Array.isArray(data) ? data : []);
  };

  const loadPayouts = async () => {
    const res = await apiFetch('/api/superadmin/affiliate-payouts', { skipTenantHeader: true });
    if (!res.ok) throw new Error('Could not load affiliate payouts.');
    const data = await res.json();
    setPayouts(Array.isArray(data) ? data : []);
  };

  const loadVideos = async () => {
    const res = await apiFetch('/api/superadmin/affiliate-training-videos', { skipTenantHeader: true });
    if (!res.ok) throw new Error('Could not load training videos.');
    const data = await res.json();
    setVideos(Array.isArray(data) ? data : []);
  };

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    const runner = async () => {
      try {
        if (view === 'requests') await loadRequests();
        else if (view === 'payouts') await loadPayouts();
        else if (view === 'training') await loadVideos();
        else await loadOverview();
      } catch (e) {
        if (!cancelled) setError(e.message || 'Could not load the affiliate admin view.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    void runner();
    return () => {
      cancelled = true;
    };
  }, [view]);

  const handleSendInvite = async (requestId) => {
    setActionMessage(null);
    try {
      const res = await apiFetch(`/api/superadmin/affiliate-requests/${requestId}/send-invite`, {
        method: 'POST',
        skipTenantHeader: true,
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        setActionMessage({ type: 'error', text: data || 'Could not send invite.' });
        return;
      }
      try {
        await navigator.clipboard.writeText(data.inviteUrl);
      } catch {
        // ignore clipboard failures
      }
      setActionMessage({ type: 'success', text: `Invite sent. Link copied: ${data.inviteUrl}` });
      await loadRequests();
    } catch {
      setActionMessage({ type: 'error', text: 'Network error while sending the invite.' });
    }
  };

  const handlePayPayout = async (payoutId) => {
    setActionMessage(null);
    try {
      const res = await apiFetch(`/api/superadmin/affiliate-payouts/${payoutId}/pay`, {
        method: 'POST',
        skipTenantHeader: true,
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        setActionMessage({ type: 'error', text: data || 'Could not process this payout.' });
        return;
      }
      setActionMessage({ type: data?.status === 'Paid' ? 'success' : 'error', text: data?.status === 'Paid' ? 'Affiliate payout processed successfully.' : (data?.failureReason || 'Payout could not be completed.') });
      await loadPayouts();
    } catch {
      setActionMessage({ type: 'error', text: 'Network error while paying the affiliate.' });
    }
  };

  const handleVideoChange = (event) => {
    const { name, value, type, checked } = event.target;
    setVideoForm((current) => ({
      ...current,
      [name]: type === 'checkbox' ? checked : value,
    }));
  };

  const handleVideoSubmit = async (event) => {
    event.preventDefault();
    setActionMessage(null);
    try {
      const url = editingVideoId
        ? `/api/superadmin/affiliate-training-videos/${editingVideoId}`
        : '/api/superadmin/affiliate-training-videos';
      const method = editingVideoId ? 'PUT' : 'POST';
      const res = await apiFetch(url, {
        method,
        skipTenantHeader: true,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...videoForm,
          sortOrder: Number(videoForm.sortOrder || 0),
        }),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        setActionMessage({ type: 'error', text: data || 'Could not save the training video.' });
        return;
      }
      setActionMessage({ type: 'success', text: editingVideoId ? 'Training video updated.' : 'Training video added.' });
      setVideoForm(emptyVideoForm);
      setEditingVideoId(null);
      await loadVideos();
    } catch {
      setActionMessage({ type: 'error', text: 'Network error while saving the training video.' });
    }
  };

  const handleEditVideo = (video) => {
    setEditingVideoId(video.id);
    setVideoForm({
      title: video.title || '',
      topic: video.topic || '',
      description: video.description || '',
      youtubeUrl: video.youtubeUrl || '',
      isPublished: Boolean(video.isPublished),
      sortOrder: Number(video.sortOrder || 0),
    });
  };

  const handleDeleteVideo = async (videoId) => {
    setActionMessage(null);
    try {
      const res = await apiFetch(`/api/superadmin/affiliate-training-videos/${videoId}`, {
        method: 'DELETE',
        skipTenantHeader: true,
      });
      if (!res.ok) {
        setActionMessage({ type: 'error', text: 'Could not delete the training video.' });
        return;
      }
      setActionMessage({ type: 'success', text: 'Training video deleted.' });
      await loadVideos();
    } catch {
      setActionMessage({ type: 'error', text: 'Network error while deleting the training video.' });
    }
  };

  const loadAffiliateDetail = async (affiliateId) => {
    setDetailsLoading(true);
    setActionMessage(null);
    try {
      const res = await apiFetch(`/api/superadmin/affiliates/${affiliateId}`, { skipTenantHeader: true });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        setActionMessage({ type: 'error', text: data || 'Could not load affiliate details.' });
        return;
      }
      setSelectedAffiliateDetail(data);
      setAdminReplyMessage('');
    } catch {
      setActionMessage({ type: 'error', text: 'Network error while loading affiliate details.' });
    } finally {
      setDetailsLoading(false);
    }
  };

  const handleReplyToAffiliate = async (event) => {
    event.preventDefault();
    if (!selectedAffiliateDetail?.affiliate?.affiliateId) return;

    const message = (adminReplyMessage || '').trim();
    if (!message) {
      setActionMessage({ type: 'error', text: 'Type a reply before sending.' });
      return;
    }

    setActionMessage(null);
    try {
      const res = await apiFetch(`/api/superadmin/affiliates/${selectedAffiliateDetail.affiliate.affiliateId}/messages`, {
        method: 'POST',
        skipTenantHeader: true,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message }),
      });

      const data = await res.json().catch(() => null);
      if (!res.ok) {
        setActionMessage({ type: 'error', text: data || 'Could not send reply to affiliate.' });
        return;
      }

      setSelectedAffiliateDetail((current) => {
        if (!current) return current;
        const notifications = [data, ...(current.notifications || [])].slice(0, 20);
        return { ...current, notifications };
      });
      setAdminReplyMessage('');
      setActionMessage({ type: 'success', text: 'Reply sent to affiliate.' });
    } catch {
      setActionMessage({ type: 'error', text: 'Network error while sending reply.' });
    }
  };

  const totals = affiliates.reduce((summary, affiliate) => {
    summary.pending += Number(affiliate.pendingPayoutAmount || 0);
    summary.paid += Number(affiliate.paidToDate || 0);
    summary.schools += Number(affiliate.referredSchoolCount || 0);
    summary.unanswered += Number(affiliate.unreadQuestionCount || 0);
    if (affiliate.hasUnansweredQuestion) summary.affiliatesWithUnread += 1;
    return summary;
  }, { pending: 0, paid: 0, schools: 0, unanswered: 0, affiliatesWithUnread: 0 });

  return (
    <PageLayout title="Super Admin — Affiliate Program" role="super">
      <h2 className="section-title">Affiliate program manager</h2>
      <p className="control-room-intro">
        Review affiliate requests, invite new partners, manage payouts, and publish YouTube training content for affiliate onboarding.
      </p>

      <div className="dashboard-actions" style={{ flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Link to="/super-admin/affiliates" className={view === 'manager' ? 'btn-primary-action' : 'btn-primary-action btn-primary-action--ghost'}>
          Overview
        </Link>
        <Link to="/super-admin/affiliate-requests" className={view === 'requests' ? 'btn-primary-action' : 'btn-primary-action btn-primary-action--ghost'}>
          Requests
        </Link>
        <Link to="/super-admin/affiliate-payouts" className={view === 'payouts' ? 'btn-primary-action' : 'btn-primary-action btn-primary-action--ghost'}>
          Payouts
        </Link>
        <Link to="/super-admin/affiliate-training" className={view === 'training' ? 'btn-primary-action' : 'btn-primary-action btn-primary-action--ghost'}>
          Training
        </Link>
      </div>

      {loading && <p className="empty-state" aria-busy="true">Loading…</p>}
      {error && <p className="empty-state empty-state--error">{error}</p>}
      {actionMessage && (
        <p className={actionMessage.type === 'error' ? 'empty-state empty-state--error' : 'affiliate-note'}>
          {actionMessage.text}
        </p>
      )}

      {!loading && !error && view === 'manager' && (
        <>
          <div className="summary-cards">
            <div className="summary-card">
              <span className="summary-value">{affiliates.length}</span>
              <span className="summary-label">Approved affiliates</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{totals.schools}</span>
              <span className="summary-label">Referred schools</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{requests.length}</span>
              <span className="summary-label">Pending requests</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{formatMoney(totals.pending)}</span>
              <span className="summary-label">Pending payouts</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{formatMoney(totals.paid)}</span>
              <span className="summary-label">Paid to date</span>
            </div>
            <div className="summary-card">
              <span className="summary-value">{totals.affiliatesWithUnread}</span>
              <span className="summary-label">Affiliates with unread questions</span>
            </div>
          </div>

          {affiliates.length === 0 ? (
            <p className="empty-state">
              No approved affiliates yet. The affiliate APIs are live and connected to the database, but there are currently no saved affiliate records to show.
              {requests.length > 0 ? ' New applications are waiting below.' : ' Once a request or invite is created, it will appear here immediately.'}
            </p>
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Affiliate</th>
                    <th>Code</th>
                    <th>Country</th>
                    <th>Schools</th>
                    <th>Billable students</th>
                    <th>Pending payout</th>
                    <th>Status</th>
                    <th>Details</th>
                  </tr>
                </thead>
                <tbody>
                  {affiliates.map((affiliate) => {
                    const headshotUrl = buildPublicUrl(affiliate.headshotPath);
                    return (
                      <tr key={affiliate.affiliateId}>
                        <td>
                          <div className="sa-school-cell">
                            {headshotUrl ? (
                              <img className="sa-school-table-logo" src={headshotUrl} alt={affiliate.fullName} loading="lazy" />
                            ) : (
                              <div className="sa-school-logo-placeholder" aria-hidden="true">
                                {(affiliate.fullName || 'A').trim().charAt(0).toUpperCase()}
                              </div>
                            )}
                            <div>
                              <strong>{affiliate.fullName}</strong>
                              {affiliate.hasUnansweredQuestion && (
                                <span className="affiliate-unread-badge">
                                  {affiliate.unreadQuestionCount} new question{affiliate.unreadQuestionCount > 1 ? 's' : ''}
                                </span>
                              )}
                              <span className="sa-school-secondary">{affiliate.email}</span>
                            </div>
                          </div>
                        </td>
                        <td>{affiliate.uniqueCode}</td>
                        <td>{affiliate.countryCode || '—'}</td>
                        <td>{affiliate.referredSchoolCount}</td>
                        <td>{affiliate.totalBillableStudents}</td>
                        <td>{formatMoney(affiliate.pendingPayoutAmount)}</td>
                        <td>{affiliate.isActive ? 'Active' : 'Inactive'}</td>
                        <td>
                          <button
                            type="button"
                            className="btn-primary-action btn-primary-action--ghost"
                            onClick={() => loadAffiliateDetail(affiliate.affiliateId)}
                          >
                            View details
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          <section className="progress-section">
            <div className="home-section-header">
              <h3 className="section-title">Affiliate details</h3>
              <p className="card-desc">Open an affiliate record to see profile, contacts, bank details, and questions.</p>
            </div>

            {selectedAffiliateDetail && !detailsLoading && (
              <div className="dashboard-actions" style={{ marginBottom: '0.75rem' }}>
                <button
                  type="button"
                  className="btn-primary-action btn-primary-action--ghost"
                  onClick={() => {
                    setSelectedAffiliateDetail(null);
                    setAdminReplyMessage('');
                  }}
                >
                  Close details
                </button>
              </div>
            )}

            {detailsLoading && <p className="empty-state" aria-busy="true">Loading details…</p>}
            {!detailsLoading && !selectedAffiliateDetail && (
              <p className="empty-state">Select an affiliate and click "View details" to open their profile.</p>
            )}

            {!detailsLoading && selectedAffiliateDetail && (
              <>
                <div className="affiliate-profile-hero">
                  <div className="affiliate-profile-identity">
                    {buildPublicUrl(selectedAffiliateDetail?.contact?.headshotPath) ? (
                      <img
                        className="affiliate-headshot"
                        src={buildPublicUrl(selectedAffiliateDetail.contact.headshotPath)}
                        alt={selectedAffiliateDetail.contact.fullName}
                        loading="lazy"
                      />
                    ) : (
                      <div className="affiliate-headshot affiliate-headshot--placeholder" aria-hidden="true">
                        {(selectedAffiliateDetail.contact.fullName || 'A').trim().charAt(0).toUpperCase()}
                      </div>
                    )}
                    <div>
                      <p className="dashboard-label">Affiliate profile</p>
                      <h3 className="card-title">{selectedAffiliateDetail.contact.fullName}</h3>
                      <p className="card-desc">Code: {selectedAffiliateDetail.affiliate.uniqueCode}</p>
                    </div>
                  </div>
                </div>

                <div className="summary-cards">
                  <div className="summary-card">
                    <span className="summary-label">Email</span>
                    <span className="summary-value" style={{ fontSize: '0.95rem' }}>{selectedAffiliateDetail.contact.email || '—'}</span>
                  </div>
                  <div className="summary-card">
                    <span className="summary-label">Phone number</span>
                    <span className="summary-value" style={{ fontSize: '1rem' }}>{selectedAffiliateDetail.contact.phoneNumber || '—'}</span>
                  </div>
                  <div className="summary-card">
                    <span className="summary-label">WhatsApp number</span>
                    <span className="summary-value" style={{ fontSize: '1rem' }}>{selectedAffiliateDetail.contact.whatsappNumber || '—'}</span>
                  </div>
                  <div className="summary-card">
                    <span className="summary-label">Bank</span>
                    <span className="summary-value" style={{ fontSize: '1rem' }}>{selectedAffiliateDetail.payoutSettings.bankName || '—'}</span>
                  </div>
                  <div className="summary-card">
                    <span className="summary-label">Account name</span>
                    <span className="summary-value" style={{ fontSize: '1rem' }}>{selectedAffiliateDetail.payoutSettings.accountName || '—'}</span>
                  </div>
                  <div className="summary-card">
                    <span className="summary-label">Account number</span>
                    <span className="summary-value" style={{ fontSize: '1rem' }}>{selectedAffiliateDetail.payoutSettings.accountNumber || '—'}</span>
                  </div>
                </div>

                <section className="progress-section" style={{ marginTop: '1rem' }}>
                  <h4 className="section-title">Latest question</h4>
                  <p className="card-desc">{selectedAffiliateDetail.contact.latestQuestion || 'No question submitted yet.'}</p>
                </section>

                <section className="progress-section" style={{ marginTop: '1rem' }}>
                  <h4 className="section-title">Chat thread</h4>
                  {selectedAffiliateDetail.notifications?.length ? (
                    <ul className="card-list">
                      {selectedAffiliateDetail.notifications.map((item) => (
                        <li key={item.id}>
                          <p className="card-title">
                            {item.type === 'QuestionToSuperAdmin' ? 'Affiliate question' : item.type === 'ReplyFromSuperAdmin' ? 'Super Admin reply' : item.title}
                          </p>
                          <p className="card-desc">{item.message}</p>
                          <p className="dashboard-label">{new Date(item.createdAtUtc).toLocaleString()}</p>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="empty-state">No chat messages yet.</p>
                  )}

                  <form className="affiliate-form-grid" onSubmit={handleReplyToAffiliate} style={{ marginTop: '1rem' }}>
                    <label className="affiliate-form-grid__wide">
                      <span className="dashboard-label">Reply to affiliate</span>
                      <textarea
                        className="form-input"
                        rows="4"
                        value={adminReplyMessage}
                        onChange={(event) => setAdminReplyMessage(event.target.value)}
                        placeholder="Type your response to this affiliate..."
                      />
                    </label>
                    <div className="affiliate-form-grid__wide dashboard-actions">
                      <button type="submit" className="btn-primary-action">Send reply</button>
                    </div>
                  </form>
                </section>
              </>
            )}
          </section>

          {requests.length > 0 && (
            <section className="progress-section">
              <div className="home-section-header">
                <h3 className="section-title">Pending affiliate requests</h3>
                <p className="card-desc">New affiliate applications now show here immediately, even before approval.</p>
              </div>
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Email</th>
                      <th>Phone</th>
                      <th>Country</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {requests.slice(0, 5).map((item) => (
                      <tr key={item.id}>
                        <td>{item.fullName}</td>
                        <td>{item.email}</td>
                        <td>{item.phoneNumber || '—'}</td>
                        <td>{item.countryCode || '—'}</td>
                        <td>{item.status}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="dashboard-actions">
                <Link to="/super-admin/affiliate-requests" className="btn-primary-action btn-primary-action--ghost">
                  Open all requests
                </Link>
              </div>
            </section>
          )}
        </>
      )}

      {!loading && !error && view === 'requests' && (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Phone</th>
                <th>Country</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {requests.map((item) => (
                <tr key={item.id}>
                  <td>{item.fullName}</td>
                  <td>{item.email}</td>
                  <td>{item.phoneNumber || '—'}</td>
                  <td>{item.countryCode || '—'}</td>
                  <td>{item.status}</td>
                  <td>
                    <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => handleSendInvite(item.id)}>
                      Send invite link
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!loading && !error && view === 'payouts' && (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Affiliate</th>
                <th>Period</th>
                <th>Amount</th>
                <th>Status</th>
                <th>Reference</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {payouts.map((payout) => (
                <tr key={payout.id}>
                  <td>{payout.affiliateName}</td>
                  <td>{new Date(payout.periodStartUtc).toLocaleDateString()} – {new Date(payout.periodEndUtc).toLocaleDateString()}</td>
                  <td>{formatMoney(payout.amount, payout.currencyCode)}</td>
                  <td>{payout.status}</td>
                  <td>{payout.paystackTransferReference || payout.failureReason || '—'}</td>
                  <td>
                    <button
                      type="button"
                      className="btn-primary-action"
                      disabled={payout.status === 'Paid'}
                      onClick={() => handlePayPayout(payout.id)}
                    >
                      {payout.status === 'Paid' ? 'Paid' : 'Pay via Paystack'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!loading && !error && view === 'training' && (
        <div className="space-y-4">
          <section className="progress-section">
            <h3 className="section-title">Add or update a YouTube training video</h3>
            <form className="affiliate-form-grid" onSubmit={handleVideoSubmit}>
              <label>
                <span className="dashboard-label">Title</span>
                <input className="form-input" name="title" value={videoForm.title} onChange={handleVideoChange} placeholder="How to pitch RiseFlow to a principal" />
              </label>
              <label>
                <span className="dashboard-label">Topic</span>
                <input className="form-input" name="topic" value={videoForm.topic} onChange={handleVideoChange} placeholder="Selling to schools" />
              </label>
              <label className="affiliate-form-grid__wide">
                <span className="dashboard-label">YouTube URL</span>
                <input className="form-input" name="youtubeUrl" value={videoForm.youtubeUrl} onChange={handleVideoChange} placeholder="https://www.youtube.com/watch?v=..." />
              </label>
              <label className="affiliate-form-grid__wide">
                <span className="dashboard-label">Description</span>
                <textarea className="form-input" name="description" rows="4" value={videoForm.description} onChange={handleVideoChange} placeholder="Explain what the affiliate should learn from this video." />
              </label>
              <label>
                <span className="dashboard-label">Sort order</span>
                <input className="form-input" type="number" name="sortOrder" value={videoForm.sortOrder} onChange={handleVideoChange} />
              </label>
              <label className="dashboard-actions" style={{ alignItems: 'center' }}>
                <input type="checkbox" name="isPublished" checked={videoForm.isPublished} onChange={handleVideoChange} />
                <span className="dashboard-label">Published</span>
              </label>
              <div className="affiliate-form-grid__wide dashboard-actions">
                <button type="submit" className="btn-primary-action">
                  {editingVideoId ? 'Update video' : 'Add video'}
                </button>
                {editingVideoId && (
                  <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => { setEditingVideoId(null); setVideoForm(emptyVideoForm); }}>
                    Cancel edit
                  </button>
                )}
              </div>
            </form>
          </section>

          <div className="affiliate-video-grid">
            {videos.map((video) => (
              <article key={video.id} className="affiliate-video-card">
                <iframe className="affiliate-video-frame" src={video.youtubeUrl} title={video.title} allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowFullScreen />
                <h4 className="card-title">{video.title}</h4>
                <p className="card-desc">{video.topic || 'Training'} • {video.description || 'No description yet.'}</p>
                <div className="dashboard-actions">
                  <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => handleEditVideo(video)}>Edit</button>
                  <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => handleDeleteVideo(video.id)}>Delete</button>
                </div>
              </article>
            ))}
          </div>
        </div>
      )}
    </PageLayout>
  );
}
