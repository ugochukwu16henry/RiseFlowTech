import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import './LegalPage.css';

export default function TermsPage() {
  return (
    <PageLayout title="Terms of Service">
      <div className="legal-page">
        <p className="legal-updated">Last updated: March 2026. For a full launch, have these terms reviewed by a Data Protection Compliance Organisation (DPCO) in your jurisdiction.</p>

        <h2>1. The Service</h2>
        <p>RiseFlow is a cloud-based school management platform (“the Service”) provided and operated by the sole owner of RiseFlow. The Service is provided <strong>“as-is”</strong> to support academic record-keeping, parent-teacher communication, billing, and related operations for schools in Nigeria and other African countries.</p>

        <h2>2. Billing</h2>
        <p>Schools are charged per student per month in their chosen local currency (e.g. 500 Naira per student in Nigeria) for any student beyond the first 50, who are free. Invoices are issued monthly. <strong>Failure to pay within 7 days of the invoice date may result in “Read-Only” access to results and certain features</strong> until payment is received. Pricing may vary by country and currency as displayed in the platform.</p>

        <h2>3. Data Ownership</h2>
        <p>The <strong>school owns the student and staff data</strong>; RiseFlow only <strong>processes</strong> it on behalf of the school as a data processor. If a school leaves the platform, they have <strong>30 days to export their data</strong> before it is deleted from our systems. RiseFlow will not use school or student data for marketing or other purposes beyond providing the Service.</p>

        <h2>4. Prohibited Use</h2>
        <p>No school or user shall use RiseFlow to store illegal content, harass teachers or parents, or use the Service in any way that violates applicable laws (including the Nigeria Data Protection Act 2023 and similar laws in other African jurisdictions). Breach may result in suspension or termination of access.</p>

        <h2>5. Data Processing Agreement</h2>
        <p>By using the Service, the school agrees to the data processing terms set out in our <Link to="/privacy">Privacy Policy</Link>, which describe how we process personal data in line with the Nigeria Data Protection Act (NDPA) 2023 and the Nigeria Data Protection Commission (NDPC) requirements. For schools in other African countries, we align with applicable national data protection laws.</p>

        <p className="legal-footer">
          <Link to="/">← Back to RiseFlow</Link>
        </p>
      </div>
    </PageLayout>
  );
}
