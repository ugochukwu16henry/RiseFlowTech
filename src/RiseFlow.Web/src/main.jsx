import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import './index.css'
import App from './App.jsx'
import VerifyTranscriptPage from './pages/VerifyTranscriptPage.jsx'
import OnboardingPage from './pages/OnboardingPage.jsx'
import SuperAdminPage from './pages/SuperAdminPage.jsx'
import ParentPage from './pages/ParentPage.jsx'
import TeacherPage from './pages/TeacherPage.jsx'
import SchoolAdminPage from './pages/SchoolAdminPage.jsx'
import StudentPage from './pages/StudentPage.jsx'
import ExcelImportPage from './pages/ExcelImportPage.jsx'
import AccessCodesPage from './pages/AccessCodesPage.jsx'
import ClaimChildPage from './pages/ClaimChildPage.jsx'
import ParentSignupPage from './pages/ParentSignupPage.jsx'
import AddStudentPage from './pages/AddStudentPage.jsx'
import TeacherSignupPage from './pages/TeacherSignupPage.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/verify/transcript/:token" element={<VerifyTranscriptPage />} />
        <Route path="/onboard" element={<OnboardingPage />} />
        <Route path="/super-admin" element={<SuperAdminPage />} />
        <Route path="/parent" element={<ParentPage />} />
        <Route path="/teacher" element={<TeacherPage />} />
        <Route path="/teacher/signup" element={<TeacherSignupPage />} />
        <Route path="/school" element={<SchoolAdminPage />} />
        <Route path="/school/students/add" element={<AddStudentPage />} />
        <Route path="/school/import" element={<ExcelImportPage />} />
        <Route path="/school/access-codes" element={<AccessCodesPage />} />
        <Route path="/parent/signup" element={<ParentSignupPage />} />
        <Route path="/parent/claim" element={<ClaimChildPage />} />
        <Route path="/student" element={<StudentPage />} />
        <Route path="/*" element={<App />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
