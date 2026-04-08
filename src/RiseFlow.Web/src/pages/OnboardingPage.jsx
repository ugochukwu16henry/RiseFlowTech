import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import './OnboardingPage.css';
import { apiFetch, getApiBase, STORAGE_ONBOARDING_KEY, STORAGE_TENANT_KEY } from '../api';

export default function OnboardingPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState({
    schoolName: '',
    email: '',
    adminFullName: '',
    adminPassword: '',
    agreedToTermsAndDpa: false,
  });
  const [logo, setLogo] = useState(null);
  const [cacDocument, setCacDocument] = useState(null);
  const [step, setStep] = useState(1);
  const [createdSchool, setCreatedSchool] = useState(null);
  const [status, setStatus] = useState({ type: null, message: null });
  const [submitting, setSubmitting] = useState(false);

  const buildPublicUrl = (relativePath) => {
    if (!relativePath) return null;
    if (relativePath.startsWith('http://') || relativePath.startsWith('https://')) return relativePath;
    const normalizedPath = relativePath.replace(/^\/+/, '');
    const base = getApiBase();
    if (!base) return `/${normalizedPath}`;
    return `${base}/${normalizedPath}`;
  };

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleContinue = (e) => {
    e.preventDefault();
    if (!form.schoolName?.trim()) {
      setStatus({ type: 'error', message: 'School name is required.' });
      return;
    }
    if (!form.email?.trim()) {
      setStatus({ type: 'error', message: 'School email is required.' });
      return;
    }
    if (!form.adminPassword || form.adminPassword.length < 8) {
      setStatus({ type: 'error', message: 'Create an admin password with at least 8 characters.' });
      return;
    }
    if (!form.agreedToTermsAndDpa) {
      setStatus({ type: 'error', message: 'Please agree to the RiseFlow Terms of Service and Data Processing Agreement.' });
      return;
    }
    setStatus({ type: null, message: null });
    setStep(2);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.schoolName?.trim() || !form.email?.trim()) {
      setStatus({ type: 'error', message: 'Complete step 1 before finishing setup.' });
      return;
    }

    setSubmitting(true);
    setStatus({ type: null, message: null });

    try {
      const fd = new FormData();
      fd.append('SchoolName', form.schoolName.trim());
      fd.append('Email', form.email.trim());
      fd.append('AdminEmail', form.email.trim());
      fd.append('AdminPassword', form.adminPassword);
      fd.append('AdminFullName', form.adminFullName?.trim() || form.schoolName.trim());
      fd.append('CountryCode', 'NG');
      fd.append('CurrencyCode', 'NGN');
      fd.append('AgreedToTermsAndDpa', form.agreedToTermsAndDpa ? 'true' : 'false');
      if (logo) fd.append('Logo', logo);
      if (cacDocument) fd.append('CacDocument', cacDocument);

      const res = await apiFetch('/api/schools/onboard-with-logo', {
        method: 'POST',
        body: fd,
      });

      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        const msg = data.errors?.length ? data.errors.join(' ') : data.message || 'Registration failed.';
        setStatus({ type: 'error', message: msg });
        return;
      }

      setCreatedSchool({
        schoolName: data.schoolName || form.schoolName,
        schoolId: data.schoolId || null,
        logoPath: data.logoPath || null,
        cacDocumentPath: data.cacDocumentPath || null,
      });

      // Auto sign-in owner and take them directly to School Admin dashboard.
      const loginRes = await apiFetch('/api/auth/login', {
        method: 'POST',
        skipTenantHeader: true,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: form.email.trim(),
          password: form.adminPassword,
        }),
      });
      const loginData = await loginRes.json().catch(() => null);
      if (loginRes.ok && loginData?.success) {
        try {
          if (loginData.schoolId) {
            localStorage.setItem(STORAGE_TENANT_KEY, loginData.schoolId);
          }
          localStorage.setItem(STORAGE_ONBOARDING_KEY, JSON.stringify({
            schoolName: data.schoolName || form.schoolName,
            schoolId: data.schoolId || loginData.schoolId || null,
            logoPath: data.logoPath || null,
            cacDocumentPath: data.cacDocumentPath || null,
            createdAtUtc: new Date().toISOString(),
          }));
        } catch {
          // ignore storage issues and still navigate
        }
        navigate('/school', { replace: true });
        return;
      }

      // Fallback: show success page if auto login fails.
      setStep(3);
    } catch (err) {
      setStatus({ type: 'error', message: err.message || 'Network error.' });
    } finally {
      setSubmitting(false);
    }
  };

  const progressIndex = step === 3 ? 3 : step;

  const authLinks = (
    <>
      <Link to="/login" className="text-sm font-medium text-indigo-600 hover:text-indigo-700 dark:text-indigo-400">
        Sign in
      </Link>
    </>
  );

  if (step === 3 && createdSchool) {
    return (
      <PageLayout variant="auth" authHeaderRight={authLinks}>
      <div className="onboarding-page">
        <div className="onboarding-card onboarding-success-card">
          <div className="success-check" aria-hidden="true">
            <svg viewBox="0 0 64 64" fill="none">
              <circle cx="32" cy="32" r="30" />
              <path d="M18 33L28 43L46 24" />
            </svg>
          </div>

          <h1 className="onboarding-title onboarding-success-title">Congratulations, {createdSchool.schoolName} is now live!</h1>
          <p className="onboarding-intro onboarding-success-intro">Your setup is complete. Welcome to RiseFlow.</p>

          <div className="school-id-box">
            <span className="school-id-label">RiseFlow ID</span>
            <strong className="school-id-value">{createdSchool.schoolId || 'Generated'}</strong>
          </div>

          {createdSchool.logoPath && (
            <div className="logo-preview-box">
              <span className="school-id-label">School Logo Preview</span>
              <a href={buildPublicUrl(createdSchool.logoPath)} target="_blank" rel="noopener noreferrer" className="logo-preview-link" title="Open full logo">
                <img
                  src={buildPublicUrl(createdSchool.logoPath)}
                  alt={`${createdSchool.schoolName} logo`}
                  className="logo-preview-image"
                  loading="lazy"
                />
              </a>
            </div>
          )}

          {(createdSchool.logoPath || createdSchool.cacDocumentPath) && (
            <div className="school-files-box">
              <span className="school-id-label">Uploaded Files</span>
              <div className="school-files-list">
                {createdSchool.logoPath && (
                  <a href={buildPublicUrl(createdSchool.logoPath)} target="_blank" rel="noopener noreferrer">View School Logo</a>
                )}
                {createdSchool.cacDocumentPath && (
                  <a href={buildPublicUrl(createdSchool.cacDocumentPath)} target="_blank" rel="noopener noreferrer">View CAC Document</a>
                )}
              </div>
            </div>
          )}

          <div className="success-actions">
            <Link to="/school/import" className="action-card">
              <h3>Import Students</h3>
              <p>Upload your student list to go live faster.</p>
            </Link>
            <Link to={createdSchool.schoolId ? `/teacher/signup?school=${encodeURIComponent(createdSchool.schoolId)}` : '/teacher'} className="action-card">
              <h3>Add Teachers</h3>
              <p>Create teacher accounts and assign classes.</p>
            </Link>
            <Link to="/school" className="action-card">
              <h3>Set Up Classes</h3>
              <p>Organize classes, subjects, and academic terms.</p>
            </Link>
          </div>

          <div className="next-checklist">
            <p className="next-checklist-title">Next Steps</p>
            <ul>
              <li><a href={`${getApiBase()}/api/public/teacher-quick-start`} target="_blank" rel="noopener noreferrer">Download the Teacher Guide</a></li>
              <li><Link to="/school">Add your first class</Link></li>
              <li><Link to="/school/access-codes">Print Parent Access Codes</Link></li>
            </ul>
          </div>
        </div>
      </div>
      </PageLayout>
    );
  }

  return (
    <PageLayout variant="auth" authHeaderRight={authLinks}>
    <div className="onboarding-page">
      <div className="onboarding-card">
        <Link to="/" className="onboarding-back">← Back to RiseFlow</Link>
        <h1 className="onboarding-title">Welcome to RiseFlow</h1>
        <p className="onboarding-intro">Let’s get your school digitalized in 2 minutes.</p>

        <div className="progress-strip" aria-label="Onboarding progress">
          <div className={`progress-dot ${progressIndex >= 1 ? 'is-active' : ''}`} />
          <div className={`progress-dot ${progressIndex >= 2 ? 'is-active' : ''}`} />
          <div className={`progress-dot ${progressIndex >= 3 ? 'is-active' : ''}`} />
        </div>

        {step === 1 ? (
          <form onSubmit={handleContinue} className="onboarding-form">
            <label className="onboarding-label">
              School Name
              <input
                type="text"
                name="schoolName"
                value={form.schoolName}
                onChange={handleChange}
                required
                placeholder="e.g. Bright Future Academy"
                className="onboarding-input"
                autoComplete="organization"
              />
            </label>

            <label className="onboarding-label">
              School Email
              <input
                type="email"
                name="email"
                value={form.email}
                onChange={handleChange}
                required
                placeholder="school@example.com"
                className="onboarding-input"
                autoComplete="email"
              />
            </label>

            <label className="onboarding-label">
              Admin full name
              <input
                type="text"
                name="adminFullName"
                value={form.adminFullName}
                onChange={handleChange}
                placeholder="e.g. Mrs. Ada Okonkwo"
                className="onboarding-input"
                autoComplete="name"
              />
            </label>

            <label className="onboarding-label">
              Create admin password
              <input
                type="password"
                name="adminPassword"
                value={form.adminPassword}
                onChange={handleChange}
                required
                placeholder="Minimum 8 characters"
                className="onboarding-input"
                autoComplete="new-password"
              />
            </label>

            <label className="onboarding-label onboarding-checkbox">
              <input
                type="checkbox"
                name="agreedToTermsAndDpa"
                checked={form.agreedToTermsAndDpa}
                onChange={(e) => setForm((prev) => ({ ...prev, agreedToTermsAndDpa: e.target.checked }))}
                className="onboarding-input"
              />
              <span>
                I agree to the <a href="/terms" target="_blank" rel="noopener noreferrer">RiseFlow Terms of Service</a> and <a href="/privacy" target="_blank" rel="noopener noreferrer">Data Processing Agreement</a>.
              </span>
            </label>

            {status.message && (
              <div className={`onboarding-status onboarding-status--${status.type}`} role="alert">
                {status.message}
              </div>
            )}

            <button type="submit" className="onboarding-submit">Continue Setup</button>
          </form>
        ) : (
          <form onSubmit={handleSubmit} className="onboarding-form">
            <div className="onboarding-label">
              <p className="onboarding-label-title">School Logo</p>
              <label className="upload-dropzone" htmlFor="logo-upload">
                <span className="upload-icon" aria-hidden="true">+</span>
                <span>Click to upload or drag and drop</span>
                <small>PNG, JPG up to 5MB</small>
              </label>
              <input
                id="logo-upload"
                type="file"
                accept=".png,.jpg,.jpeg,.webp"
                onChange={(e) => setLogo(e.target.files?.[0] || null)}
                className="sr-only"
              />
              {logo && <span className="onboarding-filename">{logo.name}</span>}
            </div>

            <div className="onboarding-label">
              <p className="onboarding-label-title">CAC Document</p>
              <label className="upload-dropzone" htmlFor="cac-upload">
                <span className="upload-icon" aria-hidden="true">+</span>
                <span>Upload CAC certificate</span>
                <small>PDF, PNG, JPG up to 10MB</small>
              </label>
              <input
                id="cac-upload"
                type="file"
                accept=".pdf,.png,.jpg,.jpeg"
                onChange={(e) => setCacDocument(e.target.files?.[0] || null)}
                className="sr-only"
              />
              {cacDocument && <span className="onboarding-filename">{cacDocument.name}</span>}
            </div>

            {status.message && (
              <div className={`onboarding-status onboarding-status--${status.type}`} role="alert">
                {status.message}
              </div>
            )}

            <div className="wizard-actions">
              <button type="button" className="onboarding-secondary" onClick={() => setStep(1)} disabled={submitting}>Back</button>
              <button type="submit" className="onboarding-submit" disabled={submitting}>
                {submitting ? 'Finishing setup…' : 'Continue to Dashboard'}
              </button>
            </div>
          </form>
        )}

        <p className="onboarding-footnote">Mobile-friendly setup: complete onboarding in under 60 seconds.</p>
      </div>
    </div>
    </PageLayout>
  );
}
