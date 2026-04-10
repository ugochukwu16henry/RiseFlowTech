import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import './index.css'
import App from './App.jsx'
import HomePage from './pages/HomePage.jsx'
import VerifyTranscriptPage from './pages/VerifyTranscriptPage.jsx'
import OnboardingPage from './pages/OnboardingPage.jsx'
import SuperAdminPage from './pages/SuperAdminPage.jsx'
import SuperAdminSchoolsPage from './pages/SuperAdminSchoolsPage.jsx'
import SuperAdminRevenuePage from './pages/SuperAdminRevenuePage.jsx'
import SuperAdminCompliancePage from './pages/SuperAdminCompliancePage.jsx'
import SuperAdminDataOffboardingPage from './pages/SuperAdminDataOffboardingPage.jsx'
import ParentPage from './pages/ParentPage.jsx'
import TeacherPage from './pages/TeacherPage.jsx'
import SchoolAdminPage from './pages/SchoolAdminPage.jsx'
import SchoolStudentsPage from './pages/SchoolStudentsPage.jsx'
import SchoolBillingPage from './pages/SchoolBillingPage.jsx'
import SchoolReportsPage from './pages/SchoolReportsPage.jsx'
import StudentPage from './pages/StudentPage.jsx'
import ExcelImportPage from './pages/ExcelImportPage.jsx'
import AccessCodesPage from './pages/AccessCodesPage.jsx'
import ClaimChildPage from './pages/ClaimChildPage.jsx'
import ParentSignupPage from './pages/ParentSignupPage.jsx'
import AddStudentPage from './pages/AddStudentPage.jsx'
import SchoolClassesPage from './pages/SchoolClassesPage.jsx'
import TeacherSignupPage from './pages/TeacherSignupPage.jsx'
import LoginPage from './pages/LoginPage.jsx'
import TermsPage from './pages/TermsPage.jsx'
import PrivacyPage from './pages/PrivacyPage.jsx'
import AffiliateProgramPage from './pages/AffiliateProgramPage.jsx'
import AffiliateSignupPage from './pages/AffiliateSignupPage.jsx'
import AffiliatePage from './pages/AffiliatePage.jsx'
import SuperAdminAffiliatesPage from './pages/SuperAdminAffiliatesPage.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/verify/transcript/:token" element={<VerifyTranscriptPage />} />
        <Route path="/onboard" element={<OnboardingPage />} />
        <Route path="/terms" element={<TermsPage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/affiliate-program" element={<AffiliateProgramPage />} />
        <Route path="/affiliate/signup" element={<AffiliateSignupPage />} />
        <Route path="/affiliate" element={<AffiliatePage />} />
        <Route path="/super-admin" element={<SuperAdminPage />} />
        <Route path="/super-admin/dashboard" element={<SuperAdminPage />} />
        <Route path="/super-admin/schools" element={<SuperAdminSchoolsPage />} />
        <Route path="/super-admin/revenue" element={<SuperAdminRevenuePage />} />
        <Route path="/super-admin/compliance" element={<SuperAdminCompliancePage />} />
        <Route path="/super-admin/data-offboarding" element={<SuperAdminDataOffboardingPage />} />
        <Route path="/super-admin/affiliates" element={<SuperAdminAffiliatesPage />} />
        <Route path="/super-admin/affiliate-requests" element={<SuperAdminAffiliatesPage />} />
        <Route path="/super-admin/affiliate-payouts" element={<SuperAdminAffiliatesPage />} />
        <Route path="/super-admin/affiliate-training" element={<SuperAdminAffiliatesPage />} />
        <Route path="/parent" element={<ParentPage />} />
        <Route path="/parent/dashboard" element={<ParentPage />} />
        <Route path="/teacher" element={<TeacherPage />} />
        <Route path="/teacher/dashboard" element={<TeacherPage />} />
        <Route path="/teacher/grading" element={<TeacherPage />} />
        <Route path="/teacher/signup" element={<TeacherSignupPage />} />
        <Route path="/school" element={<SchoolAdminPage />} />
        <Route path="/school/dashboard" element={<SchoolAdminPage />} />
        <Route path="/school/students" element={<SchoolStudentsPage />} />
        <Route path="/school/billing" element={<SchoolBillingPage />} />
        <Route path="/school/reports" element={<SchoolReportsPage />} />
        <Route path="/school/students/add" element={<AddStudentPage />} />
        <Route path="/school/classes" element={<SchoolClassesPage />} />
        <Route path="/school/import" element={<ExcelImportPage />} />
        <Route path="/school/access-codes" element={<AccessCodesPage />} />
        <Route path="/admin/dashboard" element={<SchoolAdminPage />} />
        <Route path="/admin/students" element={<SchoolStudentsPage />} />
        <Route path="/admin/billing" element={<SchoolBillingPage />} />
        <Route path="/admin/import" element={<ExcelImportPage />} />
        <Route path="/admin/access-codes" element={<AccessCodesPage />} />
        <Route path="/parent/signup" element={<ParentSignupPage />} />
        <Route path="/parent/claim" element={<ClaimChildPage />} />
        <Route path="/student" element={<StudentPage />} />
        <Route path="/student/dashboard" element={<StudentPage />} />
        <Route path="/*" element={<App />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
