# Phase 2: Multi-Platform Download Matrix & Release Catalog

## 1. Objective
Build an intuitive, comprehensive download portal supporting all target platforms (macOS, Windows, Linux, Android, iOS), client-side OS detection, architecture selectors (ARM64 vs x64), SHA-256 cryptographic checksum verification modals, and package manager CLI commands.

---

## 2. Supported Platforms & Binaries

| Platform | Primary Binary | Architecture | Package Managers |
| :--- | :--- | :--- | :--- |
| **macOS** | `.dmg` | Apple Silicon (M-series) & Intel x86_64 | `brew install --cask mouser` |
| **Windows** | `.msi` | 64-bit x64 & ARM64 (Surface / Copilot+) | `winget install Mouser.Mouser`, Scoop |
| **Linux** | `.AppImage` | x86_64 & ARM64 (.deb, .rpm) | `flatpak install flathub org.mouser.Mouser`, AUR |
| **Android** | `.apk` | Universal APK (ARMv8/v7/x86) | F-Droid repo, Direct APK |
| **iOS / iPadOS** | `.app` | Universal iOS / iPadOS | Apple TestFlight Public Beta |

---

## 3. Implementation Steps

1. **Type Definitions**:
   - Created `src/types/download.ts` specifying `DownloadArtifact`, `PlatformRelease`, and `PackageManagerOption`.
2. **Catalog Data Store**:
   - Populated `src/data/releases.ts` with release artifacts, download links, file sizes, and cryptographic SHA-256 hashes.
3. **Hero Section Auto-Detection**:
   - Created `src/components/Hero.tsx` analyzing `navigator.userAgent` to present the visitor with an immediate one-click download customized for their specific operating system.
4. **Download Grid Component**:
   - Implemented `src/components/DownloadSection.tsx` with platform switcher tabs, architecture cards, SHA-256 popup modal with terminal verification snippet, and copy-to-clipboard package manager commands.

---

## 4. Deliverables & Verification
- `web/src/types/download.ts`
- `web/src/data/releases.ts`
- `web/src/components/Hero.tsx`
- `web/src/components/DownloadSection.tsx`
