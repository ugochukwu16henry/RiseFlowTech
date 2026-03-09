# Security Implementation Notes

## 1. Tenant verification on Update/Delete

All controller **Update** (PUT/PATCH) and **Delete** actions that modify tenant-scoped entities now **explicitly verify** that the record’s `SchoolId` matches the current user’s tenant (`CurrentSchoolId`). If it does not, the API returns **403 Forbid**.

- **StudentsController:** Update, Delete  
- **TeachersController:** Update, Delete, UnassignFromClass  
- **ResultsController:** Update, Delete  
- **AcademicTermsController:** Update, Delete  
- **SubjectsController:** Update, Delete, UnassignTeacherFromSubject, UnassignSubjectFromClass, UnassignTeacherFromClassSubject  

Assign/Unassign operations that work with link tables (e.g. Teacher–Subject) verify that the related entities (Teacher, Subject, Class) belong to the current school before making changes.

**Super Admin**–only endpoints (e.g. BillingController.RecordPayment) do not enforce tenant match, by design.

---

## 2. Rate limiting (auth)

**Login** and **forgot-password** are protected by a **fixed-window rate limiter** named `"Auth"`:

- **Limit:** 10 requests per **1 minute** per client (by IP when no other partition is configured).
- **Response when exceeded:** **429 Too Many Requests**.

- **Endpoints:**
  - `POST /api/auth/login` — `[EnableRateLimiting("Auth")]`
  - `POST /api/auth/forgot-password` — `[EnableRateLimiting("Auth")]`

To change the limit or window, edit `Program.cs` → `AddRateLimiter` → `AddFixedWindowLimiter("Auth", ...)`.

**Password reset:** `POST /api/auth/forgot-password` is implemented as a stub (generates a token but does not send email). When you add email sending, keep the same rate limit to avoid abuse and email enumeration.

---

## 3. Sensitive data encryption at rest

**NIN** (National Identification Number), **NationalIdNumber**, and **phone numbers** are encrypted at rest when an encryption key is configured.

### How it works

- **Encryption:** AES-256-GCM. Stored value format: `"RFENC:"` + Base64(nonce + ciphertext + tag).
- **Scope:**  
  - **Student:** NIN, NationalIdNumber, EmergencyContactPhone  
  - **Teacher:** NIN, NationalIdNumber, Phone  
  - **Parent:** Phone  
  - **School:** Phone  

- **Configuration:** Set a 256-bit key (Base64) in configuration:
  - Key: `Encryption:Key`
  - Example (appsettings or env): `"Encryption:Key": "<base64-32-bytes>"`

- **If no key is set:** Values are stored and read as **plaintext** (backward compatible). Existing data continues to work.
- **If key is set:** New and updated values are encrypted on write and decrypted on read. Existing plaintext in the database is still returned as-is until those rows are updated (then they are stored encrypted).

### Generating a key

Run once (e.g. in a small console app or API endpoint used only by an admin):

```csharp
var key = RiseFlow.Api.Services.SensitiveDataEncryption.GenerateKeyBase64();
// Store key in configuration / secrets (e.g. Azure Key Vault, env var Encryption__Key).
```

Or generate 32 random bytes and Base64-encode them (e.g. `openssl rand -base64 32`).

### Migration

The migration **EncryptSensitiveDataAtRest** increases the column max length for the encrypted fields (e.g. to 512 characters) so that the Base64-encoded ciphertext fits. Apply with your usual process:

```bash
dotnet ef database update --project src/RiseFlow.Api
```

### Important

- **Rotating the key:** Decrypt all values with the old key and re-encrypt with the new key (custom script or migration). The value converter does not support multiple keys.
- **Backup:** Keep the key in a secure secret store (e.g. Azure Key Vault, AWS Secrets Manager, env var in production). Losing the key makes encrypted data unrecoverable.
