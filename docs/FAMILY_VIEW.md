# Family View — One Parent, Multiple Children

RiseFlow supports **one parent account linked to multiple students** (e.g. 2–4 children in the same school). Parents stay in one account and switch between children on the dashboard.

---

## 1. Parent Access Codes

- **Format:** `RF-` + 4 characters, e.g. **RF-7G2B**.
- **Character set:** `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (no `0`, `O`, `1`, `I`) so codes are easy to type and read.
- **Uniqueness:** Per school; one code per student.
- **Generation:** School Admin uses **Access Codes** → “Generate codes for students without code”. Logic lives in `StudentsController.GenerateUniqueAccessCodeAsync`.

---

## 2. Database: Linking Parents to Students

- **Student:** Has `ParentAccessCode` (nullable). No single `ParentId` on Student.
- **Link table:** `StudentParents` (many-to-many):
  - `StudentId`, `ParentId`, `RelationshipToStudent`, `IsPrimaryContact`, `CreatedAtUtc`
- One parent can have many students; one student can have many parents (e.g. mother and father).

**Linking flow:**

1. School generates codes (or assigns) per student.
2. Parent signs up (or already has an account).
3. Parent goes to **Claim your child**, enters code (e.g. RF-7G2B).
4. Backend checks code exists for the school and is not already linked to this parent; then inserts a row in `StudentParents`. Code stays on the student for other parents to use if needed.

---

## 3. Parent Dashboard (Family View)

**Top bar**

- **My Children:** Row of circular avatars (initials). Click to switch the active child.
- **Add another child:** Link to `/parent/claim` to enter another code.

**When a child is selected**

- **Left column — Student profile**
  - Class name  
  - Attendance (placeholder “—” until attendance feature exists)  
  - Current term average (from results for the current term)

- **Right column — Assigned teachers**
  - One card per teacher (for that child’s class), with **Subject** when available.
  - Actions: **Call** (`tel:`), **WhatsApp**, **Email** (`mailto:`).

**Bottom**

- **Performance snapshot:** Overall % and per-subject progress for the selected child (from `/api/results/my-children`, filtered by student).
- **Download PDF report:** Placeholder (“coming soon”).

---

## 4. APIs Used

| Endpoint | Purpose |
|---------|--------|
| `GET /api/parents/my-children` | List linked children (id, name, class, term average). |
| `GET /api/contacts/teachers?studentId=<id>` | Teachers for that child’s class, with **Subject** (from TeacherClassSubject). |
| `GET /api/results/my-children` | All results for parent’s children; frontend filters by selected child. |
| `POST /api/parents/link-by-code` | Link current parent to a student by access code. |

---

## 5. Files Touched

- **Backend:** `ParentsController` (my-children), `ContactsController` (teachers?studentId=, Subject in DTO), `StudentsController` (comment on access code format).
- **Frontend:** `ParentPage.jsx` (Family View UI), `ParentPage.css` (layout and cards).
