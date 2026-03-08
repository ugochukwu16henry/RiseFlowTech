# RiseFlow Feature Roadmap (Development Order)

| Order | Feature | Description | Status |
|-------|---------|-------------|--------|
| **1st** | **Identity & Roles** | Use ASP.NET Core Identity. Create roles: SuperAdmin, SchoolAdmin, Teacher, Parent. | ✅ Done – Identity + roles (SuperAdmin, SchoolAdmin, Teacher, Parent, Student); RoleSeeder; SchoolId claim. |
| **2nd** | **School Onboarding** | A public page where a Principal signs up, uploads their logo, and sets their school name. | ✅ API + logo: `POST /api/schools/onboard-with-logo` (multipart). `School.LogoFileName` stored. |
| **3rd** | **Student/Teacher Management** | Bulk upload students via Excel (very important for Nigerian schools with 1000+ kids). | ✅ CRUD + `POST /api/students/bulk-upload` (Excel .xlsx). Template: FirstName, LastName, MiddleName, AdmissionNumber, Gender, DateOfBirth. |
| **4th** | **The Billing Engine** | Logic: Count(Students). If > 50, trigger a payment gateway (e.g. Paystack or Flutterwave) for 500 Naira/student. | ✅ Logic + `POST /api/billing/initiate-payment` returns Paystack-style authorization URL and reference. |
| **5th** | **Result Portal** | Teachers enter scores; the system calculates the total and position in class. | ✅ Teachers enter scores; parents view; `GET /api/results/class-rankings?termId=&classId=` returns total and position in class. |

---

## Implementation details

- **1st – Identity & Roles:** `Program.cs` (Identity), `ApplicationUser`, `RoleSeeder`, `Roles` constants.
- **2nd – School Onboarding:** `SchoolsController.Onboard`, `OnboardWithLogo`; `SchoolOnboardingService.OnboardSchoolWithLogoAsync`; `School.LogoFileName`; logos under `wwwroot/logos/`.
- **3rd – Student/Teacher Management:** `StudentsController`, `TeachersController`; `StudentBulkUploadService`; `POST /api/students/bulk-upload` (SchoolAdmin, .xlsx).
- **4th – Billing:** `BillingService`, `BillingController`, `CountryBillingConfig`; `POST /api/billing/initiate-payment` (returns gateway URL + reference; wire Paystack/Flutterwave API in production).
- **5th – Result Portal:** `ResultsController`; `GET /api/results/class-rankings?termId=&classId=` (Teacher/SchoolAdmin) returns `ClassRankingDto` (TotalScore, MaxTotal, Percentage, PositionInClass).
