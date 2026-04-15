import { useState } from 'react';
import { useSearchParams, Link, useNavigate } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { getApiBase } from '../api';
import './RolePages.css';
import './ClaimChildPage.css';

export default function StaffSignupPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const schoolId = searchParams.get('school')?.trim() || '';

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [phone, setPhone] = useState('');
  const [whatsAppNumber, setWhatsAppNumber] = useState('');
  const [staffId, setStaffId] = useState('');
  const [roleTitle, setRoleTitle] = useState('Support Staff');
  const [department, setDepartment] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!schoolId) {
      setError('Invalid signup link. Ask your school admin for the correct staff signup link.');
      return;
    }
    if (!email.trim() || !password || !firstName.trim() || !lastName.trim()) {
      setError('First name, last name, email and password are required.');
      return;
    }

    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch(`${getApiBase()}/api/teachers/signup`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          schoolId,
          email: email.trim(),
          password,
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          middleName: null,
          phone: phone.trim() || null,
          whatsAppNumber: whatsAppNumber.trim() || null,
          staffId: staffId.trim() || null,
          dateOfBirth: null,
          gender: null,
          nationality: null,
          stateOfOrigin: null,
          lga: null,
          religion: null,
          nin: null,
          nationalIdType: null,
          nationalIdNumber: null,
          trcnNumber: null,
          residentialAddress: null,
          highestQualification: null,
          fieldOfStudy: null,
          yearsOfExperience: null,
          previousSchools: null,
          professionalBodies: null,
          roleTitle: roleTitle.trim() || 'Support Staff',
          department: department.trim() || null,
        }),
      });

      const text = await res.text();
      const data = text ? (() => { try { return JSON.parse(text); } catch { return { message: text }; } })() : {};
      if (!res.ok) {
        setError(data?.message || data?.title || text || 'Signup failed. Try again or contact your school admin.');
        return;
      }

      navigate('/staff', { replace: true });
    } catch (err) {
      setError(err.message || 'Network error.');
    } finally {
      setSubmitting(false);
    }
  };

  if (!schoolId) {
    return (
      <PageLayout title="Staff signup" role="teacher">
        <div className="claim-child">
          <p className="empty-state empty-state--error">
            This signup link is invalid or missing the school. Ask your school admin to resend your staff signup link.
          </p>
          <Link to="/staff" className="header-link" style={{ marginTop: '1rem', display: 'inline-block' }}>
            Back to Staff preview
          </Link>
        </div>
      </PageLayout>
    );
  }

  return (
    <PageLayout title="Staff signup" role="teacher">
      <div className="claim-child parent-signup">
        <p className="card-desc">
          Create your staff account for this school. After signup, sign in through your school&apos;s login to see your dashboard.
        </p>

        <form onSubmit={handleSubmit} className="claim-form signup-form">
          <label htmlFor="firstName" className="claim-label">First name</label>
          <input
            id="firstName"
            type="text"
            autoComplete="given-name"
            className="claim-input signup-input"
            placeholder="First name"
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            required
          />

          <label htmlFor="lastName" className="claim-label">Last name</label>
          <input
            id="lastName"
            type="text"
            autoComplete="family-name"
            className="claim-input signup-input"
            placeholder="Last name"
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            required
          />

          <label htmlFor="email" className="claim-label">Email</label>
          <input
            id="email"
            type="email"
            autoComplete="email"
            className="claim-input signup-input"
            placeholder="you@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />

          <label htmlFor="password" className="claim-label">Password</label>
          <input
            id="password"
            type="password"
            autoComplete="new-password"
            className="claim-input signup-input"
            placeholder="At least 8 characters"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            minLength={8}
            required
          />

          <label htmlFor="roleTitle" className="claim-label">Staff role title</label>
          <input
            id="roleTitle"
            type="text"
            className="claim-input signup-input"
            placeholder="e.g. Support Staff, Bursar, Front Desk"
            value={roleTitle}
            onChange={(e) => setRoleTitle(e.target.value)}
          />

          <label htmlFor="department" className="claim-label">Department (optional)</label>
          <input
            id="department"
            type="text"
            className="claim-input signup-input"
            placeholder="e.g. Administration"
            value={department}
            onChange={(e) => setDepartment(e.target.value)}
          />

          <label htmlFor="phone" className="claim-label">Phone</label>
          <input
            id="phone"
            type="tel"
            autoComplete="tel"
            className="claim-input signup-input"
            placeholder="Phone number"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
          />

          <label htmlFor="whatsApp" className="claim-label">WhatsApp number</label>
          <input
            id="whatsApp"
            type="tel"
            className="claim-input signup-input"
            placeholder="WhatsApp number"
            value={whatsAppNumber}
            onChange={(e) => setWhatsAppNumber(e.target.value)}
          />

          <label htmlFor="staffId" className="claim-label">Staff ID (optional)</label>
          <input
            id="staffId"
            type="text"
            className="claim-input signup-input"
            placeholder="e.g. ST-ADM-01"
            value={staffId}
            onChange={(e) => setStaffId(e.target.value)}
          />

          {error && (
            <p className="claim-error" role="alert">{error}</p>
          )}
          <button type="submit" className="btn-claim" disabled={submitting}>
            {submitting ? 'Creating account…' : 'Create staff account'}
          </button>
        </form>

        <p className="card-desc" style={{ marginTop: '1.25rem' }}>
          Already have an account? Sign in through your school&apos;s login.
        </p>
      </div>
    </PageLayout>
  );
}
