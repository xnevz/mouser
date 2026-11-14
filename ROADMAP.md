# Mouser: global status board

**One page that says what works and what is left.** Update it after every task (see
[roadmap/AGENT_GUIDE.md](roadmap/AGENT_GUIDE.md)). The long explanations live in [roadmap/](roadmap/README.md); the step-by-step
instructions for each task live in [roadmap/tasks/](roadmap/tasks/).

Legend: ✅ done and verified · 🟡 partly done · ⬜ not started · ⛔ blocked · 🅿️ parked (macOS backlog)

**Last verified:** 2026-11-14, commit `29a3cf1`, Windows 11, `dotnet test tests/Mouser.Tests` →
**Passed 173, Skipped 3, Failed 0** (the 3 skipped need the native macOS library). Android unit tests and the web build
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
| ✅ | A1 | Colour token file (`Styles/Tokens.axaml`, Dark + Light) | – | S |
| ✅ | A2 | Convert `App.axaml` styles to tokens | A1 | M |
| ✅ | A3 | Migrate `MainWindow.axaml` | A2 | S |
| ✅ | A4 | Migrate `AndroidRemoteView.axaml` | A2 | S |
| ✅ | A5 | Migrate `ClipboardHistoryView.axaml` | A2 | S |
| ✅ | A6 | Migrate `MonitorLayoutView.axaml` | A2 | M |
| ✅ | A7 | Migrate `SettingsView.axaml` | A2 | XS |
| ✅ | A8 | Migrate `QuickPasteWindow.axaml` | A2 | XS |
| ✅ | A9 | Theme setting System/Light/Dark, default System | A3–A8 | M |
| ✅ | A10 | Tray icon, **close-to-tray and minimize-to-tray** (two settings), pause items | A9 | M |
| ✅ | A11 | `--minimized`, start at login minimized, single instance | A10 | S |
| ✅ | A12 | In-app toasts | A9 | M |
| ⬜ | A13 | Android light + dark scheme and chooser | – | M |
| ⬜ | A14 | Website System/Light/Dark and shared token names | – | S |

### Track B: New shell ([B-shell.md](roadmap/tasks/B-shell.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ✅ | B1 | Sidebar device list (view model) | A9 | S |
| ✅ | B2 | New sidebar (All items, Pinned, Devices, Settings, Ctrl+,) | B1 | M |
| ✅ | B3 | Clipboard filters: device and time | A9 | M |
| ✅ | B4 | Day headers in the list | B3 | S |
| ✅ | B5 | Clipboard page layout (search, chips, grouped list) | B3, B4 | L |
| ✅ | B6 | Detail pane (actions, metadata cards) | B5 | M |
| ✅ | B7 | Editable device name | A9 | S |
| ✅ | B8 | Devices view model | B1, B7 | M |
| ✅ | B9 | Devices page + navigation switch (clipboard becomes the home page) | B2, B8 | L |
| ✅ | B10 | Add computer by IP/name (unicast beacons) | B9 | L |
| ✅ | B11 | Simple Settings (5 cards, Advanced collapsed) | A10, A12, B7 | M |
| ✅ | B12 | Activity page, log, "Copy diagnostics" | B9 | M |
| ✅ | B13 | First-run wizard | B9, B11 | M |

### Track C: App lock ([C-lock.md](roadmap/tasks/C-lock.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ✅ | C1 | `AppLockService` (passphrase, recovery key, backoff) + tests | – | M |
| ✅ | C2 | Host stops services while locked | C1 | M |
| ✅ | C3 | Lock screen and setup UI | C2, B11 | L |
| ✅ | C4 | Ctrl+L, tray Lock, idle lock, Windows-lock, quit blocked | C3 | M |
| ⬜ | C5 | Encrypt `clipboard.db` (SQLCipher spike first) | C3 | L |
| ⬜ | C6 | Android app lock (biometric) | A13 | M |
| ⬜ | C7 | Lock all paired devices | C4 | M |
| ⬜ | C8 | Windows Hello (spike) | C4 | M |

### Track D: Clipboard parity ([D-clipboard.md](roadmap/tasks/D-clipboard.md))
| Status | ID | Task | Needs | Size |
| :-: | :-: | :--- | :--- | :-: |
| ✅ | D1 | Event-driven capture (clipboard sequence number) | A9 | S |
| ✅ | D2 | Source app + window title | D1 | M |
| ✅ | D3 | Password-manager exclusion + ignore list | D2 | M |
| ✅ | D4 | Spike: read/write images, HTML, files | D1 | S |
| ✅ | D5 | Image clips: storage | D4 | L |
| ✅ | D6 | Image clips: list, preview, actions | D5, B6 | M |
| ✅ | D7 | HTML/RTF capture, paste as plain text | D4 | M |
| ✅ | D8 | File clips (Explorer copy) | D4, D5 | M |
| ✅ | D9 | Tags (system + custom) and sidebar Tags | B2, D5 | L |
| ✅ | D10 | Search operators (`type: app: @device #tag before:`) | D9 | M |
| ✅ | D11 | Delivery status per device | B9, D2 | L |
| ✅ | D12 | Per-device sync rules, "Send to device…" | D11, B9 | M |
| ✅ | D13 | Snippets with variables | B2 | L |
| ✅ | D14 | Text transforms | B6 | S |
| ✅ | D15 | Quick paste v2 (previews, 1–9, tabs, configurable hotkey) | D6, D9, D14 | M |
| ✅ | D16 | Paging, big clips, retention presets, maintenance, panic wipe | D10 | M |
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

- A1: Created Tokens.axaml with Dark and Light palettes and merged into App.axaml; updated Win32InputSinkTests to handle non-interactive sessions; all 130 tests green.
- A2: Converted all raw hex color setters in App.axaml styles to dynamic theme tokens; verified 0 remaining hex colors outside SystemAccentColor* lines; all 130 tests pass.
- A3: Migrated MainWindow.axaml to dynamic theme tokens; verified 0 remaining hex colors; 130 tests pass.
- A4: Migrated AndroidRemoteView.axaml to dynamic theme tokens; verified 0 remaining hex colors; 130 tests pass.
- A5: Migrated ClipboardHistoryView.axaml to dynamic theme tokens; verified 0 remaining hex colors; 130 tests pass.
- A6: Migrated MonitorLayoutView.axaml to dynamic theme tokens; verified 0 remaining hex colors; 130 tests pass.
- A7: Migrated SettingsView.axaml to dynamic theme tokens; verified 0 remaining hex colors; 130 tests pass.
- A8: Migrated QuickPasteWindow.axaml to dynamic theme tokens; verified 0 remaining hex colors; 130 tests pass.
- A9: Implemented ThemeService with System/Light/Dark options; wired theme setting in SettingsStore, SettingsViewModel, SettingsView, and App startup; switched default to System; added 3 unit tests (133 passing).
- A10: Added tray icon with menu (Open Mouser, Pause sharing, Pause clipboard, Quit), close-to-tray and minimize-to-tray settings (default true), pause handlers for sharing and clipboard; added 2 unit tests (135 passing).
- A11: Added SingleInstance mutex and show event, wired single-instance check/ping in App.axaml.cs (skipped when --data-dir is passed), updated Windows startup registry entry to append --minimized, implemented --minimized startup hide to tray, added unit test (136 passing).
- A12: Created ToastWindow and ToastService with stacking and 4-second dismiss; wired notification setting in SettingsStore, SettingsViewModel, and SettingsView; connected clip, file, KVM state, and first hide-to-tray toasts in App.axaml.cs; added 2 unit tests (138 passing).
- B1: Added SidebarDeviceViewModel and Devices collection/OnlineDeviceCount to MainViewModel with RefreshDevices in-place updates; added unit test Sidebar_ListsPairedPhoneAsOnline (139 passing).
- B2: Updated sidebar with All items, Pinned, Devices list, Add a phone, Manage devices, Settings (with Ctrl+, shortcut); added ShowLibrary command in MainViewModel; added unit test ShowLibrary_NavigatesToClipboardAndSetsLibrary (140 passing).
- B3: Added CreatedTime property to ClipboardPayload, device and time filter options to ClipboardHistoryViewModel with PopulateDeviceOptions and ReloadAsync filtering; added unit tests ClipboardFilter_ByDevice and ClipboardFilter_ByTime (142 passing).
- B4: Added TimestampLocal, DayLabel, ShowDayHeader, and DayCount to ClipboardItemViewModel; computed day headers and item counts per section in ClipboardHistoryViewModel.ReloadAsync(); added unit test ClipboardHistory_ComputesDayHeadersAndCounts (143 passing).
- B5: Updated ClipboardHistoryView with search row (including Clear history button), filter dropdown chips (Type, Device, Time), and day headers in list cards; wired Ctrl+F hotkey to focus search via MainViewModel and MainWindow; added unit test FocusSearch_NavigatesAndFiresEvent (144 passing).
- B6: Updated ClipboardHistoryView detail pane with top action buttons (Copy, Star, Delete), monospace text preview, metadata card, and empty state; added unit test ClipboardHistory_DetailPaneSelection (145 passing).
- B7: Added editable DeviceName setting defaulting to Environment.MachineName, broadcast hostname update via PeerDiscoveryService and MouserHost, wired DeviceName setting row in SettingsView and SettingsViewModel; added unit test DeviceName_SettingPersistsAndFallbackToMachineName (146 passing).
- B8: Implemented DeviceDetailViewModel and DevicesViewModel unifying online/offline computers and phones, with pairing, unpairing, and placement management; added 3 unit tests (149 passing).
- B9: Created DevicesView unified page with left list (this computer, computers, phones, screen layout expander) and right detail sheet (DataTemplates for this device, peer computer, and phone client); updated DevicesViewModel and MainViewModel to switch home page to SmartClipboard, route DisplayMatrix and AndroidRemote to DevicesPage; updated Navigation_SwitchesPagesAndActiveTab test (149 passing).
- B10: Added manual peer target unicast discovery across subnets/VPNs with automatic sender learning, persisted ManualPeers in SettingsStore, added AddManualPeerAsync in MouserHost, added manual entry UI in DevicesView and DevicesViewModel, added unit test TwoHosts_WithDifferentDiscoveryPorts_DiscoverEachOtherViaAddTarget (150 passing).
- B11: Reorganized SettingsView into 5 clean cards (Appearance, General, Clipboard, Sharing, This device) with collapsible Advanced expanders for port and retention; added AcceptRemoteInput observable property and change handler to SettingsViewModel; added unit test Settings_AcceptRemoteInput_TogglesAndPersists (151 passing).
- B12: Implemented ActivityLog service (200-event capacity, categorised events) and ActivityViewModel with CopyDiagnosticsCommand; wired key discovery, pairing, clip, file, and KVM events in MouserHost; created ActivityView with theme tokens; added Activity navigation tab and sidebar button in MainWindow; added unit tests ActivityLog_CapsAt200 and CopyDiagnostics_ContainsVersionAndRecentEvents (153 passing).
- B13: Implemented 3-step first-run onboarding wizard (computer name, feature toggles, device pairing with large PIN and live peer count) overlaid on MainWindow; added FirstRunDone setting in SettingsStore with auto-migration for existing installs; added 3 unit tests (156 passing).
- C1: Implemented AppLockService in Core with PBKDF2/AES-GCM encryption, Base32 recovery key, progressive backoff (0s, 5s, 15s, 60s, 300s), corrupt file detection, and atomic persistence; added 8 unit tests in AppLockTests (164 passing).
- C2: Added IsLocked state and SetLocked to MouserHost stopping Kvm and services, guarded Restart and SyncPeers while locked, added IsLocked guard to ClipboardHistoryViewModel Poll/ApplyRemoteClip; 166 tests pass.
- C3: Implemented LockViewModel and LockView with password box, backoff alert, and recovery key unlock; added full-window lock overlay and background interaction disable in MainWindow; integrated AppLockService into MainViewModel, SettingsViewModel, and App startup (stopping sockets when starting locked); added Security settings card with setup wizard, recovery key prompt, lock/change/remove commands, and Windows/idle lock options; added 7 unit tests in ViewModelTests (173 passing).
- C4: Implemented WindowsSession (IsWorkstationLocked, IdleTime via user32.dll); added Ctrl+L shortcut and LockNowCommand on MainViewModel; added Lock item to tray menu (visible when configured); added auto-lock check on 1s refresh timer for Windows lock and idle timeout; blocked Quit and close button while locked until passphrase entered; blocked QuickPaste while locked; added 5 unit tests in ViewModelTests (178 passing).
- D1: Implemented ClipboardWatcher using GetClipboardSequenceNumber background poller (150ms interval); wired into App.axaml.cs with UIThread dispatch, AppLock check, and 1s timer fallback; added 4 unit tests in ClipboardWatcherTests (182 passing).
- D2: Added ForegroundApp service probing GetClipboardOwner/GetForegroundWindow/GetWindowText; extended ClipboardPayload with SourceApp wire-compatibility; migrated SQLite clips schema to include source_app; wired AppProbe to ClipboardHistoryViewModel and exposed SourceApp/HasSourceApp in ClipboardItemViewModel; added App metadata row to ClipboardHistoryView; added 6 unit tests (188 passing).
- D3: Added ClipboardPrivacyFilter.IsIgnoredApp pure helper (.exe and case-insensitive matching); added IgnoredApps list in AppSettings/SettingsStore defaulting to 7 common password managers; created ClipboardFormats with ExcludeClipboardContentFromMonitorProcessing detection; integrated ExcludedByOwnerProbe and IgnoredApps checks into ClipboardHistoryViewModel.PollClipboardAsync skipping capture and sync with status message; added IgnoredAppsText editor in SettingsView Advanced Clipboard expander; added 17 unit tests in ClipboardPrivacyFilterTests (207 passing).
- D4: Created ClipboardSnapshot in Core; widened IClipboardAccess with GetSnapshotAsync/SetSnapshotAsync; investigated Avalonia 12.1.6 clipboard APIs and implemented Win32Clipboard for HTML, PNG, CF_HDROP, and CF_DIB (saving to PNG via PngBitmapEncoderOptions with no extra packages needed); implemented AvaloniaClipboardAccess; updated FakeClipboard in ViewModelTests; added unit tests in ClipboardSnapshotTests (215 passing).
- D5: Extended ClipboardPayload with media fields (MediaPath, MediaHash, Width, Height, MediaBytes) preserving wire compatibility; created PngHelper for header dimension parsing; updated ClipboardStorageService with media columns, SHA-256 deduplication, and media file cleanup on DeleteClip/Clear/Purge; added SaveImages toggle in SettingsStore, SettingsViewModel, and SettingsView; wired ClipboardHistoryViewModel image polling, deduplication, 20 MB size limit, and re-copying image snapshot; added 11 unit tests in ImageStorageTests (230 passing).
- D6: Added Thumbnail (96px width lazy-loaded) and PreviewImage bitmaps with IDisposable to ClipboardItemViewModel, with DimensionsText, FormattedSizeText, and ImageFormatText; updated ClipboardHistoryViewModel with IsImageFitToWindow/ImageStretch, zoom commands (ToggleImageZoom, SetImageFit, SetImageActualSize), CopyImageAsync, SaveImageAsAsync (with PickSavePath), and OpenImage (with ImageOpener); wired StorageProvider.SaveFilePickerAsync in App.axaml.cs; updated ClipboardHistoryView with 36x36 rounded thumbnail/badge and image subtitles, dedicated image action buttons, BgSunken preview area with Fit/100% controls and ScrollViewer, and Image Details metadata card; added 10 unit tests in ClipboardImageViewModelTests (240 passing).
- D7: Captured HTML into ClipboardPayload.HtmlContent from snapshot during PollClipboardAsync; added HasHtml/HtmlContent to ClipboardItemViewModel and small "Rich" chip with BadgeViolet token to detail pane metadata header in ClipboardHistoryView; updated QuickPasteViewModel.PasteRequested to (text, rich) and added PasteSelected(rich); wired Shift+Enter in QuickPasteWindow to paste rich format, Enter / double-tap to paste plain; updated App.axaml.cs to write full snapshot (text + html) when rich requested and text-only snapshot by default; updated CopyTextAsync/CopyClip to set plain text snapshot; added 3 unit tests in QuickPasteTests (243 passing).
- D8: Added file clips capture from Explorer (FileUri) with newline-separated paths, deduplication, and privacy checks; exposed IsFileClip, FilePaths, HasFiles, IsDirectoryOnly, FileBadgeText, FileTitle, and FileClipItems on ClipboardItemViewModel; added CopyFilesAsync and SendFileToPeerAsync wired to MouserHost.SendFileAsync on ClipboardHistoryViewModel and MainViewModel; updated ClipboardHistoryView with FOLDER/FILE badges, list subtitle, dedicated file action buttons, BgSunken file list preview with icons and sizes, and File Details metadata card; added 10 unit tests in ClipboardFileClipsTests (253 passing).
- D9: Implemented system and custom tags with tags/clip_tags SQLite tables, deterministic 8-color palette, and cascading cleanup; added TagInfo in Core and TagViewModel in UI; added Tags row with pill chips and ＋ Tag flyout in detail pane, active tag pill in filter bar, and TAGS section in sidebar with count badges; added #tag search and tag filtering; added 10 unit tests in ClipboardTagsTests (263 passing).
- D10: Implemented pure SearchQuery parser in Core supporting type:, app:, device:, @device, #tag, before:, after:, on: operators, whitespace tokenization, and quote preservation; wired search operator filtering in ClipboardHistoryViewModel.ReloadAsync(); updated search box placeholder to "Search, or type # @ type: on:"; added 40 unit and ViewModel integration tests in SearchQueryTests (303 passing).
- D11: Implemented ClipDelivery record in Core, deliveries SQLite table with SaveDeliveriesAsync/GetDeliveriesAsync/cascading deletion in ClipboardStorageService; updated MouserHost.ShareClipboardAsync to return per-device ClipDelivery list, with failed clip 10-minute retry in SyncPeers; updated ClipboardHistoryViewModel with ShareCapturedText returning delivery status, ClipDeliveryViewModel with theme-token brush mapping, SelectedClipDeliveries collection and delivery summary header; added Delivery section card to ClipboardHistoryView detail pane using dynamic theme tokens; added 11 unit tests in ClipboardDeliveryTests (314 passing).
- D12: Added SendClipboard, ReceiveClipboard, and SendFiles toggles to PeerSettings, DeviceDetailViewModel, and PeerViewModel with auto-save; enforced sync rules in MouserHost.ShareClipboardAsync (Skipped with message) and CreateTransfer.ClipReceived (dropped); implemented MouserHost.SendClipToAsync for manual push; added SendClipboard and ReceiveClipboard Setting-row toggles in DevicesView using dynamic theme tokens; added "Send to ▾" MenuFlyout to ClipboardHistoryView detail pane bound to SendablePeers and wired SendClipToPeerHandler in MainViewModel and ClipboardHistoryViewModel; added 6 unit tests in ClipboardSyncRulesTests (320 passing).
- D13: Implemented SnippetExpander in Core with support for date, time, datetime, clipboard, uuid, prompt:Label, cursor offset calculation, and nested/unclosed braces; created Snippet model and SQLite snippets table in ClipboardStorageService with GetSnippetsAsync/GetSnippetByNameAsync/SaveSnippetAsync/DeleteSnippetAsync; implemented SnippetsViewModel and two-pane SnippetsView with dynamic theme tokens, variable insertion flyout, and search filtering; wired Snippets navigation in MainViewModel and added ✂️ Snippets sidebar entry in MainWindow; implemented ! snippet search mode and snippet expansion in QuickPasteViewModel; added 18 unit and integration tests in SnippetTests (338 passing).
- D14: Implemented pure TextTransforms in Core (Upper, Lower, TitleCase, Trim, RemoveLineBreaks, SortLines, DedupeLines, TryJsonPretty, JsonPretty, JsonMinify, UrlEncode, UrlDecode, Base64Encode, Base64Decode, StripFormatting, and All list); added Transform ▾ menu and Transformed Result card with Copy result to ClipboardHistoryView using dynamic theme tokens; added transform cycling via Tab in QuickPasteViewModel and QuickPasteWindow; added 22 unit and integration tests in TextTransformTests (360 passing).
