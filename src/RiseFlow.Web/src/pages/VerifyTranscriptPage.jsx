import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import './VerifyTranscriptPage.css';
import { apiFetch } from '../api';

export default function VerifyTranscriptPage() {
  const { token } = useParams();
  const [state, setState] = useState({ status: 'loading', data: null, error: null });

  useEffect(() => {
    if (!token) {
      setState({ status: 'error', data: null, error: 'Missing verification code' });
      return;
    }
    let cancelled = false;
    apiFetch(`/verify/transcript/${encodeURIComponent(token)}`)
      .then((res) => {
        if (cancelled) return null;
        if (res.status === 404)
          return { valid: false, notFound: true };
        if (!res.ok)
          throw new Error('Verification failed');
        return res.json();
      })
      .then((data) => {
        if (cancelled) return;
        if (data?.notFound)
          setState({ status: 'notfound', data: null, error: null });
        else
          setState({ status: 'done', data: data || null, error: null });
      })
      .catch((err) => {
        if (!cancelled)
          setState({ status: 'error', data: null, error: err.message });
      });
    return () => { cancelled = true; };
  }, [token]);

  const formatDate = (utc) => {
    if (!utc) return '—';
    try {
      const d = new Date(utc);
      return d.toLocaleDateString(undefined, { dateStyle: 'medium' });
    } catch {
      return String(utc);
    }
  };

  return (
    <PageLayout title="Transcript verification" role="legal" showSignOut={false}>
      <div className="verify-page">
        <div className="verify-card">
          <p className="card-desc" style={{ marginBottom: '1rem' }}>
            <Link to="/">← Marketing home</Link>
            {' · '}
            <Link to="/login">Sign in</Link>
          </p>
          <h1 className="verify-title">Transcript verification</h1>
          <p className="verify-intro">Any school can scan the QR code on a RiseFlow transcript to verify results instantly.</p>

          {state.status === 'loading' && (
            <p className="verify-status" aria-busy="true">Verifying…</p>
          )}

          {state.status === 'notfound' && (
            <div className="verify-result verify-result--invalid">
              <span className="verify-badge" aria-label="Invalid">Invalid</span>
              <p>This verification code was not found. It may have expired or be incorrect.</p>
            </div>
          )}

          {state.status === 'error' && (
            <div className="verify-result verify-result--invalid">
              <span className="verify-badge" aria-label="Error">Error</span>
              <p>{state.error}</p>
            </div>
          )}

          {state.status === 'done' && state.data && (
            <div className="verify-result verify-result--valid">
              <span className="verify-badge" aria-label="Verified">Verified</span>
              <dl className="verify-details">
                <dt>Student</dt>
                <dd>{state.data.studentName}</dd>
                <dt>School</dt>
                <dd>{state.data.schoolName}</dd>
                <dt>Issued</dt>
                <dd>{formatDate(state.data.issuedAtUtc)}</dd>
                {state.data.issuedToName && (
                  <>
                    <dt>Issued to</dt>
                    <dd>{state.data.issuedToName}</dd>
                  </>
                )}
                {state.data.contentHash && (
                  <>
                    <dt>Verification hash</dt>
                    <dd className="verify-hash">{state.data.contentHash}</dd>
                  </>
                )}
              </dl>
              <p className="verify-note">This transcript is official. The unique hash and QR code prove it has not been forged. riseflow.com/verify</p>

              {Array.isArray(state.data.termSummaries) && state.data.termSummaries.length > 0 && (
                <div style={{ marginTop: '1rem' }}>
                  <h2 style={{ margin: '0 0 0.5rem', fontSize: '1rem' }}>Verified term summary</h2>
                  {state.data.termSummaries.map((term) => (
                    <div key={term.termId} style={{ marginBottom: '0.75rem' }}>
                      <h3 style={{ margin: '0 0 0.25rem', fontSize: '0.95rem' }}>{term.termName}</h3>
                      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
                        <thead>
                          <tr>
                            <th style={{ textAlign: 'left', padding: '0.25rem 0' }}>Subject</th>
                            <th style={{ textAlign: 'left', padding: '0.25rem 0' }}>Score</th>
                            <th style={{ textAlign: 'left', padding: '0.25rem 0' }}>Percent</th>
                            <th style={{ textAlign: 'left', padding: '0.25rem 0' }}>Grade</th>
                          </tr>
                        </thead>
                        <tbody>
                          {(term.subjects || []).map((subject) => (
                            <tr key={`${term.termId}-${subject.subjectName}`}>
                              <td style={{ padding: '0.2rem 0' }}>{subject.subjectName}</td>
                              <td style={{ padding: '0.2rem 0' }}>{subject.score}/{subject.maxScore}</td>
                              <td style={{ padding: '0.2rem 0' }}>{subject.percentage}%</td>
                              <td style={{ padding: '0.2rem 0' }}>{subject.gradeLetter || '—'}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </PageLayout>
  );
}
