<!-- BEGIN:nextjs-agent-rules -->

## This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

# AGENTS.md — Mouser Web Engineering Agent Directives

Welcome, AI Engineer / Copilot Agent. This document outlines the engineering guidelines, design systems, architectural constraints, and operational patterns for the **Mouser Web Download Portal & Showcase** (`web/`).

---

## 1. Project Overview & Identity

- **Framework**: Next.js 16 (App Router) with React 19 and TypeScript.
- **Styling**: Tailwind CSS v4 with modern CSS variables and dark-mode default theme.
- **Motion & UI**: Framer Motion for high-fidelity micro-interactions and smooth hardware simulation canvases.
- **Icons**: `lucide-react`.
- **Purpose**: Serve as the high-converting, high-performance universal download portal for Mouser across macOS, Windows, Linux, Android, and iOS, demonstrating technical capabilities (latency, E2EE, 1,000Hz polling) with interactive visualizations.

---

## 2. Directory Structure & Conventions

```
web/
├── AGENTS.md                          # This agent guide
├── vercel.json                        # Vercel deployment and security header config
├── package.json                       # Dependencies and build scripts
├── src/
│   ├── app/                           # Next.js App Router (layout.tsx, page.tsx, globals.css)
│   ├── components/                    # Atomic, modular React components
│   │   ├── Navbar.tsx                 # Fixed glassmorphism navigation bar
│   │   ├── Hero.tsx                   # OS auto-detection hero section + quick terminal install
│   │   ├── InteractiveCanvasDemo.tsx  # Live multi-monitor KVM and clipboard roaming simulator
│   │   ├── DownloadSection.tsx        # Multi-OS tabs, architectures, checksum modal & CLI
│   │   ├── FeatureGrid.tsx            # Technical capability showcase cards
│   │   ├── ArchitecturePresentation.tsx # Deep-dive tabbed systems engineering blueprint
│   │   ├── ComparisonTable.tsx        # Feature comparison vs Barrier, Synergy, ShareMouse
│   │   └── Footer.tsx                 # Resources, downloads, legal and links
│   ├── data/
│   │   └── releases.ts                # Single source of truth for platform releases & checksums
│   └── types/
│       └── download.ts                # TypeScript interfaces for artifacts, architectures, and OSes
├── roadmap/                           # Phased engineering roadmap for the web portal
│   ├── README.md                      # Global master roadmap
│   ├── phase-1-nextjs-architecture-and-tokens.md
│   ├── phase-2-multi-platform-download-catalog.md
│   ├── phase-3-interactive-kvm-canvas-demo.md
│   └── phase-4-ci-cd-vercel-and-github-pages.md
└── skills/                            # Specialized domain knowledge skill guides
    ├── nextjs-engineering/SKILL.md
    ├── tailwind-motion-design/SKILL.md
    └── release-matrix-manager/SKILL.md
```

---

## 3. Engineering Rules & Best Practices

1. **Client vs Server Components**:
   - Keep page containers as Server Components where possible; use `'use client'` strictly on interactive components (`Hero`, `DownloadSection`, `InteractiveCanvasDemo`, `Navbar`).
2. **Release Data Centralization**:
   - Always update `src/data/releases.ts` when adding new releases, SHA-256 checksums, or platform targets. Never hardcode download URLs or checksums in presentation components.
3. **Accessibility & Responsive Design**:
   - Ensure all interactive buttons, tabs, and modals have descriptive `aria-label` attributes and keyboard focus styling.
   - Maintain seamless responsiveness from mobile (`375px`) to ultra-wide (`4K`).
4. **Performance & Zero Hydration Mismatches**:
   - Avoid executing client-only browser APIs (`window`, `navigator`) during SSR. Guard with `useEffect` or `typeof window !== 'undefined'`.
5. **Deployment Readiness**:
   - Verify that `npm run build` succeeds cleanly before pushing changes.

