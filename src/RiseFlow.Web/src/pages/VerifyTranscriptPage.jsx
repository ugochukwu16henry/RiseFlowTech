import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
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
    <div className="verify-page">
      <div className="verify-card">
        <Link to="/" className="verify-back">← Back to RiseFlow</Link>
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
          </div>
        )}
      </div>
    </div>
  );
}
