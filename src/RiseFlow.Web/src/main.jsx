import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import './index.css'
import App from './App.jsx'
import VerifyTranscriptPage from './pages/VerifyTranscriptPage.jsx'
import OnboardingPage from './pages/OnboardingPage.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/verify/transcript/:token" element={<VerifyTranscriptPage />} />
        <Route path="/onboard" element={<OnboardingPage />} />
        <Route path="/*" element={<App />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
