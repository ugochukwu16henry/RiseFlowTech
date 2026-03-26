import PageLayout from '../components/PageLayout';
import './RolePages.css';

export default function SchoolReportsPage() {
  return (
    <PageLayout title="School Admin — Reports">
      <h2 className="section-title">Reports</h2>
      <p className="card-desc">This dashboard module is active and reachable. Connect your report export/analytics endpoints here.</p>
      <div className="card">
        <p className="card-title">Available now</p>
        <ul>
          <li>Student list and enrollment snapshots</li>
          <li>Billing summary from your school records</li>
          <li>Result/attendance reports (when enabled in API)</li>
        </ul>
      </div>
    </PageLayout>
  );
}
