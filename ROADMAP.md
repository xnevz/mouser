# Mouser: global status board

**One page that says what works and what is left.** Update it after every task (see
[roadmap/AGENT_GUIDE.md](roadmap/AGENT_GUIDE.md)). The long explanations live in [roadmap/](roadmap/README.md); the step-by-step
instructions for each task live in [roadmap/tasks/](roadmap/tasks/).

Legend: ✅ done and verified · 🟡 partly done · ⬜ not started · ⛔ blocked · 🅿️ parked (macOS backlog)

**Last verified:** 2026-11-13, commit `29a3cf1`, Windows 11, `dotnet test tests/Mouser.Tests` →
**Passed 130, Skipped 3, Failed 0** (the 3 skipped need the native macOS library). Android unit tests and the web build
run in CI (`.github/workflows/ci.yml`, `deploy-web.yml`); they were not re-run for this update.

---

## 1. What works today (verified)

### Desktop app (Windows)
| ✅ | Feature | Evidence |
| :-: | :--- | :--- |
| ✅ | Phone remote receiver: 14-byte UDP protocol, per-sender ordering, PIN pairing with lockout (5 tries / 60 s) | `PairingAndSequenceTests`, `UdpNetworkingTests`, `InputInjectionServiceTests` |
| ✅ | Mouse/keyboard injection on Windows (move, absolute move, buttons, wheel, keys) | `Win32InputSinkTests` (real cursor) |
| ✅ | LAN discovery by UDP broadcast on every adapter (not mDNS) | `PeerDiscoveryTests` |
| ✅ | Desktop-to-desktop KVM: edge crossing, return edge, absolute positioning, clamping, watchdog (2 s), stuck key/button release, return hotkeys (Ctrl+Alt+Esc / Scroll Lock) | `KvmControllerTests`, `VirtualKvmTests`, `TwoComputerTests`, `Win32InputCapture` |
| ✅ | Input hooks run only while a paired computer is placed | `UpdateKvmRunning` in `MouserHost` |
| ✅ | Pairing between computers by the other computer's PIN, saved across restarts | `TwoComputerTests` |
| ✅ | Clipboard history (text): SQLite + FTS5 search, stars, retention days, classification (text/link/code) | `ClipboardTests`, `ViewModelTests` |
| ✅ | Secret filter (GitHub, AWS, OpenAI, Stripe, private keys, Luhn credit cards) | `ClipboardTests` |
| ✅ | Quick-paste overlay (Win+Alt+V), plain text | `QuickPasteTests` |
| ✅ | Clipboard text sync between paired computers, AES-256-GCM (key from PIN) | `PeerTransferTests`, `TwoComputerTests` |
| ✅ | File send to a paired computer (SHA-256 verified, ≤ 4 GB) saved to `Downloads\Mouser` | `FileStreamingTests`, `PeerTransferTests` |
| ✅ | Settings page: input port, history on/off, skip secrets, sync on/off, retention days, start at login (Windows registry) | `ViewModelTests` |
| ✅ | Four pages: Phone Remote, Clipboard History, Displays & Network, Settings, on real data | `ViewModelTests` |
| ✅ | `--data-dir` runs a second independent instance; `scripts/test-kvm-single-pc.ps1` | script + `TwoComputerTests` |
| ✅ | Portable Windows build script `scripts/package-windows.sh` | script exists (not re-run) |

### Android app
| ✅ | Feature | Evidence |
| :-: | :--- | :--- |
| ✅ | Trackpad (tap, 2-finger tap/scroll, hold-to-drag), gyro air mouse, media keys, number keys | `android/app/src/test` (`InputMotionPacketTest`, `ProtocolTest`) |
| ✅ | Auto-discovery of desktops, saved PIN, manual address | `HostDiscoveryClient.kt`, `ProtocolTest` |

### Website and CI
| ✅ | Feature |
| :-: | :--- |
| ✅ | Next.js site with dark/light toggle that follows the system on first visit (`web/src/context/ThemeContext.tsx`) |
| ✅ | CI: desktop tests on Windows, Android unit tests + debug build on Ubuntu, website deploy to GitHub Pages |

## 2. Partly done

| 🟡 | Item | What exists | What is missing |
| :-: | :--- | :--- | :--- |
| 🟡 | Theme | Desktop is forced **Dark** with ~100 raw hex colours; Android only has a dark scheme; website has both | Tracks A1–A9, A13, A14 |
| 🟡 | Edge resistance | `VirtualKvmManager.EdgeResistanceMs = 80` and push threshold exist | No setting reaches them (E1) |
| 🟡 | Clipboard payload types | `ClipboardContentType` has Html/Rtf/Image/FileUri; `HtmlContent` column exists | Nothing captures or shows them (D4–D8) |
| 🟡 | Encryption of clipboard traffic | AES-GCM frames exist | Key comes from a 6-digit PIN (F-track replaces) |
| 🟡 | macOS | Code written (`MacInputSink`, `MacInputCapture`, `platform_mac.mm`) | Never run on a Mac: 🅿️ [roadmap/backlog-macos.md](roadmap/backlog-macos.md) |
| 🟡 | Packaging | Scripts for Windows (portable exe), macOS `.app`, Linux | No installer, signing, notarisation, update (H-track) |
| 🟡 | Old phase docs | Phases 0–6 in `roadmap/` | Several claim unbuilt work as done (Q7 fixes) |

## 3. Known problems found while analysing (not yet tasks of their own)

1. Closing the window ends the app, and minimizing leaves a taskbar button instead of going to the tray (fixed by A10).
1a. **Input does nothing over administrator windows** (Task Manager, elevated terminals, installers): Mouser runs un-elevated and Windows blocks it (UIPI). Fixed by E16 (warn), E18 (run as administrator) and H8 (`uiAccess`); the lock screen/UAC prompt need H5.
2. `FindAdjacentScreen` can let the cursor cross where the other computer has no screen (fixed by E9c).
3. Discovery cannot cross subnets/VLANs/VPN (fixed by B10).
4. The 1-second UI timer also runs peer syncing and KVM start/stop; anything that pauses it breaks sharing (documented in the guide).
5. Anyone who can spoof a trusted IP can send input (fixed by F4/F8).
6. `clipboard.db` is plain SQLite (fixed by C5).

---

## 4. Not done: every task (all ⬜)

**How to read:** *Needs* are the tasks that must be ✅ first. *Size* XS < S < M < L < XL. Click the ID's file for the full steps.
Tick a row by changing ⬜ to ✅ (and add a short note in section 6 if something surprised you).

### Track A: Foundation: theme, tray, background ([A-foundation.md](roadmap/tasks/A-foundation.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ⬜ | A1 | Colour token file (`Styles/Tokens.axaml`, Dark + Light) | – | S |
| ⬜ | A2 | Convert `App.axaml` styles to tokens | A1 | M |
| ⬜ | A3 | Migrate `MainWindow.axaml` | A2 | S |
| ⬜ | A4 | Migrate `AndroidRemoteView.axaml` | A2 | S |
| ⬜ | A5 | Migrate `ClipboardHistoryView.axaml` | A2 | S |
| ⬜ | A6 | Migrate `MonitorLayoutView.axaml` | A2 | M |
| ⬜ | A7 | Migrate `SettingsView.axaml` | A2 | XS |
| ⬜ | A8 | Migrate `QuickPasteWindow.axaml` | A2 | XS |
| ⬜ | A9 | Theme setting System/Light/Dark, default System | A3–A8 | M |
| ⬜ | A10 | Tray icon, **close-to-tray and minimize-to-tray** (two settings), pause items | A9 | M |
| ⬜ | A11 | `--minimized`, start at login minimized, single instance | A10 | S |
| ⬜ | A12 | In-app toasts | A9 | M |
| ⬜ | A13 | Android light + dark scheme and chooser | – | M |
| ⬜ | A14 | Website System/Light/Dark and shared token names | – | S |

### Track B: New shell ([B-shell.md](roadmap/tasks/B-shell.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ⬜ | B1 | Sidebar device list (view model) | A9 | S |
| ⬜ | B2 | New sidebar (All items, Pinned, Devices, Settings, Ctrl+,) | B1 | M |
| ⬜ | B3 | Clipboard filters: device and time | A9 | M |
| ⬜ | B4 | Day headers in the list | B3 | S |
| ⬜ | B5 | Clipboard page layout (search, chips, grouped list) | B3, B4 | L |
| ⬜ | B6 | Detail pane (actions, metadata cards) | B5 | M |
| ⬜ | B7 | Editable device name | A9 | S |
| ⬜ | B8 | Devices view model | B1, B7 | M |
| ⬜ | B9 | Devices page + navigation switch (clipboard becomes the home page) | B2, B8 | L |
| ⬜ | B10 | Add computer by IP/name (unicast beacons) | B9 | L |
| ⬜ | B11 | Simple Settings (5 cards, Advanced collapsed) | A10, A12, B7 | M |
| ⬜ | B12 | Activity page, log, "Copy diagnostics" | B9 | M |
| ⬜ | B13 | First-run wizard | B9, B11 | M |

### Track C: App lock ([C-lock.md](roadmap/tasks/C-lock.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ⬜ | C1 | `AppLockService` (passphrase, recovery key, backoff) + tests | – | M |
| ⬜ | C2 | Host stops services while locked | C1 | M |
| ⬜ | C3 | Lock screen and setup UI | C2, B11 | L |
| ⬜ | C4 | Ctrl+L, tray Lock, idle lock, Windows-lock, quit blocked | C3 | M |
| ⬜ | C5 | Encrypt `clipboard.db` (SQLCipher spike first) | C3 | L |
| ⬜ | C6 | Android app lock (biometric) | A13 | M |
| ⬜ | C7 | Lock all paired devices | C4 | M |
| ⬜ | C8 | Windows Hello (spike) | C4 | M |

### Track D: Clipboard parity ([D-clipboard.md](roadmap/tasks/D-clipboard.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ⬜ | D1 | Event-driven capture (clipboard sequence number) | A9 | S |
| ⬜ | D2 | Source app + window title | D1 | M |
| ⬜ | D3 | Password-manager exclusion + ignore list | D2 | M |
| ⬜ | D4 | Spike: read/write images, HTML, files | D1 | S |
| ⬜ | D5 | Image clips: storage | D4 | L |
| ⬜ | D6 | Image clips: list, preview, actions | D5, B6 | M |
| ⬜ | D7 | HTML/RTF capture, paste as plain text | D4 | M |
| ⬜ | D8 | File clips (Explorer copy) | D4, D5 | M |
| ⬜ | D9 | Tags (system + custom) and sidebar Tags | B2, D5 | L |
| ⬜ | D10 | Search operators (`type: app: @device #tag before:`) | D9 | M |
| ⬜ | D11 | Delivery status per device | B9, D2 | L |
| ⬜ | D12 | Per-device sync rules, "Send to device…" | D11, B9 | M |
| ⬜ | D13 | Snippets with variables | B2 | L |
| ⬜ | D14 | Text transforms | B6 | S |
| ⬜ | D15 | Quick paste v2 (previews, 1–9, tabs, configurable hotkey) | D6, D9, D14 | M |
| ⬜ | D16 | Paging, big clips, retention presets, maintenance, panic wipe | D10 | M |
| ⬜ | D17 | More secret detectors | – | S |

### Track E: Mouse and keyboard sharing ([E-kvm.md](roadmap/tasks/E-kvm.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ⬜ | E1 | Switching feel (Relaxed/Normal/Careful) + `KvmOptions` | B11 | M |
| ⬜ | E2 | Prevent switching (full-screen, maximized, corners, modifier, double-tap) | E1 | L |
| ⬜ | E3 | Hotkeys page, configurable hotkeys, panic double-press | D15 parser | L |
| ⬜ | E4 | Jump hotkeys and "next computer" | E3 | M |
| ⬜ | E5 | Mouse speed, scroll speed, reverse scroll per device | B9 | M |
| ⬜ | E6 | Coalesce moves for 1000 Hz mice | – | M |
| ⬜ | E7 | Dim inactive displays (control indicator) | A12 | M |
| ⬜ | E8 | Wrap-around and pixel-aligned entry | E1 | S |
| ⬜ | E9a | Pure `EdgeMap` (per-monitor crossing) | E1 | M |
| ⬜ | E9b | Send all monitors in the beacon | E9a | M |
| ⬜ | E9c | Use per-monitor crossing in the KVM | E9b | L |
| ⬜ | E9d | Layout view draws every monitor | E9c | S |
| ⬜ | E10 | Draggable layout with offsets | E9d | L |
| ⬜ | E11 | Several computers per side and chains (design review first) | E10 | XL |
| ⬜ | E12 | Layout-aware typing (Unicode path) | – | L |
| ⬜ | E13 | Lock/sleep/display-off sync | C4, B9 | M |
| ⬜ | E14 | Wake-on-LAN | B9 | M |
| ⬜ | E15 | Paste from the other computer hotkey | D15, E3 | S |
| ⬜ | E16 | Detect and warn about administrator (elevated) windows + setting | A12 | S |
| ⬜ | E18 | **Run Mouser as administrator** (setting, restart button, elevated start at login) | E16, A11 | L |
| ⬜ | E17 | Cross-screen drag and drop (design spike, ask first) | E9c, D11 | XL |

### Track F: Security rewrite ([F-security-and-phone.md](roadmap/tasks/F-security-and-phone.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ⬜ | F1 | Protocol v2 design note (needs user approval) | – | S |
| ⬜ | F2 | Pairing + session crypto in Core | F1 | M |
| ⬜ | F3 | Pairing over TCP + "Allow this device?" | F2, B9 | L |
| ⬜ | F4 | Encrypted, authenticated input channel | F3 | XL |
| ⬜ | F5 | Android crypto and pairing | F2, F3 | XL |
| ⬜ | F6 | Keys at rest (DPAPI); stop storing PINs | F3 | M |
| ⬜ | F7 | Incoming-file safety (prompt, limits, sanitising) | B9 | M |
| ⬜ | F8 | Remove v1 PIN trust | F4, F5, F6 | M |
| ⬜ | F9 | Fuzz tests, SECURITY.md, threat model | F4 | M |
| ⬜ | F10 | Local-network-only, adapter picker, separate ports | B11 | S |

### Track G: Phone companion v2 ([F-security-and-phone.md](roadmap/tasks/F-security-and-phone.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ⬜ | G1 | Typing and dictation | E12 | M |
| ⬜ | G2 | Clipboard bridge (foreground) | F5, D4 | L |
| ⬜ | G3 | Share-sheet target | G2 | M |
| ⬜ | G4 | Macro pad with custom buttons | – | M |
| ⬜ | G5 | Gestures and phone settings | – | M |
| ⬜ | G6 | Laser pointer and slide remote | E7 | L |
| ⬜ | G7 | Ping/battery bar, foreground service, auto-reconnect | – | M |
| ⬜ | G8 | Landscape/tablet, accessibility, localisation, release signing | A13 | M |

### Track H: Platform and release ([H-platform-and-quality.md](roadmap/tasks/H-platform-and-quality.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ⬜ | H1 | Windows installer (Inno Setup) with firewall rule | A10, A11 | M |
| ⬜ | H2 | Code signing (needs the user's certificate) | H1 | S |
| ⬜ | H3 | Update check (then optional auto-update) | H1 | L |
| ⬜ | H4 | Local log files and crash handling | B12 | S |
| ⬜ | H5 | Windows service for UAC/lock screen (design note first) | H1, H2 | XL |
| ⬜ | H6 | Linux X11 input injection | – | L |
| ⬜ | H7 | Linux capture, tray, AppImage/Flatpak | H6 | XL |
| ⬜ | H8 | `uiAccess` signed build: types into admin windows without elevation (needs certificate) | H1, H2 | M |

### Track Q: Quality, docs, website ([H-platform-and-quality.md](roadmap/tasks/H-platform-and-quality.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ⬜ | Q1 | Headless Avalonia UI tests (both themes) | B9 | M |
| ⬜ | Q2 | Extend CI (web job, release build, UI snapshots) | Q1 | S |
| ⬜ | Q3 | Two-instance integration scenarios | B10 | M |
| ⬜ | Q4 | Performance budget tests | – | M |
| ⬜ | Q5 | Soak and chaos tests | – | M |
| ⬜ | Q6 | User guide, FAQ, architecture, contributing, changelog | – | M |
| ⬜ | Q7 | Website text and old phase docs match reality | – | M |

### 🅿️ macOS backlog
All macOS work (first run on a Mac VM, menu bar, `NSPasteboard`, signing/notarisation, Sparkle, Homebrew) is in
[roadmap/backlog-macos.md](roadmap/backlog-macos.md) (44 checkboxes). Not scheduled.

---

## 5. Recommended order (waves)

1. **Wave 1: look and feel foundation:** A1 → A2 → A3…A8 → A9. In parallel (independent): C1, D17, E6, A13, A14.
2. **Wave 2: background app:** A10 → A11, A12. Then B1 → B2 → B3 → B4 → B5 → B6.
3. **Wave 3: Devices:** B7 → B8 → B9 → B10 → B11 → B12 → B13.
4. **Wave 4: clipboard core:** D1 → D2 → D3 → D4 → D5 → D6 → D7 → D14.
5. **Wave 5: KVM options:** E1 → E2 → E3 → E4, E5, E8, E7. **Do E16 → E18 right after A11/A12 (wave 2): they fix a reported bug.**
6. **Wave 6: lock:** C2 → C3 → C4 (C5 after D5/D16 so only one database migration happens).
7. **Wave 7: security rewrite:** F1 (get approval) → F2 → F3 → F4 → F6 → F7 → F5 (phone) → F8 → F9 → F10. Move this wave **earlier** if the app will run on networks you do not control.
8. **Wave 8:** the rest of D, E9–E17, G, H, Q.

## 6. Decisions and permissions needed from the user

| Needed by | Question |
| :-: | :--- |
| C1 | Is "lock" (passphrase hides and stops everything, no backdoor) what you meant? |
| C5 | OK to replace `Microsoft.Data.Sqlite` with `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlcipher`? |
| C6 | OK to add `androidx.biometric:biometric:1.1.0`? |
| C8 | Accept changing the target framework to `net8.0-windows10.0.19041.0` for Windows Hello, or skip it? |
| D4 | If PNG encoding is impossible without a package: OK to add `SixLabors.ImageSharp` (check its licence terms for your use)? |
| E11, E17, H5 | Approve each design note before any code. |
| F1 | Approve the protocol v2 design before F2. |
| H2, H8 | Provide a code-signing certificate (or choose to skip; H8 cannot work without one). |
| E18 | OK to create a scheduled task named "Mouser" (runs at logon with highest privileges) when you turn on *Run as administrator*? |
| H3 | Silent auto-update (adds `Velopack`) or just a "new version" banner? |
| F9 | Contact address for `SECURITY.md`. |
| Q1 | OK to add `Avalonia.Headless.XUnit` 12.1.6? |

## 7. Blocked

*(nothing yet. Executors: write the task ID, the exact error and what you tried here.)*

## 8. Notes from executors

*(one line per finished task if something was surprising)*
