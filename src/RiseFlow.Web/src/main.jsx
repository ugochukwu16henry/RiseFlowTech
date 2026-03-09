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

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/verify/transcript/:token" element={<VerifyTranscriptPage />} />
        <Route path="/onboard" element={<OnboardingPage />} />
        <Route path="/super-admin" element={<SuperAdminPage />} />
        <Route path="/parent" element={<ParentPage />} />
        <Route path="/teacher" element={<TeacherPage />} />
        <Route path="/school" element={<SchoolAdminPage />} />
        <Route path="/school/import" element={<ExcelImportPage />} />
        <Route path="/student" element={<StudentPage />} />
        <Route path="/*" element={<App />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
