# Phase 4: CI/CD, Deployment Configurations & GitHub Pages

## 1. Objective
Establish rock-solid hosting and deployment pipelines for Vercel and GitHub Pages, complete with security headers, build scripts, and automated GitHub Actions workflows.

---

## 2. Target Deployment Providers

### 2.1 Vercel Hosting
- File: `web/vercel.json`
- Capabilities:
  - Automatic framework detection (`nextjs`).
  - Pre-configured security headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `X-XSS-Protection: 1; mode=block`, `Referrer-Policy: strict-origin-when-cross-origin`.
  - Clean URLs and edge CDN distribution.

### 2.2 GitHub Pages / Static Export
- File: `.github/workflows/deploy-web.yml`
- Automated triggers:
  - Pushes to `main` modifying files under `web/**`.
  - Manual triggers via `workflow_dispatch`.
- Steps:
  - Node.js 20 setup with npm dependency cache.
  - `npm ci` and production compilation via `npm run build`.
  - Artifact deployment to GitHub Pages.

---

## 3. Verification & Local Testing
- Execute `npm run build` in `web/` to confirm zero static rendering errors or broken links.
- Confirm all static assets in `public/` are accessible.
