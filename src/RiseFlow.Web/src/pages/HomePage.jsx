import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { getApiBase } from '../api';
import './HomePage.css';

const THEME_STORAGE_KEY = 'riseflow-theme';

function getInitialTheme() {
  if (typeof window === 'undefined') return 'light';
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
  } catch {
    // ignore
  }
  return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

// Hero icons served from Vite public/media. Filenames are kebab-cased to avoid issues.
const HERO_ICONS = [
  { src: '/media/hero-icon-1.png', alt: 'School operations' },
  { src: '/media/hero-icon-2.png', alt: 'Parent communication' },
  { src: '/media/hero-icon-3-shielded-data.png', alt: 'Compliance & Security' },
  { src: '/media/hero-icon-4-multi-tenant-platform.png', alt: 'Multi-tenant platform' },
  { src: '/media/hero-icon-5-bulk-onboarding.png', alt: 'Bulk onboarding' },
  { src: '/media/hero-icon-6-teacher-workspace.png', alt: 'Teacher workspace' },
];

export default function HomePage() {
  const [theme, setTheme] = useState(getInitialTheme);
  const [studentCount, setStudentCount] = useState(50);

  // Pricing model: first 50 students are lifetime free.
  // From 51st student: ₦500 one-time activation and ₦100 monthly subscription each.
  const monthlySubscription = useMemo(
    () => (studentCount <= 50 ? 0 : (studentCount - 50) * 100),
    [studentCount],
  );

  const oneTimeActivation = useMemo(
    () => (studentCount <= 50 ? 0 : (studentCount - 50) * 500),
    [studentCount],
  );

  // rotate hero icon every 3 minutes
  const [heroIconIndex, setHeroIconIndex] = useState(0);

  useEffect(() => {
    try {
      document.documentElement.setAttribute('data-theme', theme);
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // ignore
    }
  }, [theme]);

  useEffect(() => {
    const id = setInterval(() => {
      setHeroIconIndex((i) => (i + 1) % HERO_ICONS.length);
    }, 3 * 60 * 1000);
    return () => clearInterval(id);
  }, []);

  const toggleTheme = () => {
    setTheme((t) => (t === 'light' ? 'dark' : 'light'));
  };

  const currentHeroIcon = HERO_ICONS[heroIconIndex];
  const formatNaira = (amount) => `₦${amount.toLocaleString('en-NG')}`;

  return (
    <div className="home-root min-h-screen bg-slate-50 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      {/* Sticky nav / header */}
      <header className="home-header sticky top-0 z-40 border-b border-slate-200/70 bg-white/80 backdrop-blur-sm dark:border-slate-800 dark:bg-slate-950/80">
        <div className="home-header-inner mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <Link to="/" className="home-brand flex items-center gap-2" aria-label="RiseFlow home">
            <img
              src="/logos/RiseFlow%20logo%20with%20name.png"
              alt="RiseFlow"
              className="home-logo-img h-8"
              onError={(e) => {
                e.target.style.display = 'none';
                const fallback = e.target.nextElementSibling;
                if (fallback) fallback.style.display = 'flex';
              }}
            />
            <span className="home-brand-fallback hidden items-center gap-1 text-sm font-semibold text-slate-900 dark:text-slate-50">
              <span className="home-logo-dot inline-flex h-2 w-2 rounded-full bg-indigo-500" aria-hidden="true" />
              <span className="home-logo-text">RiseFlow</span>
            </span>
          </Link>
          <div className="home-header-actions flex items-center gap-3">
            <nav className="flex flex-wrap items-center gap-3 text-xs font-medium text-slate-600 dark:text-slate-300 sm:gap-4">
              <a href="#how" className="hover:text-indigo-600 dark:hover:text-indigo-400">
                How it works
              </a>
              <a href="#pricing" className="hover:text-indigo-600 dark:hover:text-indigo-400">
                Pricing
              </a>
              <a href="#compare" className="hover:text-indigo-600 dark:hover:text-indigo-400">
                Compare
              </a>
            </nav>
            <button
              type="button"
              onClick={toggleTheme}
              className="home-theme-toggle flex items-center gap-1 rounded-full border border-slate-200 bg-white px-3 py-1 text-xs text-slate-700 shadow-sm hover:bg-slate-100 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200"
              aria-label={theme === 'light' ? 'Switch to night view' : 'Switch to day view'}
            >
              <span className="home-theme-icon" aria-hidden="true">
                {theme === 'light' ? '🌙' : '☀️'}
              </span>
              <span className="home-theme-label">
                {theme === 'light' ? 'Night view' : 'Day view'}
              </span>
            </button>
            <Link to="/login" className="home-link-button home-link-ghost">
              Log in
            </Link>
            <Link to="/onboard" className="home-link-button home-link-primary">
              Register your school
            </Link>
          </div>
        </div>
      </header>

      <main className="home-main mx-auto max-w-6xl px-4 pb-20 pt-10 space-y-16">
        {/* Hero */}
        <section className="home-hero grid items-center gap-10 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,0.9fr)]">
          <div className="relative">
            <div className="pointer-events-none absolute -inset-8 rounded-3xl bg-gradient-to-br from-indigo-500/20 via-emerald-400/10 to-sky-500/10 blur-2xl dark:from-indigo-500/40 dark:via-emerald-400/20 dark:to-sky-500/20" />
            <div className="relative rounded-3xl border border-slate-200/80 bg-white/70 p-6 shadow-xl backdrop-blur-sm dark:border-slate-700 dark:bg-slate-900/80">
              <p className="mb-2 inline-flex items-center rounded-full bg-emerald-50 px-3 py-1 text-[11px] font-medium text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300">
                Built for African schools • First 50 students free
              </p>
              <h1 className="mt-2 text-balance text-3xl font-semibold tracking-tight text-slate-900 dark:text-slate-50 sm:text-4xl">
                All‑in‑one school OS for African schools.
              </h1>
              <p className="home-hero-subtitle mt-4 text-sm leading-relaxed text-slate-600 dark:text-slate-300">
                RiseFlow connects school owners, teachers, parents, and students in one simple, modern platform.
                Results, fees, attendance, and communication — built for Nigerian and African schools from day one.
              </p>
              <div className="home-hero-ctas mt-6 flex flex-wrap items-center gap-3">
                <Link to="/onboard" className="home-hero-cta-primary inline-flex items-center justify-center rounded-full bg-emerald-500 px-5 py-2.5 text-xs font-semibold text-white shadow-sm hover:bg-emerald-400">
                  Register your school in 5 minutes
                </Link>
                <Link to="/login" className="home-hero-cta-secondary inline-flex items-center justify-center rounded-full border border-slate-300 bg-white/70 px-5 py-2.5 text-xs font-semibold text-slate-800 shadow-sm hover:bg-slate-50 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100">
                  Already using RiseFlow? Log in
                </Link>
                <a
                  href={`${getApiBase()}/api/public/pitch-deck`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="home-hero-cta-pdf text-[11px] text-indigo-600 underline-offset-2 hover:underline dark:text-indigo-300"
                >
                  Download pitch deck (PDF)
                </a>
                <a
                  href={`${getApiBase()}/api/public/teacher-quick-start`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="home-hero-cta-pdf text-[11px] text-indigo-600 underline-offset-2 hover:underline dark:text-indigo-300"
                >
                  Teacher&apos;s Quick Start Guide (PDF)
                </a>
                <a
                  href={`${getApiBase()}/api/public/grading-reference`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="home-hero-cta-pdf text-[11px] text-indigo-600 underline-offset-2 hover:underline dark:text-indigo-300"
                >
                  Grading Reference (PDF)
                </a>
              </div>
              <div className="home-hero-meta mt-4 flex flex-wrap gap-2 text-[11px] text-slate-500 dark:text-slate-400">
                <span>• First 50 students free after you register your school</span>
                <span>• Paystack-powered billing in Naira</span>
                <span>• Designed for primary &amp; secondary schools</span>
              </div>

              {/* Trust bar */}
              <div className="mt-6 border-t border-slate-200 pt-4 text-[11px] text-slate-500 dark:border-slate-700 dark:text-slate-400">
                <p className="mb-2 font-medium text-slate-500 dark:text-slate-300">
                  Trusted by schools across Lagos, Abuja &amp; Nairobi
                </p>
                <div className="relative overflow-hidden" aria-hidden="true">
                  <div className="flex home-trust-marquee gap-8 whitespace-nowrap w-max">
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">
                      Gracefield College
                    </span>
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">
                      Unity Academy
                    </span>
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">
                      Queens Park Schools
                    </span>
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">
                      Prime College
                    </span>
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">
                      BrightFuture Int’l
                    </span>
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">Gracefield College</span>
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">Unity Academy</span>
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">Queens Park Schools</span>
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">Prime College</span>
                    <span className="rounded-full bg-slate-100 px-3 py-1 dark:bg-slate-800">BrightFuture Int'l</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Dashboard / intro media + rotating hero icon */}
          <div className="space-y-4">
            <video
              src="/media/lottie-animations-for-school-onboarding.mp4"
              autoPlay
              muted
              loop
              playsInline
              className="w-full rounded-3xl border border-slate-200/80 bg-slate-900/80 object-cover shadow-xl dark:border-slate-700"
            />

            <div className="grid gap-3 text-[11px] sm:grid-cols-3">
              <div className="rounded-xl border border-slate-200/80 bg-white/80 p-3 shadow-sm dark:border-slate-700 dark:bg-slate-900/80">
                <p className="text-slate-500 dark:text-slate-300">Real‑time results</p>
                <p className="mt-1 font-semibold text-slate-900 dark:text-slate-50">
                  &ldquo;Chinedu • A in Math&rdquo;
                </p>
              </div>
              <div className="rounded-xl border border-slate-200/80 bg-white/80 p-3 shadow-sm dark:border-slate-700 dark:bg-slate-900/80">
                <p className="text-slate-500 dark:text-slate-300">Parent view</p>
                <p className="mt-1 font-semibold text-slate-900 dark:text-slate-50">
                  WhatsApp‑ready updates
                </p>
              </div>
              <div className="rounded-xl border border-slate-200/80 bg-white/80 p-3 shadow-sm dark:border-slate-700 dark:bg-slate-900/80">
                <p className="text-slate-500 dark:text-slate-300">Control room</p>
                <p className="mt-1 font-semibold text-slate-900 dark:text-slate-50">
                  One screen, all schools
                </p>
              </div>
            </div>

            <div className="flex items-center gap-3 rounded-2xl border border-slate-200 bg-white/80 p-3 text-[11px] shadow-sm dark:border-slate-700 dark:bg-slate-900/80">
              <img
                src={currentHeroIcon.src}
                alt={currentHeroIcon.alt}
                className="h-8 w-8 rounded-lg object-contain"
              />
              <div>
                <p className="font-semibold text-slate-900 dark:text-slate-50">
                  {currentHeroIcon.alt}
                </p>
                <p className="text-[10px] text-slate-500 dark:text-slate-400">
                  Rotating hero focus every 3 minutes.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* Bento grid */}
        <section id="bento" className="home-section space-y-6">
          <div className="home-section-header">
            <h2 className="text-sm font-semibold text-slate-900 dark:text-slate-50">
              One platform, three superpowers
            </h2>
            <p className="text-xs text-slate-600 dark:text-slate-300">
              Owners, teachers, and parents each get a tailored experience inside one multi‑tenant
              platform.
            </p>
          </div>
          <div className="grid gap-4 md:grid-cols-3">
            <article className="group relative overflow-hidden rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-indigo-500 hover:shadow-md dark:border-slate-700 dark:bg-slate-900/80">
              <img src="/media/hero-icon-1.png" alt="For owners" className="h-8 w-8" />
              <h3 className="mt-3 text-sm font-semibold text-slate-900 dark:text-slate-50">
                For owners
              </h3>
              <p className="mt-2 text-xs text-slate-600 dark:text-slate-300">
                Real‑time metrics across students, teachers, and fees. Lock result printing until
                debts are cleared, and stay on top of your billing.
              </p>
            </article>
            <article className="group relative overflow-hidden rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-emerald-500 hover:shadow-md dark:border-slate-700 dark:bg-slate-900/80">
              <img
                src="/media/hero-icon-6-teacher-workspace.png"
                alt="For teachers"
                className="h-8 w-8"
              />
              <h3 className="mt-3 text-sm font-semibold text-slate-900 dark:text-slate-50">
                For teachers
              </h3>
              <p className="mt-2 text-xs text-slate-600 dark:text-slate-300">
                Fast grid entry, automatic totals, and one-click transcript generation — with
                access limited only to students in their classes.
              </p>
            </article>
            <article className="group relative overflow-hidden rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-sky-500 hover:shadow-md dark:border-slate-700 dark:bg-slate-900/80">
              <img src="/media/hero-icon-2.png" alt="For parents" className="h-8 w-8" />
              <h3 className="mt-3 text-sm font-semibold text-slate-900 dark:text-slate-50">
                For parents
              </h3>
              <p className="mt-2 text-xs text-slate-600 dark:text-slate-300">
                Family view for multiple children, secure access codes, and instant PDF report
                cards on any smartphone.
              </p>
            </article>
          </div>
        </section>

        {/* Pricing calculator + skeleton grid */}
        <section id="pricing" className="home-section">
          <div className="home-section-header">
            <h2 className="text-sm font-semibold text-slate-900 dark:text-slate-50">
              Transparent pricing for growing schools
            </h2>
            <p className="text-xs text-slate-600 dark:text-slate-300">
              First 50 students are lifetime free. From student 51, you pay a one‑time activation and a small monthly fee.
            </p>
          </div>

          <div className="home-section--pricing">
            {/* Left: slider + cost cards */}
            <div className="home-pricing-card">
              <div className="home-pricing-header">
                <label className="text-emerald-600 dark:text-emerald-400 font-semibold">
                  Number of students
                </label>
                <span className="text-3xl font-black text-slate-900 dark:text-slate-50">
                  {studentCount}
                </span>
              </div>
              <input
                type="range"
                min={0}
                max={1000}
                step={5}
                value={studentCount}
                onChange={(e) => setStudentCount(Number(e.target.value))}
                className="home-pricing-slider"
              />
              <div className="flex justify-between text-[10px] text-slate-400 mt-1">
                <span>0</span>
                <span>50 (Free Limit)</span>
                <span>500</span>
                <span>1000+</span>
              </div>

              <div className="home-pricing-body">
                <div>
                  <p className="text-xs text-emerald-700 dark:text-emerald-300 font-bold uppercase tracking-wide">
                    Monthly subscription
                  </p>
                  <p className="home-pricing-cost">
                    {formatNaira(monthlySubscription)}{' '}
                    <span className="text-xs font-normal text-slate-500 italic">/month</span>
                  </p>
                  <p className="home-pricing-note">
                    ({studentCount > 50 ? studentCount - 50 : 0} billable students at ₦100 each)
                  </p>
                </div>
                <div>
                  <p className="text-xs text-slate-500 font-bold uppercase tracking-wide">
                    One‑time activation
                  </p>
                  <p className="home-pricing-cost">
                    {formatNaira(oneTimeActivation)}
                  </p>
                  <p className="home-pricing-note">
                    ₦500 per new student added after first 50
                  </p>
                </div>
              </div>

              {studentCount <= 50 && (
                <div className="mt-4 flex items-center justify-center gap-2 bg-yellow-100 text-yellow-800 py-2 rounded-full font-bold text-sm animate-bounce">
                  <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
                    <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
                  </svg>
                  100% Free Tier Active
                </div>
              )}

              <div className="mt-6 text-center">
                <button className="home-pricing-cta">
                  Get started with {studentCount} students
                </button>
              </div>
            </div>

            {/* Right: fast grid entry preview */}
            <div className="home-grid-preview">
              <p className="home-grid-preview-header">Fast grid entry preview</p>
              <div className="home-grid-preview-table">
                <div className="home-grid-preview-row home-grid-preview-row--head">
                  <span>Student</span>
                  <span>Math</span>
                  <span>English</span>
                  <span>Science</span>
                </div>
                {Array.from({ length: 3 }).map((_, i) => (
                  <div key={i} className="home-grid-preview-row">
                    <div className="home-grid-preview-skeleton" />
                    <div className="home-grid-preview-skeleton" />
                    <div className="home-grid-preview-skeleton" />
                    <div className="home-grid-preview-skeleton" />
                  </div>
                ))}
              </div>
              <div className="home-grid-preview-mobile">
                {Array.from({ length: 3 }).map((_, i) => (
                  <div key={i} className="home-grid-preview-mobile-card">
                    <div>
                      <div className="home-grid-preview-skeleton home-grid-preview-skeleton--wide" />
                      <div className="home-grid-preview-mobile-metrics">
                        <div className="home-grid-preview-skeleton home-grid-preview-skeleton--pill" />
                        <div className="home-grid-preview-skeleton home-grid-preview-skeleton--pill" />
                      </div>
                    </div>
                    <span className="text-[10px] text-slate-400">Grid view</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* Built for African realities (keep original content) */}
        <section className="home-section">
          <div className="home-section-header">
            <h2>Built for African realities</h2>
            <p>
              RiseFlow understands how schools really work across Nigeria, Ghana, Kenya and beyond.
              We support dense ranking, competency-based assessments, and flexible fee models that
              match your community.
            </p>
          </div>
          <div className="home-legacy-grid">
            <article className="home-tile">
              <h3>Secondary: Score &amp; rank engine</h3>
              <p>
                30% CA + 70% exam, automatic dense ranking, and subject-by-subject performance
                snapshots. Export-ready report cards with your school logo and RiseFlow seal.
              </p>
            </article>
            <article className="home-tile">
              <h3>Primary: Class assessments</h3>
              <p>
                Track handwriting, punctuality, reading fluency, social habits and more with custom
                assessment categories that fit your curriculum.
              </p>
            </article>
            <article className="home-tile">
              <h3>Parents stay in the loop</h3>
              <p>
                One parent account, many children. View pictures, teachers, results and fees in one
                clean dashboard, on any smartphone.
              </p>
            </article>
            <article className="home-tile">
              <h3>Safe, hosted &amp; future-proof</h3>
              <p>
                Cloud-hosted on modern infrastructure with daily backups. Your records are safe for
                future generations and accessible from anywhere.
              </p>
            </article>
          </div>
        </section>

        {/* How schools get started (vertical steps) */}
        <section id="how" className="home-section home-section-alt">
          <div className="home-section-header">
            <h2>How schools get started</h2>
            <p>Three simple steps to bring your school onto RiseFlow.</p>
          </div>
          <ol className="home-steps">
            <li>
              <span className="home-step-badge">1</span>
              <div>
                <h3>Register your school</h3>
                <p>
                  Click &ldquo;Register your school&rdquo; and fill in a short form. You get a
                  secure link for your teachers and parents.
                </p>
              </div>
            </li>
            <li>
              <span className="home-step-badge">2</span>
              <div>
                <h3>Import or add students</h3>
                <p>
                  Upload an Excel file or add students one by one. RiseFlow checks for duplicates
                  and keeps your data clean.
                </p>
              </div>
            </li>
            <li>
              <span className="home-step-badge">3</span>
              <div>
                <h3>Share links with staff &amp; parents</h3>
                <p>
                  Share your school link so teachers and parents can sign up with their own profiles
                  and photos.
                </p>
              </div>
            </li>
          </ol>
        </section>

        {/* RiseFlow vs Paper */}
        <section id="compare" className="home-section space-y-4">
          <h2 className="text-sm font-semibold text-slate-900 dark:text-slate-50">
            RiseFlow vs. Paper
          </h2>
          <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900/80">
            <table className="min-w-full text-xs">
              <thead className="bg-slate-50 text-slate-600 dark:bg-slate-900 dark:text-slate-300">
                <tr>
                  <th className="px-4 py-3 text-left font-medium">Feature</th>
                  <th className="px-4 py-3 text-left font-medium">Paper</th>
                  <th className="px-4 py-3 text-left font-medium">RiseFlow</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                <tr>
                  <td className="px-4 py-3">Records &amp; storage</td>
                  <td className="px-4 py-3 text-slate-500">
                    Easy to lose, hard to search, no parent view.
                  </td>
                  <td className="px-4 py-3 text-emerald-400">
                    Cloud‑safe, instant search, family dashboard.
                  </td>
                </tr>
                <tr>
                  <td className="px-4 py-3">Results</td>
                  <td className="px-4 py-3 text-slate-500">
                    Manual calculation, no QR verification.
                  </td>
                  <td className="px-4 py-3 text-emerald-400">
                    Automatic grading, QR‑verified transcripts.
                  </td>
                </tr>
                <tr>
                  <td className="px-4 py-3">Communication</td>
                  <td className="px-4 py-3 text-slate-500">
                    Phone calls and paper notes.
                  </td>
                  <td className="px-4 py-3 text-emerald-400">
                    WhatsApp‑ready updates &amp; parent portal.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        {/* Lead magnet */}
        <section className="home-section space-y-4">
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-700 dark:bg-slate-900/80">
            <h2 className="text-sm font-semibold text-slate-900 dark:text-slate-50">
              Get our guide on digitalizing your school
            </h2>
            <p className="mt-1 text-xs text-slate-600 dark:text-slate-300">
              Practical steps Nigerian school owners are using to move from paper to a secure,
              parent‑friendly platform.
            </p>
            <form className="mt-3 flex flex-col gap-2 sm:flex-row">
              <input
                type="email"
                required
                placeholder="Your work email"
                className="h-9 flex-1 rounded-md border border-slate-300 bg-white px-3 text-xs text-slate-900 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100"
              />
              <button
                type="submit"
                className="inline-flex h-9 items-center justify-center rounded-md bg-indigo-600 px-4 text-xs font-semibold text-white shadow-sm hover:bg-indigo-500"
              >
                Email me the guide
              </button>
            </form>
          </div>
        </section>
      </main>

      <footer className="home-footer">
        <div className="home-footer-inner">
          <p>RiseFlow &mdash; School management built for African schools.</p>
          <p className="home-footer-meta">
            Day &amp; night view for busy school owners and parents.
          </p>
          <p className="home-footer-legal">
            <Link to="/terms">Terms of Service</Link>
            {' · '}
            <Link to="/privacy">Privacy &amp; Data Processing</Link>
          </p>
        </div>
      </footer>

      {/* WhatsApp floaty button */}
      <a
        href="https://wa.me/2349015718484?text=Hi%20RiseFlow%2C%20I%27d%20like%20to%20see%20a%20demo%20for%20my%20school."
        target="_blank"
        rel="noopener noreferrer"
        className="fixed bottom-4 right-4 z-50 inline-flex items-center gap-2 rounded-full bg-emerald-500 px-4 py-2 text-xs font-semibold text-white shadow-lg hover:bg-emerald-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2 focus:ring-offset-slate-50 dark:focus:ring-offset-slate-950"
      >
        <span className="inline-flex h-5 w-5 items-center justify-center rounded-full bg-white/10 text-[11px]">
          WA
        </span>
        <span>Chat with an expert</span>
      </a>
    </div>
  );
}

