import { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch } from '../api';
import './RolePages.css';
import './AddStudentPage.css';

export default function AddStudentPage() {
  const [classes, setClasses] = useState([]);
  const [loadingClasses, setLoadingClasses] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(false);
  const [createdStudent, setCreatedStudent] = useState(null);
  const [photoUploading, setPhotoUploading] = useState(false);
  const [photoUploaded, setPhotoUploaded] = useState(false);
  const photoInputRef = useRef(null);
  const [form, setForm] = useState({
    firstName: '',
    lastName: '',
    middleName: '',
    admissionNumber: '',
    classId: '',
    gender: '',
    dateOfBirth: '',
    emergencyContactName: '',
    emergencyContactPhone: '',
  });

  useEffect(() => {
    let cancelled = false;
    apiFetch('/api/schools/classes')
      .then((r) => (r.ok ? r.json() : []))
      .then((data) => { if (!cancelled) setClasses(Array.isArray(data) ? data : []); })
      .catch(() => { if (!cancelled) setClasses([]); })
      .finally(() => { if (!cancelled) setLoadingClasses(false); });
    return () => { cancelled = true; };
  }, []);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    setError(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    const firstName = (form.firstName || '').trim();
    const lastName = (form.lastName || '').trim();
    if (!firstName || !lastName) {
      setError('First name and last name are required.');
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const body = {
        firstName,
        lastName,
        middleName: form.middleName?.trim() || null,
        admissionNumber: form.admissionNumber?.trim() || null,
        classId: form.classId || null,
        gender: form.gender?.trim() || null,
        dateOfBirth: form.dateOfBirth || null,
        emergencyContactName: form.emergencyContactName?.trim() || null,
        emergencyContactPhone: form.emergencyContactPhone?.trim() || null,
        nationality: null,
        stateOfOrigin: null,
        lGA: null,
        nIN: null,
        nationalIdType: null,
        nationalIdNumber: null,
        dateOfAdmission: null,
        gradeId: null,
        previousSchool: null,
        bloodGroup: null,
        genotype: null,
        allergies: null,
      };
      const res = await apiFetch('/api/students', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        setError(data?.message || data?.title || 'Failed to add student.');
        return;
      }
      setCreatedStudent({ id: data.id, firstName: data.firstName, lastName: data.lastName });
      setSuccess(true);
      setPhotoUploaded(false);
      setForm({ firstName: '', lastName: '', middleName: '', admissionNumber: '', classId: '', gender: '', dateOfBirth: '', emergencyContactName: '', emergencyContactPhone: '' });
    } catch (e) {
      setError(e.message || 'Network error.');
    } finally {
      setSubmitting(false);
    }
  };

  const onPhotoUpload = async (e) => {
    const file = e.target?.files?.[0];
    if (!file || !createdStudent) return;
    setPhotoUploading(true);
    const formData = new FormData();
    formData.append('file', file);
    try {
      const res = await apiFetch(`/api/students/${createdStudent.id}/photo`, { method: 'POST', body: formData });
      if (res.ok) setPhotoUploaded(true);
    } finally {
      setPhotoUploading(false);
      e.target.value = '';
    }
  };

  if (success) {
    return (
      <PageLayout title="Student added" backTo="/school">
        <div className="add-student-success">
          <p className="add-student-success-msg">Student registered successfully. They will appear in your student list and you can generate a parent access code for them from Access Codes.</p>
          {createdStudent && (
            <div className="add-student-photo-upload">
              <p className="form-label">Passport-size photo (optional)</p>
              <input
                type="file"
                accept=".jpg,.jpeg,.png,.gif,.webp"
                ref={photoInputRef}
                onChange={onPhotoUpload}
                style={{ display: 'none' }}
                aria-label="Upload passport photo"
              />
              <button
                type="button"
                className="btn-upload-photo"
                onClick={() => photoInputRef.current?.click()}
                disabled={photoUploading}
              >
                {photoUploading ? 'Uploading…' : photoUploaded ? 'Photo uploaded ✓' : 'Upload passport photo'}
              </button>
            </div>
          )}
          <div className="add-student-actions">
            <button type="button" className="btn-add-another" onClick={() => { setSuccess(false); setCreatedStudent(null); }}>
              Add another student
            </button>
            <Link to="/school" className="btn-back-dashboard">Back to School Admin</Link>
          </div>
        </div>
      </PageLayout>
    );
  }

  return (
    <PageLayout title="Register new student" backTo="/school">
      <div className="add-student">
        <p className="card-desc">
          Add one student at a time. New registrations will appear in your student list. You can also <Link to="/school/import">bulk upload from Excel</Link>.
        </p>

        <form onSubmit={handleSubmit} className="add-student-form">
          <div className="form-row">
            <label htmlFor="firstName" className="form-label">First name <span className="required">*</span></label>
            <input id="firstName" name="firstName" type="text" required value={form.firstName} onChange={handleChange} className="form-input" placeholder="First name" />
          </div>
          <div className="form-row">
            <label htmlFor="lastName" className="form-label">Last name <span className="required">*</span></label>
            <input id="lastName" name="lastName" type="text" required value={form.lastName} onChange={handleChange} className="form-input" placeholder="Last name" />
          </div>
          <div className="form-row">
            <label htmlFor="middleName" className="form-label">Middle name</label>
            <input id="middleName" name="middleName" type="text" value={form.middleName} onChange={handleChange} className="form-input" placeholder="Optional" />
          </div>
          <div className="form-row">
            <label htmlFor="admissionNumber" className="form-label">Admission number</label>
            <input id="admissionNumber" name="admissionNumber" type="text" value={form.admissionNumber} onChange={handleChange} className="form-input" placeholder="Optional" />
          </div>
          <div className="form-row">
            <label htmlFor="classId" className="form-label">Class</label>
            <select id="classId" name="classId" value={form.classId} onChange={handleChange} className="form-input" disabled={loadingClasses}>
              <option value="">— Select class —</option>
              {classes.map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
            {loadingClasses && <span className="form-hint">Loading classes…</span>}
          </div>
          <div className="form-row">
            <label htmlFor="gender" className="form-label">Gender</label>
            <select id="gender" name="gender" value={form.gender} onChange={handleChange} className="form-input">
              <option value="">—</option>
              <option value="Male">Male</option>
              <option value="Female">Female</option>
              <option value="Other">Other</option>
            </select>
          </div>
          <div className="form-row">
            <label htmlFor="dateOfBirth" className="form-label">Date of birth</label>
            <input id="dateOfBirth" name="dateOfBirth" type="date" value={form.dateOfBirth} onChange={handleChange} className="form-input" />
          </div>
          <div className="form-row">
            <label htmlFor="emergencyContactName" className="form-label">Emergency contact name</label>
            <input id="emergencyContactName" name="emergencyContactName" type="text" value={form.emergencyContactName} onChange={handleChange} className="form-input" placeholder="Optional" />
          </div>
          <div className="form-row">
            <label htmlFor="emergencyContactPhone" className="form-label">Emergency contact phone</label>
            <input id="emergencyContactPhone" name="emergencyContactPhone" type="tel" value={form.emergencyContactPhone} onChange={handleChange} className="form-input" placeholder="Optional" />
          </div>

          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-actions">
            <button type="submit" className="btn-submit" disabled={submitting}>
              {submitting ? 'Adding…' : 'Register student'}
            </button>
            <Link to="/school" className="btn-cancel">Cancel</Link>
          </div>
        </form>
      </div>
    </PageLayout>
  );
}
