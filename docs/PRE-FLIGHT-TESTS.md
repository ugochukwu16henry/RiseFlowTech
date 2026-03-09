# RiseFlow Pre-Flight Test Checklist (Staging)

This checklist is designed to be executed against your **staging** environment before onboarding the first real school.

## 1. Multi-tenant & Billing Safety

### 1.1 Multi-tenant data leak test

- Log in as **School A** (SchoolAdmin).
- Using the browser or API client, try to access a student from **School B**:
  - Example: `GET /api/students/{studentIdFromSchoolB}`
- **Expected**:
  - Response is **403 Forbidden** or **404 Not Found**.
  - You **never** see data for School B.

### 1.2 "First 50 Free" billing edge case

- For a fresh school with 0 students:
  - Add students one-by-one or via bulk import until you reach **exactly 50 active students**.
- Generate a billing record for the current month:
  - `POST /api/billing/generate` with the school’s Id and current month dates.
  - Inspect result or call `GET /api/billing?schoolId={schoolId}`.
- **Expected**:
  - AmountDue = `0` while student count ≤ 50.
- Then add the **51st** active student.
- Regenerate billing for the next period or re-run for the same label if not already created.
- **Expected**:
  - AmountDue increases by **₦500** (or configured rate) for the 1 paid student beyond the free 50.

### 1.3 Excel stress test (1,000 students)

- Prepare an Excel file using the RiseFlow template with ~1,000 rows of valid dummy students.
- Call:
  - `POST /api/students/bulk-upload` with the `.xlsx` file as `multipart/form-data`.
- **Expected**:
  - Request completes successfully (no timeout / gateway error).
  - Response includes `CreatedCount ≈ 1000` and a clear billing message.
  - New total student count matches previous count + created.

> Note: If your hosting environment enforces strict request timeouts, ensure the upstream reverse proxy (e.g. Nginx, Azure App Service) allows enough time for large imports.

### 1.4 Offline sync (mobile app)

_Requires mobile client implementation._

- On the mobile app:
  - Disconnect from the internet.
  - Enter or modify a grade for a student.
  - Reconnect and wait for sync.
- **Expected**:
  - Grade appears in the central API (e.g. via `GET /api/results/...`) and in the web dashboard.
  - No duplicate results are created (idempotent sync).

## 2. Paystack & Billing Verification

### 2.1 Webhook security

- Configure:
  - `Paystack:SecretKey` with your staging secret.
  - `Paystack:WebhookSecret` with the webhook signing secret (if provided).
  - `Paystack:WebhookTrustedIPs` with Paystack’s official IP addresses or CIDR ranges (comma-separated).
- In Paystack dashboard, point webhooks to:
  - `POST {STAGING_BASE_URL}/api/paystack/webhook`
- Trigger a small test payment.
- **Expected**:
  - For valid Paystack IPs + signature:
    - `PaymentService.HandlePaystackWebhookAsync` marks the correct `BillingRecord` as paid.
  - For invalid IPs:
    - Webhook is rejected with **403 Forbidden** (and logged).

### 2.2 Currency conversion

- Call:
  - `POST /api/billing/convert` with `{ "amount": 500, "fromCurrencyCode": "NGN" }`.
- **Expected**:
  - Response contains:
    - `UsdAmount` > 0.
    - `AmountsByCurrency["GHS"]`, `["KES"]`, etc., matching your configured exchange rates.

### 2.3 Receipt generation

- After a successful test payment (via Paystack webhook or `PATCH /api/billing/{id}/pay`):
  - Call `GET /api/billing/{billingRecordId}/receipt` as:
    - SuperAdmin, or
    - SchoolAdmin of the billed school.
- **Expected**:
  - Response is `200 OK` with `Content-Type: application/pdf`.
  - PDF shows:
    - School name.
    - Billing period.
    - Amount paid and currency.
    - Payment reference.

## 3. Nigerian Legal & NDPC Compliance (2026)

### 3.1 Data Protection Officer (DPO)

- As SuperAdmin, call:
  - `PUT /api/superadmin/compliance-settings` with:
    - `dataProtectionOfficerName`
    - `dataProtectionOfficerEmail`
    - `dpiaDocumentUrl` (link to your DPIA PDF or page).
- Verify via:
  - `GET /api/superadmin/compliance-settings`.
- **Expected**:
  - Values persist and are displayed in the Control Room UI under a “Data Protection / NDPC” section.

### 3.2 DPIA readiness

- Ensure the DPIA document referenced by `dpiaDocumentUrl` is:
  - Up to date.
  - Describes data flows (students, parents, teachers).
  - Covers NDPC requirements for EdTech platforms in Nigeria.
- **Expected**:
  - When schools request your DPIA, you can share this URL or PDF immediately.

### 3.3 NIN privacy (encryption at rest)

- Confirm `Encryption:Key` is set in configuration (Base64, 32 bytes).
- In staging:
  - Create or update a student with a **NIN** and/or **NationalIdNumber**.
  - Inspect the underlying database row directly.
- **Expected**:
  - NIN and NationalIdNumber fields are stored as encrypted values starting with `"RFENC:"`.
  - Application reads/decrypts them transparently.

## 4. Principal’s Demo Flow

Run this flow on staging with demo data when presenting to a principal:

1. **Excel Import**
   - Use `POST /api/students/bulk-upload` or the UI to import a list.
   - Emphasize: “Your whole school is online in minutes.”
2. **Parent View**
   - Show Parent Access Codes and parent welcome letters.
   - Demonstrate WhatsApp / communication entry point in the parent UI.
3. **Transcript QR**
   - Generate a transcript via the API or UI.
   - Print or open the PDF and scan the QR code to show verification is “unforgeable”.
4. **Control Room**
   - Open the SuperAdmin and School dashboards:
     - Show grading completion views.
     - Highlight which teachers have pending grading.

