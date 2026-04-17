import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
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

function dedupeCaseInsensitive(values) {
  const seen = new Set();
  const result = [];
  values.forEach((value) => {
    const trimmed = String(value || '').trim();
    if (!trimmed) return;
    const key = trimmed.toLowerCase();
    if (seen.has(key)) return;
    seen.add(key);
    result.push(trimmed);
  });
  return result;
}

/** Best-effort: junior/senior secondary vs early years from class + grade labels (school naming varies). */
function isSecondarySchoolSectionClass(classRow) {
  if (!classRow) return false;
  const label = `${classRow.name || ''} ${classRow.gradeName || ''}`.toLowerCase();
  const secondaryHints = [
    'jss',
    'j.s.s',
    'junior secondary',
    'ss1',
    'ss2',
    'ss3',
    'ss ',
    's.s. 1',
    's.s. 2',
    's.s. 3',
    'senior secondary',
    'secondary',
    'high school',
    'form 1',
    'form 2',
    'form 3',
    'form 4',
    'shs',
    'jhs',
    'wassce',
    'sss',
    'grade 10',
    'grade 11',
    'grade 12',
  ];
  return secondaryHints.some((h) => label.includes(h));
}

function parseTransitionJson(rawJson) {
  const raw = String(rawJson || '').trim();
  if (!raw) return { map: {}, error: null };

  try {
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return { map: {}, error: 'Transition JSON must be an object map (for example {"Primary 1":["Primary 2"]}).' };
    }

    const normalized = {};
    for (const [source, targets] of Object.entries(parsed)) {
      const sourceName = String(source || '').trim();
      if (!sourceName) continue;
      if (!Array.isArray(targets)) {
        return { map: {}, error: `Target list for ${sourceName} must be an array.` };
      }
      normalized[sourceName] = dedupeCaseInsensitive(targets);
    }

    return { map: normalized, error: null };
  } catch {
    return { map: {}, error: 'Transition JSON is invalid. Fix JSON syntax before saving.' };
  }
}

function toPrettyTransitionJson(map) {
  const next = {};
  Object.entries(map || {}).forEach(([source, targets]) => {
    const sourceName = String(source || '').trim();
    const cleanedTargets = dedupeCaseInsensitive(targets || []);
    if (!sourceName || cleanedTargets.length === 0) return;
    next[sourceName] = cleanedTargets;
  });
  return JSON.stringify(next, null, 2);
}

function formatTransitionJsonForEditor(raw) {
  if (!raw || !String(raw).trim()) return '';
  try {
    return toPrettyTransitionJson(JSON.parse(raw));
  } catch {
    return String(raw);
  }
}

function normalizeNameKey(value) {
  return String(value || '').trim().toLowerCase();
}

function buildTerminalGradeToggleStorageKey(schoolId) {
  return `riseflow:terminal-grade-toggle:${schoolId}`;
}

function buildTransitionDraftStorageKey(schoolId) {
  return `riseflow:promotion-transition-draft:${schoolId}`;
}

function buildTransitionDiffFilterStorageKey(schoolId) {
  return `riseflow:promotion-transition-diff-filter:${schoolId}`;
}

function isLikelyTerminalGradeName(name) {
  const value = normalizeNameKey(name);
  return value.includes('ss3')
    || value.includes('shs 3')
    || value.includes('jhs 3')
    || value.includes('form 4')
    || value.includes('grade 12')
    || value.includes('year 13')
    || value.includes('final');
}

function toDateTimeLocalValue(value) {
  if (!value) return '';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const yyyy = date.getFullYear();
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const dd = String(date.getDate()).padStart(2, '0');
  const hh = String(date.getHours()).padStart(2, '0');
  const min = String(date.getMinutes()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}T${hh}:${min}`;
}

function formatAuditTimestamp(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleString();
}

function toCsvCell(value) {
  const normalized = String(value ?? '').replace(/\r?\n|\r/g, ' ');
  const escaped = normalized.replace(/"/g, '""');
  return `"${escaped}"`;
}

function buildDeniedAttemptsCsv(rows) {
  const header = ['Timestamp (UTC)', 'Entity Type', 'Action', 'Entity Id', 'User Email', 'User Name', 'Details'];
  const lines = [header.map(toCsvCell).join(',')];
  rows.forEach((row) => {
    lines.push([
      row.createdAtUtc || '',
      row.entityType || '',
      row.action || '',
      row.entityId || '',
      row.userEmail || '',
      row.userName || '',
      row.details || '',
    ].map(toCsvCell).join(','));
  });
  return `${lines.join('\n')}\n`;
}

function resolvePeopleRole(person) {
  const fromApi = String(person?.personRole || '').trim();
  if (fromApi.toLowerCase() === 'staff') return 'Staff';
  if (fromApi.toLowerCase() === 'teacher') return 'Teacher';

  const roleTitle = String(person?.roleTitle || '').trim().toLowerCase();
  if (!roleTitle) return 'Teacher';

  const looksStaff = roleTitle.includes('staff')
    || roleTitle.includes('bursar')
    || roleTitle.includes('clerk')
    || roleTitle.includes('secretary')
    || roleTitle.includes('front desk')
    || roleTitle.includes('account')
    || roleTitle.includes('support')
    || roleTitle.includes('office');

  return looksStaff ? 'Staff' : 'Teacher';
}

function resolveAdminView(view) {
  const normalized = String(view || '').toLowerCase();
  if (normalized === 'operations' || normalized === 'profile' || normalized === 'settings') return 'operations';
  if (normalized === 'people') return 'people';
  return 'overview';
}

export default function SchoolAdminPage({ view = 'overview' }) {
  const activeView = useMemo(() => resolveAdminView(view), [view]);

  const [dashboard, setDashboard] = useState(null);
  const [teachers, setTeachers] = useState([]);
  const [peopleRoleFilter, setPeopleRoleFilter] = useState('all');
  const [students, setStudents] = useState([]);
  const [classes, setClasses] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [grades, setGrades] = useState([]);
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
  const [academicProfileError, setAcademicProfileError] = useState(null);
  const [promotionTransitionError, setPromotionTransitionError] = useState(null);
  const [transitionPanelNotice, setTransitionPanelNotice] = useState(null);
  const [academicProfiles, setAcademicProfiles] = useState([]);
  const [savingAcademicProfile, setSavingAcademicProfile] = useState(false);
  const [savingPromotionTransition, setSavingPromotionTransition] = useState(false);
  const [promotionTransitionDraft, setPromotionTransitionDraft] = useState('');
  const [transitionSourceInput, setTransitionSourceInput] = useState('');
  const [transitionTargetInput, setTransitionTargetInput] = useState('');
  const [transitionDiffFilter, setTransitionDiffFilter] = useState('all');
  const [treatTerminalGradesAsValid, setTreatTerminalGradesAsValid] = useState(true);
  const [terminalToggleHydratedSchoolId, setTerminalToggleHydratedSchoolId] = useState(null);
  const [transitionDraftHydratedSchoolId, setTransitionDraftHydratedSchoolId] = useState(null);
  const [transitionDiffFilterHydratedSchoolId, setTransitionDiffFilterHydratedSchoolId] = useState(null);
  const [isTransitionDraftFromCache, setIsTransitionDraftFromCache] = useState(false);
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
    academicSystemProfileId: null,
    academicSystemProfileCode: null,
    academicSystemProfileName: null,
    profilePromotionTransitionJson: null,
    promotionTransitionOverrideJson: null,
    effectivePromotionTransitionJson: null,
    termsPerYear: null,
  });
  const [savingClassId, setSavingClassId] = useState(null);
  const [selectedStudentIds, setSelectedStudentIds] = useState([]);
  const [bulkClassId, setBulkClassId] = useState('');
  const [bulkAssigning, setBulkAssigning] = useState(false);
  const [selectedTeacherId, setSelectedTeacherId] = useState(null);
  const [selectedTeacherProfile, setSelectedTeacherProfile] = useState(null);
  const [teacherAssignClassId, setTeacherAssignClassId] = useState('');
  const [teacherAssignRoleInClass, setTeacherAssignRoleInClass] = useState('');
  const [customTeacherAssignRoleInClass, setCustomTeacherAssignRoleInClass] = useState('');
  const [assigningTeacherClass, setAssigningTeacherClass] = useState(false);
  const [removingTeacherClassId, setRemovingTeacherClassId] = useState(null);
  const [classSubjectClassId, setClassSubjectClassId] = useState('');
  const [classSubjectSubjectId, setClassSubjectSubjectId] = useState('');
  const [savingClassSubject, setSavingClassSubject] = useState(false);
  const [removingClassSubjectKey, setRemovingClassSubjectKey] = useState(null);
  const [teacherSubjectClassId, setTeacherSubjectClassId] = useState('');
  const [teacherSubjectSubjectId, setTeacherSubjectSubjectId] = useState('');
  const [savingTeacherClassSubject, setSavingTeacherClassSubject] = useState(false);
  const [removingTeacherClassSubjectKey, setRemovingTeacherClassSubjectKey] = useState(null);
  const [staffStructureOptions, setStaffStructureOptions] = useState(null);
  const [staffStructureConfig, setStaffStructureConfig] = useState(null);
  const [staffPermissionMatrixDraft, setStaffPermissionMatrixDraft] = useState([]);
  const [customHierarchyRoleDraft, setCustomHierarchyRoleDraft] = useState({ roleTitle: '', stageScope: '', hierarchyOrder: '' });
  const [savingStaffStructureConfig, setSavingStaffStructureConfig] = useState(false);
  const [selectedTeacherRoleTitle, setSelectedTeacherRoleTitle] = useState('');
  const [customTeacherRoleTitle, setCustomTeacherRoleTitle] = useState('');
  const [selectedTeacherDepartment, setSelectedTeacherDepartment] = useState('');
  const [savingTeacherRoleProfile, setSavingTeacherRoleProfile] = useState(false);
  const [teacherFieldSettings, setTeacherFieldSettings] = useState([]);
  const [loadingTeacherProfile, setLoadingTeacherProfile] = useState(false);
  const [loadingDeniedAttempts, setLoadingDeniedAttempts] = useState(false);
  const [exportingDeniedAttempts, setExportingDeniedAttempts] = useState(false);
  const [deniedAttemptsError, setDeniedAttemptsError] = useState(null);
  const [deniedAttempts, setDeniedAttempts] = useState([]);
  const [deniedAttemptsCurrentCursor, setDeniedAttemptsCurrentCursor] = useState(null);
  const [deniedAttemptsNextCursor, setDeniedAttemptsNextCursor] = useState(null);
  const [deniedAttemptsHasMore, setDeniedAttemptsHasMore] = useState(false);
  const [deniedAttemptsCursorHistory, setDeniedAttemptsCursorHistory] = useState([]);
  const [deniedAttemptsFetchedTotal, setDeniedAttemptsFetchedTotal] = useState(0);
  const [deniedAuditFilters, setDeniedAuditFilters] = useState(() => {
    const now = new Date();
    const sevenDaysAgo = new Date(now.getTime() - (7 * 24 * 60 * 60 * 1000));
    return {
      fromUtc: toDateTimeLocalValue(sevenDaysAgo),
      toUtc: toDateTimeLocalValue(now),
      entityType: '',
      userEmail: '',
      limit: '200',
    };
  });
  const [savingFieldSettingKey, setSavingFieldSettingKey] = useState(null);
  const [newCustomField, setNewCustomField] = useState({ displayName: '', fieldKey: '' });
  const [showTeacherFieldControls, setShowTeacherFieldControls] = useState(false);
  const [showSchoolHierarchyCatalog, setShowSchoolHierarchyCatalog] = useState(false);
  const [showDeniedPermissionAttempts, setShowDeniedPermissionAttempts] = useState(false);
  const fileInputRefs = useRef({});
  const schoolFileInputRef = useRef(null);
  const schoolLogoInputRef = useRef(null);
  const registrationDocInputRef = useRef(null);
  const deniedAttemptsSeenIdsRef = useRef(new Set());
  const [paying, setPaying] = useState(false);
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
      apiFetch('/api/teachers/people').then((r) => readJsonOrThrow(r, 'Failed to load school people.')),
      apiFetch('/api/students').then((r) => readJsonOrThrow(r, 'Failed to load students.')),
      apiFetch('/api/schools/classes').then((r) => readJsonOrThrow(r, 'Failed to load classes.')),
      apiFetch('/api/subjects').then((r) => readJsonOrThrow(r, 'Failed to load subjects.')),
      apiFetch('/api/schools/grades').then((r) => readJsonOrThrow(r, 'Failed to load grades.')),
      apiFetch('/api/parents').then((r) => readJsonOrThrow(r, 'Failed to load parents.')),
      apiFetch('/api/billing').then((r) => readJsonOrThrow(r, 'Failed to load billing records.')),
      apiFetch('/api/schools/academic-system-profiles').then((r) => readJsonOrThrow(r, 'Failed to load academic system profiles.')),
      apiFetch('/api/schools/staff-structure-options').then((r) => readJsonOrThrow(r, 'Failed to load staff structure options.')),
      apiFetch('/api/schools/staff-structure-config').then((r) => readJsonOrThrow(r, 'Failed to load staff structure config.')),
    ])
      .then((results) => {
        const [dashResult, profileResult, teacherResult, studentResult, classResult, subjectResult, gradeResult, parentResult, billingResult, profileOptionsResult, staffStructureResult, staffStructureConfigResult] = results;
        const dash = dashResult.status === 'fulfilled' ? dashResult.value : null;
        const profile = profileResult.status === 'fulfilled' ? profileResult.value : null;

        if (!dash) {
          const failure = results.find((result) => result.status === 'rejected');
          throw new Error(failure?.reason?.message || 'Failed to load school dashboard.');
        }

        setDashboard(dash);
        if (profile) {
          const profileTransitionJson = profile.profilePromotionTransitionJson || null;
          const overrideTransitionJson = profile.promotionTransitionOverrideJson || null;
          const effectiveTransitionJson = profile.effectivePromotionTransitionJson || null;
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
            academicSystemProfileId: profile.academicSystemProfileId || null,
            academicSystemProfileCode: profile.academicSystemProfileCode || null,
            academicSystemProfileName: profile.academicSystemProfileName || null,
            profilePromotionTransitionJson: profileTransitionJson,
            promotionTransitionOverrideJson: overrideTransitionJson,
            effectivePromotionTransitionJson: effectiveTransitionJson,
            termsPerYear: Number.isInteger(profile.termsPerYear) ? profile.termsPerYear : null,
          });
        }
        setAcademicProfiles(profileOptionsResult.status === 'fulfilled' && Array.isArray(profileOptionsResult.value) ? profileOptionsResult.value : []);
        setTeachers(teacherResult.status === 'fulfilled' && Array.isArray(teacherResult.value) ? teacherResult.value : []);
        setStudents(studentResult.status === 'fulfilled' && Array.isArray(studentResult.value) ? studentResult.value : []);
        setClasses(classResult.status === 'fulfilled' && Array.isArray(classResult.value) ? classResult.value : []);
        setSubjects(subjectResult.status === 'fulfilled' && Array.isArray(subjectResult.value) ? subjectResult.value : []);
        setGrades(gradeResult.status === 'fulfilled' && Array.isArray(gradeResult.value) ? gradeResult.value : []);
        setParents(parentResult.status === 'fulfilled' && Array.isArray(parentResult.value) ? parentResult.value : []);
        setBilling(billingResult.status === 'fulfilled' && Array.isArray(billingResult.value) ? billingResult.value : []);
        setStaffStructureOptions(staffStructureResult.status === 'fulfilled' ? staffStructureResult.value : null);
        setStaffStructureConfig(staffStructureConfigResult.status === 'fulfilled' ? staffStructureConfigResult.value : null);
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

  const loadDeniedAttempts = useCallback(async (filters, cursor = null, options = {}) => {
    const { resetPaging = false } = options;
    setLoadingDeniedAttempts(true);
    setDeniedAttemptsError(null);
    try {
      if (resetPaging) {
        setDeniedAttemptsCursorHistory([]);
        deniedAttemptsSeenIdsRef.current = new Set();
        setDeniedAttemptsFetchedTotal(0);
      }

      const params = new URLSearchParams();
      if (filters?.fromUtc) {
        const parsed = new Date(filters.fromUtc);
        if (!Number.isNaN(parsed.getTime())) params.set('fromUtc', parsed.toISOString());
      }
      if (filters?.toUtc) {
        const parsed = new Date(filters.toUtc);
        if (!Number.isNaN(parsed.getTime())) params.set('toUtc', parsed.toISOString());
      }
      if (filters?.entityType) params.set('entityType', filters.entityType.trim());
      if (filters?.userEmail) params.set('userEmail', filters.userEmail.trim());
      const parsedLimit = Number.parseInt(filters?.limit, 10);
      if (Number.isFinite(parsedLimit) && parsedLimit > 0) {
        params.set('limit', String(Math.min(parsedLimit, 1000)));
      }
      if (Number.isFinite(cursor) && cursor > 0) {
        params.set('beforeId', String(cursor));
      }

      const queryString = params.toString();
      const res = await apiFetch(`/api/schools/audit/denied-attempts${queryString ? `?${queryString}` : ''}`);
      const payload = await readJsonOrThrow(res, 'Could not load denied permission attempts.');
      const items = Array.isArray(payload)
        ? payload
        : (Array.isArray(payload?.items) ? payload.items : []);

      const nextCursorRaw = payload && !Array.isArray(payload) ? Number(payload.nextCursor) : NaN;
      const hasMoreRaw = payload && !Array.isArray(payload) ? payload.hasMore : false;

      setDeniedAttempts(items);
      items.forEach((item) => {
        if (item?.id != null) deniedAttemptsSeenIdsRef.current.add(item.id);
      });
      setDeniedAttemptsFetchedTotal(deniedAttemptsSeenIdsRef.current.size);
      setDeniedAttemptsCurrentCursor(Number.isFinite(cursor) && cursor > 0 ? cursor : null);
      setDeniedAttemptsNextCursor(Number.isFinite(nextCursorRaw) && nextCursorRaw > 0 ? nextCursorRaw : null);
      setDeniedAttemptsHasMore(Boolean(hasMoreRaw));
    } catch (e) {
      setDeniedAttemptsError(e.message || 'Could not load denied permission attempts.');
      setDeniedAttempts([]);
      setDeniedAttemptsCurrentCursor(null);
      setDeniedAttemptsNextCursor(null);
      setDeniedAttemptsHasMore(false);
      if (resetPaging) {
        setDeniedAttemptsCursorHistory([]);
        deniedAttemptsSeenIdsRef.current = new Set();
        setDeniedAttemptsFetchedTotal(0);
      }
    } finally {
      setLoadingDeniedAttempts(false);
    }
  }, [readJsonOrThrow]);

  useEffect(() => {
    if (activeView === 'people') {
      loadDeniedAttempts(deniedAuditFilters, null, { resetPaging: true });
    }
  }, [activeView, loadDeniedAttempts]);

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
  const currentSchoolId = dashboard?.schoolId || null;
  const serverTransitionDraftValue = formatTransitionJsonForEditor(
    schoolProfile.promotionTransitionOverrideJson
      || schoolProfile.effectivePromotionTransitionJson
      || '',
  );

  useEffect(() => {
    if (!currentSchoolId || transitionDraftHydratedSchoolId === currentSchoolId) return;

    const serverValue = serverTransitionDraftValue;

    let nextDraft = serverValue;
    let usedCachedDraft = false;
    if (typeof localStorage !== 'undefined') {
      try {
        const key = buildTransitionDraftStorageKey(currentSchoolId);
        const cached = localStorage.getItem(key);
        const cachedValue = formatTransitionJsonForEditor(cached || '');
        if (cachedValue && cachedValue !== serverValue) {
          nextDraft = cachedValue;
          usedCachedDraft = true;
        } else if (cachedValue && cachedValue === serverValue) {
          localStorage.removeItem(key);
        }
      } catch {
        // ignore storage read errors
      }
    }

    setPromotionTransitionDraft(nextDraft);
    setIsTransitionDraftFromCache(usedCachedDraft);
    setTransitionDraftHydratedSchoolId(currentSchoolId);
  }, [
    currentSchoolId,
    transitionDraftHydratedSchoolId,
    serverTransitionDraftValue,
  ]);

  useEffect(() => {
    if (!currentSchoolId || transitionDraftHydratedSchoolId !== currentSchoolId || typeof localStorage === 'undefined') return;

    try {
      const key = buildTransitionDraftStorageKey(currentSchoolId);
      const draftValue = formatTransitionJsonForEditor(promotionTransitionDraft);
      if (!draftValue || draftValue === serverTransitionDraftValue) {
        localStorage.removeItem(key);
      } else {
        localStorage.setItem(key, draftValue);
      }
    } catch {
      // ignore storage write errors
    }
  }, [currentSchoolId, transitionDraftHydratedSchoolId, promotionTransitionDraft, serverTransitionDraftValue]);

  useEffect(() => {
    if (!currentSchoolId || transitionDiffFilterHydratedSchoolId === currentSchoolId) return;

    let nextFilter = 'all';
    if (typeof localStorage !== 'undefined') {
      try {
        const raw = localStorage.getItem(buildTransitionDiffFilterStorageKey(currentSchoolId));
        if (raw === 'all' || raw === 'added' || raw === 'removed' || raw === 'changed') {
          nextFilter = raw;
        }
      } catch {
        // ignore storage read errors
      }
    }

    setTransitionDiffFilter(nextFilter);
    setTransitionDiffFilterHydratedSchoolId(currentSchoolId);
  }, [currentSchoolId, transitionDiffFilterHydratedSchoolId]);

  useEffect(() => {
    if (!currentSchoolId || transitionDiffFilterHydratedSchoolId !== currentSchoolId || typeof localStorage === 'undefined') return;

    try {
      localStorage.setItem(buildTransitionDiffFilterStorageKey(currentSchoolId), transitionDiffFilter);
    } catch {
      // ignore storage write errors
    }
  }, [currentSchoolId, transitionDiffFilterHydratedSchoolId, transitionDiffFilter]);

    useEffect(() => {
      if (!currentSchoolId || typeof localStorage === 'undefined') return;

      try {
        const raw = localStorage.getItem(buildTerminalGradeToggleStorageKey(currentSchoolId));
        if (raw === '0' || raw === '1') {
          setTreatTerminalGradesAsValid(raw === '1');
        }
      } catch {
        // ignore storage read errors
      } finally {
        setTerminalToggleHydratedSchoolId(currentSchoolId);
      }
    }, [currentSchoolId]);

    useEffect(() => {
      if (!currentSchoolId || terminalToggleHydratedSchoolId !== currentSchoolId || typeof localStorage === 'undefined') return;

      try {
        localStorage.setItem(
          buildTerminalGradeToggleStorageKey(currentSchoolId),
          treatTerminalGradesAsValid ? '1' : '0',
        );
      } catch {
        // ignore storage write errors
      }
    }, [currentSchoolId, terminalToggleHydratedSchoolId, treatTerminalGradesAsValid]);
  const selectedTeacher = selectedTeacherProfile?.teacher || teachers.find((teacher) => teacher.id === selectedTeacherId) || null;
  const peopleRows = useMemo(() => {
    const teacherRows = teachers.map((teacher) => {
      const accountRole = resolvePeopleRole(teacher);
      return {
        id: `teacher-${teacher.id}`,
        sourceType: 'teacher',
        sourceId: teacher.id,
        fullName: [teacher.firstName, teacher.middleName, teacher.lastName].filter(Boolean).join(' '),
        accountRole,
        roleLabel: teacher.roleTitle || accountRole,
        department: teacher.department || teacher.subjectSpecialization || '—',
        isActive: teacher.isActive !== false,
      };
    });

    const studentRows = students.map((student) => ({
      id: `student-${student.id}`,
      sourceType: 'student',
      sourceId: student.id,
      fullName: [student.firstName, student.middleName, student.lastName].filter(Boolean).join(' '),
      accountRole: 'Student',
      roleLabel: 'Student',
      department: student.className || '—',
      isActive: student.isActive !== false,
    }));

    const parentRows = parents.map((parent) => ({
      id: `parent-${parent.id}`,
      sourceType: 'parent',
      sourceId: parent.id,
      fullName: [parent.firstName, parent.middleName, parent.lastName].filter(Boolean).join(' '),
      accountRole: 'Parent',
      roleLabel: 'Parent',
      department: '—',
      isActive: parent.isActive !== false,
    }));

    return [...teacherRows, ...studentRows, ...parentRows];
  }, [teachers, students, parents]);

  const peopleRoleOptions = useMemo(() => {
    const preferredOrder = ['Teacher', 'Staff', 'Student', 'Parent'];
    const counts = new Map();
    peopleRows.forEach((row) => {
      const key = String(row.accountRole || '').trim();
      if (!key) return;
      counts.set(key, (counts.get(key) || 0) + 1);
    });

    const ordered = [];
    preferredOrder.forEach((role) => {
      if (counts.has(role)) {
        ordered.push({ value: role.toLowerCase(), label: role, count: counts.get(role) });
        counts.delete(role);
      }
    });

    Array.from(counts.entries())
      .sort((a, b) => a[0].localeCompare(b[0]))
      .forEach(([role, count]) => {
        ordered.push({ value: role.toLowerCase(), label: role, count });
      });

    return ordered;
  }, [peopleRows]);

  const filteredPeople = useMemo(() => {
    if (peopleRoleFilter === 'all') return peopleRows;
    return peopleRows.filter((person) => String(person.accountRole || '').toLowerCase() === peopleRoleFilter);
  }, [peopleRoleFilter, peopleRows]);
  const staffCatalogRoles = useMemo(() => {
    const configRoles = Array.isArray(staffStructureConfig?.roleCatalog)
      ? staffStructureConfig.roleCatalog
      : [];
    const fallbackRoles = Array.isArray(staffStructureOptions?.roleOptions)
      ? staffStructureOptions.roleOptions.map((role) => ({
        roleCode: role.roleCode,
        roleTitle: role.roleTitle,
        stageScope: role.defaultStageScope,
        hierarchyOrder: role.hierarchyOrder,
        isSystemDefault: true,
      }))
      : [];

    const source = configRoles.length > 0 ? configRoles : fallbackRoles;
    return [...source].sort((a, b) => (a.hierarchyOrder || 0) - (b.hierarchyOrder || 0));
  }, [staffStructureConfig?.roleCatalog, staffStructureOptions?.roleOptions]);

  const staffRoleTitles = useMemo(() => dedupeCaseInsensitive([
    ...staffCatalogRoles.map((role) => role?.roleTitle),
    ...teachers.map((teacher) => teacher?.roleTitle),
  ]), [staffCatalogRoles, teachers]);

  const stageScopeOptions = useMemo(() => dedupeCaseInsensitive([
    ...(Array.isArray(staffStructureConfig?.stageScopes) ? staffStructureConfig.stageScopes : []),
    ...(Array.isArray(staffStructureOptions?.stageScopes) ? staffStructureOptions.stageScopes : []),
    ...teachers.map((teacher) => teacher?.department),
  ]), [staffStructureConfig?.stageScopes, staffStructureOptions?.stageScopes, teachers]);

  const classRoleOptions = useMemo(() => dedupeCaseInsensitive([
    ...(Array.isArray(staffStructureConfig?.classAssignmentRoles) ? staffStructureConfig.classAssignmentRoles : []),
    ...(Array.isArray(staffStructureOptions?.classAssignmentRoles) ? staffStructureOptions.classAssignmentRoles : []),
    ...teachers.flatMap((teacher) => (teacher.teacherClasses || []).map((tc) => tc?.roleInClass)),
  ]), [staffStructureConfig?.classAssignmentRoles, staffStructureOptions?.classAssignmentRoles, teachers]);

  const deniedEntityTypeOptions = useMemo(() => dedupeCaseInsensitive(deniedAttempts.map((item) => item?.entityType)), [deniedAttempts]);
  const deniedAttemptsPageNumber = deniedAttemptsCursorHistory.length + 1;

  useEffect(() => {
    if (!staffStructureConfig || !Array.isArray(staffStructureConfig.roleCatalog)) {
      setStaffPermissionMatrixDraft([]);
      return;
    }

    const matrixMap = new Map((staffStructureConfig.permissionMatrix || []).map((item) => [String(item.roleTitle || '').toLowerCase(), item]));
    const draft = staffStructureConfig.roleCatalog.map((role) => {
      const key = String(role.roleTitle || '').toLowerCase();
      const existing = matrixMap.get(key);
      return existing || {
        roleTitle: role.roleTitle,
        canManageTeachers: false,
        canAssignClasses: false,
        canApproveResults: false,
        canSendParentBroadcasts: false,
        canManageFees: false,
      };
    });

    setStaffPermissionMatrixDraft(draft);
  }, [staffStructureConfig?.roleCatalog, staffStructureConfig?.permissionMatrix]);

  useEffect(() => {
    if (!selectedTeacher) {
      setSelectedTeacherRoleTitle('');
      setCustomTeacherRoleTitle('');
      setSelectedTeacherDepartment('');
      setTeacherAssignRoleInClass('');
      setCustomTeacherAssignRoleInClass('');
      setTeacherSubjectClassId('');
      setTeacherSubjectSubjectId('');
      return;
    }

    const teacherRoleTitle = selectedTeacher.roleTitle || '';
    const teacherDepartment = selectedTeacher.department || '';
    const isKnownRoleTitle = staffRoleTitles.some((item) => item.toLowerCase() === teacherRoleTitle.toLowerCase());

    setSelectedTeacherRoleTitle(isKnownRoleTitle ? teacherRoleTitle : 'custom');
    setCustomTeacherRoleTitle(isKnownRoleTitle ? '' : teacherRoleTitle);
    setSelectedTeacherDepartment(teacherDepartment);
    setTeacherAssignRoleInClass('');
    setCustomTeacherAssignRoleInClass('');
    setTeacherSubjectClassId('');
    setTeacherSubjectSubjectId('');
  }, [selectedTeacher?.id, selectedTeacher?.roleTitle, selectedTeacher?.department, staffRoleTitles]);

  const selectedTeacherClassIds = selectedTeacher
    ? Array.from(new Set([
      ...(selectedTeacher.teacherClasses || []).map((tc) => tc.classId),
      ...(selectedTeacher.teacherClassSubjects || []).map((tcs) => tcs.classId),
    ].filter(Boolean)))
    : [];
  const classNameById = useMemo(() => {
    const map = new Map();
    classes.forEach((schoolClass) => {
      map.set(schoolClass.id, schoolClass.name || schoolClass.id);
    });
    return map;
  }, [classes]);
  const subjectNameById = useMemo(() => {
    const map = new Map();
    subjects.forEach((subject) => {
      map.set(subject.id, subject.name || subject.id);
    });
    return map;
  }, [subjects]);
  const selectedTeacherClassSubjects = useMemo(() => {
    const rows = selectedTeacher?.teacherClassSubjects || [];
    return rows
      .map((row) => ({
        classId: row.classId,
        subjectId: row.subjectId,
        className: classNameById.get(row.classId) || row.class?.name || row.classId,
        subjectName: subjectNameById.get(row.subjectId) || row.subject?.name || row.subjectId,
      }))
      .filter((row) => row.classId && row.subjectId)
      .sort((a, b) => {
        const left = `${a.className} ${a.subjectName}`.toLowerCase();
        const right = `${b.className} ${b.subjectName}`.toLowerCase();
        return left.localeCompare(right);
      });
  }, [classNameById, selectedTeacher?.teacherClassSubjects, subjectNameById]);
  const teacherSubjectOptions = useMemo(() => subjects, [subjects]);
  const teacherSubjectClassForHint = useMemo(
    () => (teacherSubjectClassId ? classes.find((c) => c.id === teacherSubjectClassId) : null),
    [classes, teacherSubjectClassId],
  );
  const teacherSubjectClassIsSecondary = teacherSubjectClassForHint
    ? isSecondarySchoolSectionClass(teacherSubjectClassForHint)
    : false;
  const selectedTeacherStudentCount = selectedTeacherClassIds.length === 0
    ? 0
    : students.filter((s) => s.classId && selectedTeacherClassIds.includes(s.classId)).length;

  const assignSubjectToClass = async () => {
    if (!classSubjectClassId || !classSubjectSubjectId || savingClassSubject) return;
    setSavingClassSubject(true);
    setError(null);
    try {
      const res = await apiFetch(`/api/subjects/classes/${classSubjectClassId}/subjects/${classSubjectSubjectId}`, {
        method: 'POST',
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not assign subject to class.');

      setClassSubjectClassId('');
      setClassSubjectSubjectId('');
      await loadData({ background: true });
      if (selectedTeacherId) await fetchTeacherProfile(selectedTeacherId);
    } catch (e) {
      setError(e.message || 'Could not assign subject to class.');
    } finally {
      setSavingClassSubject(false);
    }
  };

  const unassignSubjectFromClass = async (classId, subjectId) => {
    if (!classId || !subjectId || removingClassSubjectKey === `${classId}:${subjectId}`) return;
    setRemovingClassSubjectKey(`${classId}:${subjectId}`);
    setError(null);
    try {
      const res = await apiFetch(`/api/subjects/classes/${classId}/subjects/${subjectId}`, {
        method: 'DELETE',
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not remove subject from class.');

      await loadData({ background: true });
      if (selectedTeacherId) await fetchTeacherProfile(selectedTeacherId);
    } catch (e) {
      setError(e.message || 'Could not remove subject from class.');
    } finally {
      setRemovingClassSubjectKey(null);
    }
  };

  const assignTeacherToClassSubject = async () => {
    if (!selectedTeacher?.id || !teacherSubjectClassId || !teacherSubjectSubjectId || savingTeacherClassSubject) return;
    setSavingTeacherClassSubject(true);
    setError(null);
    try {
      const res = await apiFetch(`/api/subjects/teachers/${selectedTeacher.id}/classes/${teacherSubjectClassId}/subjects/${teacherSubjectSubjectId}`, {
        method: 'POST',
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not assign teacher to class subject.');

      setTeacherSubjectClassId('');
      setTeacherSubjectSubjectId('');
      await loadData({ background: true });
      if (selectedTeacherId) await fetchTeacherProfile(selectedTeacherId);
    } catch (e) {
      setError(e.message || 'Could not assign teacher to class subject.');
    } finally {
      setSavingTeacherClassSubject(false);
    }
  };

  const unassignTeacherFromClassSubject = async (classId, subjectId) => {
    if (!selectedTeacher?.id || !classId || !subjectId || removingTeacherClassSubjectKey === `${classId}:${subjectId}`) return;
    setRemovingTeacherClassSubjectKey(`${classId}:${subjectId}`);
    setError(null);
    try {
      const res = await apiFetch(`/api/subjects/teachers/${selectedTeacher.id}/classes/${classId}/subjects/${subjectId}`, {
        method: 'DELETE',
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not unassign teacher from class subject.');

      await loadData({ background: true });
      if (selectedTeacherId) await fetchTeacherProfile(selectedTeacherId);
    } catch (e) {
      setError(e.message || 'Could not unassign teacher from class subject.');
    } finally {
      setRemovingTeacherClassSubjectKey(null);
    }
  };

  const updateTeacherRoleProfile = async () => {
    if (!selectedTeacher?.id || savingTeacherRoleProfile) return;

    const roleTitle = (selectedTeacherRoleTitle === 'custom' ? customTeacherRoleTitle : selectedTeacherRoleTitle).trim();
    const department = selectedTeacherDepartment.trim();

    setSavingTeacherRoleProfile(true);
    setError(null);
    try {
      const res = await apiFetch(`/api/teachers/${selectedTeacher.id}/role-profile`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          roleTitle: roleTitle || null,
          department: department || null,
        }),
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not update teacher role profile.');

      await loadData({ background: true });
      if (selectedTeacherId) await fetchTeacherProfile(selectedTeacherId);
    } catch (e) {
      setError(e.message || 'Could not update teacher role profile.');
    } finally {
      setSavingTeacherRoleProfile(false);
    }
  };

  const addCustomHierarchyRole = () => {
    const roleTitle = (customHierarchyRoleDraft.roleTitle || '').trim();
    if (!roleTitle) {
      setError('Custom hierarchy role title is required.');
      return;
    }

    const stageScope = (customHierarchyRoleDraft.stageScope || '').trim() || 'Whole School';
    const orderRaw = Number.parseInt(customHierarchyRoleDraft.hierarchyOrder, 10);
    const hierarchyOrder = Number.isFinite(orderRaw) && orderRaw > 0 ? orderRaw : 900;

    setStaffStructureConfig((current) => {
      if (!current) return current;
      const exists = (current.roleCatalog || []).some((item) => String(item.roleTitle || '').toLowerCase() === roleTitle.toLowerCase());
      if (exists) return current;

      const roleCode = roleTitle.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '') || 'custom_role';
      const nextCatalog = [
        ...(current.roleCatalog || []),
        {
          roleCode,
          roleTitle,
          stageScope,
          hierarchyOrder,
          isSystemDefault: false,
        },
      ].sort((a, b) => (a.hierarchyOrder || 0) - (b.hierarchyOrder || 0));

      return { ...current, roleCatalog: nextCatalog };
    });

    setCustomHierarchyRoleDraft({ roleTitle: '', stageScope: '', hierarchyOrder: '' });
    setError(null);
  };

  const removeCustomHierarchyRole = (roleTitle) => {
    setStaffStructureConfig((current) => {
      if (!current) return current;
      const nextCatalog = (current.roleCatalog || []).filter((item) => {
        if (String(item.roleTitle || '').toLowerCase() !== String(roleTitle || '').toLowerCase()) return true;
        return !!item.isSystemDefault;
      });
      return { ...current, roleCatalog: nextCatalog };
    });
  };

  const updatePermissionRule = (roleTitle, key, checked) => {
    setStaffPermissionMatrixDraft((current) => current.map((item) => (
      String(item.roleTitle || '').toLowerCase() === String(roleTitle || '').toLowerCase()
        ? { ...item, [key]: checked }
        : item
    )));
  };

  const saveStaffStructureConfig = async () => {
    if (!staffStructureConfig || savingStaffStructureConfig) return;

    setSavingStaffStructureConfig(true);
    setError(null);
    try {
      const payload = {
        roleCatalog: (staffStructureConfig.roleCatalog || []).map((item) => ({
          roleCode: item.roleCode || null,
          roleTitle: (item.roleTitle || '').trim(),
          stageScope: (item.stageScope || '').trim() || 'Whole School',
          hierarchyOrder: Number(item.hierarchyOrder || 0),
          isSystemDefault: !!item.isSystemDefault,
        })).filter((item) => item.roleTitle),
        permissionMatrix: (staffPermissionMatrixDraft || []).map((item) => ({
          roleTitle: item.roleTitle,
          canManageTeachers: !!item.canManageTeachers,
          canAssignClasses: !!item.canAssignClasses,
          canApproveResults: !!item.canApproveResults,
          canSendParentBroadcasts: !!item.canSendParentBroadcasts,
          canManageFees: !!item.canManageFees,
        })).filter((item) => String(item.roleTitle || '').trim()),
      };

      const res = await apiFetch('/api/schools/staff-structure-config', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not save staff structure config.');

      const updated = text ? JSON.parse(text) : null;
      if (updated) setStaffStructureConfig(updated);
      await loadData({ background: true });
    } catch (e) {
      setError(e.message || 'Could not save staff structure config.');
    } finally {
      setSavingStaffStructureConfig(false);
    }
  };

  const assignTeacherToClass = async () => {
    if (!selectedTeacher?.id || !teacherAssignClassId || assigningTeacherClass) return;
    const roleInClass = (teacherAssignRoleInClass === 'custom' ? customTeacherAssignRoleInClass : teacherAssignRoleInClass).trim();
    setAssigningTeacherClass(true);
    setError(null);
    try {
      const res = await apiFetch(`/api/teachers/${selectedTeacher.id}/classes/${teacherAssignClassId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ roleInClass: roleInClass || null }),
      });
      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not assign teacher to class.');

      setTeacherAssignClassId('');
      setTeacherAssignRoleInClass('');
      setCustomTeacherAssignRoleInClass('');
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

  const applyDeniedAuditFilters = async () => {
    await loadDeniedAttempts(deniedAuditFilters, null, { resetPaging: true });
  };

  const resetDeniedAuditFilters = async () => {
    const now = new Date();
    const sevenDaysAgo = new Date(now.getTime() - (7 * 24 * 60 * 60 * 1000));
    const defaults = {
      fromUtc: toDateTimeLocalValue(sevenDaysAgo),
      toUtc: toDateTimeLocalValue(now),
      entityType: '',
      userEmail: '',
      limit: '200',
    };
    setDeniedAuditFilters(defaults);
    await loadDeniedAttempts(defaults, null, { resetPaging: true });
  };

  const nextDeniedAttemptsPage = async () => {
    if (loadingDeniedAttempts || !deniedAttemptsHasMore || !deniedAttemptsNextCursor) return;
    setDeniedAttemptsCursorHistory((current) => [...current, deniedAttemptsCurrentCursor]);
    await loadDeniedAttempts(deniedAuditFilters, deniedAttemptsNextCursor);
  };

  const previousDeniedAttemptsPage = async () => {
    if (loadingDeniedAttempts || deniedAttemptsCursorHistory.length === 0) return;
    const previousCursor = deniedAttemptsCursorHistory[deniedAttemptsCursorHistory.length - 1];
    setDeniedAttemptsCursorHistory((current) => current.slice(0, -1));
    await loadDeniedAttempts(deniedAuditFilters, previousCursor);
  };

  const exportDeniedAttemptsCsv = async () => {
    if (deniedAttempts.length === 0 || exportingDeniedAttempts) return;
    setExportingDeniedAttempts(true);
    try {
      const csv = buildDeniedAttemptsCsv(deniedAttempts);
      const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const stamp = new Date().toISOString().replace(/[:]/g, '-').replace(/\..+$/, '');
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `denied-attempts-${stamp}.csv`;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      URL.revokeObjectURL(url);
    } finally {
      setExportingDeniedAttempts(false);
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
        const parsed = text ? JSON.parse(text) : null;
        logoPath = parsed?.logoPath || parsed?.logoFileName || null;
      } catch {
        logoPath = null;
      }

      if (logoPath) {
        setOnboardingSummary((current) => (current ? { ...current, logoPath } : current));
        setSchoolProfile((current) => ({ ...current, logoPath }));
        window.dispatchEvent(new Event('riseflow:school-brand-updated'));
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
          academicSystemProfileId: updated.academicSystemProfileId || current.academicSystemProfileId,
          academicSystemProfileCode: updated.academicSystemProfileCode || current.academicSystemProfileCode,
          academicSystemProfileName: updated.academicSystemProfileName || current.academicSystemProfileName,
          profilePromotionTransitionJson: updated.profilePromotionTransitionJson || current.profilePromotionTransitionJson,
          promotionTransitionOverrideJson: updated.promotionTransitionOverrideJson || current.promotionTransitionOverrideJson,
          effectivePromotionTransitionJson: updated.effectivePromotionTransitionJson || current.effectivePromotionTransitionJson,
          termsPerYear: Number.isInteger(updated.termsPerYear) ? updated.termsPerYear : current.termsPerYear,
        }));
      }

      await loadData({ background: true });
    } catch (e) {
      setSchoolProfileError(e.message || 'Could not update school profile.');
    } finally {
      setSavingSchoolProfile(false);
    }
  };

  const saveAcademicSystemProfile = async () => {
    if (savingAcademicProfile || !schoolProfile.academicSystemProfileId) return;

    setSavingAcademicProfile(true);
    setAcademicProfileError(null);
    try {
      const res = await apiFetch('/api/schools/profile/academic-system', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ academicSystemProfileId: schoolProfile.academicSystemProfileId }),
      });

      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not update academic system profile.');

      const updated = text ? JSON.parse(text) : null;
      if (updated) {
        const profileTransitionJson = updated.profilePromotionTransitionJson || null;
        const overrideTransitionJson = updated.promotionTransitionOverrideJson || null;
        const effectiveTransitionJson = updated.effectivePromotionTransitionJson || null;
        setSchoolProfile((current) => ({
          ...current,
          academicSystemProfileId: updated.academicSystemProfileId || current.academicSystemProfileId,
          academicSystemProfileCode: updated.academicSystemProfileCode || current.academicSystemProfileCode,
          academicSystemProfileName: updated.academicSystemProfileName || current.academicSystemProfileName,
          profilePromotionTransitionJson: profileTransitionJson,
          promotionTransitionOverrideJson: overrideTransitionJson,
          effectivePromotionTransitionJson: effectiveTransitionJson,
        }));
        setPromotionTransitionDraft(overrideTransitionJson || effectiveTransitionJson || '');
      }

      await loadData({ background: true });
    } catch (e) {
      setAcademicProfileError(e.message || 'Could not update academic system profile.');
    } finally {
      setSavingAcademicProfile(false);
    }
  };

  const normalizeJsonForEditor = (raw) => {
    return formatTransitionJsonForEditor(raw);
  };

  const discardLocalTransitionDraft = () => {
    setPromotionTransitionDraft(serverTransitionDraftValue);
    setPromotionTransitionError(null);
    setTransitionPanelNotice(null);
    setIsTransitionDraftFromCache(false);

    if (currentSchoolId && typeof localStorage !== 'undefined') {
      try {
        localStorage.removeItem(buildTransitionDraftStorageKey(currentSchoolId));
      } catch {
        // ignore storage write errors
      }
    }
  };

  const revertEditorToProfileDefaultTransitions = () => {
    const profileDraft = formatTransitionJsonForEditor(schoolProfile.profilePromotionTransitionJson || '');
    if (!profileDraft) {
      setPromotionTransitionError('No profile-default transition map is available for the selected academic profile.');
      return;
    }

    setPromotionTransitionDraft(profileDraft);
    setPromotionTransitionError(null);
    setTransitionPanelNotice(null);
    setIsTransitionDraftFromCache(false);
  };

  const resetTransitionPanelPreferences = () => {
    setPromotionTransitionDraft(serverTransitionDraftValue);
    setIsTransitionDraftFromCache(false);
    setTreatTerminalGradesAsValid(true);
    setTransitionDiffFilter('all');
    setPromotionTransitionError(null);
    setTransitionPanelNotice('Panel preferences reset.');

    if (currentSchoolId && typeof localStorage !== 'undefined') {
      try {
        localStorage.removeItem(buildTransitionDraftStorageKey(currentSchoolId));
        localStorage.removeItem(buildTerminalGradeToggleStorageKey(currentSchoolId));
        localStorage.removeItem(buildTransitionDiffFilterStorageKey(currentSchoolId));
      } catch {
        // ignore storage write errors
      }
    }
  };

  const parsedTransitionDraft = useMemo(() => parseTransitionJson(promotionTransitionDraft), [promotionTransitionDraft]);
  const transitionMap = parsedTransitionDraft.map;
  const transitionParseError = parsedTransitionDraft.error;
  const parsedProfileTransition = useMemo(
    () => parseTransitionJson(schoolProfile.profilePromotionTransitionJson || ''),
    [schoolProfile.profilePromotionTransitionJson],
  );
  const profileTransitionMap = parsedProfileTransition.map;

  const transitionGradeOptions = useMemo(() => {
    const values = [
      ...grades.map((item) => item.name).filter(Boolean),
      ...classes.map((item) => item.gradeName).filter(Boolean),
      ...Object.keys(transitionMap),
      ...Object.values(transitionMap).flat(),
    ];
    return dedupeCaseInsensitive(values).sort((a, b) => a.localeCompare(b));
  }, [grades, classes, transitionMap]);

  const transitionImpact = useMemo(() => {
    const knownGrades = dedupeCaseInsensitive([
      ...grades.map((item) => item.name).filter(Boolean),
      ...classes.map((item) => item.gradeName).filter(Boolean),
    ]);

    const knownGradeKeyToName = new Map(knownGrades.map((name) => [normalizeNameKey(name), name]));
    const sourceGrades = Object.keys(transitionMap || {});
    const sourceKeyToName = new Map(sourceGrades.map((name) => [normalizeNameKey(name), name]));

    const allTargetGrades = dedupeCaseInsensitive(Object.values(transitionMap || {}).flat());
    const targetKeys = new Set(allTargetGrades.map((name) => normalizeNameKey(name)));

    const unknownSourceGrades = sourceGrades.filter((name) => !knownGradeKeyToName.has(normalizeNameKey(name)));
    const unknownTargetGrades = allTargetGrades.filter((name) => !knownGradeKeyToName.has(normalizeNameKey(name)));

    const terminalGradeKeys = new Set();
    if (treatTerminalGradesAsValid) {
      const gradeLevels = grades
        .filter((item) => !!item?.name)
        .map((item) => ({
          key: normalizeNameKey(item.name),
          levelOrder: Number(item.levelOrder ?? 0),
        }));

      const maxLevelOrder = gradeLevels.length > 0
        ? Math.max(...gradeLevels.map((item) => item.levelOrder))
        : null;

      gradeLevels.forEach((item) => {
        if (maxLevelOrder != null && item.levelOrder === maxLevelOrder) terminalGradeKeys.add(item.key);
      });

      knownGrades.forEach((gradeName) => {
        if (isLikelyTerminalGradeName(gradeName)) terminalGradeKeys.add(normalizeNameKey(gradeName));
      });
    }

    const gradesWithoutOutgoingPath = knownGrades.filter((gradeName) => {
      const key = normalizeNameKey(gradeName);
      if (terminalGradeKeys.has(key)) return false;
      return !sourceKeyToName.has(key);
    });

    const classesByGrade = classes.reduce((acc, schoolClass) => {
      const gradeName = String(schoolClass?.gradeName || '').trim();
      if (!gradeName) return acc;
      const key = normalizeNameKey(gradeName);
      const current = acc[key] || [];
      current.push(schoolClass.name || 'Unnamed class');
      acc[key] = current;
      return acc;
    }, {});

    const classesAtUnmappedGrades = Object.entries(classesByGrade)
      .filter(([gradeKey]) => !sourceKeyToName.has(gradeKey) && !terminalGradeKeys.has(gradeKey))
      .map(([gradeKey, classNames]) => ({
        gradeName: knownGradeKeyToName.get(gradeKey) || gradeKey,
        classNames,
      }));

    return {
      knownGradeCount: knownGrades.length,
      sourceGradeCount: sourceGrades.length,
      unknownSourceGrades,
      unknownTargetGrades,
      terminalGradeNames: knownGrades.filter((gradeName) => terminalGradeKeys.has(normalizeNameKey(gradeName))),
      gradesWithoutOutgoingPath,
      classesAtUnmappedGrades,
    };
  }, [classes, grades, transitionMap, treatTerminalGradesAsValid]);

  const transitionDiff = useMemo(() => {
    const draftSources = Object.keys(transitionMap || {});
    const profileSources = Object.keys(profileTransitionMap || {});

    const draftKeyToSource = new Map(draftSources.map((name) => [normalizeNameKey(name), name]));
    const profileKeyToSource = new Map(profileSources.map((name) => [normalizeNameKey(name), name]));
    const allSourceKeys = Array.from(new Set([...draftKeyToSource.keys(), ...profileKeyToSource.keys()]));

    const addedSources = [];
    const removedSources = [];
    const changedSources = [];

    allSourceKeys.forEach((sourceKey) => {
      const draftSource = draftKeyToSource.get(sourceKey);
      const profileSource = profileKeyToSource.get(sourceKey);

      const draftTargets = draftSource
        ? dedupeCaseInsensitive(transitionMap[draftSource] || []).map((x) => normalizeNameKey(x))
        : [];
      const profileTargets = profileSource
        ? dedupeCaseInsensitive(profileTransitionMap[profileSource] || []).map((x) => normalizeNameKey(x))
        : [];

      if (!profileSource && draftSource) {
        addedSources.push(draftSource);
        return;
      }

      if (profileSource && !draftSource) {
        removedSources.push(profileSource);
        return;
      }

      const draftSet = new Set(draftTargets);
      const profileSet = new Set(profileTargets);
      const sameSize = draftSet.size === profileSet.size;
      const sameEntries = sameSize && [...draftSet].every((entry) => profileSet.has(entry));

      if (!sameEntries) {
        changedSources.push({
          source: draftSource || profileSource,
          draftTargets: dedupeCaseInsensitive(transitionMap[draftSource] || []),
          profileTargets: dedupeCaseInsensitive(profileTransitionMap[profileSource] || []),
        });
      }
    });

    return {
      addedSources: addedSources.sort((a, b) => a.localeCompare(b)),
      removedSources: removedSources.sort((a, b) => a.localeCompare(b)),
      changedSources: changedSources.sort((a, b) => String(a.source || '').localeCompare(String(b.source || ''))),
      isDifferent: addedSources.length > 0 || removedSources.length > 0 || changedSources.length > 0,
    };
  }, [transitionMap, profileTransitionMap]);

  const transitionDiffCounts = useMemo(() => ({
    added: transitionDiff.addedSources.length,
    removed: transitionDiff.removedSources.length,
    changed: transitionDiff.changedSources.length,
    total: transitionDiff.addedSources.length + transitionDiff.removedSources.length + transitionDiff.changedSources.length,
  }), [transitionDiff]);

  const showAddedDiff = transitionDiffFilter === 'all' || transitionDiffFilter === 'added';
  const showRemovedDiff = transitionDiffFilter === 'all' || transitionDiffFilter === 'removed';
  const showChangedDiff = transitionDiffFilter === 'all' || transitionDiffFilter === 'changed';

  const applyTransitionMapDraft = (map) => {
    setPromotionTransitionDraft(toPrettyTransitionJson(map));
  };

  const addTransitionRule = () => {
    const source = transitionSourceInput.trim();
    const target = transitionTargetInput.trim();

    if (!source || !target) {
      setPromotionTransitionError('Select or type both source and target grade names.');
      return;
    }

    if (transitionParseError) {
      setPromotionTransitionError('Fix JSON syntax first, or click Use profile defaults and start again.');
      return;
    }

    const next = { ...transitionMap };
    const existingTargets = Array.isArray(next[source]) ? next[source] : [];
    next[source] = dedupeCaseInsensitive([...existingTargets, target]);
    applyTransitionMapDraft(next);
    setPromotionTransitionError(null);
    setTransitionPanelNotice(null);
    setTransitionTargetInput('');
  };

  const removeTransitionTarget = (source, target) => {
    if (transitionParseError) return;

    const next = { ...transitionMap };
    const remaining = (next[source] || []).filter((item) => String(item).toLowerCase() !== String(target).toLowerCase());
    if (remaining.length === 0) {
      delete next[source];
    } else {
      next[source] = remaining;
    }
    applyTransitionMapDraft(next);
    setTransitionPanelNotice(null);
  };

  const removeTransitionSource = (source) => {
    if (transitionParseError) return;
    const next = { ...transitionMap };
    delete next[source];
    applyTransitionMapDraft(next);
    setTransitionPanelNotice(null);
  };

  const initializeTransitionFromSchoolGrades = () => {
    const ordered = [...grades]
      .filter((grade) => !!grade?.name)
      .sort((a, b) => {
        const ao = Number(a?.levelOrder ?? 0);
        const bo = Number(b?.levelOrder ?? 0);
        if (ao !== bo) return ao - bo;
        return String(a?.name || '').localeCompare(String(b?.name || ''));
      });

    if (ordered.length < 2) {
      setPromotionTransitionError('Add at least two grades first, then initialize promotion rules.');
      return;
    }

    const next = {};
    for (let i = 0; i < ordered.length - 1; i += 1) {
      const source = String(ordered[i].name || '').trim();
      const target = String(ordered[i + 1].name || '').trim();
      if (!source || !target) continue;
      next[source] = dedupeCaseInsensitive([...(next[source] || []), target]);
    }

    applyTransitionMapDraft(next);
    setPromotionTransitionError(null);
    setTransitionPanelNotice(null);
  };

  const savePromotionTransitionOverride = async () => {
    if (savingPromotionTransition) return;

    setSavingPromotionTransition(true);
    setPromotionTransitionError(null);
    setTransitionPanelNotice(null);
    try {
      const res = await apiFetch('/api/schools/profile/promotion-transition', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ promotionTransitionJson: promotionTransitionDraft }),
      });

      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not save promotion transition rules.');

      const updated = text ? JSON.parse(text) : null;
      if (updated) {
        const profileTransitionJson = updated.profilePromotionTransitionJson || null;
        const overrideTransitionJson = updated.promotionTransitionOverrideJson || null;
        const effectiveTransitionJson = updated.effectivePromotionTransitionJson || null;
        setSchoolProfile((current) => ({
          ...current,
          profilePromotionTransitionJson: profileTransitionJson,
          promotionTransitionOverrideJson: overrideTransitionJson,
          effectivePromotionTransitionJson: effectiveTransitionJson,
        }));
        setPromotionTransitionDraft(normalizeJsonForEditor(overrideTransitionJson || effectiveTransitionJson || ''));
        setIsTransitionDraftFromCache(false);
        if (currentSchoolId && typeof localStorage !== 'undefined') {
          try {
            localStorage.removeItem(buildTransitionDraftStorageKey(currentSchoolId));
          } catch {
            // ignore storage write errors
          }
        }
      }
    } catch (e) {
      setPromotionTransitionError(e.message || 'Could not save promotion transition rules.');
    } finally {
      setSavingPromotionTransition(false);
    }
  };

  const resetPromotionTransitionsToProfileDefault = async () => {
    if (savingPromotionTransition) return;

    setSavingPromotionTransition(true);
    setPromotionTransitionError(null);
    setTransitionPanelNotice(null);
    try {
      const res = await apiFetch('/api/schools/profile/promotion-transition', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ useProfileDefault: true }),
      });

      const text = await res.text().catch(() => '');
      if (!res.ok) throw new Error(text || 'Could not reset promotion transition rules.');

      const updated = text ? JSON.parse(text) : null;
      if (updated) {
        const profileTransitionJson = updated.profilePromotionTransitionJson || null;
        const overrideTransitionJson = updated.promotionTransitionOverrideJson || null;
        const effectiveTransitionJson = updated.effectivePromotionTransitionJson || null;
        setSchoolProfile((current) => ({
          ...current,
          profilePromotionTransitionJson: profileTransitionJson,
          promotionTransitionOverrideJson: overrideTransitionJson,
          effectivePromotionTransitionJson: effectiveTransitionJson,
        }));
        setPromotionTransitionDraft(normalizeJsonForEditor(effectiveTransitionJson || ''));
        setIsTransitionDraftFromCache(false);
        if (currentSchoolId && typeof localStorage !== 'undefined') {
          try {
            localStorage.removeItem(buildTransitionDraftStorageKey(currentSchoolId));
          } catch {
            // ignore storage write errors
          }
        }
      }
    } catch (e) {
      setPromotionTransitionError(e.message || 'Could not reset promotion transition rules.');
    } finally {
      setSavingPromotionTransition(false);
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
        const parsed = text ? JSON.parse(text) : null;
        docPath = parsed?.registrationDocumentPath || null;
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
  const activeTeachers = dashboard?.teacherCount ?? teacherCount;
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
  const governancePanelStyle = {
    marginTop: '1rem',
    background: '#f8fafc',
    border: '1px solid #cbd5e1',
    color: '#0f172a',
  };
  const governanceLabelStyle = { color: '#0f172a', fontWeight: 600 };
  const governanceDescStyle = { color: '#334155' };
  const governanceInputStyle = { color: '#0f172a', background: '#ffffff', borderColor: '#94a3b8' };
  const governanceTableStyle = { color: '#0f172a' };
  const governanceCheckboxStyle = { accentColor: '#1d4ed8', width: '1rem', height: '1rem' };

  return (
    <PageLayout title="School Admin" role="school">
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
        <Link to="/school/operations" className="btn-primary-action btn-primary-action--ghost">School profile</Link>
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
            <span className="summary-value">{activeTeachers}</span>
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
      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>People</h2>
      {teachers.length === 0 ? (
        <p className="empty-state">No teachers or staff yet.</p>
      ) : (
        <>
          <div className="form-actions" style={{ marginBottom: '0.75rem', justifyContent: 'space-between', gap: '0.75rem', flexWrap: 'wrap' }}>
            <label className="form-field" style={{ marginBottom: 0 }}>
              Role filter
              <select className="form-input" value={peopleRoleFilter} onChange={(e) => setPeopleRoleFilter(e.target.value)}>
                <option value="all">All people</option>
                {peopleRoleOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}s ({option.count})
                  </option>
                ))}
              </select>
            </label>
            <span className="card-desc">Showing {filteredPeople.length} of {peopleRows.length} people.</span>
          </div>
          <p className="card-desc" style={{ marginBottom: '0.75rem' }}>
            Teacher contact details stay hidden here until you click <strong>View details</strong>.
          </p>
          <div className="data-table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Account role</th>
                  <th>Role</th>
                  <th>Department</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {filteredPeople.map((person) => (
                  <tr key={person.id}>
                    <td>{person.fullName || '—'}</td>
                    <td>{person.accountRole}</td>
                    <td>{person.roleLabel || person.accountRole}</td>
                    <td>{person.department || '—'}</td>
                    <td>{person.isActive ? 'Active' : 'Inactive'}</td>
                    <td>
                      {person.sourceType === 'teacher' ? (
                        <button
                          type="button"
                          className="btn-primary-action btn-primary-action--ghost"
                          onClick={() => openTeacherProfile(person.sourceId)}
                        >
                          {selectedTeacherId === person.sourceId ? 'Hide details' : 'View details'}
                        </button>
                      ) : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {loadingTeacherProfile && (
            <p className="card-desc" style={{ marginTop: '0.75rem' }}>Loading selected teacher details…</p>
          )}

          <section className="dashboard-panel" style={governancePanelStyle} aria-label="Teacher profile governance">
            <div className="form-actions" style={{ justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
              <h3 className="card-title" style={{ margin: 0 }}>Teacher field controls</h3>
              <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => setShowTeacherFieldControls((current) => !current)}>
                {showTeacherFieldControls ? 'Hide controls' : 'Open controls'}
              </button>
            </div>
            <p className="card-desc" style={governanceDescStyle}>Control what teachers can see or edit. Salary, allowances and recognitions can be locked here. You can also add custom fields.</p>
            {showTeacherFieldControls && (
            <>
            <div className="form-grid" style={{ marginTop: '0.75rem' }}>
              <label className="form-field" style={governanceLabelStyle}>Custom field label
                <input
                  className="form-input"
                  style={governanceInputStyle}
                  value={newCustomField.displayName}
                  onChange={(e) => setNewCustomField((prev) => ({ ...prev, displayName: e.target.value }))}
                  placeholder="e.g. Teaching License Expiry"
                />
              </label>
              <label className="form-field" style={governanceLabelStyle}>Custom field key
                <input
                  className="form-input"
                  style={governanceInputStyle}
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
              <table className="data-table" style={governanceTableStyle}>
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
                          style={governanceCheckboxStyle}
                          checked={!!setting.isVisibleToTeacher}
                          disabled={savingFieldSettingKey === setting.fieldKey}
                          onChange={(e) => toggleSetting(setting, { isVisibleToTeacher: e.target.checked })}
                        />
                      </td>
                      <td>
                        <input
                          type="checkbox"
                          style={governanceCheckboxStyle}
                          checked={!!setting.isEditableByTeacher}
                          disabled={savingFieldSettingKey === setting.fieldKey || !!setting.isAdminOnly}
                          onChange={(e) => toggleSetting(setting, { isEditableByTeacher: e.target.checked })}
                        />
                      </td>
                      <td>
                        <input
                          type="checkbox"
                          style={governanceCheckboxStyle}
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
            </>
            )}
          </section>

          <section className="dashboard-panel" style={governancePanelStyle} aria-label="School hierarchy catalog">
            <div className="form-actions" style={{ justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
              <h3 className="card-title" style={{ margin: 0 }}>School hierarchy catalog</h3>
              <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => setShowSchoolHierarchyCatalog((current) => !current)}>
                {showSchoolHierarchyCatalog ? 'Hide catalog' : 'Open catalog'}
              </button>
            </div>
            <p className="card-desc" style={governanceDescStyle}>
              Manage your school&apos;s role catalog and governance matrix. These settings are saved for your school and reused across teacher assignments.
            </p>
            {showSchoolHierarchyCatalog && (
            <>
            <div className="data-table-wrap" style={{ marginTop: '0.75rem' }}>
              <table className="data-table" style={governanceTableStyle}>
                <thead>
                  <tr>
                    <th>Role title</th>
                    <th>Stage scope</th>
                    <th>Order</th>
                    <th>Source</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {staffCatalogRoles.map((role) => (
                    <tr key={`${role.roleCode}-${role.roleTitle}`}>
                      <td>{role.roleTitle}</td>
                      <td>{role.stageScope || 'Whole School'}</td>
                      <td>{role.hierarchyOrder || '—'}</td>
                      <td>{role.isSystemDefault ? 'Default' : 'Custom'}</td>
                      <td>
                        {role.isSystemDefault ? '—' : (
                          <button
                            type="button"
                            className="btn-primary-action btn-primary-action--ghost"
                            onClick={() => removeCustomHierarchyRole(role.roleTitle)}
                          >
                            Remove
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="form-actions" style={{ marginTop: '0.75rem', flexWrap: 'wrap' }}>
              <input
                className="form-input"
                style={{ ...governanceInputStyle, minWidth: '220px' }}
                value={customHierarchyRoleDraft.roleTitle}
                onChange={(e) => setCustomHierarchyRoleDraft((current) => ({ ...current, roleTitle: e.target.value }))}
                placeholder="Custom role title"
              />
              <select
                className="form-input"
                style={{ ...governanceInputStyle, minWidth: '220px' }}
                value={customHierarchyRoleDraft.stageScope}
                onChange={(e) => setCustomHierarchyRoleDraft((current) => ({ ...current, stageScope: e.target.value }))}
              >
                <option value="">— Stage scope —</option>
                {stageScopeOptions.map((scope) => <option key={scope} value={scope}>{scope}</option>)}
              </select>
              <input
                className="form-input"
                style={{ ...governanceInputStyle, width: '150px' }}
                type="number"
                min="1"
                value={customHierarchyRoleDraft.hierarchyOrder}
                onChange={(e) => setCustomHierarchyRoleDraft((current) => ({ ...current, hierarchyOrder: e.target.value }))}
                placeholder="Order"
              />
              <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={addCustomHierarchyRole}>
                Add custom role
              </button>
            </div>

            <h4 className="card-title" style={{ marginTop: '1rem' }}>Permission matrix</h4>
            <div className="data-table-wrap" style={{ marginTop: '0.5rem' }}>
              <table className="data-table" style={governanceTableStyle}>
                <thead>
                  <tr>
                    <th>Role</th>
                    <th>Manage teachers</th>
                    <th>Assign classes</th>
                    <th>Approve results</th>
                    <th>Broadcast to parents</th>
                    <th>Manage fees</th>
                  </tr>
                </thead>
                <tbody>
                  {staffPermissionMatrixDraft.map((row) => (
                    <tr key={`perm-${row.roleTitle}`}>
                      <td>{row.roleTitle}</td>
                      <td><input type="checkbox" style={governanceCheckboxStyle} checked={!!row.canManageTeachers} onChange={(e) => updatePermissionRule(row.roleTitle, 'canManageTeachers', e.target.checked)} /></td>
                      <td><input type="checkbox" style={governanceCheckboxStyle} checked={!!row.canAssignClasses} onChange={(e) => updatePermissionRule(row.roleTitle, 'canAssignClasses', e.target.checked)} /></td>
                      <td><input type="checkbox" style={governanceCheckboxStyle} checked={!!row.canApproveResults} onChange={(e) => updatePermissionRule(row.roleTitle, 'canApproveResults', e.target.checked)} /></td>
                      <td><input type="checkbox" style={governanceCheckboxStyle} checked={!!row.canSendParentBroadcasts} onChange={(e) => updatePermissionRule(row.roleTitle, 'canSendParentBroadcasts', e.target.checked)} /></td>
                      <td><input type="checkbox" style={governanceCheckboxStyle} checked={!!row.canManageFees} onChange={(e) => updatePermissionRule(row.roleTitle, 'canManageFees', e.target.checked)} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="form-actions" style={{ marginTop: '0.75rem' }}>
              <button
                type="button"
                className="btn-primary-action"
                onClick={saveStaffStructureConfig}
                disabled={savingStaffStructureConfig}
              >
                {savingStaffStructureConfig ? 'Saving hierarchy config…' : 'Save school hierarchy config'}
              </button>
              {staffStructureConfig?.updatedAtUtc && (
                <span className="card-desc">
                  Last updated: {new Date(staffStructureConfig.updatedAtUtc).toLocaleString()}
                  {staffStructureConfig.updatedBy ? ` by ${staffStructureConfig.updatedBy}` : ''}
                </span>
              )}
            </div>
            </>
            )}
          </section>

          <section className="progress-section" style={governancePanelStyle} aria-label="Denied permission attempts">
            <div className="form-actions" style={{ justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
              <h3 className="card-title" style={{ margin: 0 }}>Denied permission attempts</h3>
              <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={() => setShowDeniedPermissionAttempts((current) => !current)}>
                {showDeniedPermissionAttempts ? 'Hide log' : 'Open log'}
              </button>
            </div>
            <p className="card-desc" style={governanceDescStyle}>
              Review denied teacher actions captured by the staff hierarchy matrix. Filter and export this log for governance reviews.
            </p>
            {showDeniedPermissionAttempts && (
            <>
            <div className="form-grid" style={{ marginTop: '0.75rem' }}>
              <label className="form-field" style={governanceLabelStyle}>From
                <input
                  className="form-input"
                  style={governanceInputStyle}
                  type="datetime-local"
                  value={deniedAuditFilters.fromUtc}
                  onChange={(e) => setDeniedAuditFilters((current) => ({ ...current, fromUtc: e.target.value }))}
                />
              </label>
              <label className="form-field" style={governanceLabelStyle}>To
                <input
                  className="form-input"
                  style={governanceInputStyle}
                  type="datetime-local"
                  value={deniedAuditFilters.toUtc}
                  onChange={(e) => setDeniedAuditFilters((current) => ({ ...current, toUtc: e.target.value }))}
                />
              </label>
              <label className="form-field" style={governanceLabelStyle}>Entity type
                <select
                  className="form-input"
                  style={governanceInputStyle}
                  value={deniedAuditFilters.entityType}
                  onChange={(e) => setDeniedAuditFilters((current) => ({ ...current, entityType: e.target.value }))}
                >
                  <option value="">All entities</option>
                  {deniedEntityTypeOptions.map((entityType) => (
                    <option key={entityType} value={entityType}>{entityType}</option>
                  ))}
                </select>
              </label>
              <label className="form-field" style={governanceLabelStyle}>Teacher email
                <input
                  className="form-input"
                  style={governanceInputStyle}
                  type="email"
                  placeholder="teacher@school.com"
                  value={deniedAuditFilters.userEmail}
                  onChange={(e) => setDeniedAuditFilters((current) => ({ ...current, userEmail: e.target.value }))}
                />
              </label>
              <label className="form-field" style={governanceLabelStyle}>Result limit
                <select
                  className="form-input"
                  style={governanceInputStyle}
                  value={deniedAuditFilters.limit}
                  onChange={(e) => setDeniedAuditFilters((current) => ({ ...current, limit: e.target.value }))}
                >
                  <option value="100">100</option>
                  <option value="200">200</option>
                  <option value="500">500</option>
                  <option value="1000">1000</option>
                </select>
              </label>
            </div>

            <div className="form-actions" style={{ marginTop: '0.75rem', flexWrap: 'wrap' }}>
              <button type="button" className="btn-primary-action" onClick={applyDeniedAuditFilters} disabled={loadingDeniedAttempts}>
                {loadingDeniedAttempts ? 'Loading denied attempts…' : 'Apply filters'}
              </button>
              <button type="button" className="btn-primary-action btn-primary-action--ghost" onClick={resetDeniedAuditFilters} disabled={loadingDeniedAttempts}>
                Reset to last 7 days
              </button>
              <button
                type="button"
                className="btn-primary-action btn-primary-action--ghost"
                onClick={exportDeniedAttemptsCsv}
                disabled={loadingDeniedAttempts || deniedAttempts.length === 0 || exportingDeniedAttempts}
              >
                {exportingDeniedAttempts ? 'Exporting…' : `Export CSV (${deniedAttempts.length})`}
              </button>
              <button
                type="button"
                className="btn-primary-action btn-primary-action--ghost"
                onClick={previousDeniedAttemptsPage}
                disabled={loadingDeniedAttempts || deniedAttemptsCursorHistory.length === 0}
              >
                Previous page
              </button>
              <button
                type="button"
                className="btn-primary-action btn-primary-action--ghost"
                onClick={nextDeniedAttemptsPage}
                disabled={loadingDeniedAttempts || !deniedAttemptsHasMore || !deniedAttemptsNextCursor}
              >
                Next page
              </button>
            </div>

            <p className="card-desc" style={{ marginTop: '0.35rem' }}>
              Page {deniedAttemptsPageNumber} • Page size: {deniedAttempts.length} • Fetched this session: {deniedAttemptsFetchedTotal} • {deniedAttemptsHasMore ? 'More results available.' : 'End of results.'}
            </p>

            {deniedAttemptsError && (
              <p className="empty-state empty-state--error" style={{ marginTop: '0.75rem' }}>{deniedAttemptsError}</p>
            )}

            {!deniedAttemptsError && deniedAttempts.length === 0 && !loadingDeniedAttempts && (
              <p className="empty-state" style={{ marginTop: '0.75rem' }}>No denied attempts found for the selected filters.</p>
            )}

            {deniedAttempts.length > 0 && (
              <div className="data-table-wrap" style={{ marginTop: '0.75rem' }}>
                <table className="data-table" style={governanceTableStyle}>
                  <thead>
                    <tr>
                      <th>When</th>
                      <th>Teacher</th>
                      <th>Entity</th>
                      <th>Action</th>
                      <th>Details</th>
                    </tr>
                  </thead>
                  <tbody>
                    {deniedAttempts.map((item) => (
                      <tr key={item.id}>
                        <td>{formatAuditTimestamp(item.createdAtUtc)}</td>
                        <td>
                          <div>{item.userEmail || '—'}</div>
                          <div className="card-desc" style={{ marginTop: '0.15rem' }}>{item.userName || '—'}</div>
                        </td>
                        <td>
                          <div>{item.entityType || '—'}</div>
                          <div className="card-desc" style={{ marginTop: '0.15rem' }}>ID: {item.entityId || '—'}</div>
                        </td>
                        <td>{item.action || '—'}</td>
                        <td>{item.details || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            </>
            )}
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
                  <p className="dashboard-label" style={governanceLabelStyle}>Hierarchy role setup</p>
                  <div className="form-actions" style={{ marginTop: '0.5rem', flexWrap: 'wrap' }}>
                    <select
                      className="form-input"
                      style={{ ...governanceInputStyle, minWidth: '220px' }}
                      value={selectedTeacherRoleTitle}
                      onChange={(e) => setSelectedTeacherRoleTitle(e.target.value)}
                    >
                      <option value="">— Select hierarchy role —</option>
                      {staffRoleTitles.map((roleTitle) => (
                        <option key={roleTitle} value={roleTitle}>{roleTitle}</option>
                      ))}
                      <option value="custom">Custom role</option>
                    </select>

                    {selectedTeacherRoleTitle === 'custom' && (
                      <input
                        className="form-input"
                        style={{ ...governanceInputStyle, minWidth: '220px' }}
                        value={customTeacherRoleTitle}
                        onChange={(e) => setCustomTeacherRoleTitle(e.target.value)}
                        placeholder="e.g. Assistant Head Teacher"
                      />
                    )}

                    <select
                      className="form-input"
                      style={{ ...governanceInputStyle, minWidth: '220px' }}
                      value={selectedTeacherDepartment}
                      onChange={(e) => setSelectedTeacherDepartment(e.target.value)}
                    >
                      <option value="">— Select stage scope / department —</option>
                      {stageScopeOptions.map((scope) => (
                        <option key={scope} value={scope}>{scope}</option>
                      ))}
                    </select>

                    <button
                      type="button"
                      className="btn-primary-action"
                      onClick={updateTeacherRoleProfile}
                      disabled={savingTeacherRoleProfile}
                    >
                      {savingTeacherRoleProfile ? 'Saving role…' : 'Save hierarchy role'}
                    </button>
                  </div>
                  <p className="card-desc" style={{ ...governanceDescStyle, marginTop: '0.5rem' }}>
                    {staffStructureOptions?.countryName
                      ? `Country reference: ${staffStructureOptions.countryName} (${staffStructureOptions.countryCode || '—'}).`
                      : 'Country-specific hierarchy references are loading.'}
                  </p>
                </article>
                <article className="dashboard-card" style={{ gridColumn: '1 / -1' }}>
                  <p className="dashboard-label" style={governanceLabelStyle}>Class assignment</p>
                  <div className="form-actions" style={{ marginTop: '0.5rem', flexWrap: 'wrap' }}>
                    <select
                      className="form-input"
                      style={{ ...governanceInputStyle, minWidth: '220px' }}
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
                    <select
                      className="form-input"
                      style={{ ...governanceInputStyle, minWidth: '220px' }}
                      value={teacherAssignRoleInClass}
                      onChange={(e) => setTeacherAssignRoleInClass(e.target.value)}
                      disabled={assigningTeacherClass}
                    >
                      <option value="">— Role in class (optional) —</option>
                      {classRoleOptions.map((role) => (
                        <option key={role} value={role}>{role}</option>
                      ))}
                      <option value="custom">Custom class role</option>
                    </select>
                    {teacherAssignRoleInClass === 'custom' && (
                      <input
                        className="form-input"
                        style={{ ...governanceInputStyle, minWidth: '220px' }}
                        value={customTeacherAssignRoleInClass}
                        onChange={(e) => setCustomTeacherAssignRoleInClass(e.target.value)}
                        placeholder="e.g. Assistant Class Teacher"
                        disabled={assigningTeacherClass}
                      />
                    )}
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
                      const assignedRole = (selectedTeacher.teacherClasses || []).find((item) => item.classId === classId)?.roleInClass;
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
                          {removingTeacherClassId === classId ? 'Removing…' : `Remove ${label}${assignedRole ? ` • ${assignedRole}` : ''}`}
                        </button>
                      );
                    })}
                  </div>
                </article>
                <article className="dashboard-card" style={{ gridColumn: '1 / -1' }}>
                  <p className="dashboard-label" style={governanceLabelStyle}>Subject to class assignment</p>
                  <div className="form-actions" style={{ marginTop: '0.5rem', flexWrap: 'wrap' }}>
                    <select
                      className="form-input"
                      style={{ ...governanceInputStyle, minWidth: '220px' }}
                      value={classSubjectClassId}
                      onChange={(e) => setClassSubjectClassId(e.target.value)}
                      disabled={savingClassSubject || classes.length === 0}
                    >
                      <option value="">- Select class -</option>
                      {classes.map((schoolClass) => (
                        <option key={schoolClass.id} value={schoolClass.id}>
                          {schoolClass.name}{schoolClass.gradeName ? ` (${schoolClass.gradeName})` : ''}
                        </option>
                      ))}
                    </select>
                    <select
                      className="form-input"
                      style={{ ...governanceInputStyle, minWidth: '220px' }}
                      value={classSubjectSubjectId}
                      onChange={(e) => setClassSubjectSubjectId(e.target.value)}
                      disabled={savingClassSubject || subjects.length === 0}
                    >
                      <option value="">- Select subject -</option>
                      {subjects.map((subject) => (
                        <option key={subject.id} value={subject.id}>
                          {subject.name}
                        </option>
                      ))}
                    </select>
                    <button
                      type="button"
                      className="btn-primary-action"
                      onClick={assignSubjectToClass}
                      disabled={savingClassSubject || !classSubjectClassId || !classSubjectSubjectId}
                    >
                      {savingClassSubject ? 'Saving…' : 'Assign subject to class'}
                    </button>
                  </div>
                  <div className="form-actions" style={{ marginTop: '0.5rem', flexWrap: 'wrap' }}>
                    <button
                      type="button"
                      className="btn-primary-action btn-primary-action--ghost"
                      onClick={() => unassignSubjectFromClass(classSubjectClassId, classSubjectSubjectId)}
                      disabled={!classSubjectClassId || !classSubjectSubjectId || removingClassSubjectKey === `${classSubjectClassId}:${classSubjectSubjectId}`}
                    >
                      {removingClassSubjectKey === `${classSubjectClassId}:${classSubjectSubjectId}` ? 'Removing…' : 'Remove selected class-subject'}
                    </button>
                  </div>
                  <p className="card-desc" style={{ ...governanceDescStyle, marginTop: '0.5rem' }}>
                    Pick a class and subject to map curriculum coverage. Remove actions apply to the selected class.
                  </p>
                </article>
                <article className="dashboard-card" style={{ gridColumn: '1 / -1' }}>
                  <p className="dashboard-label" style={governanceLabelStyle}>Teacher to class + subject assignment</p>
                  <div className="form-actions" style={{ marginTop: '0.5rem', flexWrap: 'wrap' }}>
                    <select
                      className="form-input"
                      style={{ ...governanceInputStyle, minWidth: '220px' }}
                      value={teacherSubjectClassId}
                      onChange={(e) => setTeacherSubjectClassId(e.target.value)}
                      disabled={savingTeacherClassSubject || classes.length === 0}
                    >
                      <option value="">- Select class -</option>
                      {classes.map((schoolClass) => (
                        <option key={schoolClass.id} value={schoolClass.id}>
                          {schoolClass.name}{schoolClass.gradeName ? ` (${schoolClass.gradeName})` : ''}
                        </option>
                      ))}
                    </select>
                    <select
                      className="form-input"
                      style={{ ...governanceInputStyle, minWidth: '220px' }}
                      value={teacherSubjectSubjectId}
                      onChange={(e) => setTeacherSubjectSubjectId(e.target.value)}
                      disabled={savingTeacherClassSubject || teacherSubjectOptions.length === 0}
                    >
                      <option value="">- Select subject -</option>
                      {teacherSubjectOptions.map((subject) => (
                        <option key={subject.id} value={subject.id}>
                          {subject.name}
                        </option>
                      ))}
                    </select>
                    <button
                      type="button"
                      className="btn-primary-action"
                      onClick={assignTeacherToClassSubject}
                      disabled={savingTeacherClassSubject || !teacherSubjectClassId || !teacherSubjectSubjectId}
                    >
                      {savingTeacherClassSubject ? 'Saving…' : 'Assign teacher to class subject'}
                    </button>
                  </div>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginTop: '0.75rem' }}>
                    {selectedTeacherClassSubjects.length === 0 && (
                      <span className="card-desc">No class-subject assignment yet.</span>
                    )}
                    {selectedTeacherClassSubjects.map((item) => {
                      const key = `${item.classId}:${item.subjectId}`;
                      return (
                        <button
                          key={key}
                          type="button"
                          className="btn-primary-action btn-primary-action--ghost"
                          onClick={() => unassignTeacherFromClassSubject(item.classId, item.subjectId)}
                          disabled={removingTeacherClassSubjectKey === key}
                          title="Remove teacher from this class subject"
                        >
                          {removingTeacherClassSubjectKey === key ? 'Removing…' : `Remove ${item.className} • ${item.subjectName}`}
                        </button>
                      );
                    })}
                  </div>
                  <p className="card-desc" style={{ ...governanceDescStyle, marginTop: '0.5rem' }}>
                    Assign each subject this teacher teaches in that class. For{' '}
                    <strong>secondary sections</strong> (JSS, SS, forms, SHS, etc.), the same subject teacher can be
                    given <strong>more than one subject</strong> in the same class — use Assign once per subject.
                    Map subjects to the class first under &quot;Subject to class assignment&quot; when needed.
                  </p>
                  {teacherSubjectClassId && teacherSubjectClassIsSecondary && (
                    <p className="card-desc" style={{ ...governanceDescStyle, marginTop: '0.35rem', color: '#0f766e' }}>
                      This class matches a secondary-style label — multiple subjects per teacher here are expected.
                    </p>
                  )}
                  <p className="card-desc" style={{ ...governanceDescStyle, marginTop: '0.35rem' }}>
                    You can assign class-subjects even if the teacher has not been assigned as a class teacher yet.
                  </p>
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

      <h2 className="section-title" style={{ marginTop: '1.5rem' }}>Share with staff</h2>
      <p className="card-desc">Share this link with support staff so they can sign up under your school and appear in your staff list.</p>
      <StaffSignupLink schoolIdFromApi={dashboard?.schoolId} />

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
      <section className="dashboard-panel" style={{ marginTop: '0.75rem' }} aria-label="Academic system profile">
        <h3 className="card-title">Academic system profile</h3>
        <p className="card-desc">
          Choose the education system your school follows. Switching profiles can change grade quick templates and future validation rules.
        </p>
        <div className="form-grid" style={{ marginTop: '0.75rem' }}>
          <label className="form-field form-field--full">Profile
            <select
              className="form-input"
              value={schoolProfile.academicSystemProfileId || ''}
              onChange={(e) => {
                const selected = academicProfiles.find((item) => item.id === e.target.value);
                onSchoolProfileFieldChange('academicSystemProfileId', e.target.value || null);
                onSchoolProfileFieldChange('academicSystemProfileCode', selected?.code || null);
                onSchoolProfileFieldChange('academicSystemProfileName', selected?.name || null);
              }}
            >
              <option value="">— Select academic system profile —</option>
              {academicProfiles.map((profile) => (
                <option key={profile.id} value={profile.id}>{profile.name} ({profile.code})</option>
              ))}
            </select>
          </label>
        </div>
        {schoolProfile.academicSystemProfileName && (
          <p className="card-desc" style={{ marginTop: '0.5rem' }}>
            Current: <strong>{schoolProfile.academicSystemProfileName}</strong>
            {schoolProfile.academicSystemProfileCode ? ` (${schoolProfile.academicSystemProfileCode})` : ''}
          </p>
        )}
        <p className="card-desc" style={{ marginTop: '0.5rem' }}>
          Warning: if your school already has active grade/class data, review mappings before changing this profile.
        </p>
        <div className="form-actions" style={{ marginTop: '0.75rem' }}>
          <button type="button" className="btn-primary-action" onClick={saveAcademicSystemProfile} disabled={savingAcademicProfile || !schoolProfile.academicSystemProfileId}>
            {savingAcademicProfile ? 'Saving profile…' : 'Save academic system profile'}
          </button>
        </div>
        {academicProfileError && <p className="empty-state empty-state--error" style={{ marginTop: '0.75rem' }}>{academicProfileError}</p>}
      </section>

      <section className="dashboard-panel" style={{ marginTop: '0.75rem' }} aria-label="Promotion transition rules">
        <h3 className="card-title">Promotion transition rules</h3>
        <p className="card-desc">
          Define exactly which target grade(s) each source grade can promote into. This override is school-specific and used when strict promotion validation is enabled.
        </p>
        {isTransitionDraftFromCache && (
          <p className="card-desc" style={{ marginTop: '0.35rem' }}>
            Draft restored from your local unsaved edits. Save promotion rules to sync this draft to your school profile.
          </p>
        )}
        <div className="form-grid" style={{ marginTop: '0.75rem' }}>
          <label className="form-field">Source grade
            <input
              className="form-input"
              list="promotion-grade-options"
              value={transitionSourceInput}
              onChange={(e) => setTransitionSourceInput(e.target.value)}
              placeholder="e.g. Primary 1"
            />
          </label>
          <label className="form-field">Allowed target grade
            <input
              className="form-input"
              list="promotion-grade-options"
              value={transitionTargetInput}
              onChange={(e) => setTransitionTargetInput(e.target.value)}
              placeholder="e.g. Primary 2"
            />
          </label>
          <div className="form-actions" style={{ alignSelf: 'end' }}>
            <button
              type="button"
              className="btn-primary-action"
              onClick={addTransitionRule}
              disabled={savingPromotionTransition}
            >
              Add rule
            </button>
          </div>
        </div>
        <datalist id="promotion-grade-options">
          {transitionGradeOptions.map((name) => <option key={name} value={name} />)}
        </datalist>
        {Object.keys(transitionMap).length > 0 && (
          <div className="data-table-wrap" style={{ marginTop: '0.75rem' }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Source grade</th>
                  <th>Allowed targets</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {Object.entries(transitionMap).map(([source, targets]) => (
                  <tr key={source}>
                    <td>{source}</td>
                    <td>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.4rem' }}>
                        {targets.map((target) => (
                          <button
                            key={`${source}-${target}`}
                            type="button"
                            className="btn-primary-action btn-primary-action--ghost"
                            style={{ padding: '0.2rem 0.55rem' }}
                            onClick={() => removeTransitionTarget(source, target)}
                            disabled={savingPromotionTransition}
                            title="Remove this target"
                          >
                            {target} ×
                          </button>
                        ))}
                      </div>
                    </td>
                    <td>
                      <button
                        type="button"
                        className="btn-primary-action btn-primary-action--ghost"
                        onClick={() => removeTransitionSource(source)}
                        disabled={savingPromotionTransition}
                      >
                        Remove source
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <div className="dashboard-card" style={{ marginTop: '0.75rem' }}>
          <p className="dashboard-label">Preview impact</p>
          <label className="form-field" style={{ marginTop: '0.5rem' }}>
            <span className="card-desc" style={{ display: 'inline-flex', gap: '0.5rem', alignItems: 'center' }}>
              <input
                type="checkbox"
                checked={treatTerminalGradesAsValid}
                onChange={(e) => setTreatTerminalGradesAsValid(e.target.checked)}
              />
              Treat terminal grades as valid endpoints
            </span>
          </label>
          <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
            Known grades: {transitionImpact.knownGradeCount} • Mapped source grades: {transitionImpact.sourceGradeCount}
          </p>
          {treatTerminalGradesAsValid && transitionImpact.terminalGradeNames.length > 0 && (
            <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
              Terminal grades ignored for outgoing-path warnings: {transitionImpact.terminalGradeNames.join(', ')}
            </p>
          )}
          {transitionImpact.unknownSourceGrades.length > 0 && (
            <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
              Unknown source grades in map: {transitionImpact.unknownSourceGrades.join(', ')}
            </p>
          )}
          {transitionImpact.unknownTargetGrades.length > 0 && (
            <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
              Unknown target grades in map: {transitionImpact.unknownTargetGrades.join(', ')}
            </p>
          )}
          {transitionImpact.gradesWithoutOutgoingPath.length > 0 && (
            <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
              Grades without outgoing paths: {transitionImpact.gradesWithoutOutgoingPath.join(', ')}
            </p>
          )}
          {transitionImpact.classesAtUnmappedGrades.length > 0 && (
            <div style={{ marginTop: '0.35rem' }}>
              {transitionImpact.classesAtUnmappedGrades.map((row) => (
                <p key={row.gradeName} className="dashboard-sub" style={{ marginTop: '0.2rem' }}>
                  Classes at unmapped grade {row.gradeName}: {row.classNames.join(', ')}
                </p>
              ))}
            </div>
          )}
          {transitionImpact.unknownSourceGrades.length === 0
            && transitionImpact.unknownTargetGrades.length === 0
            && transitionImpact.classesAtUnmappedGrades.length === 0
            && transitionImpact.gradesWithoutOutgoingPath.length === 0 && (
              <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
                Mapping looks aligned with your current school grades and classes.
              </p>
          )}
        </div>
        <div className="dashboard-card" style={{ marginTop: '0.75rem' }}>
          <p className="dashboard-label">Draft vs profile default</p>
          {schoolProfile.profilePromotionTransitionJson && !parsedProfileTransition.error && transitionDiff.isDifferent && (
            <div className="form-actions" style={{ marginTop: '0.5rem', flexWrap: 'wrap' }}>
              <button
                type="button"
                className={`btn-primary-action ${transitionDiffFilter === 'all' ? '' : 'btn-primary-action--ghost'}`}
                onClick={() => setTransitionDiffFilter('all')}
              >
                All ({transitionDiffCounts.total})
              </button>
              <button
                type="button"
                className={`btn-primary-action ${transitionDiffFilter === 'added' ? '' : 'btn-primary-action--ghost'}`}
                onClick={() => setTransitionDiffFilter('added')}
              >
                Added ({transitionDiffCounts.added})
              </button>
              <button
                type="button"
                className={`btn-primary-action ${transitionDiffFilter === 'removed' ? '' : 'btn-primary-action--ghost'}`}
                onClick={() => setTransitionDiffFilter('removed')}
              >
                Removed ({transitionDiffCounts.removed})
              </button>
              <button
                type="button"
                className={`btn-primary-action ${transitionDiffFilter === 'changed' ? '' : 'btn-primary-action--ghost'}`}
                onClick={() => setTransitionDiffFilter('changed')}
              >
                Changed ({transitionDiffCounts.changed})
              </button>
            </div>
          )}
          {!schoolProfile.profilePromotionTransitionJson && (
            <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
              No profile default transition map available for comparison.
            </p>
          )}
          {schoolProfile.profilePromotionTransitionJson && parsedProfileTransition.error && (
            <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
              Profile default transition map could not be parsed for diff.
            </p>
          )}
          {schoolProfile.profilePromotionTransitionJson && !parsedProfileTransition.error && !transitionDiff.isDifferent && (
            <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
              Draft matches profile defaults.
            </p>
          )}
          {schoolProfile.profilePromotionTransitionJson && !parsedProfileTransition.error && transitionDiff.isDifferent && (
            <>
              {showAddedDiff && transitionDiff.addedSources.length > 0 && (
                <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
                  Added source grades: {transitionDiff.addedSources.join(', ')}
                </p>
              )}
              {showRemovedDiff && transitionDiff.removedSources.length > 0 && (
                <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
                  Removed source grades: {transitionDiff.removedSources.join(', ')}
                </p>
              )}
              {showChangedDiff && transitionDiff.changedSources.length > 0 && (
                <div style={{ marginTop: '0.35rem' }}>
                  {transitionDiff.changedSources.slice(0, 6).map((item) => (
                    <p key={item.source} className="dashboard-sub" style={{ marginTop: '0.2rem' }}>
                      {item.source}: draft [{item.draftTargets.join(', ') || 'none'}] vs profile [{item.profileTargets.join(', ') || 'none'}]
                    </p>
                  ))}
                  {transitionDiff.changedSources.length > 6 && (
                    <p className="dashboard-sub" style={{ marginTop: '0.2rem' }}>
                      +{transitionDiff.changedSources.length - 6} more changed source mappings
                    </p>
                  )}
                </div>
              )}
              {((showAddedDiff && transitionDiff.addedSources.length === 0)
                || (showRemovedDiff && transitionDiff.removedSources.length === 0)
                || (showChangedDiff && transitionDiff.changedSources.length === 0))
                && transitionDiffFilter !== 'all' && (
                  <p className="dashboard-sub" style={{ marginTop: '0.35rem' }}>
                    No {transitionDiffFilter} differences.
                  </p>
              )}
            </>
          )}
        </div>
        <label className="form-field form-field--full" style={{ marginTop: '0.75rem' }}>Transition map JSON
          <textarea
            className="form-input"
            rows={12}
            value={promotionTransitionDraft}
            onChange={(e) => setPromotionTransitionDraft(e.target.value)}
            placeholder='{"Primary 1":["Primary 2"],"Primary 2":["Primary 3"]}'
            spellCheck={false}
          />
        </label>
        <p className="card-desc" style={{ marginTop: '0.5rem' }}>
          Current mode: {schoolProfile.promotionTransitionOverrideJson ? 'Custom school override' : 'Using academic profile defaults'}.
        </p>
        {transitionPanelNotice && <p className="card-desc card-desc--success" style={{ marginTop: '0.35rem' }}>{transitionPanelNotice}</p>}
        {transitionParseError && <p className="empty-state empty-state--error" style={{ marginTop: '0.75rem' }}>{transitionParseError}</p>}
        <div className="form-actions" style={{ marginTop: '0.75rem' }}>
          <button
            type="button"
            className="btn-primary-action btn-primary-action--ghost"
            onClick={resetTransitionPanelPreferences}
            disabled={savingPromotionTransition}
          >
            Reset panel preferences
          </button>
          <button
            type="button"
            className="btn-primary-action btn-primary-action--ghost"
            onClick={discardLocalTransitionDraft}
            disabled={savingPromotionTransition || !isTransitionDraftFromCache}
          >
            Discard local draft
          </button>
          <button
            type="button"
            className="btn-primary-action btn-primary-action--ghost"
            onClick={revertEditorToProfileDefaultTransitions}
            disabled={savingPromotionTransition || !schoolProfile.profilePromotionTransitionJson}
          >
            Revert editor to profile defaults
          </button>
          <button
            type="button"
            className="btn-primary-action btn-primary-action--ghost"
            onClick={initializeTransitionFromSchoolGrades}
            disabled={savingPromotionTransition || grades.length < 2}
          >
            Initialize from school grades
          </button>
          <button
            type="button"
            className="btn-primary-action"
            onClick={savePromotionTransitionOverride}
            disabled={savingPromotionTransition || !promotionTransitionDraft.trim() || !!transitionParseError}
          >
            {savingPromotionTransition ? 'Saving rules…' : 'Save promotion rules'}
          </button>
          <button
            type="button"
            className="btn-primary-action btn-primary-action--ghost"
            onClick={resetPromotionTransitionsToProfileDefault}
            disabled={savingPromotionTransition || !schoolProfile.promotionTransitionOverrideJson}
          >
            Use profile defaults
          </button>
        </div>
        {promotionTransitionError && <p className="empty-state empty-state--error" style={{ marginTop: '0.75rem' }}>{promotionTransitionError}</p>}
      </section>

      <section className="dashboard-panel" style={{ ...governancePanelStyle, marginTop: '0.75rem' }} aria-label="School profile information">
        <h3 className="card-title">School information</h3>
        <p className="card-desc" style={governanceDescStyle}>Update your school profile, contacts, compliance details, and leadership names shown to Super Admin.</p>
        <p className="card-desc" style={{ ...governanceDescStyle, marginTop: '0.4rem' }}>
          Terms per year:{' '}
          <strong>{Number.isInteger(schoolProfile.termsPerYear) ? schoolProfile.termsPerYear : 'Not set'}</strong>
        </p>
        <div className="form-grid" style={{ marginTop: '0.75rem' }}>
          <label className="form-field" style={governanceLabelStyle}>School name
            <input className="form-input" style={governanceInputStyle} value={schoolProfile.name} onChange={(e) => onSchoolProfileFieldChange('name', e.target.value)} placeholder="School name" />
          </label>
          <label className="form-field" style={governanceLabelStyle}>Owner name
            <input className="form-input" style={governanceInputStyle} value={schoolProfile.ownerName} onChange={(e) => onSchoolProfileFieldChange('ownerName', e.target.value)} placeholder="School owner name" />
          </label>
          <label className="form-field" style={governanceLabelStyle}>School admin name
            <input className="form-input" style={governanceInputStyle} value={schoolProfile.schoolAdminName} onChange={(e) => onSchoolProfileFieldChange('schoolAdminName', e.target.value)} placeholder="School admin full name" />
          </label>
          <label className="form-field" style={governanceLabelStyle}>Principal name
            <input className="form-input" style={governanceInputStyle} value={schoolProfile.principalName} onChange={(e) => onSchoolProfileFieldChange('principalName', e.target.value)} placeholder="Principal name" />
          </label>
          <label className="form-field" style={governanceLabelStyle}>School email
            <input className="form-input" style={governanceInputStyle} type="email" value={schoolProfile.email} onChange={(e) => onSchoolProfileFieldChange('email', e.target.value)} placeholder="school@example.com" />
          </label>
          <label className="form-field" style={governanceLabelStyle}>Phone
            <input className="form-input" style={governanceInputStyle} value={schoolProfile.phone} onChange={(e) => onSchoolProfileFieldChange('phone', e.target.value)} placeholder="+234..." />
          </label>
          <label className="form-field" style={governanceLabelStyle}>WhatsApp number
            <input className="form-input" style={governanceInputStyle} value={schoolProfile.whatsAppNumber} onChange={(e) => onSchoolProfileFieldChange('whatsAppNumber', e.target.value)} placeholder="+234..." />
          </label>
          <label className="form-field" style={governanceLabelStyle}>Country code (ISO2)
            <input className="form-input" style={governanceInputStyle} value={schoolProfile.countryCode} onChange={(e) => onSchoolProfileFieldChange('countryCode', e.target.value.toUpperCase())} maxLength={2} placeholder="NG" />
          </label>
          <label className="form-field" style={governanceLabelStyle}>CAC / registration number
            <input className="form-input" style={governanceInputStyle} value={schoolProfile.cacNumber} onChange={(e) => onSchoolProfileFieldChange('cacNumber', e.target.value)} placeholder="RC1234567" />
          </label>
          <label className="form-field form-field--full" style={governanceLabelStyle}>School address
            <textarea className="form-input" style={governanceInputStyle} rows={3} value={schoolProfile.address} onChange={(e) => onSchoolProfileFieldChange('address', e.target.value)} placeholder="Street, city, state, country" />
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

function StaffSignupLink({ schoolIdFromApi }) {
  const schoolId = resolveSchoolId(schoolIdFromApi);
  const staffSignupUrl = schoolId ? `${typeof window !== 'undefined' ? window.location.origin : ''}/staff/signup?school=${encodeURIComponent(schoolId)}` : '';

  const copyStaffSignup = () => {
    if (staffSignupUrl) navigator.clipboard.writeText(staffSignupUrl);
  };

  if (!staffSignupUrl) {
    return <p className="empty-state">Loading your school link… If this persists, sign out and sign in again as School Admin.</p>;
  }

  return (
    <div className="parent-signup-link-box" style={{ marginTop: '0.5rem' }}>
      <code className="parent-signup-url">{staffSignupUrl}</code>
      <button type="button" className="btn-copy" onClick={copyStaffSignup} title="Copy staff signup link">
        Copy link
      </button>
    </div>
  );
}

