import { StrictMode, Suspense, lazy } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import './index.css'
import RouteSeoManager from './seo/RouteSeoManager.jsx'
import { SESSION_EXPIRED_BROADCAST_KEY } from './api'

const App = lazy(() => import('./App.jsx'))
const HomePage = lazy(() => import('./pages/HomePage.jsx'))
const VerifyTranscriptPage = lazy(() => import('./pages/VerifyTranscriptPage.jsx'))
const OnboardingPage = lazy(() => import('./pages/OnboardingPage.jsx'))
const SuperAdminPage = lazy(() => import('./pages/SuperAdminPage.jsx'))
const SuperAdminSchoolsPage = lazy(() => import('./pages/SuperAdminSchoolsPage.jsx'))
const SuperAdminRevenuePage = lazy(() => import('./pages/SuperAdminRevenuePage.jsx'))
const SuperAdminCompliancePage = lazy(() => import('./pages/SuperAdminCompliancePage.jsx'))
const SuperAdminDataOffboardingPage = lazy(() => import('./pages/SuperAdminDataOffboardingPage.jsx'))
const ParentPage = lazy(() => import('./pages/ParentPage.jsx'))
const TeacherPage = lazy(() => import('./pages/TeacherPage.jsx'))
const SchoolAdminPage = lazy(() => import('./pages/SchoolAdminPage.jsx'))
const SchoolStudentsPage = lazy(() => import('./pages/SchoolStudentsPage.jsx'))
const SchoolBillingPage = lazy(() => import('./pages/SchoolBillingPage.jsx'))
const SchoolReportsPage = lazy(() => import('./pages/SchoolReportsPage.jsx'))
const StudentPage = lazy(() => import('./pages/StudentPage.jsx'))
const ExcelImportPage = lazy(() => import('./pages/ExcelImportPage.jsx'))
const AccessCodesPage = lazy(() => import('./pages/AccessCodesPage.jsx'))
const ClaimChildPage = lazy(() => import('./pages/ClaimChildPage.jsx'))
const ParentSignupPage = lazy(() => import('./pages/ParentSignupPage.jsx'))
const AddStudentPage = lazy(() => import('./pages/AddStudentPage.jsx'))
const SchoolClassesPage = lazy(() => import('./pages/SchoolClassesPage.jsx'))
const GradingSystemPage = lazy(() => import('./pages/GradingSystemPage.jsx'))
const StudentPromotionPage = lazy(() => import('./pages/StudentPromotionPage.jsx'))
const TimetablePage = lazy(() => import('./pages/TimetablePage.jsx'))
const SchoolCommunicationsPage = lazy(() => import('./pages/SchoolCommunicationsPage.jsx'))
const TeacherAssignmentsPage = lazy(() => import('./pages/TeacherAssignmentsPage.jsx'))
const TeacherSignupPage = lazy(() => import('./pages/TeacherSignupPage.jsx'))
const StaffSignupPage = lazy(() => import('./pages/StaffSignupPage.jsx'))
const StaffPage = lazy(() => import('./pages/StaffPage.jsx'))
const LoginPage = lazy(() => import('./pages/LoginPage.jsx'))
const TermsPage = lazy(() => import('./pages/TermsPage.jsx'))
const PrivacyPage = lazy(() => import('./pages/PrivacyPage.jsx'))
const AffiliateProgramPage = lazy(() => import('./pages/AffiliateProgramPage.jsx'))
const AffiliateSignupPage = lazy(() => import('./pages/AffiliateSignupPage.jsx'))
const AffiliatePage = lazy(() => import('./pages/AffiliatePage.jsx'))
const SuperAdminAffiliatesPage = lazy(() => import('./pages/SuperAdminAffiliatesPage.jsx'))
const SchoolFeesPage = lazy(() => import('./pages/SchoolFeesPage.jsx'))
const SchoolTermsPage = lazy(() => import('./pages/SchoolTermsPage.jsx'))
const ParentFeesPage = lazy(() => import('./pages/ParentFeesPage.jsx'))

function PageLoader() {
  return (
    <div style={{ minHeight: '40vh', display: 'grid', placeItems: 'center', fontSize: '0.95rem' }}>
      Loading...
    </div>
  )
}

function withSuspense(element) {
  return <Suspense fallback={<PageLoader />}>{element}</Suspense>
}

function prefetchLikelyRoutes() {
  const preload = [
    () => import('./pages/LoginPage.jsx'),
    () => import('./pages/SchoolAdminPage.jsx'),
    () => import('./pages/TeacherPage.jsx'),
    () => import('./pages/ParentPage.jsx'),
    () => import('./pages/StudentPage.jsx'),
    () => import('./pages/SuperAdminPage.jsx'),
  ]
  const run = () => preload.forEach((load) => load().catch(() => {}))
  if (typeof window !== 'undefined' && 'requestIdleCallback' in window) {
    window.requestIdleCallback(run, { timeout: 1800 })
  } else {
    setTimeout(run, 800)
  }
}

prefetchLikelyRoutes()

if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key !== SESSION_EXPIRED_BROADCAST_KEY || !event.newValue) return
    if (window.location.pathname !== '/login') {
      const next = encodeURIComponent(`${window.location.pathname}${window.location.search}`)
      window.location.assign(`/login?reason=session_expired&next=${next}`)
    }
  })
}

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter>
      <RouteSeoManager />
      <Routes>
        <Route path="/" element={withSuspense(<HomePage />)} />
        <Route path="/verify/transcript/:token" element={withSuspense(<VerifyTranscriptPage />)} />
        <Route path="/onboard" element={withSuspense(<OnboardingPage />)} />
        <Route path="/terms" element={withSuspense(<TermsPage />)} />
        <Route path="/privacy" element={withSuspense(<PrivacyPage />)} />
        <Route path="/login" element={withSuspense(<LoginPage />)} />
        <Route path="/affiliate-program" element={withSuspense(<AffiliateProgramPage />)} />
        <Route path="/affiliate/signup" element={withSuspense(<AffiliateSignupPage />)} />
        <Route path="/affiliate" element={withSuspense(<AffiliatePage />)} />
        <Route path="/affiliate/dashboard" element={withSuspense(<AffiliatePage />)} />
        <Route path="/affiliate/schools" element={withSuspense(<AffiliatePage />)} />
        <Route path="/affiliate/payouts" element={withSuspense(<AffiliatePage />)} />
        <Route path="/affiliate/training" element={withSuspense(<AffiliatePage />)} />
        <Route path="/super-admin" element={withSuspense(<SuperAdminPage />)} />
        <Route path="/super-admin/dashboard" element={withSuspense(<SuperAdminPage />)} />
        <Route path="/super-admin/schools" element={withSuspense(<SuperAdminSchoolsPage />)} />
        <Route path="/super-admin/revenue" element={withSuspense(<SuperAdminRevenuePage />)} />
        <Route path="/super-admin/compliance" element={withSuspense(<SuperAdminCompliancePage />)} />
        <Route path="/super-admin/data-offboarding" element={withSuspense(<SuperAdminDataOffboardingPage />)} />
        <Route path="/super-admin/affiliates" element={withSuspense(<SuperAdminAffiliatesPage />)} />
        <Route path="/super-admin/affiliate-requests" element={withSuspense(<SuperAdminAffiliatesPage />)} />
        <Route path="/super-admin/affiliate-payouts" element={withSuspense(<SuperAdminAffiliatesPage />)} />
        <Route path="/super-admin/affiliate-training" element={withSuspense(<SuperAdminAffiliatesPage />)} />
        <Route path="/parent" element={withSuspense(<ParentPage />)} />
        <Route path="/parent/dashboard" element={withSuspense(<ParentPage />)} />
        <Route path="/teacher" element={withSuspense(<TeacherPage />)} />
        <Route path="/teacher/dashboard" element={withSuspense(<TeacherPage />)} />
        <Route path="/teacher/grading" element={withSuspense(<TeacherPage />)} />
        <Route path="/teacher/promotions" element={withSuspense(<StudentPromotionPage />)} />
        <Route path="/teacher/assignments" element={withSuspense(<TeacherAssignmentsPage />)} />
        <Route path="/teacher/signup" element={withSuspense(<TeacherSignupPage />)} />
        <Route path="/staff" element={withSuspense(<StaffPage />)} />
        <Route path="/staff/dashboard" element={withSuspense(<StaffPage />)} />
        <Route path="/staff/signup" element={withSuspense(<StaffSignupPage />)} />
        <Route path="/school" element={withSuspense(<SchoolAdminPage view="overview" />)} />
        <Route path="/school/dashboard" element={withSuspense(<SchoolAdminPage view="overview" />)} />
        <Route path="/school/overview" element={withSuspense(<SchoolAdminPage view="overview" />)} />
        <Route path="/school/people" element={withSuspense(<SchoolAdminPage view="people" />)} />
        <Route path="/school/operations" element={withSuspense(<SchoolAdminPage view="operations" />)} />
        <Route path="/school/students" element={withSuspense(<SchoolStudentsPage />)} />
        <Route path="/school/billing" element={withSuspense(<SchoolBillingPage />)} />
        <Route path="/school/reports" element={withSuspense(<SchoolReportsPage />)} />
        <Route path="/school/students/add" element={withSuspense(<AddStudentPage />)} />
        <Route path="/school/classes" element={withSuspense(<SchoolClassesPage />)} />
        <Route path="/school/grading-systems" element={withSuspense(<GradingSystemPage />)} />
        <Route path="/school/promotions" element={withSuspense(<StudentPromotionPage />)} />
        <Route path="/school/timetable" element={withSuspense(<TimetablePage />)} />
        <Route path="/school/communications" element={withSuspense(<SchoolCommunicationsPage />)} />
        <Route path="/school/import" element={withSuspense(<ExcelImportPage />)} />
        <Route path="/school/access-codes" element={withSuspense(<AccessCodesPage />)} />
        <Route path="/school/fees" element={withSuspense(<SchoolFeesPage />)} />
        <Route path="/school/terms" element={withSuspense(<SchoolTermsPage />)} />
        <Route path="/admin" element={withSuspense(<SchoolAdminPage view="overview" />)} />
        <Route path="/admin/dashboard" element={withSuspense(<SchoolAdminPage view="overview" />)} />
        <Route path="/admin/profile" element={withSuspense(<SchoolAdminPage view="operations" />)} />
        <Route path="/admin/overview" element={withSuspense(<SchoolAdminPage view="overview" />)} />
        <Route path="/admin/people" element={withSuspense(<SchoolAdminPage view="people" />)} />
        <Route path="/admin/operations" element={withSuspense(<SchoolAdminPage view="operations" />)} />
        <Route path="/admin/students" element={withSuspense(<SchoolStudentsPage />)} />
        <Route path="/admin/students/add" element={withSuspense(<AddStudentPage />)} />
        <Route path="/admin/classes" element={withSuspense(<SchoolClassesPage />)} />
        <Route path="/admin/billing" element={withSuspense(<SchoolBillingPage />)} />
        <Route path="/admin/reports" element={withSuspense(<SchoolReportsPage />)} />
        <Route path="/admin/fees" element={withSuspense(<SchoolFeesPage />)} />
        <Route path="/admin/terms" element={withSuspense(<SchoolTermsPage />)} />
        <Route path="/admin/import" element={withSuspense(<ExcelImportPage />)} />
        <Route path="/admin/grading-systems" element={withSuspense(<GradingSystemPage />)} />
        <Route path="/admin/promotions" element={withSuspense(<StudentPromotionPage />)} />
        <Route path="/admin/timetable" element={withSuspense(<TimetablePage />)} />
        <Route path="/admin/communications" element={withSuspense(<SchoolCommunicationsPage />)} />
        <Route path="/admin/access-codes" element={withSuspense(<AccessCodesPage />)} />
        <Route path="/parent/signup" element={withSuspense(<ParentSignupPage />)} />
        <Route path="/parent/claim" element={withSuspense(<ClaimChildPage />)} />
        <Route path="/parent/fees" element={withSuspense(<ParentFeesPage />)} />
        <Route path="/student" element={withSuspense(<StudentPage />)} />
        <Route path="/student/dashboard" element={withSuspense(<StudentPage />)} />
        <Route path="/*" element={withSuspense(<App />)} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
