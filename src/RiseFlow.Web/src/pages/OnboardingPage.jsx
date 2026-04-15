import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import './OnboardingPage.css';
import { apiFetch, getApiBase, STORAGE_ONBOARDING_KEY, STORAGE_TENANT_KEY } from '../api';

export default function OnboardingPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const referralCode = searchParams.get('ref')?.trim() || '';
  const fallbackSchoolModels = useMemo(() => ([
    {
      modelCode: 'PUBLIC',
      modelName: 'Government (Public) School',
      curriculumApproach: 'Strict adherence to national curriculum and exam policy.',
      resourceProfile: 'Often constrained by larger class sizes and tighter infrastructure budgets.',
      languageApproach: 'Official/national language with mother-tongue support in early years where applicable.',
      costProfile: 'Low or subsidized tuition.',
    },
    {
      modelCode: 'PRIVATE',
      modelName: 'Private School',
      curriculumApproach: 'National curriculum plus optional international blend (British/IGCSE or American).',
      resourceProfile: 'Typically stronger facilities and extracurricular offerings.',
      languageApproach: 'Usually English or French from early nursery years.',
      costProfile: 'Higher tuition supporting facilities and staffing.',
    },
  ]), []);

  const fallbackCountryOptions = useMemo(() => ([
    {
      countryCode: 'NG',
      countryName: 'Nigeria',
      currencyCode: 'NGN',
      regionalSystem: 'Anglophone',
      systemStructure: '6-3-3-4',
      prePrimaryStages: [
        { levelName: 'Creche / Daycare', ageRange: '3 months - 2 years', typicalFocus: 'Basic childcare and social play.' },
        { levelName: 'Pre-Nursery / Playgroup', ageRange: '2 - 3 years', typicalFocus: 'Intro to social interaction and basic motor skills.' },
        { levelName: 'Nursery 1 / KG 1', ageRange: '3 - 4 years', typicalFocus: 'Pre-literacy and pre-numeracy foundations.' },
        { levelName: 'Nursery 2 / Reception', ageRange: '4 - 5 years', typicalFocus: 'Preparation for Primary 1 with basic reading and writing.' },
      ],
      defaultClassLevels: ['Creche / Daycare', 'Pre-Nursery / Playgroup', 'Nursery 1', 'Nursery 2', 'Primary 1', 'Primary 2', 'Primary 3', 'Primary 4', 'Primary 5', 'Primary 6', 'JSS 1', 'JSS 2', 'JSS 3', 'SS 1', 'SS 2', 'SS 3'],
      defaultSubjects: ['English Language', 'Mathematics', 'Basic Science', 'Social Studies', 'Civic Education', 'Computer Studies', 'Agricultural Science', 'Business Studies', 'Literature in English', 'Economics'],
      primarySubjectSamples: ['English Language', 'Mathematics', 'Basic Science', 'Social Studies', 'Physical and Health Education', 'Local Language'],
      juniorSubjectSamples: ['English Language', 'Mathematics', 'Integrated Science', 'ICT', 'Business Studies', 'Home Economics', 'Agricultural Science'],
      seniorSubjectSamples: ['English Language', 'Mathematics', 'Physics', 'Chemistry', 'Biology', 'Government', 'Literature', 'Economics', 'Accounting'],
      notes: 'Senior secondary usually branches into Sciences, Arts, and Commercial tracks.',
    },
    {
      countryCode: 'GH',
      countryName: 'Ghana',
      currencyCode: 'GHS',
      regionalSystem: 'Anglophone',
      systemStructure: '6-3-3',
      prePrimaryStages: [
        { levelName: 'Creche / Daycare', ageRange: '3 months - 2 years', typicalFocus: 'Basic childcare and social play.' },
        { levelName: 'Playgroup', ageRange: '2 - 3 years', typicalFocus: 'Language and social interaction development.' },
        { levelName: 'KG 1', ageRange: '3 - 4 years', typicalFocus: 'Early literacy and numeracy foundations.' },
        { levelName: 'KG 2', ageRange: '4 - 5 years', typicalFocus: 'Preparation for Primary 1 and classroom routines.' },
      ],
      defaultClassLevels: ['Creche / Daycare', 'Playgroup', 'KG 1', 'KG 2', 'Primary 1', 'Primary 2', 'Primary 3', 'Primary 4', 'Primary 5', 'Primary 6', 'JHS 1', 'JHS 2', 'JHS 3', 'SHS 1', 'SHS 2', 'SHS 3'],
      defaultSubjects: ['English Language', 'Mathematics', 'Integrated Science', 'Social Studies', 'Creative Arts', 'Religious and Moral Education', 'Computing', 'Career Technology', 'Economics', 'Literature'],
      primarySubjectSamples: ['English Language', 'Mathematics', 'Integrated Science', 'Social Studies', 'Creative Arts', 'Ghanaian Language'],
      juniorSubjectSamples: ['English Language', 'Mathematics', 'Integrated Science', 'Computing', 'Career Technology', 'Social Studies'],
      seniorSubjectSamples: ['English Language', 'Core Mathematics', 'Integrated Science', 'Elective Mathematics', 'Economics', 'Literature'],
      notes: 'National curriculum remains core, with electives expanding in SHS.',
    },
    {
      countryCode: 'KE',
      countryName: 'Kenya',
      currencyCode: 'KES',
      regionalSystem: 'Anglophone',
      systemStructure: 'CBC (2-6-3-3 transition from 8-4-4)',
      prePrimaryStages: [
        { levelName: 'Daycare', ageRange: '3 months - 2 years', typicalFocus: 'Care, bonding, and social play.' },
        { levelName: 'Playgroup', ageRange: '2 - 3 years', typicalFocus: 'Language, socialization, and motor skills.' },
        { levelName: 'PP1', ageRange: '3 - 4 years', typicalFocus: 'Pre-literacy and pre-numeracy.' },
        { levelName: 'PP2', ageRange: '4 - 5 years', typicalFocus: 'School-readiness in competency-based learning.' },
      ],
      defaultClassLevels: ['Daycare', 'Playgroup', 'PP1', 'PP2', 'Grade 1', 'Grade 2', 'Grade 3', 'Grade 4', 'Grade 5', 'Grade 6', 'Junior Secondary 1', 'Junior Secondary 2', 'Junior Secondary 3', 'Senior Secondary 1', 'Senior Secondary 2', 'Senior Secondary 3'],
      defaultSubjects: ['English', 'Kiswahili', 'Mathematics', 'Integrated Science', 'Social Studies', 'Agriculture', 'Creative Arts', 'Computer Science', 'Business Studies', 'Life Skills'],
      primarySubjectSamples: ['English', 'Kiswahili', 'Mathematics', 'Integrated Science', 'Social Studies', 'Creative Arts', 'Physical Education'],
      juniorSubjectSamples: ['English', 'Kiswahili', 'Mathematics', 'Integrated Science', 'Agriculture', 'Business Studies', 'Computer Science'],
      seniorSubjectSamples: ['English', 'Kiswahili', 'Mathematics', 'Physics', 'Chemistry', 'Biology', 'Business Studies'],
      notes: 'CBC emphasizes competencies, projects, and continuous assessment.',
    },
    {
      countryCode: 'SN',
      countryName: 'Senegal',
      currencyCode: 'XOF',
      regionalSystem: 'Francophone',
      systemStructure: '6-4-3',
      prePrimaryStages: [
        { levelName: 'Creche', ageRange: '3 months - 2 years', typicalFocus: 'Care, motor play, and social adaptation.' },
        { levelName: 'Pre-maternelle', ageRange: '2 - 3 years', typicalFocus: 'Language and social readiness.' },
        { levelName: 'Petite / Moyenne Section', ageRange: '3 - 4 years', typicalFocus: 'French phonics and number sense.' },
        { levelName: 'Grande Section', ageRange: '4 - 5 years', typicalFocus: 'Preparation for Cours Preparatoire.' },
      ],
      defaultClassLevels: ['Creche', 'Pre-maternelle', 'Petite Section', 'Moyenne Section', 'Grande Section', 'CP1', 'CP2', 'CE1', 'CE2', 'CM1', 'CM2', 'College 1', 'College 2', 'College 3', 'College 4', 'Lycee 1', 'Lycee 2', 'Lycee 3'],
      defaultSubjects: ['Francais', 'Mathematiques', 'Sciences', 'Geographie', 'Education civique', 'Technologie', 'Informatique'],
      primarySubjectSamples: ['Francais', 'Mathematiques', 'SVT', 'Technologie', 'Education civique', 'Langue nationale'],
      juniorSubjectSamples: ['Francais', 'Mathematiques', 'Sciences', 'Informatique', 'Geographie'],
      seniorSubjectSamples: ['Francais', 'Mathematiques', 'Physique', 'Chimie', 'SVT', 'Philosophie', 'Economie'],
      notes: 'Culminates in Baccalaureat pathways for university entry.',
    },
    {
      countryCode: 'CI',
      countryName: "Cote d'Ivoire",
      currencyCode: 'XOF',
      regionalSystem: 'Francophone',
      systemStructure: '6-4-3',
      prePrimaryStages: [
        { levelName: 'Creche', ageRange: '3 months - 2 years', typicalFocus: 'Basic childcare and social play.' },
        { levelName: 'Pre-maternelle', ageRange: '2 - 3 years', typicalFocus: 'Early communication and social skills.' },
        { levelName: 'Maternelle 1', ageRange: '3 - 4 years', typicalFocus: 'French readiness and numeracy.' },
        { levelName: 'Maternelle 2', ageRange: '4 - 5 years', typicalFocus: 'Preparation for primary entry.' },
      ],
      defaultClassLevels: ['Creche', 'Pre-maternelle', 'Maternelle 1', 'Maternelle 2', 'CP1', 'CP2', 'CE1', 'CE2', 'CM1', 'CM2', 'College 1', 'College 2', 'College 3', 'College 4', 'Lycee 1', 'Lycee 2', 'Lycee 3'],
      defaultSubjects: ['Francais', 'Mathematiques', 'Sciences', 'Geographie', 'Education civique', 'Technologie'],
      primarySubjectSamples: ['Francais', 'Mathematiques', 'Geographie', 'Education civique', 'Sciences'],
      juniorSubjectSamples: ['Francais', 'Mathematiques', 'Sciences', 'Technologie', 'Education civique'],
      seniorSubjectSamples: ['Francais', 'Mathematiques', 'Physique', 'Chimie', 'SVT', 'Histoire-Geographie', 'Philosophie'],
      notes: 'Francophone curriculum aligned to regional exam standards.',
    },
    {
      countryCode: 'MA',
      countryName: 'Morocco',
      currencyCode: 'MAD',
      regionalSystem: 'Francophone',
      systemStructure: '6-4-3',
      prePrimaryStages: [
        { levelName: 'Creche', ageRange: '3 months - 2 years', typicalFocus: 'Early childcare and social development.' },
        { levelName: 'Pre-maternelle', ageRange: '2 - 3 years', typicalFocus: 'Communication and motor skills.' },
        { levelName: 'Maternelle 1', ageRange: '3 - 4 years', typicalFocus: 'French pre-literacy and numeracy.' },
        { levelName: 'Maternelle 2', ageRange: '4 - 5 years', typicalFocus: 'Preparation for CP1.' },
      ],
      defaultClassLevels: ['Creche', 'Pre-maternelle', 'Maternelle 1', 'Maternelle 2', 'CP1', 'CP2', 'CE1', 'CE2', 'CM1', 'CM2', 'College 1', 'College 2', 'College 3', 'College 4', 'Lycee 1', 'Lycee 2', 'Lycee 3'],
      defaultSubjects: ['Francais', 'Mathematiques', 'Sciences', 'Geographie', 'Education civique', 'Technologie', 'Informatique'],
      primarySubjectSamples: ['Francais', 'Mathematiques', 'Geographie', 'Education civique', 'Sciences'],
      juniorSubjectSamples: ['Francais', 'Mathematiques', 'Sciences', 'Technologie', 'Informatique'],
      seniorSubjectSamples: ['Francais', 'Mathematiques', 'Physique', 'Chimie', 'SVT', 'Philosophie', 'Economie'],
      notes: 'Francophone delivery is common in private and urban school systems.',
    },
  ]), []);
  const [form, setForm] = useState({
    schoolName: '',
    email: '',
    adminFullName: '',
    adminPassword: '',
    schoolType: 'PRIVATE',
    agreedToTermsAndDpa: false,
  });
  const [schoolModels, setSchoolModels] = useState(fallbackSchoolModels);
  const [countryOptions, setCountryOptions] = useState(fallbackCountryOptions);
  const [countryCode, setCountryCode] = useState('NG');
  const [selectedClassLevels, setSelectedClassLevels] = useState([]);
  const [selectedSubjects, setSelectedSubjects] = useState([]);
  const [customClassLevels, setCustomClassLevels] = useState([]);
  const [customSubjects, setCustomSubjects] = useState([]);
  const [customClassInput, setCustomClassInput] = useState('');
  const [customSubjectInput, setCustomSubjectInput] = useState('');
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [logo, setLogo] = useState(null);
  const [cacDocument, setCacDocument] = useState(null);
  const [step, setStep] = useState(1);
  const [createdSchool, setCreatedSchool] = useState(null);
  const [status, setStatus] = useState({ type: null, message: null });
  const [submitting, setSubmitting] = useState(false);

  const activeCountry = useMemo(
    () => countryOptions.find((option) => option.countryCode === countryCode) || countryOptions[0],
    [countryCode, countryOptions],
  );

  const activeSchoolModel = useMemo(
    () => schoolModels.find((model) => model.modelCode === form.schoolType) || schoolModels[0],
    [form.schoolType, schoolModels],
  );

  const applyCountryDefaults = (nextCountryCode, optionsList) => {
    const match = optionsList.find((item) => item.countryCode === nextCountryCode) || optionsList[0];
    if (!match) return;
    setCountryCode(match.countryCode);
    setSelectedClassLevels(match.defaultClassLevels || []);
    setSelectedSubjects(match.defaultSubjects || []);
    setCustomClassLevels([]);
    setCustomSubjects([]);
  };

  useEffect(() => {
    let isMounted = true;

    const loadOptions = async () => {
      try {
        const res = await apiFetch('/api/schools/onboarding-options');
        const data = await res.json().catch(() => ({}));
        const fetched = Array.isArray(data?.countries) ? data.countries : [];
        const fetchedSchoolModels = Array.isArray(data?.schoolModels) ? data.schoolModels : [];
        if (!isMounted) return;
        if (res.ok && fetched.length > 0) {
          setCountryOptions(fetched);
          applyCountryDefaults(fetched[0].countryCode, fetched);
        } else {
          applyCountryDefaults(fallbackCountryOptions[0].countryCode, fallbackCountryOptions);
        }

        if (res.ok && fetchedSchoolModels.length > 0) {
          setSchoolModels(fetchedSchoolModels);
          setForm((prev) => ({ ...prev, schoolType: fetchedSchoolModels[0].modelCode }));
        } else {
          setForm((prev) => ({ ...prev, schoolType: fallbackSchoolModels[0].modelCode }));
        }
      } catch {
        if (!isMounted) return;
        applyCountryDefaults(fallbackCountryOptions[0].countryCode, fallbackCountryOptions);
        setForm((prev) => ({ ...prev, schoolType: fallbackSchoolModels[0].modelCode }));
      } finally {
        if (isMounted) setLoadingOptions(false);
      }
    };

    loadOptions();
    return () => {
      isMounted = false;
    };
  }, [fallbackCountryOptions, fallbackSchoolModels]);

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

  const toggleSelected = (value, setter) => {
    setter((prev) => (
      prev.includes(value)
        ? prev.filter((item) => item !== value)
        : [...prev, value]
    ));
  };

  const addCustomValue = (rawValue, setter, currentValues, clearInput) => {
    const normalized = rawValue.trim();
    if (!normalized) return;
    if (currentValues.some((item) => item.toLowerCase() === normalized.toLowerCase())) {
      clearInput('');
      return;
    }
    setter((prev) => [...prev, normalized]);
    clearInput('');
  };

  const removeCustomValue = (value, setter) => {
    setter((prev) => prev.filter((item) => item !== value));
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

    if ((selectedClassLevels.length + customClassLevels.length) === 0) {
      setStatus({ type: 'error', message: 'Select at least one class level or add a custom one.' });
      return;
    }

    if ((selectedSubjects.length + customSubjects.length) === 0) {
      setStatus({ type: 'error', message: 'Select at least one subject or add a custom one.' });
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
      fd.append('SchoolType', form.schoolType);
      fd.append('CountryCode', countryCode);
      fd.append('CurrencyCode', activeCountry?.currencyCode || 'NGN');
      fd.append('AgreedToTermsAndDpa', form.agreedToTermsAndDpa ? 'true' : 'false');
      selectedClassLevels.forEach((level) => fd.append('SelectedClassLevels', level));
      customClassLevels.forEach((level) => fd.append('CustomClassLevels', level));
      selectedSubjects.forEach((subject) => fd.append('SelectedSubjects', subject));
      customSubjects.forEach((subject) => fd.append('CustomSubjects', subject));
      if (referralCode) fd.append('ReferralCode', referralCode);
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
            <Link to="/teacher/signup" className="action-card">
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

        {referralCode && (
          <div className="onboarding-status onboarding-status--success" role="status">
            Affiliate referral applied: <strong>{referralCode}</strong>
          </div>
        )}

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
              School Type
              <select
                name="schoolType"
                value={form.schoolType}
                onChange={handleChange}
                className="onboarding-input"
                disabled={loadingOptions}
              >
                {schoolModels.map((model) => (
                  <option key={model.modelCode} value={model.modelCode}>
                    {model.modelName}
                  </option>
                ))}
              </select>
            </label>

            {activeSchoolModel && (
              <div className="curriculum-insight-card" role="status">
                <p className="curriculum-insight-title">{activeSchoolModel.modelName}</p>
                <ul className="curriculum-list compact">
                  <li>Curriculum: {activeSchoolModel.curriculumApproach}</li>
                  <li>Resources: {activeSchoolModel.resourceProfile}</li>
                  <li>Language: {activeSchoolModel.languageApproach}</li>
                  <li>Cost: {activeSchoolModel.costProfile}</li>
                </ul>
              </div>
            )}

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
            <div className="curriculum-insight-card">
              <p className="curriculum-insight-title">{activeCountry?.countryName} Curriculum Context</p>
              <p className="curriculum-insight-meta">
                {activeCountry?.regionalSystem || 'Regional'} system • Structure: {activeCountry?.systemStructure || 'National'}
              </p>
              {activeCountry?.notes && <p className="curriculum-insight-note">{activeCountry.notes}</p>}
            </div>

            <div className="onboarding-label">
              <p className="onboarding-label-title">Pre-Primary (Nursery) Structure</p>
              <div className="preprimary-grid">
                {(activeCountry?.prePrimaryStages || []).map((stage) => (
                  <article key={stage.levelName} className="preprimary-card">
                    <h4>{stage.levelName}</h4>
                    <p className="preprimary-age">Age: {stage.ageRange}</p>
                    <p>{stage.typicalFocus}</p>
                  </article>
                ))}
              </div>
            </div>

            <div className="onboarding-label">
              <p className="onboarding-label-title">Country</p>
              <select
                className="onboarding-input"
                value={countryCode}
                onChange={(e) => applyCountryDefaults(e.target.value, countryOptions)}
                disabled={loadingOptions || submitting}
              >
                {countryOptions.map((option) => (
                  <option key={option.countryCode} value={option.countryCode}>
                    {option.countryName} ({option.currencyCode})
                  </option>
                ))}
              </select>
              <small className="onboarding-helper-text">
                Choose your school country to preload the right nursery, primary, and secondary structure.
              </small>
            </div>

            <div className="onboarding-label">
              <p className="onboarding-label-title">Subject Guidance By Level</p>
              <div className="subject-level-grid">
                <article className="subject-level-card">
                  <h4>Primary</h4>
                  <p>{(activeCountry?.primarySubjectSamples || []).join(', ')}</p>
                </article>
                <article className="subject-level-card">
                  <h4>Junior Secondary</h4>
                  <p>{(activeCountry?.juniorSubjectSamples || []).join(', ')}</p>
                </article>
                <article className="subject-level-card">
                  <h4>Senior Secondary</h4>
                  <p>{(activeCountry?.seniorSubjectSamples || []).join(', ')}</p>
                </article>
              </div>
            </div>

            <div className="onboarding-label">
              <p className="onboarding-label-title">Default Class Levels</p>
              <div className="selection-grid">
                {(activeCountry?.defaultClassLevels || []).map((level) => (
                  <label key={level} className="selection-chip">
                    <input
                      type="checkbox"
                      checked={selectedClassLevels.includes(level)}
                      onChange={() => toggleSelected(level, setSelectedClassLevels)}
                      disabled={submitting}
                    />
                    <span>{level}</span>
                  </label>
                ))}
              </div>

              <div className="custom-entry-row">
                <input
                  type="text"
                  className="onboarding-input"
                  placeholder="Add custom class level e.g. Reception"
                  value={customClassInput}
                  onChange={(e) => setCustomClassInput(e.target.value)}
                  disabled={submitting}
                />
                <button
                  type="button"
                  className="onboarding-secondary"
                  onClick={() => addCustomValue(customClassInput, setCustomClassLevels, [...customClassLevels, ...selectedClassLevels], setCustomClassInput)}
                  disabled={submitting}
                >
                  Add
                </button>
              </div>

              {customClassLevels.length > 0 && (
                <div className="custom-pill-list">
                  {customClassLevels.map((level) => (
                    <button
                      key={level}
                      type="button"
                      className="custom-pill"
                      onClick={() => removeCustomValue(level, setCustomClassLevels)}
                      disabled={submitting}
                    >
                      {level} ×
                    </button>
                  ))}
                </div>
              )}
            </div>

            <div className="onboarding-label">
              <p className="onboarding-label-title">Default Subjects</p>
              <div className="selection-grid">
                {(activeCountry?.defaultSubjects || []).map((subject) => (
                  <label key={subject} className="selection-chip">
                    <input
                      type="checkbox"
                      checked={selectedSubjects.includes(subject)}
                      onChange={() => toggleSelected(subject, setSelectedSubjects)}
                      disabled={submitting}
                    />
                    <span>{subject}</span>
                  </label>
                ))}
              </div>

              <div className="custom-entry-row">
                <input
                  type="text"
                  className="onboarding-input"
                  placeholder="Add custom subject e.g. French"
                  value={customSubjectInput}
                  onChange={(e) => setCustomSubjectInput(e.target.value)}
                  disabled={submitting}
                />
                <button
                  type="button"
                  className="onboarding-secondary"
                  onClick={() => addCustomValue(customSubjectInput, setCustomSubjects, [...customSubjects, ...selectedSubjects], setCustomSubjectInput)}
                  disabled={submitting}
                >
                  Add
                </button>
              </div>

              {customSubjects.length > 0 && (
                <div className="custom-pill-list">
                  {customSubjects.map((subject) => (
                    <button
                      key={subject}
                      type="button"
                      className="custom-pill"
                      onClick={() => removeCustomValue(subject, setCustomSubjects)}
                      disabled={submitting}
                    >
                      {subject} ×
                    </button>
                  ))}
                </div>
              )}
            </div>

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
