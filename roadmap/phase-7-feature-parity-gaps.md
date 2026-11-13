# Phase 7+: What is still missing (gap analysis)

Written after reading the code, not the design docs. Every "today" statement below was checked in source;
anything I could not run (macOS, Android on a device) is marked *unverified*.

**Yardsticks.** ShareMouse (and Synergy/Barrier/Input Leap/Mouse Without Borders) for the KVM half. Ditto,
Windows Win+V, Paste and Maccy for the clipboard-manager half. The original
[feature matrix](../SYSTEM_DESIGN_AND_ROADMAP.md) is the promise; this page is the distance to it.

> **Status tracking moved:** the checkboxes in section 4 are the *feature wish list*. The live status and the
> executable step-by-step tasks are in [../ROADMAP.md](../ROADMAP.md) and [tasks/](./tasks/). Update the board, not this list.

**macOS is parked for now.** macOS findings in this analysis are kept, and every macOS-only work item is in
[backlog-macos.md](./backlog-macos.md). Sections 4–6 below are the Windows + Android plan.

---

## 1. Why the app feels so simple

It is not an accident of polish. Four structural reasons:

1. **The sidebar is four pages because only four things exist.** Phone Remote, Clipboard History, Displays &
   Network, Settings ([MainViewModel.cs:10](../src/Mouser.UI/ViewModels/MainViewModel.cs#L10)). Nothing is hidden. Whole
   product areas have no code at all: snippets, file transfer history, device management, hotkeys, activity.
2. **Phases were closed by "the engine works", not "a user can drive it".** The roadmap marks KVM, clipboard and
   files done, but each is a thin engine with one button on one page. ShareMouse's depth lives in its *options*
   (edge resistance, switch delay, hotkeys, per-peer rules); Mouser has one toggle.
3. **The clipboard is text-only.** `IClipboardAccess` has `GetTextAsync`/`SetTextAsync` and nothing else
   ([PlatformServices.cs:9](../src/Mouser.UI/Services/PlatformServices.cs#L9)). `ClipboardContentType` declares Html, Rtf,
   Image and FileUri, and `ClipboardPayload.HtmlContent` exists, but nothing ever fills or reads them. A clipboard
   manager that cannot hold an image is not competing with Ditto, whatever the design doc says.
4. **No always-on shell.** There is no tray icon, no close-to-tray, no notifications (searched `src/`: zero
   hits). As written, closing the window exits the process, which stops the KVM, sync and the phone receiver.
   A tool whose whole job is to run in the background currently needs a visible window.

---

## 2. What is missing, by area

### 2.1 App shell and UI (the "empty sidebar" problem)

| Missing | Why it matters |
| :--- | :--- |
| Tray icon, close-to-tray, start minimized | The app must live in the background. Biggest single gap. |
| Notifications / toasts (clip received, file received, peer connected/lost, control switched) | Today the only feedback is text inside a page you may not be looking at. |
| **Devices** page (all computers and phones, status, rename, forget, per-device options) | Pairing lives inside the layout page; phones and computers are managed in two different places. |
| **Files** page (transfer history, progress, accept/reject, open folder, resend) | Transfers show one status line (`TransferStatus`). No history, no cancel, no queue. |
| **Snippets / templates** page | Promised in the matrix (`{{date}}`, `{{clipboard}}`). Not started. |
| **Hotkeys** page | All hotkeys are hard-coded constants (Ctrl+Alt+Esc, Scroll Lock, Win+Alt+V). |
| **Activity / diagnostics** page (log, latency, why a peer is "Not responding", firewall help) | When it fails on a LAN nothing tells the user why. |
| First-run onboarding (name this PC, firewall prompt, Accessibility on macOS, "pair your first device") | Setup is currently tribal knowledge in the README. |
| Light theme, UI scale, localisation | Colors are hard-coded hex in every view. No theme layer. |
| About / update check / release notes | No version display, no update path. |

### 2.2 Mouse and keyboard sharing vs ShareMouse

| Feature | State |
| :--- | :--- |
| Edge hop between two computers, return edge, absolute positioning | **Done (Windows).** |
| Hotkey to return or lock cursor | Done, but fixed keys, no UI. |
| Dead-peer watchdog, stuck-key release | Done. |
| **Per-monitor layout** (cross from one specific monitor, not the whole desktop bounding box) | Missing. `SetLocalDesktop` uses the bounding box, so with mismatched monitor heights the cursor can leave through a gap that has no screen. |
| **Draggable layout** (place a peer anywhere along an edge, offset it) | Missing. Peers snap to top/left aligned Left/Right/Above/Below only (`Place` in [KvmController.cs:135](../src/Mouser.Core/Kvm/KvmController.cs#L135)). |
| **More than two computers in a chain** (A–B–C) and several peers on one side | Missing. Every peer is positioned relative to *this* computer only. |
| Edge resistance / switch delay / corner dead zones | `VirtualKvmManager.EdgeResistanceMs` exists (fixed 80 ms) but no setting reaches it, and there are no dead zones. Accidental switching while docking windows is the #1 ShareMouse complaint. |
| Per-peer **switch hotkeys** (jump straight to computer N) | Missing. Only "return" exists. |
| **Mac-style key translation on the wire** (Cmd↔Ctrl as a *setting*) | Hard-wired in `KeyTranslationService`; no user option, no per-peer mapping, no layout (AZERTY/QWERTZ) handling. *Unverified on macOS.* |
| **Cross-screen drag and drop** of files | Missing. Files go through "Send file…" only. |
| **Synchronised lock / screensaver / sleep** | Missing. |
| **Media and volume keys** forwarded | Android sends them; desktop capture path only forwards plain VK keys, no test coverage for media keys. |
| **Clipboard follows the cursor** (copy on A, hop, paste on B with no waiting) | Works through 1 s polling, so a fast hop-and-paste can beat it. |
| **Secure desktop / elevated windows** (UAC prompt, lock screen, admin apps) | Missing and *hard* (see §3). |
| Gaming pass-through, high-poll-rate smoothing (500–1000 Hz) | Missing. Each move is one UDP packet; no coalescing or jitter buffer. |
| Manual "add computer by IP" on desktop | Missing. The error text says "Enter its address manually" but only the *phone* page has an address; there is no UI to add a peer by IP. Discovery is UDP broadcast only, so VLANs, guest Wi-Fi and VPNs (Tailscale) do not work. |
| mDNS / Bonjour discovery | Missing (broadcast instead). |
| Audio follows cursor, screen "glance" preview | Not started (ideas section of the design doc). |

### 2.3 Clipboard vs Ditto / Win+V / Paste / Maccy

| Feature | State |
| :--- | :--- |
| Searchable history (FTS5), stars, retention, secret filter | **Done**, text only. |
| Quick-paste overlay | Done on Windows only. Always pastes plain text. |
| **Images** | Missing. Highest-value gap. |
| **Rich text / HTML / RTF** (keep formatting, "paste as plain text") | Missing. Column exists, never populated. |
| **Files copied in Explorer / Finder** | Missing. Not captured, not synced. |
| **Source app, window title, per-app ignore list** | Missing. This also blocks the password-manager exclusion promised in the matrix (1Password, Bitwarden, KeePass): today only regex patterns catch secrets, and short passwords are not matched. |
| Event-driven capture (`WM_CLIPBOARDUPDATE`, `NSPasteboard.changeCount`) | Missing. It is a 1 s poll in `App.axaml.cs`. Misses fast copy-copy and wastes wake-ups. Phase 5 doc claims otherwise. |
| **Pin groups, tags, folders, notes** | Only a single star flag. |
| **Edit a clip**, merge clips, multi-select paste, paste stack / sequential paste | Missing. |
| **Snippets and templates**, text expansion | Missing. |
| **Transforms** (UPPER/lower, trim, strip formatting, JSON prettify, URL decode, Base64) | Missing. Cheap and popular. |
| **OCR / image text search**, link previews, colour swatches, QR decode | Missing. |
| Direct-push mode ("send selection to Mac") with a device picker | Missing. Sync is all-or-nothing. |
| **Per-device sync rules** (this device sends only, receives only) | Missing. |
| Android clipboard bridge | Missing. Phone cannot send or receive clips. |
| **History encrypted at rest** | Missing. `clipboard.db` is plain SQLite; the matrix promises "Encrypted Local History". `ClipboardCryptoEngine` exists but is only used on the wire. |
| Import/export, backup, "clear on exit", pause capture hotkey | Missing. |
| De-duplication that keeps context | Dedupes by exact text and silently moves the old row's timestamp; the old source device is overwritten. |
| Paging | Loads the newest 200 clips; older ones are only reachable by search. |

### 2.4 Phone companion

Done: trackpad, tap/two-finger gestures, hold-to-drag, gyro air mouse, media keys, numpad, auto-discovery, saved PIN.

Missing: **typed text / voice dictation** (the desktop protocol only carries key *codes*, so Unicode needs a new
packet type), **clipboard bridge**, **share-sheet target** ("Share → Mouser" to send a link or photo), three-finger
gestures, pinch zoom, macro pad with custom actions, laser-pointer overlay, battery/ping diagnostics, haptics and
sensitivity settings. No iOS.

### 2.5 Security and networking

- **Pairing is not cryptographic.** 6-digit PIN in clear text, trust by source IP
  ([PairingGate.cs:9](../src/Mouser.Core/Networking/PairingGate.cs#L9)); wire key is PBKDF2 of the PIN, so a captured
  frame can be brute-forced offline. The design promised X25519 + ChaCha20-Poly1305 mutual authentication.
- **Input packets are not encrypted or authenticated per packet.** Anyone who can spoof a trusted IP can inject
  keystrokes into a computer. For a tool that types for you this is the highest-severity item here.
- Two PINs per pair (each side keeps the other's PIN in plain `settings.json`).
- No QR pairing, no device certificates, no revoke-from-the-other-side, no pairing approval prompt on the target.
- No "ask before accepting a file", no size/type rules, no quarantine of received files.
- Replay: sequence numbers are per-sender ordering, not a replay defence across restarts.

### 2.6 Platforms, packaging, quality

- **Linux:** no input injection, no capture, no clipboard hook. `InputSinks` returns `NullInputSink` there. X11 (XTest) then Wayland (portals/libei) are both missing.
- **macOS:** code exists but has never run on a Mac (README). Accessibility, Input Monitoring and notarization are all unproven.
- **Windows:** the hooks only work on the user desktop. Start-at-login is a registry Run key; no service, no elevated companion.
- No installers (MSI/MSIX, DMG, AppImage/Flatpak), no signing, no auto-update, no crash reporting or opt-in telemetry.
- Tests: ~130 unit/integration tests, but **nothing UI-level** (no Avalonia headless tests) and no two-machine CI.
- Settings have no schema version, so adding fields later has no migration path.

---

## 3. What is genuinely hard (and why)

| Hard thing | Why |
| :--- | :--- |
| **Input on the lock screen, UAC and elevated apps (Windows)** | Hooks and `SendInput` are blocked across integrity levels and on the secure desktop. ShareMouse solves it with a signed Windows service plus a helper in the secure session. Needs code signing and an installer; cannot be done from a normal user process. |
| **Wayland (Linux)** | No global capture or injection by design. Needs `libei`/`libeis`, the RemoteDesktop/InputCapture portals, or compositor-specific protocols. Behaviour differs on GNOME, KDE, Sway. |
| **macOS permissions and notarization** | Accessibility + Input Monitoring grants reset when the binary signature changes. Unsigned builds make users re-grant constantly. Needs an Apple Developer account and a Mac to test. |
| **Real pairing crypto** | Mutual authentication with a short PIN means a PAKE (SPAKE2/OPAQUE) or X25519 with a confirmation code, plus a protocol version bump and migration of existing pairings. Easy to get subtly wrong. |
| **Multi-monitor + mixed DPI** | Physical pixels vs scaled pixels differ per monitor and per OS; the "edge" must map between non-aligned rectangles. Needs a per-monitor geometry model on the wire, not a bounding box. |
| **Keyboard layouts** | The wire sends virtual-key codes. Different layouts, dead keys, IME and AltGr produce wrong characters on the far side. Correct fix is sending scan codes (physical) or Unicode (text) depending on context. |
| **Rich clipboard formats** | Each OS has its own multi-format model (CF_HTML, CF_DIB vs NSPasteboard types vs MIME). Images need size limits, format negotiation, and a streaming path (the 4 MB frame limit is too small). |
| **Password-manager exclusion** | Needs per-OS clipboard format hints (`ExcludeClipboardContentFromMonitorProcessing`, `org.nspasteboard.ConcealedType`) and source-app detection. Not derivable from text. |
| **Clipboard on Android 10+** | Background clipboard reads are blocked; needs an IME or Accessibility service, which also carries Play Store policy risk. |
| **Cross-screen drag and drop** | Needs a transparent drag-capture window on both machines and OS-specific drag sources/targets, then hands off to the file transfer engine. |
| **Testing without hardware** | Two real computers (one Mac, one Linux) plus a phone are needed to trust any of the above. |

---

## 4. Work plan (phases 7–13)

**Scope rule for now: Windows + Android first.** Anything that only matters on macOS is in
[backlog-macos.md](./backlog-macos.md) and is deliberately parked, not dropped. Linux items are listed in
Phase 12 but come after Windows. Tags: **[Win]** Windows, **[And]** Android, **[All]** cross-platform code,
**[Lin]** Linux.

Each item is meant to be one reviewable change. Items marked ★ are the ones that most change how the app feels.

### Phase 7: App shell (make it a real background app) — start here

> **Detailed design lives in [phase-7a-ui-redesign-lock-and-tray.md](./phase-7a-ui-redesign-lock-and-tray.md):**
> the simple UniClip-style UI, cross-platform tray/menu-bar and keep-running rules, the app lock, dark/light mode for
> desktop, Android and web, the single Device model, and ShareMouse preference parity. Where the two lists overlap,
> 7a wins; the items below stay as the checklist for what is not redesigned.

**Tray and lifecycle**
- [ ] ★ [Win] Tray icon with menu: Open, Pause sharing, Pause clipboard capture, Send clipboard now, Quit
- [ ] ★ [Win] Close-to-tray (window close hides, Quit exits) and a "closing window keeps Mouser running" setting
- [ ] [Win] `--minimized` flag and "Start minimized" option; start-at-login launches minimized
- [ ] [All] Single-instance guard (second launch focuses the first; `--data-dir` bypasses it for testing)
- [ ] [Win] Tray icon states: idle / connected / controlling a peer / paused / error
- [ ] [All] Graceful shutdown: release held keys and buttons on every peer, flush the clipboard DB

**Notifications and feedback**
- [ ] ★ [Win] Toasts: clip received, file received, peer found/lost, control moved to/from a peer, pairing request
- [ ] [All] Per-event notification toggles in Settings and a Do-Not-Disturb switch
- [ ] [All] In-app status bar showing the current control owner and the active hotkeys
- [ ] [All] Small on-screen indicator when the cursor is on another computer (border glow or corner badge)

**New pages and navigation**
- [ ] ★ [All] **Devices** page: computers and phones in one list; rename, forget, status, last seen, OS, IP, per-device sync/KVM/file toggles
- [ ] ★ [All] **Files** page: transfer history, progress bars, cancel, retry, open file/folder, resend, clear history
- [ ] [All] **Hotkeys** page: every hotkey editable, with conflict detection and reset to default
- [ ] [All] **Activity** page: connection log, packet loss and latency per peer, port/firewall checks, "Copy diagnostics"
- [ ] [All] **Snippets** page (stub now, filled in Phase 8)
- [ ] [All] **About** page: version, build, licences, check for updates, links to docs and repo
- [ ] [All] Sidebar grouping (Devices / Clipboard / Sharing / Settings) and collapsible sidebar for small windows
- [ ] [All] Keyboard navigation and focus order on every page; screen-reader names on all buttons (accessibility)
- [ ] [All] Remember window size, position and last page

**Onboarding and settings structure**
- [ ] ★ [All] First-run wizard: name this PC, firewall check, choose features, pair first device
- [ ] [Win] Firewall helper: detect blocked ports and offer the one-click rule (with UAC prompt)
- [ ] [All] Settings split into tabs: General, Sharing, Clipboard, Notifications, Security, Advanced
- [ ] [All] Settings schema version and a migration step so new fields never break old files
- [ ] [All] Settings import/export and "reset to defaults"
- [ ] [All] Editable device name (currently the machine name)
- [ ] [All] Theme tokens replacing hard-coded hex colours; light theme, dark theme, follow-system
- [ ] [All] UI scale / font size setting
- [ ] [All] Localisation scaffolding (resource files) with English first, Arabic/RTL and French as the first targets
- [ ] [All] Empty states and inline help on each page (what this page does, what to do next)

**Connectivity**
- [ ] ★ [All] Add computer by IP:port (desktop), with test-connection and saved manual peers
- [ ] [All] Re-resolve saved peers by device id when their IP changes
- [ ] [All] Beyond plain broadcast: also try subnet-directed broadcast and a unicast probe of saved peers
- [ ] [All] mDNS (`_mouser._udp`) advertise and browse alongside broadcast
- [ ] [All] Tailscale/VPN friendly mode: connect by DNS name or 100.x address
- [ ] [All] Network adapter picker (which NIC to advertise on) and IPv6 support

### Phase 8: Clipboard parity (Ditto / Win+V / Paste)

**Capture**
- [ ] ★ [Win] Event-driven capture with `AddClipboardFormatListener` / `WM_CLIPBOARDUPDATE`; keep a slow poll only as a fallback
- [ ] [All] Clipboard abstraction that returns all formats at once (text, HTML, RTF, image, file list) instead of text only
- [ ] [All] Record source app, window title and process icon at copy time
- [ ] ★ [Win] Honour `ExcludeClipboardContentFromMonitorProcessing` and `CanIncludeInClipboardHistory` so password managers are skipped
- [ ] [All] Per-app ignore list (add from a running-apps picker) and per-app "capture only" list
- [ ] [All] Pause capture hotkey and "pause for 5 minutes" tray item
- [ ] [All] Larger-clip policy: store big text on disk, show size, optional per-type limits instead of one 1 MB cut-off

**Formats**
- [ ] ★ [Win] Image clips (PNG/DIB): capture, thumbnail, full preview, copy back, size limit, stored as files not DB blobs
- [ ] ★ [All] HTML and RTF capture, formatting-preserving paste, "paste as plain text" default with a modifier for rich
- [ ] [Win] File-list clips (Explorer copy): store paths, show icons, re-copy, send to a peer through the file transfer path
- [ ] [All] Link clips: fetch title and favicon in the background (opt-in), show domain
- [ ] [All] Colour clips (`#rrggbb`, `rgb()`) with swatch
- [ ] [All] Better classifier: email, phone, path, JSON, number, IP, Markdown, address
- [ ] [All] OCR text from image clips so they are searchable (Windows.Media.Ocr)
- [ ] [All] QR code detection in image clips

**Organise and search**
- [ ] [All] Tags and groups; drag a clip onto a group
- [ ] [All] Notes on a clip and rename a clip title
- [ ] [All] Edit clip text in place and "save as new clip"
- [ ] [All] Filters by device, source app, date range and type; saved searches
- [ ] [All] Search operators (`type:image app:chrome device:laptop before:2026-01-01`)
- [ ] [All] Regex search toggle and match highlighting in results
- [ ] [All] Infinite scroll / paging instead of the newest 200 only
- [ ] [All] De-duplication that keeps the first copy time, a use count and every source device
- [ ] [All] Per-clip expiry ("delete in 1 hour") and sensitive-clip auto-clear after paste
- [ ] [All] Merge selected clips and multi-select copy/delete/star

**Paste experience**
- [ ] ★ [Win] Quick-paste overlay v2: image and rich previews, numbered shortcuts (1–9), arrow/Enter/Esc, type to filter, group tabs
- [ ] [Win] Overlay opens near the caret/cursor and remembers its size
- [ ] [All] Sequential paste stack ("paste next") and "paste all as list"
- [ ] [All] Transform on paste: UPPER, lower, Title, trim, remove line breaks, strip formatting, JSON prettify/minify, URL encode/decode, Base64, sort lines, dedupe lines
- [ ] [All] Typed-text fallback for apps that block a synthetic Ctrl+V
- [ ] [All] Configurable quick-paste hotkey (currently Win+Alt+V)

**Snippets and templates**
- [ ] ★ [All] Snippet library with folders, search and favourites
- [ ] [All] Variables: `{{date}}`, `{{time}}`, `{{clipboard}}`, `{{cursor}}`, `{{prompt:name}}`, `{{uuid}}`
- [ ] [All] Trigger by hotkey and by text abbreviation (`;sig`) with an expansion engine
- [ ] [All] Import/export snippets (JSON) and sync snippets to paired devices

**Sync**
- [ ] ★ [All] Sync images, HTML and files-as-clips between computers (raise the 4 MB frame limit, chunk large clips)
- [ ] [All] Per-device rules: send only / receive only / both / off, per content type
- [ ] ★ [All] Manual push: "Send to device…" from the overlay, the tray and a hotkey
- [ ] [All] Conflict rule when two devices copy at once (last writer wins, both kept in history)
- [ ] [All] Sync history (not just the live clip) on request, with a size cap
- [ ] [All] Per-clip origin badge and "sent to" status

**Storage and privacy**
- [ ] ★ [All] Encrypt `clipboard.db` at rest (SQLCipher or per-row AES-GCM with a key in Windows DPAPI)
- [ ] [All] Secret filter additions: JWTs, Slack/Google/Azure keys, SSH keys, `.env` lines, IBANs, national IDs; user-defined regex rules
- [ ] [All] Masked preview for sensitive clips and a "reveal" action
- [ ] [All] Clear-on-exit, "panic wipe" hotkey, and retention presets (1 h, 24 h, 7 d, 30 d)
- [ ] [All] Export/import history (JSON + media folder) and backup/restore
- [ ] [All] Database maintenance: vacuum, integrity check, size display, FTS rebuild
- [ ] [All] Stats: clips per day, top source apps, storage used

### Phase 9: ShareMouse-level mouse and keyboard sharing

**Layout**
- [ ] ★ [All] Per-monitor geometry on the wire (each monitor's rectangle, scale, primary), replacing the bounding box
- [ ] ★ [All] Draggable layout canvas: place a peer anywhere along an edge with an offset, snap to edges
- [ ] [All] Multiple peers on the same side and chains (A–B–C), with transitive routing
- [ ] [All] Per-peer DPI and scale handling so the cursor speed feels the same across screens
- [ ] [All] Auto-detect monitor changes (plug/unplug, resolution change) on both sides and re-layout
- [ ] [All] Wrap-around option (right edge of the last screen returns to the first)
- [ ] [All] Layout presets and quick "swap sides"

**Switching behaviour**
- [ ] ★ [All] Edge-resistance and switch-delay settings (wire up `EdgeResistanceMs` and the push threshold) with a UI slider
- [ ] [All] Corner dead zones and "double-tap edge to switch"
- [ ] [All] Do not switch while a mouse button is held, while a game/fullscreen app is focused, or when Scroll Lock is on
- [ ] [All] Modifier-to-switch ("hold Shift to cross")
- [ ] [All] Per-peer jump hotkeys (Ctrl+Alt+1, +2…) and "next computer" hotkey
- [ ] [All] Configurable return/lock hotkeys (currently Ctrl+Alt+Esc and Scroll Lock)
- [ ] [All] Cursor-lock to a single monitor and a "game mode" that disables hooks until toggled

**Keyboard**
- [ ] ★ [All] Layout-aware typing: send scan codes for shortcuts and Unicode for text so AZERTY/QWERTZ/Arabic layouts type correctly on the other side
- [ ] [All] IME, dead-key and AltGr handling tests
- [ ] [All] Modifier mapping settings (Win↔Ctrl, Alt, Caps Lock behaviour) per peer
- [ ] [All] Media and volume keys forwarded to a chosen "media host"
- [ ] [All] Caps/Num/Scroll lock state sync

**Mouse**
- [ ] [All] Move coalescing and a jitter buffer for 500–1000 Hz mice; measured latency target under 5 ms on LAN
- [ ] [All] Pointer acceleration/speed matching option per peer
- [ ] [All] Horizontal scroll and high-resolution wheel support end to end
- [ ] [All] Extra mouse buttons (back/forward) and tilt wheel

**Cross-computer features**
- [ ] ★ [All] Cross-screen drag-and-drop of files and text
- [ ] [Win] Synchronised lock (Win+L), screensaver and display sleep across computers
- [ ] [All] Wake-on-LAN for a sleeping peer from the Devices page
- [ ] [All] Clipboard is flushed to the peer before control moves so hop-and-paste never beats sync
- [ ] [All] Remote cursor "glance" thumbnail of the target screen (stretch)
- [ ] [All] Audio output follows the cursor owner (stretch)
- [ ] [All] Shared-terminal "run on peer" action (stretch)

**Windows edge cases**
- [ ] ★ [Win] Input on UAC prompts, the lock screen and elevated windows via a signed helper service (see Phase 12)
- [ ] [Win] Detect an elevated foreground window and warn instead of silently dropping input
- [ ] [Win] Touchscreen and pen passthrough policy
- [ ] [Win] Handle fast user switching and RDP sessions without leaving hooks installed

### Phase 10: Security rewrite

- [ ] ★ [All] Pairing with X25519 key exchange plus a short confirmation code shown on both screens (or SPAKE2 with the PIN)
- [ ] ★ [All] Authenticated and encrypted input channel: AEAD per datagram with a session key and counter; remove IP-based trust
- [ ] [All] Approval prompt on the target: "Allow *Laptop* to control this PC?" with remember choice
- [ ] [All] Device identity keys stored in the OS keystore (DPAPI on Windows), not in `settings.json`
- [ ] [All] Stop storing the peer PIN; store the derived session secret instead
- [ ] [All] QR-code pairing (desktop shows QR, phone scans) and one-time pairing links
- [ ] [All] Revoke from either side and a "paired devices" audit list with first-paired date
- [ ] [All] Replay protection across restarts (session nonces) and rate limiting on every listener
- [ ] [All] Accept-file prompt, per-device auto-accept, max size, blocked extensions, quarantine folder
- [ ] [All] Protocol version negotiation with a clear "update the other computer" message
- [ ] [All] Fuzz tests for the packet parsers and frame reader
- [ ] [All] Threat-model document and a SECURITY.md with a reporting address
- [ ] [All] Optional "local network only" enforcement (ignore non-private source addresses)

### Phase 11: Phone companion v2 (Android)

- [ ] ★ [And] Text typing and voice dictation through the system keyboard, sent as Unicode (new packet type)
- [ ] ★ [And] Clipboard bridge (phone clipboard to PC and PC clip to phone) using an IME or accessibility service
- [ ] [And] Share-sheet target: "Share → Mouser" sends links, text, photos and files to a chosen computer
- [ ] [And] Receive files from a computer and open them
- [ ] [And] QR-code pairing (needs Phase 10) and multiple saved computers with a picker
- [ ] [And] Macro pad with user-defined buttons (hotkeys, text, app launch) and import/export
- [ ] [And] Pinch-zoom, three-finger swipe (desktop switch, app switch), drag lock and tap-and-hold options
- [ ] [And] Sensitivity, acceleration, scroll direction and haptics settings
- [ ] [And] Laser-pointer / spotlight overlay for presentations (needs a desktop overlay window)
- [ ] [And] Slide remote mode (next/previous, timer, notes)
- [ ] [And] Ping, battery and Wi-Fi diagnostics on the connection bar
- [ ] [And] Reconnect automatically after sleep and Wi-Fi roaming; keep-alive foreground service
- [ ] [And] Landscape layout, tablet layout, dark/light theme and accessibility labels
- [ ] [And] Localisation and a Play Store listing checklist
- [ ] [And] Instrumented tests on the emulator and a release signing config
- [ ] [And] iOS companion (backlog, only after Android is solid)

### Phase 12: Windows platform, Linux and release

**Windows**
- [ ] ★ [Win] Signed helper service for secure-desktop and elevated input, with an installer that registers it
- [ ] ★ [Win] Installer (MSIX or MSI via WiX) with firewall rules, start menu entry and uninstall cleanup
- [ ] [Win] Code signing of the exe and installer
- [ ] [Win] Auto-update (Velopack or MSIX update channel) with release notes
- [ ] [Win] Portable mode flag that keeps all data next to the exe
- [ ] [Win] Opt-in crash reports and log files with rotation
- [ ] [Win] High-DPI and multi-monitor verification on mixed-scale setups
- [ ] [Win] ARM64 build

**Linux** (after the Windows items above)
- [ ] [Lin] Input injection on X11 via XTest, and capture via XInput2
- [ ] [Lin] Clipboard monitoring on X11 (XFixes) and Wayland (`wl-clipboard`/data-control)
- [ ] [Lin] Wayland input through libei/portals (GNOME, KDE) as a research spike first
- [ ] [Lin] Start-at-login (XDG autostart) and tray via StatusNotifierItem
- [ ] [Lin] AppImage and Flatpak packaging
- [ ] [Lin] Replace the `NullInputSink` fallback with a clear "unsupported on this desktop" message in the UI

### Phase 13: Quality, docs and website

- [ ] ★ [All] Headless Avalonia UI tests for each page and the view-model navigation
- [ ] [All] Two-instance integration job in CI (the existing `scripts/test-kvm-single-pc.ps1` flow, automated)
- [ ] [All] CI: build and test on Windows, Android unit tests, packaging artifacts on tags
- [ ] [All] Performance benchmarks: input latency, clipboard capture time, FTS query time, memory at idle
- [ ] [All] Soak test: 24-hour two-instance run checking for stuck keys, leaks and reconnect bugs
- [ ] [All] Chaos tests: drop/reorder/duplicate packets, kill a peer mid-transfer, change IP mid-session
- [ ] [All] Structured logging with a log viewer in the Activity page
- [ ] [All] User guide (setup, pairing, KVM, clipboard, phone, troubleshooting) and a FAQ for firewall issues
- [ ] [All] Developer docs: protocol specification, architecture diagram, how to add a platform backend
- [ ] [All] Update `web/` marketing site and release data once features land (its feature grid should not promise more than the app does)
- [ ] [All] Reconcile the older phase documents with reality (several still describe unbuilt work as done)
- [ ] [All] Contribution guide, issue templates and a CHANGELOG

### macOS backlog (parked on purpose)
Everything that is macOS-only lives in [backlog-macos.md](./backlog-macos.md). It is not scheduled, but it is
fully listed so nothing is lost. A macOS virtual machine is enough to start on most of it (see that file for
what a VM can and cannot test).

---

## 5. If you only do five things

1. Tray + notifications + close-to-tray (Phase 7). Without these it never feels like a product.
2. Devices page + add-by-IP (Phase 7). Fixes a dead-end error message and a confusing layout.
3. Images and rich text in the clipboard, with event-driven capture (Phase 8). The line between "text log" and "clipboard manager".
4. KVM options: edge resistance, hotkeys, per-monitor layout (Phase 9). This is what ShareMouse users actually tweak.
5. Authenticated input channel (Phase 10). Do it before telling anyone else to run this on a shared network.

## 6. Suggested order

Phase 7a Step A (design tokens, theme, tray/background) → Phase 7a Step B (new shell) → Phase 7 leftovers →
Phase 8 (capture + images first; the lock in 7a Step C needs its encryption) → Phase 9 (switching options first) → Phase 10 → Phase 11 →
Phase 12 (Windows) → Phase 13 continuously. Phase 10 can jump ahead of 8 and 9 if the app will be used outside a
trusted home network.
