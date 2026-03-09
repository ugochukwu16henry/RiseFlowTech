import { Link } from 'react-router-dom';

export default function PageLayout({ title, children, backTo = '/', showRoleLinks = true }) {
  return (
    <div className="app">
      <header className="header">
        <div className="header-row">
          <Link to={backTo} className="header-back" aria-label="Back to dashboard">← Back</Link>
          <span className="header-logo-text">{title}</span>
          {showRoleLinks && (
            <nav className="header-nav header-nav--role">
              <Link to="/school" className="header-link">School Admin</Link>
              <Link to="/teacher" className="header-link">Teacher</Link>
              <Link to="/student" className="header-link">Student</Link>
              <Link to="/parent" className="header-link">Parent</Link>
              <Link to="/super-admin" className="header-link">Super Admin</Link>
            </nav>
          )}
        </div>
      </header>
      <main className="main">{children}</main>
    </div>
  );
}
