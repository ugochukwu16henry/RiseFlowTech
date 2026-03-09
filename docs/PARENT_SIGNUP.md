# Parent Signup: School Gateway vs App Home Page

RiseFlow supports **two ways** for parents to create an account. Both are valid; the **school gateway** is recommended for most schools.

---

## Option 1: School gateway (recommended)

**How it works**

1. **School Admin** gets a **Parent signup link** for their school (e.g. from the **Access Codes** or **Manage access codes** page).
2. The link looks like:  
   `https://yourapp.com/parent/signup?school=<SCHOOL_ID>`
3. The school shares this link with parents (WhatsApp, SMS, letter, etc.).
4. Parent opens the link → lands on the **signup form already scoped to that school** (no need to choose a school).
5. Parent enters **email**, **password**, **name**, **phone** → account is created for **that school only**.
6. After signup, parent is sent to **Claim your child** to enter the **Parent Access Code** (e.g. RF-8821) and link to their child.

**Pros**

- Parent is always tied to the correct school.
- No “which school?” step; fewer mistakes.
- School controls who gets the link; feels like the school’s own portal.
- Same link for all parents of that school; easy to share.

**Cons**

- School must share the link (e.g. at enrollment or when sending codes).

**Where it’s implemented**

- **Backend:** `POST /api/parents/signup` (body: `schoolId`, `email`, `password`, `fullName`, `phone`).
- **Frontend:** Route `/parent/signup?school=<id>`. Form builds the signup link and uses the `school` query param so the frontend knows which school the parent is joining.
- **School Admin:** On the **Access Codes** page, a “Parent signup link” section with a **Copy** button that uses the current school ID (e.g. from tenant context / `riseflow-tenant-id`) to build the gateway URL.

---

## Option 2: App home page signup

**How it works**

1. Parent goes to the main RiseFlow site (e.g. home page).
2. Clicks **Sign up** or **Parent? Sign up**.
3. On the signup page they choose or search for **their school** (or enter a school code if you add one).
4. Then they enter email, password, name, phone → account created for the **chosen school**.
5. Then they can **Claim your child** with the access code.

**Pros**

- One place for all parents; good for discovery and marketing.
- Works when the parent finds the app themselves (e.g. from the store or a general link).

**Cons**

- Parent must know which school to pick; wrong school = wrong data.
- Needs a school selector (list/search or code) and possibly a “school code” if you don’t want to show a full list.

**Implementation**

- **Backend:** Same `POST /api/parents/signup` with `schoolId` (and optionally a public “school code” that the API resolves to `schoolId`).
- **Frontend:** Route `/parent/signup` **without** `?school=`. Show a step to **select or enter school** first, then the same signup form, then redirect to **Claim your child**.

---

## Recommendation

- **Primary:** Use the **school gateway** link. School Admin copies the link from the Access Codes (or similar) page and shares it with parents. Parents sign up via that link, then claim their child with the code.
- **Secondary:** Add **home page signup** with a school selector (or school code) so parents who land on the app can still sign up without the gateway link.

---

## Summary

| Question | Answer |
|----------|--------|
| Will parents sign up via a **school gateway**? | **Yes.** Each school has a signup URL with `?school=<id>`. School Admin gets it from the Access Codes (or similar) page and shares it. |
| Will parents sign up from the **app home page**? | **Optional.** You can add a generic “Parent sign up” flow where they select or enter the school, then use the same signup API and then Claim by code. |
| Which one to use first? | Implement **school gateway** first (link + `/parent/signup?school=` + API). Add home page signup later if you want. |
