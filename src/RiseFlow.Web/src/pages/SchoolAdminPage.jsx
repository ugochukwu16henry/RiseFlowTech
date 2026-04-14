import { useState, useEffect, useCallback, useRef } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import StudentPhoto from '../components/StudentPhoto';
import { apiFetch, getApiBase, STORAGE_ONBOARDING_KEY, STORAGE_TENANT_KEY } from '../api';
import './RolePages.css';

function formatMoney(amount, currencyCode) {
  const n = Number(amount);
  if (Number.isNaN(n)) return '—';
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: currencyCode || 'NGN', maximumFractionDigits: 0 }).format(n);
}

export default function SchoolAdminPage() {
  const [dashboard, setDashboard] = useState(null);
  const [teachers, setTeachers] = useState([]);
  const [students, setStudents] = useState([]);
  const [classes, setClasses] = useState([]);
  const [parents, setParents] = useState([]);
  const [billing, setBilling] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [uploadingId, setUploadingId] = useState(null);
  const [uploadingAsset, setUploadingAsset] = useState(false);
  const [uploadingLogo, setUploadingLogo] = useState(false);
  const [uploadingRegistrationDoc, setUploadingRegistrationDoc] = useState(false);
  const [savingSchoolProfile, setSavingSchoolProfile] = useState(false);
  const [logoUploadError, setLogoUploadError] = useState(null);
  const [schoolProfileError, setSchoolProfileError] = useState(null);
  const [schoolProfile, setSchoolProfile] = useState({
    name: '',
    ownerName: '',
    schoolAdminName: '',
    principalName: '',
    address: '',
    countryCode: '',
    email: '',
    phone: '',
    whatsAppNumber: '',
    cacNumber: '',
    logoPath: null,
    registrationDocumentPath: null,
  });
  const [savingClassId, setSavingClassId] = useState(null);
  const [selectedStudentIds, setSelectedStudentIds] = useState([]);
  const [bulkClassId, setBulkClassId] = useState('');
  const [bulkAssigning, setBulkAssigning] = useState(false);
  const [selectedTeacherId, setSelectedTeacherId] = useState(null);
  const [selectedTeacherProfile, setSelectedTeacherProfile] = useState(null);
  const [teacherAssignClassId, setTeacherAssignClassId] = useState('');
  const [assigningTeacherClass, setAssigningTeacherClass] = useState(false);
  const [removingTeacherClassId, setRemovingTeacherClassId] = useState(null);
  const [teacherFieldSettings, setTeacherFieldSettings] = useState([]);
  const [loadingTeacherProfile, setLoadingTeacherProfile] = useState(false);
  const [savingFieldSettingKey, setSavingFieldSettingKey] = useState(null);
  const [newCustomField, setNewCustomField] = useState({ displayName: '', fieldKey: '' });
  const fileInputRefs = useRef({});
  const schoolFileInputRef = useRef(null);
  const schoolLogoInputRef = useRef(null);
  const registrationDocInputRef = useRef(null);
  const [paying, setPaying] = useState(false);
  const [activeView, setActiveView] = useState('overview');
  const [onboardingSummary, setOnboardingSummary] = useState(() => {
    try {
      const raw = localStorage.getItem(STORAGE_ONBOARDING_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  });

  const readJsonOrThrow = useCallback(async (response, fallbackMessage) => {
    if (response.status === 401 || response.status === 403) {
      throw new Error('Your session expired or your school access is missing. Please sign in again as School Admin.');
    }
    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(text || fallbackMessage);
    }
    return response.json();
  }, []);

  const loadData = useCallback((options = {}) => {
    const { background = false } = options;
    if (background) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }
    setError(null);
    return Promise.allSettled([
      apiFetch('/api/schools/dashboard').then((r) => readJsonOrThrow(r, 'Failed to load school dashboard.')),
      apiFetch('/api/schools/profile').then((r) => readJsonOrThrow(r, 'Failed to load school profile.')),
      apiFetch('/api/teachers').then((r) => readJsonOrThrow(r, 'Failed to load teachers.')),
      apiFetch('/api/students').then((r) => readJsonOrThrow(r, 'Failed to load students.')),
      apiFetch('/api/schools/classes').then((r) => readJsonOrThrow(r, 'Failed to load classes.')),
      apiFetch('/api/parents').then((r) => readJsonOrThrow(r, 'Failed to load parents.')),
      apiFetch('/api/billing').then((r) => readJsonOrThrow(r, 'Failed to load billing records.')),
    ])
      .then((results) => {
        const [dashResult, profileResult, teacherResult, studentResult, classResult, parentResult, billingResult] = results;
        const dash = dashResult.status === 'fulfilled' ? dashResult.value : null;
        const profile = profileResult.status === 'fulfilled' ? profileResult.value : null;

        if (!dash) {
          const failure = results.find((result) => result.status === 'rejected');
          throw new Error(failure?.reason?.message || 'Failed to load school dashboard.');
        }

        setDashboard(dash);
        if (profile) {
          setSchoolProfile({
            name: profile.name || '',
            ownerName: profile.ownerName || '',
            schoolAdminName: profile.schoolAdminName || '',
            principalName: profile.principalName || '',
            address: profile.address || '',
            countryCode: profile.countryCode || '',
            email: profile.email || '',
            phone: profile.phone || '',
            whatsAppNumber: profile.whatsAppNumber || '',
            cacNumber: profile.cacNumber || '',
            logoPath: profile.logoPath || null,
            registrationDocumentPath: profile.registrationDocumentPath || null,
          });
        }
        setTeachers(teacherResult.status === 'fulfilled' && Array.isArray(teacherResult.value) ? teacherResult.value : []);
        setStudents(studentResult.status === 'fulfilled' && Array.isArray(studentResult.value) ? studentResult.value : []);
        setClasses(classResult.status === 'fulfilled' && Array.isArray(classResult.value) ? classResult.value : []);
        setParents(parentResult.status === 'fulfilled' && Array.isArray(parentResult.value) ? parentResult.value : []);
        setBilling(billingResult.status === 'fulfilled' && Array.isArray(billingResult.value) ? billingResult.value : []);
      })
      .catch((err) => {
        const message = /blocked or unreachable|failed to fetch|networkerror/i.test(String(err?.message || ''))
          ? 'The live school dashboard is syncing right now. Please refresh again shortly.'
          : (err.message || 'Failed to load data');
        setError(message);
      })
      .finally(() => {
        if (background) {
          setRefreshing(false);
        } else {
          setLoading(false);
        }
      });
  }, [readJsonOrThrow]);

  useEffect(() => { loadData(); }, [loadData]);

  const loadTeacherFieldSettings = useCallback(async () => {
    try {
      const res = await apiFetch('/api/teachers/profile-field-settings');
      if (!res.ok) throw new Error(await res.text());
      const settings = await res.json();
      setTeacherFieldSettings(Array.isArray(settings) ? settings : []);
    } catch {
      setTeacherFieldSettings([]);
    }
  }, []);

  useEffect(() => {
    if (activeView === 'people') {
      loadTeacherFieldSettings();
    }
  }, [activeView, loadTeacherFieldSettings]);

  const fetchTeacherProfile = async (teacherId) => {
    setLoadingTeacherProfile(true);
    try {
      const res = await apiFetch(`/api/teachers/${teacherId}/profile-config`);
      if (!res.ok) throw new Error(await res.text());
      const profile = await res.json();
      setSelectedTeacherProfile(profile);
      if (Array.isArray(profile?.fieldSettings) && profile.fieldSettings.length > 0) {
        setTeacherFieldSettings(profile.fieldSettings);
      }
    } catch {
      setSelectedTeacherProfile(null);
    } finally {
      setLoadingTeacherProfile(false);
    }
  };

  const openTeacherProfile = async (teacherId) => {
    const nextId = selectedTeacherId === teacherId ? null : teacherId;
    setSelectedTeacherId(nextId);
    if (!nextId) {
      setSelectedTeacherProfile(null);
      return;
    }
    await fetchTeacherProfile(nextId);
  };

  const upsertTeacherFieldSetting = async (payload) => {
    const res = await apiFetch('/api/teachers/profile-field-settings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    if (!res.ok) throw new Error(await res.text());
    return res.json();
  };

  const toggleSetting = async (setting, patch) => {
    setSavingFieldSettingKey(setting.fieldKey);
    try {
      await upsertTeacherFieldSetting({
        fieldKey: setting.fieldKey,
        displayName: setting.displayName,
        isCustom: setting.isCustom,
        isVisibleToTeacher: patch.isVisibleToTeacher ?? setting.isVisibleToTeacher,
        isEditableByTeacher: patch.isEditableByTeacher ?? setting.isEditableByTeacher,
        isAdminOnly: patch.isAdminOnly ?? setting.isAdminOnly,
        sortOrder: setting.sortOrder ?? 0,
      });
      await loadTeacherFieldSettings();
      if (selectedTeacherId) await fetchTeacherProfile(selectedTeacherId);
    } catch (e) {
      setError(e.message || 'Could not update field setting.');
    } finally {
      setSavingFieldSettingKey(null);
    }
  };

  const addCustomField = async () => {
    const key = (newCustomField.fieldKey || '').trim().toLowerCase().replace(/[^a-z0-9]/g, '');
    const name = (newCustomField.displayName || '').trim();
    if (!key || !name) {
      setError('Custom field key and display name are required.');
      return;
    }
    setSavingFieldSettingKey(key);
    try {
      await upsertTeacherFieldSetting({
        fieldKey: key,
        displayName: name,
        isCustom: true,
        isVisibleToTeacher: true,
        isEditableByTeacher: true,
        isAdminOnly: false,
        sortOrder: 600,
      });
      setNewCustomField({ displayName: '', fieldKey: '' });
      await loadTeacherFieldSettings();
      if (selectedTeacherId) await fetchTeacherProfile(selectedTeacherId);
    } catch (e) {
      setError(e.message || 'Could not add custom field.');
    } finally {
      setSavingFieldSettingKey(null);
    }
  };

  // Keep X-Tenant-Id in sync with the signed-in school (fixes teacher/parent share links if localStorage was cleared).
  useEffect(() => {
    const id = dashboard?.schoolId;
    if (!id || typeof localStorage === 'undefined') return;
    try {
      localStorage.setItem(STORAGE_TENANT_KEY, id);
    } catch {
      // ignore
    }
  }, [dashboard?.schoolId]);

  useEffect(() => {
    if (!onboardingSummary) return;

    // Hide after 24h
    const createdAt = onboardingSummary.createdAtUtc ? Date.parse(onboardingSummary.createdAtUtc) : NaN;
    const expired = Number.isFinite(createdAt) && (Date.now() - createdAt) > (24 * 60 * 60 * 1000);

    // Hide once first student exists (imported or added manually)
    const hasStudents = (dashboard?.studentCount ?? dashboard?.activeStudentCount ?? 0) > 0 || students.length > 0;

    if (expired || hasStudents) {
      try {
        localStorage.removeItem(STORAGE_ONBOARDING_KEY);
      } catch {
        // ignore
      }
      setOnboardingSummary(null);
    }
  }, [onboardingSummary, dashboard?.activeStudentCount, students.length]);

  const currentBilling = billing.length > 0 ? billing[0] : null;
  const outstanding = currentBilling ? Math.max(0, (currentBilling.amountDue || 0) - (currentBilling.amountPaid || 0)) : 0;
  const selectedTeacher = selectedTeacherProfile?.teacher || teachers.find((teacher) => teacher.id === selectedTeacherId) || null;
  const selectedTeacherClassIds = selectedTeacher
    ? Array.from(new Set([
      ...(selectedTeacher.teacherClasses || []).map((tc) => tc.classId),
      ...(selectedTeacher.teacherClassSubjects || []).map((tcs) => tcs.classId),
    ].filter(Boolean)))
    : [];
  const selectedTeacherStudentCount = selectedTeacherClassIds.length === 0
    ? 0
    : students.filter((s) => s.classId && selectedTeacherClassIds.includes(s.classId)).length;

  const assignTeacherToClass = async () => {
    if (!selectedTeacher?.id || !teacherAssignClassId || assigningTeacherClass) return;
    setAssigningTeacherClass(true);
    setError(null);
    try {
      const res = await apiFetch(`/api/teachers/${selectedTeacher.id}/classes/${teacherAssignClassId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ roleInClass: null }),
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not assign teacher to class.');

      setTeacherAssignClassId('');
      await loadData({ background: true });
      if (selectedTeacherId) await fetchTeacherProfile(selectedTeacherId);
    } catch (e) {
      setError(e.message || 'Could not assign teacher to class.');
    } finally {
      setAssigningTeacherClass(false);
    }
  };

  const unassignTeacherFromClass = async (classId) => {
    if (!selectedTeacher?.id || !classId || removingTeacherClassId === classId) return;
    setRemovingTeacherClassId(classId);
    setError(null);
    try {
      const res = await apiFetch(`/api/teachers/${selectedTeacher.id}/classes/${classId}`, {
        method: 'DELETE',
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not unassign teacher from class.');

      await loadData({ background: true });
      if (selectedTeacherId) await fetchTeacherProfile(selectedTeacherId);
    } catch (e) {
      setError(e.message || 'Could not unassign teacher from class.');
    } finally {
      setRemovingTeacherClassId(null);
    }
  };

  const handlePayWithPaystack = async () => {
    if (!currentBilling || outstanding <= 0 || paying) return;
    setPaying(true);
    try {
      const res = await apiFetch('/api/billing/initiate-payment', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ billingRecordId: currentBilling.id }),
      });
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      if (data.authorizationUrl) {
        window.location.assign(data.authorizationUrl);
      }
    } catch (e) {
      // eslint-disable-next-line no-alert
      alert(e.message || 'Could not start payment. Try again or contact support.');
    } finally {
      setPaying(false);
    }
  };

  const onSchoolFileChange = async (e) => {
    const file = e.target?.files?.[0];
    if (!file || uploadingAsset) return;
    setUploadingAsset(true);
    const form = new FormData();
    form.append('file', file);
    form.append('category', 'school-document');
    try {
      const res = await apiFetch('/api/files/upload', { method: 'POST', body: form });
      if (!res.ok) {
        // eslint-disable-next-line no-alert
        alert('Could not upload file. Please try again.');
      } else {
        // eslint-disable-next-line no-alert
        alert('File uploaded successfully.');
      }
    } catch {
      // eslint-disable-next-line no-alert
      alert('Could not upload file. Please try again.');
    } finally {
      setUploadingAsset(false);
      if (e.target) e.target.value = '';
    }
  };

  const onSchoolLogoChange = async (e) => {
    const file = e.target?.files?.[0];
    if (!file || uploadingLogo) return;

    setUploadingLogo(true);
    setLogoUploadError(null);

    const form = new FormData();
    form.append('file', file);

    try {
      const res = await apiFetch('/api/schools/logo', { method: 'POST', body: form });
      const text = await res.text().catch(() => '');

      if (!res.ok) {
        let message = text || 'Could not upload school logo.';
        try {
          const parsed = text ? JSON.parse(text) : null;
          message = parsed?.message || parsed?.title || parsed?.error || message;
        } catch {
          // Keep plain-text fallback for non-JSON responses.
        }
        throw new Error(message);
      }

      let logoPath = null;
      try {
        logoPath = text ? JSON.parse(text)?.logoFileName : null;
      } catch {
        logoPath = null;
      }

      if (logoPath) {
        setOnboardingSummary((current) => (current ? { ...current, logoPath } : current));
        setSchoolProfile((current) => ({ ...current, logoPath }));
      }

      await loadData({ background: true });
    } catch (err) {
      setLogoUploadError(err?.message || 'Could not upload school logo.');
    } finally {
      setUploadingLogo(false);
      if (e.target) e.target.value = '';
    }
  };

  const onSchoolProfileFieldChange = (field, value) => {
    setSchoolProfile((current) => ({ ...current, [field]: value }));
  };

  const saveSchoolProfile = async () => {
    if (savingSchoolProfile) return;

    setSavingSchoolProfile(true);
    setSchoolProfileError(null);
    try {
      const payload = {
        name: (schoolProfile.name || '').trim(),
        ownerName: schoolProfile.ownerName || null,
        schoolAdminName: schoolProfile.schoolAdminName || null,
        principalName: schoolProfile.principalName || null,
        address: schoolProfile.address || null,
        countryCode: schoolProfile.countryCode || null,
        email: schoolProfile.email || null,
        phone: schoolProfile.phone || null,
        whatsAppNumber: schoolProfile.whatsAppNumber || null,
        cacNumber: schoolProfile.cacNumber || null,
      };

      const res = await apiFetch('/api/schools/profile', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not update school profile.');

      const updated = text ? JSON.parse(text) : null;
      if (updated) {
        setSchoolProfile((current) => ({
          ...current,
          name: updated.name || current.name,
          ownerName: updated.ownerName || '',
          schoolAdminName: updated.schoolAdminName || '',
          principalName: updated.principalName || '',
          address: updated.address || '',
          countryCode: updated.countryCode || '',
          email: updated.email || '',
          phone: updated.phone || '',
          whatsAppNumber: updated.whatsAppNumber || '',
          cacNumber: updated.cacNumber || '',
          logoPath: updated.logoPath || current.logoPath,
          registrationDocumentPath: updated.registrationDocumentPath || current.registrationDocumentPath,
        }));
      }

      await loadData({ background: true });
    } catch (e) {
      setSchoolProfileError(e.message || 'Could not update school profile.');
    } finally {
      setSavingSchoolProfile(false);
    }
  };

  const onRegistrationDocumentChange = async (e) => {
    const file = e.target?.files?.[0];
    if (!file || uploadingRegistrationDoc) return;

    setUploadingRegistrationDoc(true);
    setSchoolProfileError(null);

    const form = new FormData();
    form.append('file', file);

    try {
      const res = await apiFetch('/api/schools/registration-document', { method: 'POST', body: form });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not upload registration document.');

      let docPath = null;
      try {
        docPath = text ? JSON.parse(text)?.registrationDocumentPath : null;
      } catch {
        docPath = null;
      }

      if (docPath) {
        setSchoolProfile((current) => ({ ...current, registrationDocumentPath: docPath }));
      }

      await loadData({ background: true });
    } catch (e1) {
      setSchoolProfileError(e1.message || 'Could not upload registration document.');
    } finally {
      setUploadingRegistrationDoc(false);
      if (e.target) e.target.value = '';
    }
  };

  const onPhotoFileChange = async (studentId, e) => {
    const file = e.target?.files?.[0];
    if (!file) return;
    setUploadingId(studentId);
    const form = new FormData();
    form.append('file', file);
    try {
      const res = await apiFetch(`/api/students/${studentId}/photo`, { method: 'POST', body: form });
      if (res.ok) loadData({ background: true });
    } finally {
      setUploadingId(null);
      e.target.value = '';
    }
  };

  const saveStudentClassAssignment = async (studentId, nextClassId) => {
    const res = await apiFetch(`/api/students/${studentId}/class-assignment`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ classId: nextClassId || null }),
    });
    const text = await res.text().catch(() => '');
    if (!res.ok) throw new Error(text || 'Could not assign class.');
  };

  const handleQuickAssignClass = async (studentId, nextClassId) => {
    if (!studentId || savingClassId === studentId) return;
    setSavingClassId(studentId);
    setError(null);
    try {
      await saveStudentClassAssignment(studentId, nextClassId);
      await loadData({ background: true });
    } catch (e) {
      setError(e.message || 'Failed to assign class.');
    } finally {
      setSavingClassId(null);
    }
  };

  const toggleStudentSelection = (studentId, isChecked) => {
    setSelectedStudentIds((current) => {
      if (isChecked) return current.includes(studentId) ? current : [...current, studentId];
      return current.filter((id) => id !== studentId);
    });
  };

  const visibleStudents = students.slice(0, 50);
  const visibleStudentIds = visibleStudents.map((student) => student.id);
  const allVisibleSelected = visibleStudentIds.length > 0 && visibleStudentIds.every((id) => selectedStudentIds.includes(id));

  const toggleSelectAllVisible = (isChecked) => {
    setSelectedStudentIds((current) => {
      if (isChecked) return Array.from(new Set([...current, ...visibleStudentIds]));
      return current.filter((id) => !visibleStudentIds.includes(id));
    });
  };

  const handleBulkAssignClass = async () => {
    if (!bulkClassId) {
      setError('Select a class first for bulk assignment.');
      return;
    }
    if (selectedStudentIds.length === 0) {
      setError('Select at least one student first.');
      return;
    }

    setBulkAssigning(true);
    setError(null);
    try {
      for (const studentId of selectedStudentIds) {
        await saveStudentClassAssignment(studentId, bulkClassId);
      }
      setSelectedStudentIds([]);
      setBulkClassId('');
      await loadData({ background: true });
    } catch (e) {
      setError(e.message || 'Failed to bulk assign class.');
    } finally {
      setBulkAssigning(false);
    }
  };

  if (loading && !dashboard) return <PageLayout title="School Admin" role="school"><p className="empty-state" aria-busy="true">Loading…</p></PageLayout>;
  if (!loading && error && !dashboard) return <PageLayout title="School Admin" role="school"><p className="empty-state empty-state--error">{error}</p></PageLayout>;

  const currencyCode = dashboard?.currencyCode || 'NGN';
  const activeStudents = dashboard?.studentCount ?? dashboard?.activeStudentCount ?? students.length;
  const unpaidFees = dashboard?.unpaidFeesTotal ?? 0;
  const buildPublicUrl = (relativePath) => {
    if (!relativePath) return null;
    if (relativePath.startsWith('http://') || relativePath.startsWith('https://')) return relativePath;
    const normalizedPath = relativePath.replace(/^\/+/, '');
    const base = getApiBase();
    if (!base) return `/${normalizedPath}`;
    return `${base}/${normalizedPath}`;
  };
  const dismissOnboardingSummary = () => {
    setOnboardingSummary(null);
    try {
      localStorage.removeItem(STORAGE_ONBOARDING_KEY);
    } catch {
      // ignore
    }
  };

  return (
    <PageLayout title="School Admin" role="school">
      <div className="school-admin-shell">
        <aside className="school-admin-nav">
          <button type="button" className={`school-admin-nav-btn ${activeView === 'overview' ? 'is-active' : ''}`} onClick={() => setActiveView('overview')}>
            Overview
          </button>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'people' ? 'is-active' : ''}`} onClick={() => setActiveView('people')}>
            People
          </button>
          <Link to="/school/classes" className="school-admin-nav-btn school-admin-nav-link">
            Grades &amp; classes
          </Link>
          <Link to="/school/grading-systems" className="school-admin-nav-btn school-admin-nav-link">
            Grading systems
          </Link>
          <button type="button" className={`school-admin-nav-btn ${activeView === 'operations' ? 'is-active' : ''}`} onClick={() => setActiveView('operations')}>
            Operations
          </button>
          <Link to="/school/promotions" className="school-admin-nav-btn school-admin-nav-link">
            Promotions
          </Link>
          <Link to="/school/timetable" className="school-admin-nav-btn school-admin-nav-link">
            Timetable
          </Link>
          <Link to="/school/communications" className="school-admin-nav-btn school-admin-nav-link">
            Notices &amp; events
          </Link>
        </aside>

        <section className="school-admin-view">
      {error && (
        <p className="empty-state empty-state--error" style={{ marginBottom: '1rem' }}>{error}</p>
      )}
      {refreshing && !error && (
        <p className="card-desc" aria-live="polite" style={{ marginBottom: '0.75rem' }}>Refreshing school data…</p>
      )}
      {onboardingSummary?.schoolName && (
        <section className="school-welcome-panel" aria-label="School setup complete">
          <button type="button" className="school-welcome-close" onClick={dismissOnboardingSummary} aria-label="Dismiss welcome panel">×</button>
          <h2 className="school-welcome-title">Congratulations, {onboardingSummary.schoolName} is now live!</h2>
          <p className="school-welcome-sub">Your setup is complete. Welcome to RiseFlow.</p>

          <div className="school-id-box">
            <span className="school-id-label">RiseFlow ID</span>
            <strong className="school-id-value">{onboardingSummary.schoolId || 'Generated'}</strong>
          </div>

          {onboardingSummary.logoPath && (
            <div className="logo-preview-box">
              <span className="school-id-label">School Logo Preview</span>
              <a
                href={buildPublicUrl(onboardingSummary.logoPath)}
                target="_blank"
                rel="noopener noreferrer"
                className="logo-preview-link"
              >
                <img
                  src={buildPublicUrl(onboardingSummary.logoPath)}
                  alt={`${onboardingSummary.schoolName} logo`}
                  className="logo-preview-image"
                  loading="lazy"
                />
              </a>
            </div>
          )}

          {(onboardingSummary.logoPath || onboardingSummary.cacDocumentPath) && (
            <div className="school-files-box">
              <span className="school-id-label">Uploaded Files</span>
              <div className="school-files-list">
                {onboardingSummary.logoPath && (
                  <a href={buildPublicUrl(onboardingSummary.logoPath)} target="_blank" rel="noopener noreferrer">View School Logo</a>
                )}
                {onboardingSummary.cacDocumentPath && (
                  <a href={buildPublicUrl(onboardingSummary.cacDocumentPath)} target="_blank" rel="noopener noreferrer">View CAC Document</a>
                )}
              </div>
            </div>
          )}

          <div className="success-actions">
            <Link to="/school/import" className="action-card">
              <h3>Import Students</h3>
              <p>Upload your student list to go live faster.</p>
            </Link>
            <Link to="/teacher/signup" className="action-card">
              <h3>Add Teachers</h3>
              <p>Create teacher accounts and assign classes.</p>
            </Link>
            <Link to="/school/classes" className="action-card">
              <h3>Set up grades &amp; classes</h3>
              <p>Nursery, Primary, JSS, SS — add the levels and classes your school uses.</p>
            </Link>
          </div>

          <div className="next-checklist">
            <p className="next-checklist-title">Next Steps</p>
            <ul>
              <li><a href={`${getApiBase()}/api/public/teacher-quick-start`} target="_blank" rel="noopener noreferrer">Download the Teacher Guide</a></li>
              <li><Link to="/school/classes">Add your first grade &amp; class</Link></li>
              <li><Link to="/school/access-codes">Print Parent Access Codes</Link></li>
            </ul>
          </div>
        </section>
      )}

      {activeView === 'overview' && (
        <>
      <div className="dashboard-actions" style={{ flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Link to="/school/students" className="btn-primary-action">Students</Link>
        <Link to="/school/classes" className="btn-primary-action btn-primary-action--ghost">Grades & classes</Link>
        <Link to="/school/grading-systems" className="btn-primary-action btn-primary-action--ghost">Grading systems</Link>
        <Link to="/school/billing" className="btn-primary-action btn-primary-action--ghost">Billing</Link>
        <Link to="/school/reports" className="btn-primary-action btn-primary-action--ghost">Reports</Link>
        <Link to="/school/promotions" className="btn-primary-action btn-primary-action--ghost">Promotions</Link>
        <Link to="/school/timetable" className="btn-primary-action btn-primary-action--ghost">Timetable</Link>
        <Link to="/school/communications" className="btn-primary-action btn-primary-action--ghost">Notices & events</Link>
        <Link to="/school/import" className="btn-primary-action btn-primary-action--ghost">Import</Link>
        <Link to="/school/access-codes" className="btn-primary-action btn-primary-action--ghost">Access codes</Link>
      </div>
      {outstanding > 0 && (
        <div className="access-codes-result access-codes-result--error" style={{ marginBottom: '1rem' }}>
          <p style={{ margin: 0 }}>
            You have an outstanding balance of <strong>{formatMoney(outstanding, currencyCode)}</strong> for {currentBilling?.periodLabel || 'this period'}.
          </p>
          <button
            type="button"
            className="btn-excel btn-generate"
            style={{ marginTop: '0.5rem' }}
            onClick={handlePayWithPaystack}
            disabled={paying}
          >
            {paying ? 'Redirecting…' : 'Pay with Paystack'}
          </button>
        </div>
      )}
      <h2 className="section-title">Dashboard (from database)</h2>
      {dashboard && (
        <div className="summary-cards">
          <div className="summary-card">
            <span className="summary-value">{activeStudents}</span>
            <span className="summary-label">Active students</span>
          </div>
          <div className="summary-card">
            <span className="summary-value">{teachers.length}</span>
            <span className="summary-label">Teachers</span>
          </div>
          <div className="summary-card">
            <span className="summary-value">{parents.length}</span>
            <span className="summary-label">Parents</span>
          </div>
          <div className="summary-card summary-card--warning">
            <span className="summary-value">{formatMoney(unpaidFees, currencyCode)}</span>
            <span className="summary-label">Unpaid fees</span>
          </div>
        </div>
      )}
        </>
      )}

      {activeView === 'people' && (
        <>
      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Teachers</h2>
      {teachers.length === 0 ? (
        <p className="empty-state">No teachers yet.</p>
      ) : (
        <>
          <p className="card-desc" style={{ marginBottom: '0.75rem' }}>
            Teacher contact details stay hidden here until you click <strong>View details</strong>.
          </p>
          <div className="data-table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Role</th>
                  <th>Department</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {teachers.map((t) => (
                  <tr key={t.id}>
                    <td>{[t.firstName, t.middleName, t.lastName].filter(Boolean).join(' ')}</td>
                    <td>{t.roleTitle || 'Teacher'}</td>
                    <td>{t.department || t.subjectSpecialization || '—'}</td>
                    <td>{t.isActive ? 'Active' : 'Inactive'}</td>
                    <td>
                      <button
                        type="button"
                        className="btn-primary-action btn-primary-action--ghost"
                        onClick={() => openTeacherProfile(t.id)}
                      >
                        {selectedTeacherId === t.id ? 'Hide details' : 'View details'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {loadingTeacherProfile && (
            <p className="card-desc" style={{ marginTop: '0.75rem' }}>Loading selected teacher details…</p>
          )}

          <section className="dashboard-panel" style={{ marginTop: '1rem' }} aria-label="Teacher profile governance">
            <h3 className="card-title">Teacher field controls</h3>
            <p className="card-desc">Control what teachers can see or edit. Salary, allowances and recognitions can be locked here. You can also add custom fields.</p>
            <div className="form-grid" style={{ marginTop: '0.75rem' }}>
              <label className="form-field">Custom field label
                <input
                  className="form-input"
                  value={newCustomField.displayName}
                  onChange={(e) => setNewCustomField((prev) => ({ ...prev, displayName: e.target.value }))}
                  placeholder="e.g. Teaching License Expiry"
                />
              </label>
              <label className="form-field">Custom field key
                <input
                  className="form-input"
                  value={newCustomField.fieldKey}
                  onChange={(e) => setNewCustomField((prev) => ({ ...prev, fieldKey: e.target.value }))}
                  placeholder="e.g. teachinglicenseexpiry"
                />
              </label>
              <div className="form-actions" style={{ alignSelf: 'end' }}>
                <button type="button" className="btn-primary-action" onClick={addCustomField} disabled={!!savingFieldSettingKey}>
                  Add custom field
                </button>
              </div>
            </div>

            <div className="data-table-wrap" style={{ marginTop: '0.75rem' }}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Field</th>
                    <th>Visible to teacher</th>
                    <th>Editable by teacher</th>
                    <th>Admin only</th>
                  </tr>
                </thead>
                <tbody>
                  {teacherFieldSettings.map((setting) => (
                    <tr key={setting.fieldKey}>
                      <td>{setting.displayName}{setting.isCustom ? ' (custom)' : ''}</td>
                      <td>
                        <input
                          type="checkbox"
                          checked={!!setting.isVisibleToTeacher}
                          disabled={savingFieldSettingKey === setting.fieldKey}
                          onChange={(e) => toggleSetting(setting, { isVisibleToTeacher: e.target.checked })}
                        />
                      </td>
                      <td>
                        <input
                          type="checkbox"
                          checked={!!setting.isEditableByTeacher}
                          disabled={savingFieldSettingKey === setting.fieldKey || !!setting.isAdminOnly}
                          onChange={(e) => toggleSetting(setting, { isEditableByTeacher: e.target.checked })}
                        />
                      </td>
                      <td>
                        <input
                          type="checkbox"
                          checked={!!setting.isAdminOnly}
                          disabled={savingFieldSettingKey === setting.fieldKey}
                          onChange={(e) => toggleSetting(setting, { isAdminOnly: e.target.checked, isEditableByTeacher: e.target.checked ? false : setting.isEditableByTeacher })}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {selectedTeacher && (
            <section className="dashboard-panel" style={{ marginTop: '1rem' }} aria-label="Teacher details">
              <h3 className="card-title">Teacher details</h3>
              <p className="card-desc">Sensitive teacher details are only shown after explicit review.</p>
              <div className="dashboard-grid" style={{ marginTop: '0.75rem' }}>
                <article className="dashboard-card"><p className="dashboard-label">Name</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{[selectedTeacher.firstName, selectedTeacher.middleName, selectedTeacher.lastName].filter(Boolean).join(' ')}</p><p className="dashboard-sub">Staff ID: {selectedTeacher.staffId || '—'}</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Contact</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedTeacher.email || '—'}</p><p className="dashboard-sub">Phone: {selectedTeacher.phone || '—'} • WhatsApp: {selectedTeacher.whatsAppNumber || selectedTeacher.phone || '—'}</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Role</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedTeacher.roleTitle || 'Teacher'}</p><p className="dashboard-sub">Department: {selectedTeacher.department || '—'}</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Professional summary</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedTeacher.highestQualification || selectedTeacher.subjectSpecialization || '—'}</p><p className="dashboard-sub">Experience: {selectedTeacher.yearsOfExperience ?? '—'} years</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Workload</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedTeacherClassIds.length} class(es)</p><p className="dashboard-sub">Handling {selectedTeacherStudentCount} student(s)</p></article>
                <article className="dashboard-card" style={{ gridColumn: '1 / -1' }}>
                  <p className="dashboard-label">Class assignment</p>
                  <div className="form-actions" style={{ marginTop: '0.5rem', flexWrap: 'wrap' }}>
                    <select
                      className="form-input"
                      style={{ minWidth: '220px' }}
                      value={teacherAssignClassId}
                      onChange={(e) => setTeacherAssignClassId(e.target.value)}
                      disabled={assigningTeacherClass || classes.length === 0}
                    >
                      <option value="">— Assign teacher to class —</option>
                      {classes
                        .filter((schoolClass) => !selectedTeacherClassIds.includes(schoolClass.id))
                        .map((schoolClass) => (
                          <option key={schoolClass.id} value={schoolClass.id}>
                            {schoolClass.name}{schoolClass.gradeName ? ` (${schoolClass.gradeName})` : ''}
                          </option>
                        ))}
                    </select>
                    <button
                      type="button"
                      className="btn-primary-action"
                      onClick={assignTeacherToClass}
                      disabled={assigningTeacherClass || !teacherAssignClassId}
                    >
                      {assigningTeacherClass ? 'Assigning…' : 'Assign class'}
                    </button>
                  </div>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginTop: '0.75rem' }}>
                    {selectedTeacherClassIds.length === 0 && (
                      <span className="card-desc">No class assigned yet.</span>
                    )}
                    {selectedTeacherClassIds.map((classId) => {
                      const assignedClass = classes.find((item) => item.id === classId);
                      const label = assignedClass
                        ? `${assignedClass.name}${assignedClass.gradeName ? ` (${assignedClass.gradeName})` : ''}`
                        : classId;
                      return (
                        <button
                          key={classId}
                          type="button"
                          className="btn-primary-action btn-primary-action--ghost"
                          onClick={() => unassignTeacherFromClass(classId)}
                          disabled={removingTeacherClassId === classId}
                          title="Remove teacher from this class"
                        >
                          {removingTeacherClassId === classId ? 'Removing…' : `Remove ${label}`}
                        </button>
                      );
                    })}
                  </div>
                </article>
                <article className="dashboard-card"><p className="dashboard-label">Location / identity</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedTeacher.nationality || '—'}</p><p className="dashboard-sub">State: {selectedTeacher.stateOfOrigin || '—'} • LGA: {selectedTeacher.lga || '—'}</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Personal</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>Religion: {selectedTeacher.religion || '—'}</p><p className="dashboard-sub">Gender: {selectedTeacher.gender || '—'} • DOB: {selectedTeacher.dateOfBirth || '—'}</p></article>
                <article className="dashboard-card"><p className="dashboard-label">Address</p><p className="dashboard-value" style={{ fontSize: '1rem' }}>{selectedTeacher.residentialAddress || '—'}</p><p className="dashboard-sub">Prev. schools: {selectedTeacher.previousSchools || '—'}</p></article>
                {selectedTeacherProfile?.customFields && Object.keys(selectedTeacherProfile.customFields).length > 0 && (
                  <article className="dashboard-card" style={{ gridColumn: '1 / -1' }}>
                    <p className="dashboard-label">Custom profile fields</p>
                    {Object.entries(selectedTeacherProfile.customFields).map(([key, value]) => {
                      const setting = teacherFieldSettings.find((s) => s.fieldKey === key);
                      return (
                        <p key={key} className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
                          <strong>{setting?.displayName || key}:</strong> {value || '—'}
                        </p>
                      );
                    })}
                  </article>
                )}
              </div>
            </section>
          )}
        </>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Parents</h2>
      {parents.length === 0 ? (
        <p className="empty-state">No parents have signed up yet. Share your parent signup link and access codes so families can join.</p>
      ) : (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Phone</th>
              </tr>
            </thead>
            <tbody>
              {parents.map((p) => (
                <tr key={p.id}>
                  <td>{[p.firstName, p.middleName, p.lastName].filter(Boolean).join(' ')}</td>
                  <td>{p.email || '—'}</td>
                  <td>{p.phone || p.whatsAppNumber || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Students</h2>
      <p className="card-desc">Register students one at a time, assign them to classes quickly, or bulk upload many from Excel.</p>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', marginTop: '0.5rem', marginBottom: '0.75rem' }}>
        <Link to="/school/students/add" className="btn-excel btn-download" style={{ display: 'inline-flex' }}>
          Add one student
        </Link>
        <Link to="/school/import" className="btn-excel btn-download" style={{ display: 'inline-flex', background: 'var(--color-neutral-border)', color: 'var(--color-neutral-text)' }}>
          Bulk upload (Excel)
        </Link>
        <Link to="/school/students" className="btn-excel btn-download" style={{ display: 'inline-flex', background: 'var(--color-neutral-bg)', color: 'var(--color-primary)' }}>
          Open full students manager
        </Link>
      </div>

      {students.length > 0 && classes.length > 0 && (
        <div className="form-actions" style={{ marginBottom: '0.75rem', flexWrap: 'wrap' }}>
          <span className="card-desc"><strong>{selectedStudentIds.length}</strong> selected</span>
          <select
            className="form-input"
            style={{ minWidth: '220px' }}
            value={bulkClassId}
            onChange={(e) => setBulkClassId(e.target.value)}
            disabled={bulkAssigning}
          >
            <option value="">— Bulk assign to class —</option>
            {classes.map((schoolClass) => (
              <option key={schoolClass.id} value={schoolClass.id}>
                {schoolClass.name}{schoolClass.gradeName ? ` (${schoolClass.gradeName})` : ''}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="btn-primary-action"
            onClick={handleBulkAssignClass}
            disabled={bulkAssigning || !bulkClassId || selectedStudentIds.length === 0}
          >
            {bulkAssigning ? 'Assigning…' : `Assign selected (${selectedStudentIds.length})`}
          </button>
          <button
            type="button"
            className="btn-primary-action btn-primary-action--ghost"
            onClick={() => toggleSelectAllVisible(!allVisibleSelected)}
            disabled={bulkAssigning}
          >
            {allVisibleSelected ? 'Clear selection' : 'Select all shown'}
          </button>
        </div>
      )}

      {students.length > 0 && classes.length === 0 && (
        <p className="empty-state">Create at least one class in <strong>Grades &amp; classes</strong> to enable quick and bulk student assignment.</p>
      )}

      {students.length === 0 ? (
        <p className="empty-state">No students yet. Add one student or bulk upload from Excel.</p>
      ) : (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th style={{ width: '36px' }}>
                  <input
                    type="checkbox"
                    checked={allVisibleSelected}
                    onChange={(e) => toggleSelectAllVisible(e.target.checked)}
                    aria-label="Select all visible students"
                    disabled={bulkAssigning}
                  />
                </th>
                <th style={{ width: '56px' }}>Photo</th>
                <th>Name</th>
                <th>Admission #</th>
                <th>Class</th>
                <th>Quick assign class</th>
                <th>Photo</th>
              </tr>
            </thead>
            <tbody>
              {visibleStudents.map((s) => (
                <tr key={s.id}>
                  <td>
                    <input
                      type="checkbox"
                      checked={selectedStudentIds.includes(s.id)}
                      onChange={(e) => toggleStudentSelection(s.id, e.target.checked)}
                      aria-label={`Select ${[s.firstName, s.lastName].filter(Boolean).join(' ')}`}
                      disabled={bulkAssigning}
                    />
                  </td>
                  <td>
                    <StudentPhoto studentId={s.id} firstName={s.firstName} lastName={s.lastName} size={40} />
                  </td>
                  <td>{[s.firstName, s.middleName, s.lastName].filter(Boolean).join(' ')}</td>
                  <td>{s.admissionNumber || '—'}</td>
                  <td>{s.class?.name || '—'}</td>
                  <td>
                    <select
                      className="form-input"
                      style={{ minWidth: '180px' }}
                      value={s.class?.id || ''}
                      onChange={(e) => handleQuickAssignClass(s.id, e.target.value)}
                      disabled={savingClassId === s.id || bulkAssigning || classes.length === 0}
                    >
                      <option value="">— No class —</option>
                      {classes.map((schoolClass) => (
                        <option key={schoolClass.id} value={schoolClass.id}>
                          {schoolClass.name}{schoolClass.gradeName ? ` (${schoolClass.gradeName})` : ''}
                        </option>
                      ))}
                    </select>
                    {savingClassId === s.id && <span className="form-hint">Saving…</span>}
                  </td>
                  <td>
                    <input
                      type="file"
                      accept=".jpg,.jpeg,.png,.gif,.webp"
                      ref={(el) => { fileInputRefs.current[s.id] = el; }}
                      onChange={(e) => onPhotoFileChange(s.id, e)}
                      style={{ display: 'none' }}
                      aria-label={`Upload photo for ${[s.firstName, s.lastName].filter(Boolean).join(' ')}`}
                    />
                    <button
                      type="button"
                      className="btn-upload-photo"
                      onClick={() => fileInputRefs.current[s.id]?.click()}
                      disabled={uploadingId === s.id}
                    >
                      {uploadingId === s.id ? '…' : 'Upload photo'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {students.length > 50 && <p className="card-desc" style={{ marginTop: '0.5rem' }}>Showing first 50 of {students.length} students.</p>}
        </div>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Parent Access Codes</h2>
      <p className="card-desc">Generate unique codes (e.g. RF-8821) for each student. Give the code to the parent so they can claim their child in the app or web.</p>
      <Link to="/school/access-codes" className="btn-excel btn-download" style={{ display: 'inline-flex', marginTop: '0.5rem' }}>
        Manage access codes
      </Link>

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Share with teachers</h2>
      <p className="card-desc">Share this link with teachers so they can sign up directly under your school.</p>
      <TeacherSignupLink schoolIdFromApi={dashboard?.schoolId} />

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Share with parents</h2>
      <p className="card-desc">Parents create an account and link to their child using the access code you provide.</p>
      <ParentSignupLink schoolIdFromApi={dashboard?.schoolId} />
        </>
      )}

      {activeView === 'operations' && (
        <>
      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Grades &amp; classes</h2>
      <p className="card-desc">Create your school&apos;s programme levels (Nursery, Primary 1–6, JSS, SS1–SS3, etc.) and classes for each level.</p>
      <Link to="/school/classes" className="btn-excel btn-download" style={{ display: 'inline-flex', marginTop: '0.5rem' }}>
        Open grades &amp; classes setup
      </Link>

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>School files &amp; documents</h2>
      <p className="card-desc">
        Upload photos or documents (e.g. letterhead, logo variations) so they are stored safely in your RiseFlow account.
      </p>
      <section className="dashboard-panel" style={{ marginTop: '0.75rem' }} aria-label="School profile information">
        <h3 className="card-title">School information</h3>
        <p className="card-desc">Update your school profile, contacts, compliance details, and leadership names shown to Super Admin.</p>
        <div className="form-grid" style={{ marginTop: '0.75rem' }}>
          <label className="form-field">School name
            <input className="form-input" value={schoolProfile.name} onChange={(e) => onSchoolProfileFieldChange('name', e.target.value)} placeholder="School name" />
          </label>
          <label className="form-field">Owner name
            <input className="form-input" value={schoolProfile.ownerName} onChange={(e) => onSchoolProfileFieldChange('ownerName', e.target.value)} placeholder="School owner name" />
          </label>
          <label className="form-field">School admin name
            <input className="form-input" value={schoolProfile.schoolAdminName} onChange={(e) => onSchoolProfileFieldChange('schoolAdminName', e.target.value)} placeholder="School admin full name" />
          </label>
          <label className="form-field">Principal name
            <input className="form-input" value={schoolProfile.principalName} onChange={(e) => onSchoolProfileFieldChange('principalName', e.target.value)} placeholder="Principal name" />
          </label>
          <label className="form-field">School email
            <input className="form-input" type="email" value={schoolProfile.email} onChange={(e) => onSchoolProfileFieldChange('email', e.target.value)} placeholder="school@example.com" />
          </label>
          <label className="form-field">Phone
            <input className="form-input" value={schoolProfile.phone} onChange={(e) => onSchoolProfileFieldChange('phone', e.target.value)} placeholder="+234..." />
          </label>
          <label className="form-field">WhatsApp number
            <input className="form-input" value={schoolProfile.whatsAppNumber} onChange={(e) => onSchoolProfileFieldChange('whatsAppNumber', e.target.value)} placeholder="+234..." />
          </label>
          <label className="form-field">Country code (ISO2)
            <input className="form-input" value={schoolProfile.countryCode} onChange={(e) => onSchoolProfileFieldChange('countryCode', e.target.value.toUpperCase())} maxLength={2} placeholder="NG" />
          </label>
          <label className="form-field">CAC / registration number
            <input className="form-input" value={schoolProfile.cacNumber} onChange={(e) => onSchoolProfileFieldChange('cacNumber', e.target.value)} placeholder="RC1234567" />
          </label>
          <label className="form-field form-field--full">School address
            <textarea className="form-input" rows={3} value={schoolProfile.address} onChange={(e) => onSchoolProfileFieldChange('address', e.target.value)} placeholder="Street, city, state, country" />
          </label>
        </div>
        <div className="form-actions" style={{ marginTop: '0.75rem' }}>
          <button type="button" className="btn-primary-action" onClick={saveSchoolProfile} disabled={savingSchoolProfile}>
            {savingSchoolProfile ? 'Saving profile…' : 'Save school information'}
          </button>
        </div>
      </section>

      <input
        type="file"
        ref={schoolLogoInputRef}
        style={{ display: 'none' }}
        onChange={onSchoolLogoChange}
        accept=".jpg,.jpeg,.png,.gif,.webp"
      />
      <button
        type="button"
        className="btn-excel btn-download"
        style={{ display: 'inline-flex', marginTop: '0.5rem', marginRight: '0.5rem' }}
        onClick={() => schoolLogoInputRef.current?.click()}
        disabled={uploadingLogo}
      >
        {uploadingLogo ? 'Updating logo…' : 'Update school logo'}
      </button>
      <input
        type="file"
        ref={registrationDocInputRef}
        style={{ display: 'none' }}
        onChange={onRegistrationDocumentChange}
        accept=".pdf,.png,.jpg,.jpeg,.webp"
      />
      <button
        type="button"
        className="btn-excel btn-download"
        style={{ display: 'inline-flex', marginTop: '0.5rem', marginRight: '0.5rem' }}
        onClick={() => registrationDocInputRef.current?.click()}
        disabled={uploadingRegistrationDoc}
      >
        {uploadingRegistrationDoc ? 'Uploading registration doc…' : 'Update registration document'}
      </button>
      <input
        type="file"
        ref={schoolFileInputRef}
        style={{ display: 'none' }}
        onChange={onSchoolFileChange}
        accept=".jpg,.jpeg,.png,.gif,.webp,.pdf,.doc,.docx"
      />
      <button
        type="button"
        className="btn-excel btn-download"
        style={{ display: 'inline-flex', marginTop: '0.5rem' }}
        onClick={() => schoolFileInputRef.current?.click()}
        disabled={uploadingAsset}
      >
        {uploadingAsset ? 'Uploading…' : 'Upload a file'}
      </button>
      {logoUploadError && <p className="empty-state empty-state--error" style={{ marginTop: '0.75rem' }}>{logoUploadError}</p>}
      {schoolProfileError && <p className="empty-state empty-state--error" style={{ marginTop: '0.75rem' }}>{schoolProfileError}</p>}
      {(schoolProfile.logoPath || schoolProfile.registrationDocumentPath) && (
        <p className="card-desc" style={{ marginTop: '0.75rem' }}>
          {schoolProfile.logoPath && <a href={buildPublicUrl(schoolProfile.logoPath)} target="_blank" rel="noopener noreferrer">View current logo</a>}
          {schoolProfile.logoPath && schoolProfile.registrationDocumentPath ? ' • ' : ''}
          {schoolProfile.registrationDocumentPath && <a href={buildPublicUrl(schoolProfile.registrationDocumentPath)} target="_blank" rel="noopener noreferrer">View registration document</a>}
        </p>
      )}

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Bulk upload</h2>
      <p className="card-desc">Import students from Excel with preview and validation. First 50 students free after you register your school.</p>
      <Link to="/school/import" className="btn-excel btn-download" style={{ display: 'inline-flex', marginTop: '0.5rem' }}>
        Open Excel import
      </Link>

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Billing records (from database)</h2>
      {billing.length === 0 ? (
        <p className="empty-state">No billing records yet.</p>
      ) : (
        <div className="data-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Period</th>
                <th>Amount due</th>
                <th>Amount paid</th>
              </tr>
            </thead>
            <tbody>
              {billing.map((b) => (
                <tr key={b.id}>
                  <td>{b.periodLabel || '—'}</td>
                  <td>{formatMoney(b.amountDue, b.currencyCode)}</td>
                  <td>{b.amountPaid != null ? formatMoney(b.amountPaid, b.currencyCode) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
        </>
      )}
        </section>
      </div>
    </PageLayout>
  );
}

function resolveSchoolId(schoolIdFromApi) {
  if (schoolIdFromApi) return schoolIdFromApi;
  try {
    return typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_TENANT_KEY) : null;
  } catch {
    return null;
  }
}

function TeacherSignupLink({ schoolIdFromApi }) {
  const schoolId = resolveSchoolId(schoolIdFromApi);
  const teacherSignupUrl = schoolId ? `${typeof window !== 'undefined' ? window.location.origin : ''}/teacher/signup?school=${encodeURIComponent(schoolId)}` : '';

  const copyTeacherSignup = () => {
    if (teacherSignupUrl) navigator.clipboard.writeText(teacherSignupUrl);
  };

  if (!teacherSignupUrl) {
    return <p className="empty-state">Loading your school link… If this persists, sign out and sign in again as School Admin.</p>;
  }

  return (
    <div className="parent-signup-link-box" style={{ marginTop: '0.5rem' }}>
      <code className="parent-signup-url">{teacherSignupUrl}</code>
      <button type="button" className="btn-copy" onClick={copyTeacherSignup} title="Copy teacher signup link">
        Copy link
      </button>
    </div>
  );
}

function ParentSignupLink({ schoolIdFromApi }) {
  const schoolId = resolveSchoolId(schoolIdFromApi);
  const parentSignupUrl = schoolId ? `${typeof window !== 'undefined' ? window.location.origin : ''}/parent/signup?school=${encodeURIComponent(schoolId)}` : '';

  const copyParentSignup = () => {
    if (parentSignupUrl) navigator.clipboard.writeText(parentSignupUrl);
  };

  if (!parentSignupUrl) {
    return <p className="empty-state">Loading your school link… If this persists, sign out and sign in again as School Admin.</p>;
  }

  return (
    <div className="parent-signup-link-box" style={{ marginTop: '0.5rem' }}>
      <code className="parent-signup-url">{parentSignupUrl}</code>
      <button type="button" className="btn-copy" onClick={copyParentSignup} title="Copy parent signup link">
        Copy link
      </button>
    </div>
  );
}

