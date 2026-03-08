# RiseFlow UI/UX Design Strategy

## Target Context: Nigerian & African Markets

Design must be **mobile-first** and **low-bandwidth friendly**. Many users will be on 3G/4G; data costs and load times matter.

---

## 1. Core Principles

| Principle | Application |
|-----------|-------------|
| **Mobile-first** | Design for small screens first; enhance for tablet/desktop. Touch targets ≥ 44px. |
| **Low-bandwidth** | Minimize payloads: lazy-load images, small assets, avoid heavy JS. Prefer server-rendered or minimal SPA. |
| **Progressive disclosure** | Show essentials first; details on tap/expand. |
| **Offline-tolerant** | Where possible, cache critical views (e.g. last results) for repeat visits. |

---

## 2. Design Language: EdTech Professional

- **Vibe:** Clean, trustworthy, “growth-focused” — suitable for schools, parents, and teachers.
- **Avoid:** Playful/kiddy visuals; cluttered dashboards; long forms without clear progress.

---

## 3. Color Palette

| Role | Color | Use | Hex (suggestion) |
|------|--------|-----|-------------------|
| **Success Green** | Growth, progress, positive actions | CTAs, success states, progress bars, “View Results” | `#0D9488` (teal) or `#059669` (emerald) |
| **Deep Blue** | Trust, stability, primary UI | Headers, primary buttons, links, nav | `#1E3A5F` or `#1E40AF` |
| **White** | Clarity, space | Backgrounds, cards | `#FFFFFF` |
| **Neutral** | Text, borders, disabled | Body text, secondary text, dividers | `#374151`, `#9CA3AF`, `#E5E7EB` |

**Accessibility:** Ensure contrast ratios ≥ 4.5:1 for body text, ≥ 3:1 for large text and UI components.

---

## 4. Dashboard Cards (Large, Easy-to-Tap)

- **Size:** Cards should be large enough to tap with a thumb — minimum ~48px height, generous padding.
- **Content:** One primary action per card; label clearly (e.g. “View Results”, “Message Teacher”, “Pay Fees”).
- **Hierarchy:** Primary action = solid Success Green or Deep Blue; secondary = outline or text.
- **Icons:** Simple, recognizable icons (e.g. results, message, payment) to support quick scanning.

Example card layout (conceptual):

```
┌─────────────────────────────────────┐
│  [Icon]  View Results               │
│          See latest grades           │
└─────────────────────────────────────┘
```

Use a 1-column layout on mobile; 2 columns on larger screens if needed.

---

## 5. Progress Visualization (Academic Performance)

- **Use simple progress bars** so parents can see “how the student is doing” at a glance.
- **Metrics:** e.g. “Overall”, “By subject”, “This term vs last term”.
- **Visual:** Single horizontal bar (Success Green for fill, light gray for track); optional short label (e.g. “Math 78%”).
- **Avoid:** Complex charts on mobile; prefer one clear number + one bar per subject or one overall bar.

Example:

```
Math        ████████████░░░░  78%
English     ██████████████░░  88%
```

---

## 6. Performance & Bandwidth

- **Images:** Lazy-load below the fold; use WebP/AVIF with fallbacks; compress.
- **Fonts:** System fonts or a single webfont (e.g. one weight of Inter or Open Sans) to reduce requests.
- **JS/CSS:** Minimize bundle size; code-split by route; avoid large dependencies on first load.
- **API:** Design APIs to return only needed fields; support field selection or lightweight DTOs for lists.

---

## 7. Responsive Breakpoints (suggestion)

- **Mobile:** &lt; 768px (primary).
- **Tablet:** 768px–1024px.
- **Desktop:** &gt; 1024px.

---

## 8. Localization & Tailoring

- **Country/currency** is set at sign-up; billing and currency display should follow the school’s country.
- **Language:** Prepare for RTL if expanding to Arabic; keep strings in resource files for future i18n.
- **Dates/numbers:** Format according to locale (e.g. DD/MM/YYYY, appropriate decimal separators).

---

## 9. Summary Checklist for Implementation

- [ ] Mobile-first layout and touch-friendly targets (≥ 44px).
- [ ] Palette: Success Green, Deep Blue, White, neutrals; contrast checked.
- [ ] Large, tappable dashboard cards for “View Results”, “Message Teacher”, “Pay Fees”.
- [ ] Simple progress bars for academic performance.
- [ ] Low-bandwidth: small assets, lazy-loading, minimal initial JS.
- [ ] Billing and currency tailored to school’s country (from sign-up).

This document should be used as the single source of truth for RiseFlow’s UI/UX direction across web and (future) MAUI clients.
