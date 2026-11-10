# Skill: Next.js 16 & React 19 Engineering

## Overview
This skill provides conventions and patterns for building and scaling high-performance web applications using **Next.js 16 (App Router)** and **React 19** within the `web/` directory.

---

## Key Principles

1. **App Router File Conventions**:
   - `page.tsx`: Route entry point. Keep server-rendered where feasible.
   - `layout.tsx`: Persistent shell wrapping routes, handling metadata and root HTML structures.
   - `globals.css`: Tailwind v4 import and custom utility classes.
2. **React 19 & Client Boundaries**:
   - Mark files with `'use client'` only when utilizing React state (`useState`, `useEffect`), browser event listeners, or Framer Motion hooks.
   - Always avoid accessing browser globals (`window`, `navigator`, `localStorage`) directly during top-level evaluation to prevent hydration mismatches.
3. **Turbopack & Bundling**:
   - Ensure zero circular dependencies between components and type definitions.
   - Maintain fast compilation times by keeping dependencies modern and tree-shakeable.
