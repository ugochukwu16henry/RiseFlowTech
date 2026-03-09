import { getApiBase } from '../api';
import './StudentPhoto.css';

/**
 * Renders a student's passport photo with fallback to initials.
 * Use for lists and detail views. Photo is loaded from GET /api/students/{id}/photo (credentials sent for same-origin).
 */
export default function StudentPhoto({ studentId, firstName, lastName, size = 40, className = '' }) {
  const photoUrl = `${getApiBase()}/api/students/${studentId}/photo`;
  const initials = [firstName, lastName].filter(Boolean).map((s) => (s || '').charAt(0).toUpperCase()).join('') || '?';

  return (
    <span className={`student-photo student-photo--${size} ${className}`.trim()} aria-hidden="true">
      <img
        src={photoUrl}
        alt=""
        className="student-photo-img"
        onError={(e) => {
          e.target.style.display = 'none';
          e.target.nextElementSibling?.classList.add('student-photo-initials--show');
        }}
      />
      <span className="student-photo-initials">{initials}</span>
    </span>
  );
}
