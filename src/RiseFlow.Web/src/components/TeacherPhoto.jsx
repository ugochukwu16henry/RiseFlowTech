import { getApiBase } from '../api';
import './StudentPhoto.css';

/**
 * Renders a teacher's passport photo with fallback to initials.
 * Photo is loaded from GET /api/teachers/{id}/photo.
 */
export default function TeacherPhoto({ teacherId, fullName, size = 40, className = '' }) {
  const photoUrl = `${getApiBase()}/api/teachers/${teacherId}/photo`;
  const parts = (fullName || '').split(' ').filter(Boolean);
  const first = (parts[0] || '').charAt(0).toUpperCase();
  const last = (parts[parts.length - 1] || '').charAt(0).toUpperCase();
  const initials = (first && last ? `${first}${last}` : first || '?') || '?';

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

