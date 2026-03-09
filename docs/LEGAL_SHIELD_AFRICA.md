# Legal Shield for RiseFlow (African Markets)

RiseFlow is an EdTech processing data of minors. In Nigeria (as of 2026), the **Nigeria Data Protection Commission (NDPC)** is very active, and such processing classifies RiseFlow as a **Data Controller of Major Importance**. The following is tailored for African nations (Nigeria, Ghana, Kenya, South Africa, etc.). **For a full launch, have all templates reviewed by a Data Protection Compliance Organisation (DPCO) in your jurisdiction.**

---

## 1. Terms of Service (The Contract)

- **Location:** Web app at `/terms` (and linked from onboarding and footer).
- **Purpose:** Limits liability and explains billing.
- **Key points:**
  - **The Service:** RiseFlow is a cloud-based school management tool provided **“as-is”.**
  - **Billing:** Schools are charged per student per month (e.g. 500 Naira per student in Nigeria) for any student beyond the first 50. Failure to pay within 7 days of the invoice may result in **“Read-Only”** access to results.
  - **Data Ownership:** The school owns the student data; RiseFlow only **processes** it. If a school leaves, they have **30 days** to export their data before it is deleted.
  - **Prohibited Use:** No school may use RiseFlow to store illegal content or harass teachers/parents.

**Implementation:** At school signup (onboarding), a checkbox **“I agree to the RiseFlow Terms of Service and Data Processing Agreement”** is required when creating an admin account. The backend rejects registration without this consent.

---

## 2. Privacy Policy (NDPA 2023 Compliant)

- **Location:** Web app at `/privacy`.
- **Purpose:** Tells parents and regulators how sensitive information is handled.
- **Covers:**
  - **Data collected:** Names, DOB, gender, NIN (or equivalent), grades, parent contact info.
  - **Purpose:** Academic record-keeping, parent-teacher communication, official transcript generation.
  - **Security:** AES-256 at rest, TLS 1.3 in transit (standard for .NET).
  - **Minors’ privacy:** Student data only with **explicit parent/guardian consent** via the Access Code system and Parent Welcome Letter.
  - **Data rights:** Access, correct, or delete data at any time (via school or RiseFlow).

---

## 3. Admin Control Room Metrics (Super Admin Dashboard)

The Super Admin dashboard shows these “big picture” numbers:

| Metric | Description |
|--------|-------------|
| **Total Revenue (MRR)** | Total revenue this month from all schools (USD equivalent). |
| **Active Students** | Total count across all schools (for scaling and planning). |
| **Data Health** | Number of schools that have completed “Term Results” (at least one result entered) vs active schools. |
| **Payment Delinquency** | List of schools with **>50 students** that have **not paid** (may lead to read-only access). |
| **Compliance Status** | List of schools that have **not yet** had their **Signed Data Consent** forms recorded. Super Admin can **“Mark received”** when forms are uploaded. |

- **API:** `GET /api/superadmin/dashboard` returns the above (including `paymentDelinquency`, `schoolsWithTermResultsCount`, `compliancePending`).
- **Mark consent received:** `PATCH /api/schools/{id}/data-consent-received` (Super Admin only).

---

## 4. Project Structure (Current)

RiseFlow uses a **single API project** plus a React web client. For reference, a typical split could be:

| Layer | Role | RiseFlow implementation |
|-------|------|--------------------------|
| **API** | Entry point, controllers | `RiseFlow.Api` — Controllers (Schools, Students, Billing, SuperAdmin, Parents, etc.). |
| **Domain** | Core rules (e.g. “First 50 free”, grading) | Logic lives in **RiseFlow.Api** (e.g. `BillingService`, `CountryBillingConfig`, services). |
| **Infrastructure** | Database, payments, file storage | **RiseFlow.Api** — EF Core (`RiseFlowDbContext`), Paystack (e.g. `PaymentService`), file storage (e.g. `wwwroot/logos`). |
| **Client** | Web dashboard (and future mobile) | **RiseFlow.Web** (React). Future: .NET MAUI mobile app. |

So: **RiseFlow.Api** = API + domain-style logic + infrastructure; **RiseFlow.Web** = Web dashboard. A future refactor could separate Domain and Infrastructure into their own projects if needed.

---

## 5. Database Fields for Legal/Compliance

- **School.TermsAndDpaAgreedAt** — Set when the school (admin) agrees to ToS and DPA at signup.
- **School.DataConsentFormReceivedAt** — Set when Super Admin records receipt of the school’s signed Data Consent forms (NDPA compliance). Used for the “Compliance Status” list and “Mark received” action.

---

## 6. Parent Welcome Letter (NDPA Consent)

The **Parent Welcome Letter** (one per student, PDF from Access Codes page) doubles as a **manual** and a **legal consent form** for processing minors’ data under NDPA 2023. It includes the school name/logo, student access code, and a **Data Protection & Consent (NDPA 2023)** paragraph. Schools print and collect signed copies; Super Admin can track which schools have had their signed forms recorded via **Compliance Status** and **“Mark received”**.
