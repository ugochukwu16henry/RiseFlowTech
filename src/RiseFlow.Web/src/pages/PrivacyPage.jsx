import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import './LegalPage.css';

export default function PrivacyPage() {
  return (
    <PageLayout title="Privacy Policy & Data Processing">
      <div className="legal-page">
        <p className="legal-updated">Last updated: March 2026. This policy is aligned with the Nigeria Data Protection Act (NDPA) 2023. For a full launch, have it reviewed by a Data Protection Compliance Organisation (DPCO) in your jurisdiction.</p>

        <h2>1. Who We Are</h2>
        <p>RiseFlow is an EdTech platform operated by me as a single owner and processes data on behalf of schools. Because I process data of minors (under 18), I am classified in Nigeria as a <strong>Data Controller of Major Importance</strong> and comply with Nigeria Data Protection Commission (NDPC) regulations. I apply similar standards across African markets where the Service operates.</p>

        <h2>2. Data We Collect</h2>
        <p>I collect and process the following categories of data for educational purposes:</p>
        <ul>
          <li><strong>Student:</strong> Names, date of birth, gender, National Identity Number (NIN) or equivalent where required, grades, class, and assessment results.</li>
          <li><strong>Parent/Guardian:</strong> Name, email, phone number, and link to student(s) via the Access Code system.</li>
          <li><strong>School staff:</strong> Name, email, phone, role, and class/subject assignments as provided by the school.</li>
        </ul>

        <h2>3. Purpose of Processing</h2>
        <p>Data is processed to facilitate <strong>academic record-keeping</strong>, <strong>parent-teacher communication</strong>, <strong>official transcript generation</strong>, billing (per school contract), and notifications related to the Service. I do not use personal data for marketing or profiling unrelated to the Service.</p>

        <h2>4. Security</h2>
        <p>I use <strong>AES-256 encryption for data at rest</strong> and <strong>TLS 1.3 for data in transit</strong>, in line with standard .NET and industry practice. Access to personal data is restricted to authorised personnel and systems necessary to operate the Service.</p>

        <h2>5. Minors’ Privacy</h2>
        <p>I only process student (minor) data with the <strong>explicit consent of the parent or guardian</strong>, obtained through the school’s use of the RiseFlow Parent Welcome Letter and the <strong>Access Code</strong> system. Parents link their account to a child by entering the unique code provided by the school.</p>

        <h2>6. Your Rights (Data Subjects)</h2>
        <p>Parents and data subjects have the right to <strong>access</strong>, <strong>correct</strong>, or <strong>request deletion</strong> of their or their child’s information at any time. Requests should be made through the school administration or by contacting RiseFlow. I will respond in line with NDPA 2023 and local data protection laws.</p>

        <h2>7. Data Retention</h2>
        <p>I retain data for as long as the school uses the Service and for a limited period thereafter as required by law or contract. When a school leaves, they have 30 days to export data before I delete it.</p>

        <h2>8. Cross-Border and African Jurisdictions</h2>
        <p>RiseFlow serves schools in Nigeria, Ghana, Kenya, South Africa, and other African countries. Where local law requires (e.g. NDPA in Nigeria), I ensure appropriate safeguards for cross-border processing and work with schools to meet national registration or DPCO requirements.</p>

        <p className="legal-footer">
          <Link to="/">← Back to RiseFlow</Link>
        </p>
      </div>
    </PageLayout>
  );
}
