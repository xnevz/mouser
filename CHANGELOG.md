# Changelog

All notable changes to Mouser will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Performance & Benchmark Suite (Task Q4):** Added `tests/Mouser.Tests/PerfTests.cs` validating sub-millisecond input handling, fast FTS search, and bounded memory growth under `Category=Perf`.
- **Soak & Chaos Testing (Task Q5):** Added `scripts/soak-two-instances.ps1` for multi-hour stability validation, and `ChaosTests.cs` verifying network drop/duplicate/reorder resilience and partial file cleanup.
- **Two-Computer Integration Scenarios (Task Q3):** Added multi-computer scenarios for add-by-IP, KVM edge crossing, clipboard image sync, and lock-all.
- **Headless UI Testing & CI Automation (Tasks Q1, Q2):** Added Avalonia Headless UI test harness and GitHub Actions workflows for web, release desktop builds, and snapshot artifacts.
- **Encrypted Storage with SQLCipher (Task C5):** Switched clipboard database persistence from plain SQLite to SQLCipher with automated plaintext migration.
- **Diagnostics & Crash Handling (Task H4):** Added `FileLogger` with 2 MB rollover, 7-day retention, and `CrashReporter` with secret sanitization.
- **Protocol v2 Cryptography (Tasks F1–F10):** Implemented P-256 ECDH pairing with Short Authentication String (SAS), 51-byte AES-256-GCM authenticated UDP input packets, DPAPI secrets storage, and fuzz testing.
- **Rich Clipboard Pipeline (Tasks D1–D14):** Added clipboard polling watcher, image thumbnailing, zoom preview, rich HTML sync, code snippets manager, and tag filtering.
- **App Lock & Security (Tasks C1–C4):** Added master passphrase protection with PBKDF2/AES-GCM encryption, Base32 recovery keys, brute-force backoff, and workstation auto-lock.
- **Unified Navigation & Modern Shell (Tasks B1–B13):** Created new sidebar, unified Devices page, multi-adapter network configuration, first-run onboarding wizard, and activity event log.
- **Theming & System Tray (Tasks A1–A12, A14):** Created complete dynamic token-based design system supporting Light, Dark, and System modes, in-app toast notifications, and close/minimize-to-tray.

---

## [0.1.0] - 2026-11-13

### Added
- Initial release with desktop-to-desktop KVM sharing, 14-byte UDP motion protocol, PIN-based pairing, plain SQLite clipboard history, and Android companion receiver.
