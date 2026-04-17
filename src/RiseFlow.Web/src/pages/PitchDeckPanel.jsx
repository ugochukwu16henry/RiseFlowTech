const PITCH_DECK_SECTIONS = [
  {
    title: 'The Problem (The Paper Burden)',
    points: [
      'The reality: Teachers spend major time writing on paper instead of teaching.',
      'The risk: Paper records get lost, burnt, or damaged. Long-term proof of graduation becomes hard.',
      'The communication gap: Parents mostly hear from schools only during PTA meetings or result days.',
    ],
  },
  {
    title: 'The Solution (Introducing RiseFlow)',
    points: [
      'One platform, total control: Manage students, teachers, and results from phone or laptop.',
      'Instant digitalization: Upload your full school list in minutes using Excel.',
      'Brand identity: Your logo, your school name, and your official digital stamp on documents.',
    ],
  },
  {
    title: 'Why Parents Will Love Your School',
    points: [
      'Dedicated parent hub with real-time student progress.',
      'Direct teacher access through WhatsApp links.',
      'One login for parents with multiple children.',
      'Digital result alerts immediately after approval.',
    ],
  },
  {
    title: 'Why Teachers Will Be More Productive',
    points: [
      'Secondary: Automatic ranking and grade calculations.',
      'Primary: Customizable social habit and psychomotor tracking.',
      'Zero math errors: System handles calculations while teachers focus on scoring.',
    ],
  },
  {
    title: 'The Digital Transcript (Competitive Edge)',
    points: [
      'Future-ready transcripts for local and international transfer needs.',
      'QR verification for authenticity of school records.',
      'Aligned with NDPR-focused data handling and future government integrations.',
    ],
  },
  {
    title: 'Pricing (Fair-Growth Model)',
    points: [
      'Start free: first 50 students at N0.',
      'Scale affordably: N500 per student after the first 50.',
      'No hidden fees: includes portal, mobile experience, and future updates.',
    ],
  },
  {
    title: 'How to Deliver the Pitch',
    points: [
      'Run a 5-minute Excel import demo live.',
      'Show the WhatsApp parent-to-teacher contact flow.',
      'Show paper vs digital side-by-side and ask which reflects a modern school.',
      'Final pro tip: one successful school in a locality drives referrals from neighboring schools.',
    ],
  },
];

export default function PitchDeckPanel({ roleTitle }) {
  return (
    <section className="progress-section pitch-deck-panel" aria-label="RiseFlow pitch deck">
      <p className="dashboard-label">RiseFlow</p>
      <h3 className="section-title pitch-deck-heading">The "Future-Ready" School Pitch Deck</h3>
      <p className="card-desc">
        One platform. Total control. Built for African schools. Use this script from your {roleTitle} dashboard.
      </p>
      <p className="pitch-deck-domain">riseflow.com</p>

      <div className="pitch-deck-grid">
        {PITCH_DECK_SECTIONS.map((section, idx) => (
          <article key={section.title} className="pitch-deck-card">
            <p className="pitch-deck-step">{idx + 1}</p>
            <h4>{section.title}</h4>
            <ul>
              {section.points.map((point) => (
                <li key={point}>{point}</li>
              ))}
            </ul>
          </article>
        ))}
      </div>
    </section>
  );
}
