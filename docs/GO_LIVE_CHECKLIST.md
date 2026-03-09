# Technical "Go-Live" Checklist

Before you sign up your first paying school, ensure these **enterprise** features are ready.

---

## 1. Database isolation (tenant security)

**Goal:** School A must never see or change School B’s data, even if someone tampers with URLs or headers.

### What RiseFlow does

- **Global query filter:** Every entity that implements `ITenantEntity` (Students, Teachers, Results, Classes, etc.) is filtered by `SchoolId`. The value comes from:
  - `X-Tenant-Id` request header (set by the frontend from logged-in user’s school), or
  - The authenticated user’s `SchoolId` claim.
- **Controller checks:** Protected endpoints that need a tenant return `403 Forbid()` when `CurrentSchoolId` is null.
- **Per-entity scope:** Queries like `GetById(id)` use the same filter, so a request for another school’s ID returns no row and the API returns **404**, not data.

### Penetration test (run before go-live)

1. **Create two schools** (e.g. School A and School B) and get at least one student and one result for each.
2. **Log in as a user from School A** (School Admin or Teacher). Note the `SchoolId` for School A and the student/result IDs for **School B** (e.g. from a backup/export or a separate Super Admin view).
3. **Call the API as School A** with the correct `X-Tenant-Id` for School A:
   - `GET /api/students/{schoolB_studentId}`  
     **Expected:** 404 (not 200 with School B’s student).
   - `GET /api/results/{schoolB_resultId}`  
     **Expected:** 404 (not 200 with School B’s result).
   - `PUT /api/results/{schoolB_resultId}` with a body  
     **Expected:** 404 (not 200; grade must not change).
4. **Optional:** Change the request header to `X-Tenant-Id: {schoolB_id}` while still using a School A user. The API should either still scope by the user’s claim (if you enforce “user must belong to tenant”) or return 403 when the user’s `SchoolId` doesn’t match the header, depending on your policy.

**Sign-off:** No cross-tenant data is returned or updated when using another school’s IDs or tampering with tenant context.

---

## 2. Automated backups

**Goal:** If the server or database fails, you do not lose a school’s long-term academic history (e.g. 10+ years).

### What to do (hosting-specific)

- **Database:** Turn on **daily automated snapshots/backups** for your PostgreSQL (or current DB) instance.
  - **Railway:** Use the project’s database backup/snapshot feature and confirm retention (e.g. 7–30 days).
  - **Azure:** Enable automated backups for the SQL/PostgreSQL resource and set retention.
  - **AWS:** RDS automated backups + optional export to S3 for long-term retention.
- **Retention:** Keep at least 7 days of daily backups; consider 30 days or more for compliance.
- **Restore test:** Periodically run a restore to a separate instance and verify data and app connectivity.
- **Application assets:** If you store files (e.g. logos, photos) on disk, include them in your backup or use object storage (e.g. S3) with versioning/backup.

**Sign-off:** Daily backups are enabled, retention is documented, and a restore has been tested at least once.

---

## 3. WhatsApp API approval (if you send automated WhatsApp messages)

**Goal:** If you send result notifications or other messages to parents via WhatsApp, avoid being banned for “spamming” by using official Meta channels.

### What to do

- **Meta Business Account:** Apply for and complete the **Meta Business Account** and **WhatsApp Business API** onboarding (or WhatsApp Cloud API).
- **Templates:** Use only **approved message templates** for proactive notifications (e.g. “Your child’s results are ready”); do not send free-form marketing or unsolicited bulk messages.
- **Opt-in:** Where required, keep evidence of parent opt-in for notifications.
- **Rate limits:** Respect WhatsApp rate limits and back off on errors.

If RiseFlow does **not** yet send automated WhatsApp messages (e.g. only shows “WhatsApp” links that open the user’s WhatsApp app), you can defer this until you add that feature. Then implement it using the official API and approved templates.

**Sign-off:** If WhatsApp notifications are in scope, Meta/WhatsApp approval and templates are in place; otherwise, document “Not applicable until feature is added.”

---

## 4. Audit logs (“Who did what?”)

**Goal:** When a grade (or other sensitive data) is changed, the system records **who** changed it and **when**, so you can investigate and comply with school or regulatory requirements.

### What RiseFlow implements

- **Audit log entity:** `AuditLog` stores:
  - School (tenant) ID  
  - Action: `Created`, `Updated`, `Deleted`  
  - Entity type (e.g. `StudentResult`)  
  - Entity ID  
  - User identifier (e.g. email) and optional display name  
  - Optional details (e.g. “Score 65 → 72”)  
  - Timestamp (UTC)  
- **Result (grade) changes:** Every create, update, and delete of a `StudentResult` is logged with the current user’s email and a short description (e.g. “Result created” or “Result updated: Score 65 → 72”).
- **Where to view:** Call **`GET /api/superadmin/audit`** (Super Admin only). Optional query params: `schoolId`, `fromUtc`, `toUtc`, `limit` (default 200, max 1000). Returns who changed what and when.

### Extending audit logging

- Add logging for other sensitive actions (e.g. student record updates, teacher assignment changes, billing actions) by calling the same audit service from the relevant controllers.
- Optionally add a **retention policy** (e.g. keep audit logs for 2 or 7 years) and document it in your privacy/terms.

**Sign-off:** Grade create/update/delete are audited; process for reviewing logs (and any retention) is documented.

---

## Quick reference

| Item                 | Owner    | Status |
|----------------------|----------|--------|
| Tenant isolation     | Dev/Ops  | [ ] Penetration test passed |
| Automated backups    | Ops      | [ ] Daily backups + restore tested |
| WhatsApp approval    | Product  | [ ] N/A or approved |
| Audit logs           | Dev      | [ ] Result changes logged; review process set |

Once all items are signed off, you’re in a strong position to onboard your first paying school with enterprise-grade safety and compliance in mind.
