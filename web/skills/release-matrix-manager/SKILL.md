# Skill: Multi-Platform Release Matrix & Checksum Management

## Overview
Guidelines for managing downloadable binaries, architectures, checksum hashes, and package manager commands across macOS, Windows, Linux, Android, and iOS.

---

## Operating Procedures

1. **Adding a New Release**:
   - Update `CURRENT_RELEASE_VERSION` and `RELEASE_DATE` in `src/data/releases.ts`.
   - Ensure every artifact contains:
     - Exact `filename` and file `extension` (`.dmg`, `.msi`, `.AppImage`, `.apk`).
     - Accurate `size` string (e.g., `24.8 MB`).
     - Cryptographic `sha256` hash (64 hex characters).
     - Working GitHub Releases `downloadUrl`.
2. **Platform Specifics**:
   - **macOS**: Provide both Apple Silicon (`arm64`) and Intel (`x64`) DMG packages, plus Homebrew Cask command.
   - **Windows**: Signed MSI installers for x64 and ARM64, plus Winget and Scoop commands.
   - **Linux**: Universal AppImage, Debian `.deb`, Fedora `.rpm`, plus Flatpak Flathub command.
   - **Android**: Direct APK download with QR-code pairing instructions.
   - **iOS / iPadOS**: Apple TestFlight public beta link.
