import { useState } from 'react';
import { Link } from 'react-router-dom';
import './OnboardingPage.css';
import { apiFetch } from '../api';

export default function OnboardingPage() {
  const [form, setForm] = useState({
    schoolName: '',
    address: '',
    phone: '',
    email: '',
    countryCode: 'NG',
    currencyCode: 'NGN',
    adminEmail: '',
    adminPassword: '',
    adminFullName: '',
    agreedToTermsAndDpa: false,
  });
  const [logo, setLogo] = useState(null);
  const [status, setStatus] = useState({ type: null, message: null });
  const [submitting, setSubmitting] = useState(false);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.schoolName?.trim()) {
      setStatus({ type: 'error', message: 'School name is required.' });
      return;
    }
    if (form.adminEmail?.trim() && !form.adminPassword?.trim()) {
      setStatus({ type: 'error', message: 'Admin password is required when you provide an admin email.' });
      return;
    }
    if (form.adminEmail?.trim() && !form.agreedToTermsAndDpa) {
      setStatus({ type: 'error', message: 'You must agree to the RiseFlow Terms of Service and Data Processing Agreement to register.' });
      return;
    }
    setSubmitting(true);
    setStatus({ type: null, message: null });
    try {
      const fd = new FormData();
      fd.append('SchoolName', form.schoolName.trim());
      fd.append('Address', form.address || '');
      fd.append('Phone', form.phone || '');
      fd.append('Email', form.email || '');
      fd.append('CountryCode', form.countryCode || '');
      fd.append('CurrencyCode', form.currencyCode || '');
      fd.append('AdminEmail', form.adminEmail || '');
      fd.append('AdminPassword', form.adminPassword || '');
      fd.append('AdminFullName', form.adminFullName || '');
      fd.append('AgreedToTermsAndDpa', form.agreedToTermsAndDpa ? 'true' : 'false');
      if (logo) fd.append('Logo', logo);

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
      setStatus({
        type: 'success',
        message: `School "${data.schoolName}" has been created. You can now sign in with your admin email.`,
      });
      setForm({ schoolName: '', address: '', phone: '', email: '', countryCode: 'NG', currencyCode: 'NGN', adminEmail: '', adminPassword: '', adminFullName: '', agreedToTermsAndDpa: false });
      setLogo(null);
    } catch (err) {
      setStatus({ type: 'error', message: err.message || 'Network error.' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="onboarding-page">
      <div className="onboarding-card">
        <Link to="/" className="onboarding-back">← Back to RiseFlow</Link>
        <h1 className="onboarding-title">Register your school</h1>
        <p className="onboarding-intro">Create your school account, set your school name, and upload your logo. You’ll get an admin account to manage everything.</p>

        <form onSubmit={handleSubmit} className="onboarding-form">
          <fieldset className="onboarding-fieldset">
            <legend>School details</legend>
            <label className="onboarding-label">
              School name <span className="required">*</span>
              <input type="text" name="schoolName" value={form.schoolName} onChange={handleChange} required placeholder="e.g. Excellence Academy" className="onboarding-input" autoComplete="organization" />
            </label>
            <label className="onboarding-label">
              Address
              <input type="text" name="address" value={form.address} onChange={handleChange} placeholder="Full address" className="onboarding-input" />
            </label>
            <label className="onboarding-label">
              Phone
              <input type="tel" name="phone" value={form.phone} onChange={handleChange} placeholder="+234..." className="onboarding-input" />
            </label>
            <label className="onboarding-label">
              School email
              <input type="email" name="email" value={form.email} onChange={handleChange} placeholder="school@example.com" className="onboarding-input" />
            </label>
            <div className="onboarding-row">
              <label className="onboarding-label">
                Country
                <select name="countryCode" value={form.countryCode} onChange={handleChange} className="onboarding-input">
                  <option value="NG">Nigeria</option>
                  <option value="GH">Ghana</option>
                  <option value="KE">Kenya</option>
                  <option value="ZA">South Africa</option>
                  <option value="TZ">Tanzania</option>
                  <option value="UG">Uganda</option>
                </select>
              </label>
              <label className="onboarding-label">
                Currency
                <select name="currencyCode" value={form.currencyCode} onChange={handleChange} className="onboarding-input">
                  <option value="NGN">NGN</option>
                  <option value="GHS">GHS</option>
                  <option value="KES">KES</option>
                  <option value="ZAR">ZAR</option>
                  <option value="USD">USD</option>
                </select>
              </label>
            </div>
          </fieldset>

          <fieldset className="onboarding-fieldset">
            <legend>Logo</legend>
            <label className="onboarding-label">
              School logo (optional)
              <input type="file" accept=".png,.jpg,.jpeg,.gif,.webp" onChange={(e) => setLogo(e.target.files?.[0] || null)} className="onboarding-file" />
              {logo && <span className="onboarding-filename">{logo.name}</span>}
            </label>
          </fieldset>

          <fieldset className="onboarding-fieldset">
            <legend>Admin account</legend>
            <label className="onboarding-label">
              Admin email
              <input type="email" name="adminEmail" value={form.adminEmail} onChange={handleChange} placeholder="principal@school.com" className="onboarding-input" autoComplete="email" />
            </label>
            <label className="onboarding-label">
              Admin full name
              <input type="text" name="adminFullName" value={form.adminFullName} onChange={handleChange} placeholder="Principal name" className="onboarding-input" autoComplete="name" />
            </label>
            <label className="onboarding-label">
              Admin password
              <input type="password" name="adminPassword" value={form.adminPassword} onChange={handleChange} placeholder="Min 8 characters" className="onboarding-input" autoComplete="new-password" minLength={8} />
            </label>
            <label className="onboarding-label onboarding-checkbox">
              <input
                type="checkbox"
                name="agreedToTermsAndDpa"
                checked={form.agreedToTermsAndDpa}
                onChange={(e) => setForm((prev) => ({ ...prev, agreedToTermsAndDpa: e.target.checked }))}
                className="onboarding-input"
              />
              <span>I agree to the <a href="/terms" target="_blank" rel="noopener noreferrer">RiseFlow Terms of Service</a> and <a href="/privacy" target="_blank" rel="noopener noreferrer">Data Processing Agreement</a>.</span>
            </label>
          </fieldset>

          {status.message && (
            <div className={`onboarding-status onboarding-status--${status.type}`} role="alert">
              {status.message}
            </div>
          )}

          <button type="submit" className="onboarding-submit" disabled={submitting}>
            {submitting ? 'Creating…' : 'Register school'}
          </button>
        </form>
      </div>
    </div>
  );
}
