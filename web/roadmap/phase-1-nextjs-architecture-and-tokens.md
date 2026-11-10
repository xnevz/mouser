# Phase 1: Next.js 16 App Router & Design System Tokens

## 1. Objective
Scaffold the foundational Next.js web application utilizing official CLI tooling, configure Tailwind CSS v4 design tokens, and establish a modern dark-mode aesthetic with luminous linear gradients and glassmorphism.

---

## 2. Technical Stack
- **Framework**: Next.js 16.4+ (App Router)
- **Runtime**: React 19
- **CSS**: Tailwind CSS v4 (`@import "tailwindcss"`)
- **Typography & Font**: Geist Sans & Geist Mono via `next/font/google`
- **Iconography**: `lucide-react`

---

## 3. Implementation Steps

1. **CLI Scaffolding**:
   - Initialized via `npx create-next-app@latest web` with flags `--ts --tailwind --eslint --app --src-dir --import-alias "@/*" --disable-git`.
2. **Global Styling & Dark Palette**:
   - Defined base variables in `src/app/globals.css`:
     - Deep space slate background `#060911`.
     - Radial lighting effects (`bg-radial-gradient`).
     - 40px blueprint grid backdrop (`bg-grid-pattern`).
3. **Responsive Navigation**:
   - Developed `src/components/Navbar.tsx` featuring glassmorphism backdrop blur on scroll, version indicator badge, navigation links, and mobile drawer.
4. **Layout & SEO Metadata**:
   - Configured OpenGraph titles, descriptions, and keywords in `src/app/layout.tsx`.

---

## 4. Deliverables & Verification
- `web/package.json` with Next.js 16 & React 19 dependencies.
- `web/src/app/layout.tsx` with metadata tags and Geist fonts.
- `web/src/app/globals.css` with custom glow utilities.
- Clean build via `npm run build`.
