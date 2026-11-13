# Phase 7a: Simple UI redesign, background app, lock, and the merged feature set

Added after reviewing UniClip (clipboard) and ShareMouse 7 (mouse and keyboard) screenshots. This page owns
**design, shell, tray/background behaviour, the app lock, dark/light mode and the way the two feature sets merge**.
The feature checklists for clipboard and KVM stay in [phase-7-feature-parity-gaps.md](./phase-7-feature-parity-gaps.md)
(phases 8–9); items here that extend them are marked *(→ P8)* or *(→ P9)*.

Scope: Windows + Android first, Linux tray noted, macOS in [backlog-macos.md](./backlog-macos.md).

---

## 1. Design rules ("super simple")

The app will have *more* features than today, so the rule is: **everything is reachable, almost nothing is visible.**

1. **One screen does the main job.** The default window is the clipboard (like UniClip). Sharing the mouse is a device setting, not a separate product.
2. **Defaults first.** Every control has a good default. A new user should never have to open Settings.
3. **Max 6 controls above the fold** on any page. Everything else goes under an **Advanced** expander, collapsed.
4. **Presets instead of numbers.** "Switching feel: Relaxed / Normal / Careful" replaces edge-resistance ms, push distance and dead-zone fields. Raw numbers live in Advanced.
5. **Toggles and sliders, not text boxes.** Typing is for names, ports and hotkeys only.
6. **Plain words.** "Share clipboard", not "Enable synchronization subsystem". No jargon (UDP, PIN hash, FTS) in the main UI.
7. **One place per thing.** A computer or phone appears once, in Devices, and carries all its options (sync, files, mouse sharing, placement).
8. **Show state, not settings.** Status dots, delivery ticks, a glowing border on the screen that has control. Users should *see* what is happening.
9. **Keyboard first.** Search is Ctrl+F, Settings is Ctrl+, , Lock is Ctrl+L; every action has a shortcut shown in its tooltip.
10. **No dead ends.** Every empty state says what to do next; every error says how to fix it.

## 2. What to take from UniClip and ShareMouse

### UniClip (clipboard UI) — adopt

| What the screenshots show | Decision |
| :--- | :--- |
| 3-pane layout: sidebar, list, detail | **Adopt.** The new main window. |
| Sidebar: All items (count), Pinned, Tags (coloured, counted), Devices (online/offline), Manage Devices…, Settings | **Adopt.** Replaces today's four nav buttons. |
| Top search with operators hint (`#` tag, `@` device, `/` command, `on:` date) and Ctrl+F | **Adopt** *(→ P8)*. |
| Filter chips: Type, Device, Tag, Time | **Adopt.** |
| List grouped by day with item count ("Yesterday · 22 items") | **Adopt.** |
| Row = type badge or thumbnail, title, device · time | **Adopt.** |
| List / grid view switch, always-on-top pin, send-to arrow, "…" menu in the title bar | **Adopt.** |
| Detail pane: primary action (Copy Image), Save as…, Open in preview, pin, delete | **Adopt.** |
| Image viewer with Fit / 100% and Quick Look | **Adopt** *(→ P8)*. |
| Metadata cards (dimensions, format and size, copied at) | **Adopt.** |
| "Add tag" and coloured system tags (#Link, #Code, #Image, #Folder) | **Adopt** *(→ P8)*. |
| **Delivery** list: per device, "0 of 2 devices", info icon with the reason | **Adopt, and extend** (see §6). This is the best idea in the screenshots. |
| Footer "Encrypted at rest · End-to-end between devices" | **Adopt** as a real, truthful badge once P8/P10 land. |
| Devices dialog: This device + paired list, online count, Enable sync, File sync, Profile, **Danger zone: Rebuild space**, + and − buttons | **Adopt**, merged with mouse sharing (see §5). |
| Mac and phone shown as devices in one list | **Adopt.** Phones become full devices. |

### ShareMouse (mouse and keyboard preferences) — adopt, simplified

| ShareMouse option | Decision | Where it lives |
| :--- | :--- | :--- |
| Equalize display dimensions | Adopt | Layout → Advanced *(→ P9)* |
| Wrap pointer around displays | Adopt | Layout → Advanced |
| QuickJump hotkey | Adopt | Hotkeys |
| Display layout toggle hotkey | Adopt | Hotkeys |
| Enable/disable sharing hotkey | Adopt | Hotkeys, also the tray "Pause" item |
| Prevent switching: full-screen windows, maximized windows, display corners, without a modifier held, specific programs | Adopt all; **one "Switching feel" preset** covers the common ones | Sharing → Advanced |
| Double-tap the display border to override prevention | Adopt | Sharing → Advanced |
| Allow drag and drop | Adopt | Device → Sharing |
| Insert remote clipboard with a hotkey | Adopt, **better**: see §6 | Hotkeys |
| Sync remote events: shutdown, suspend, lock, screensaver, unlock/login | Lock, screensaver, suspend: adopt. Shutdown: adopt but off, with a confirmation. Unlock/login: needs the Windows service (P12) | Device → Sharing → Advanced |
| Synchronize keyboard language layout | Adopt | Sharing → Advanced |
| Synchronize Num/Caps/Scroll lock | Adopt | Sharing → Advanced |
| Vertical and horizontal scroll speed, invert scroll | Adopt (per device) | Device → Mouse |
| Mouse speed | Adopt (per device) | Device → Mouse |
| Draw cursor if invisible / large cursor | Adopt (touch screens, some games) | Device → Mouse → Advanced |
| Use relative mouse positions | Adopt as the "Gaming" scene (Mouser is absolute today) | Scenes |
| Password for connecting clients | Already stronger via pairing (P10); keep an optional extra passphrase | Security |
| Preferred network adapter, TCP port, UDP port, "Find free port", IPv6 | Adopt (split TCP/UDP ports) | Network → Advanced |
| Accepted clients by IP, add by IP:port, show local IP, broadcast own info toggle | Adopt (this is the missing add-by-IP) | Devices + Network |
| Online update check (important / all), auto-install | Adopt | About / Updates |
| Proxy settings | Adopt, only for update checks | Updates → Advanced |
| Monitor Manager (draggable A/B blocks, offset allowed) | Adopt as the layout view | Devices → Layout |
| Panic key with "double-press required" | Adopt, replaces fixed Ctrl+Alt+Esc/Scroll Lock | Hotkeys |
| Hotkey sent as Ctrl+Alt+Del on the other PC | Adopt, needs the Windows service (P12) | Hotkeys → Advanced |
| Dim inactive displays (with hardware dimming) | Adopt, it *is* the "who has control" indicator | Sharing → Advanced |
| Diagnostics mode | Adopt as the Activity page | Advanced |
| Show tray notifications | Adopt | Notifications |
| "[Demo]" tags, "Upgrade" button, free/paid tiers | **Skip.** Everything stays free. | — |

## 3. New main window (target layout)

```
┌───────────────────────────────────────────────────────────────────────────────┐
│ ☰   [ Search, or type # @ / on:            Ctrl+F ]      ▤ ▦   ➜   📌   🔒   … │
├───────────────┬─────────────────────────────┬─────────────────────────────────┤
│ ● All items 492│  All  492                   │  [ Copy ]  Save as…  Open        │
│   Pinned     0 │  [Type▾][Device▾][Tag▾][Time▾]│  Image · This PC · 14:43        │
│               │  Yesterday · 22 items        │  ┌───────────────────────────┐ │
│ Tags        ＋ │  ▣ Image          ASUS 14:43 │  │ preview (Fit / 100%)      │ │
│  ■ Link    42 │  TXT 69970127     ASUS 14:42 │  └───────────────────────────┘ │
│  ■ Code    43 │  URL https://…    ASUS 14:37 │  [Dimensions][Format][Copied]   │
│  ■ Image   25 │  …                           │  ＋ Add tag                     │
│               │                              │  Delivery  2 of 2 devices ✓✓    │
│ Devices     ⌄ │                              │   • Laptop     ✓ delivered     │
│  ● Laptop     │                              │   • S22 Ultra  ✓ delivered     │
│  ○ S22 Ultra  │                              │  Encrypted at rest · end-to-end │
│ Manage devices│                              │                                 │
│ Settings Ctrl+,│                             │                                 │
└───────────────┴─────────────────────────────┴─────────────────────────────────┘
```

The sidebar footer shows a single status line ("Controlling LAPTOP · Ctrl+Alt+Esc to return").
**Devices** opens a dialog/page (UniClip style) that also holds mouse sharing and the layout (§5). **Phone Remote**
stops being a page and becomes the phone's Device page. **Files** is a type filter plus the Delivery list, with a
transfers sheet in the "…" menu. **Settings** becomes five short groups with Advanced collapsed.

## 4. Background app: tray, menu bar, keep running

The answer to "did you account for minimising to the tray": the first roadmap only had a Windows tray and a
macOS/Linux mention in other files. This replaces it with one cross-platform plan.

**Rule: closing the window never stops Mouser.** Only *Quit* (tray menu or Ctrl+Q) does, and Quit asks for the lock
passphrase if the app is locked.

- [ ] ★ [All] One tray implementation through Avalonia's `TrayIcon`, which targets the Windows tray, macOS menu bar and Linux status area. *Unverified for Avalonia 12 on each OS: confirm in a spike first.*
- [ ] ★ [Win] Window close hides to tray; first time shows a toast "Mouser is still running in the tray"
- [ ] [Win] Show hidden tray icons guidance (Windows hides new icons by default): first-run tip to pin it
- [ ] [Lin] StatusNotifierItem (works on KDE, XFCE, Cinnamon); GNOME needs the AppIndicator extension, detect and explain
- [ ] [Mac→backlog] Menu-bar icon, hide Dock icon while in background, Login Item
- [ ] [All] Tray menu: Open, Pause sharing, Pause clipboard, Lock, Send clipboard to…, Switch to (each device), Settings, Quit
- [ ] [All] Tray left-click toggles the window; middle-click pauses; double-click opens Devices
- [ ] [All] Tray icon states and tooltip: idle, connected, controlling a device, paused, locked, error (with reason)
- [ ] [All] Tray badges for unseen incoming clips/files
- [ ] [Win] Start at login, minimized to tray, and restart on unexpected exit (watchdog/Task Scheduler "restart on failure")
- [ ] [All] Survive sleep/resume, network changes and display changes: re-bind sockets, re-pair, re-detect monitors, re-install hooks
- [ ] [All] Single instance: a second launch brings the first to front (tray or window)
- [ ] [All] Hotkeys, quick-paste and clipboard capture keep working with no window open (already true for quick-paste; verify all)
- [ ] [All] Low idle cost: no UI timer when hidden (the 1 s `DispatcherTimer` today runs always), CPU/RAM budget stated in Phase 13
- [ ] [And] Android: foreground service with a persistent notification (connected/idle, Pause and Disconnect actions) so the connection survives the app being swiped away
- [ ] [And] Quick-settings tile and home-screen widget (Connect, Pause, Send clipboard)
- [ ] [And] Battery-optimisation exemption prompt and reconnect after Doze

## 5. One Device model (the smart merge)

Today computers appear in the layout page and phones in a different page. New model: **a Device is anything paired
with you**, and each Device has the same simple sheet (UniClip's dialog, with ShareMouse's controls added):

```
 Laptop-Pro            ● Online · Windows · 192.168.1.14
 ─────────────────────────────────────────────────────────────
 Share clipboard      [on]      Text, images and files, both ways
 Send & receive files [on]
 Share mouse & keyboard [on]    Position: [ left | right | above | below ]   Open layout…
 Profile ▾                       Work · Home · Presentation · Gaming · Custom
 Advanced ▾   Switching feel · speeds · hotkey · allow list · adapter
 ─────────────────────────────────────────────────────────────
 Danger zone   Forget device     Rebuild space (keep history, clear pairings)
```

- [ ] ★ [All] Single Device list in the sidebar with online/offline dots and count ("2 online")
- [ ] ★ [All] Device sheet as above; phones get the same sheet with phone-appropriate rows (clipboard bridge, remote, files)
- [ ] [All] "This device" entry with its own name, sync master switch and sharing master switch
- [ ] [All] **Profiles** per device and global: bundles of settings (Work, Home, Presentation, Gaming) switchable from tray or hotkey
- [ ] [All] Add (+) opens "Pair a device": discovered list, QR code, manual IP:port; remove (−) forgets
- [ ] [All] Refresh/rescan button; show *why* a device is offline ("not responding for 12 s", "firewall may block port")
- [ ] [All] "Rebuild space": keep local history, clear pairings and keys, re-pair from scratch
- [ ] [All] Layout (Monitor Manager) opens from the Device sheet or the title-bar menu, as a window with draggable blocks

## 6. Combining clipboard and mouse sharing in smart ways

These are the features neither product has on its own.

1. ★ **Clipboard rides along with the cursor.** When the cursor crosses an edge, the latest clip is already on the other side; no waiting for the 1 s poll. Optional: on return, the other computer's last copy comes back too. *(→ P9)*
2. ★ **Delivery ticks everywhere.** Every clip and file shows per-device status (queued, sent, delivered, failed with reason), using the same list as UniClip's Delivery pane. Failed deliveries retry when the device returns.
3. ★ **Paste from the other computer.** Hotkey (default Ctrl+Shift+V) opens the quick-paste overlay filtered to the *device you came from*; ShareMouse's "insert remote clipboard" but with history, search and images.
4. **Overlay follows control.** While controlling a peer, the quick-paste overlay lists that peer's history and pastes into its active window.
5. **Drag a clip to the edge.** Drag any history item to a screen edge (or onto a device in the sidebar) to send it; it arrives as a paste-ready clip or a saved file. Same engine as cross-screen drag and drop *(→ P9)*.
6. **Send selection to…** one hotkey copies the current selection and sends it to the next/chosen device, without touching the local clipboard.
7. **Tag rules.** Tag `#private` never syncs; `#shared` always syncs even if auto-sync is off; tags can be set per source app.
8. **Scenes (profiles) that touch both halves.** *Presentation*: no edge switching, no clipboard sync, phone becomes laser pointer. *Gaming*: hooks off, relative mouse, notifications silenced. *Work*: everything on.
9. **One Pause.** Tray/hotkey "Pause everything" stops capture, sync and sharing together; panic key (double-press) returns control *and* pauses sync until resumed.
10. **Lock locks both.** The app lock (§7) also stops sharing and sync, and can lock all paired devices at once.
11. **Unified search across devices** with `@device`; results show where the item lives and a "Fetch" button if it is on another computer.
12. **Phone is a first-class device:** its clipboard, share-sheet sends, files and the remote all appear in the same history and Delivery list.
13. **Control indicator doubles as clipboard indicator:** the dimmed/glowing display and the tray icon show who has control *and* whose clipboard is active.

## 7. App lock ("locked, and only the key opens it")

My reading of the request: a **real lock** that hides everything and that nobody, including someone sitting at the
unlocked PC, can open without the credential. If you meant something different, say so and this section changes.

**What lock does**
- Hides the window; the window cannot be shown, and Quit is disabled, until unlocked.
- Clipboard history, quick-paste overlay, Devices and Settings are unavailable.
- Encrypted history key is wiped from memory; the database stays encrypted on disk (needs "encrypt at rest" from P8).
- Clipboard capture and sync: **paused by default** while locked (option: keep sharing but never show anything).
- Mouse and keyboard sharing: off by default while locked (option: keep an active session alive).
- Incoming phone input, files and clips are refused or held in an encrypted queue.
- Tray shows a padlock; the only actions are *Unlock* and, optionally, *Lock all devices*.

**How it opens**
- Passphrase (Argon2id-derived key; wrong guesses back off exponentially; optional "wipe history after N failures").
- Windows Hello / biometrics on Windows, biometric on Android, as a convenience that unwraps the same key.
- **No backdoor.** Forgetting the passphrase means the history stays unreadable. A one-time recovery key is shown at setup so the user can choose a printed fallback.
- Lock state survives restarts: if Mouser was locked, it starts locked.

**When it locks**
- Manually: Ctrl+L, tray, title-bar padlock, hotkey, or from another paired device.
- Automatically: after N minutes idle, on Windows lock/sleep, on screen-saver, when a paired phone disconnects (optional "proximity lock").
- Synchronised: lock one device and, if enabled, all paired devices lock.

**Honest limits** (to show in the UI): a local administrator can still kill the process, and malware running as the
user can read the live clipboard while unlocked. What the lock guarantees is that stored history and Mouser's
controls are unreadable and unusable without the key.

- [ ] ★ [All] Lock screen view (large padlock, passphrase field, Windows Hello button, "Forgot?" explains the no-recovery rule)
- [ ] ★ [All] Key hierarchy: passphrase → Argon2id → key-encryption key → database key; stored wrapped, never in `settings.json`
- [ ] [All] Lock now / auto-lock timers / lock on session lock, sleep and screensaver
- [ ] [All] Locked-state behaviour switches (capture, sync, sharing, incoming) with the safe defaults above
- [ ] [All] Block Quit, window show and quick-paste while locked; locked state persisted across restarts
- [ ] [Win] Windows Hello unlock via the platform credential API
- [ ] [And] Android app lock with biometric/PIN and lock-on-background
- [ ] [All] Failed-attempt backoff, optional wipe after N failures, audit entry for each unlock attempt
- [ ] [All] Recovery key at setup (print/save once) and change-passphrase flow
- [ ] [All] Lock-all-devices command and "locked by LAPTOP" state on the others
- [ ] [All] Tests: wrong passphrase, restart while locked, capture while locked, brute-force backoff

## 8. Dark and light mode in every app

Current state (read from source): the desktop app is **forced to Dark** and has ~100 hard-coded hex colours across
its views ([App.axaml:5](../src/Mouser.UI/App.axaml#L5)); the Android app only defines a **dark** colour scheme
([Theme.kt](../android/app/src/main/java/com/mouser/companion/ui/theme/Theme.kt)); the website already has a dark/light
toggle that follows the system ([ThemeContext.tsx](../web/src/context/ThemeContext.tsx)).

**One palette, three implementations.** Define the colour tokens once in a short design file (`design/tokens.md`):
background, surface, surface-raised, border, text, text-muted, accent, success, warning, danger, plus type-badge
colours (text, link, code, image, file). Each app maps the same names.

- [ ] ★ [Desktop] Theme setting: **System (default) / Light / Dark**; follow OS changes live
- [ ] ★ [Desktop] Replace every hard-coded hex in `App.axaml` and views with theme resources (`ThemeDictionaries` for Light and Dark, `DynamicResource` in views)
- [ ] [Desktop] Design both themes together; check contrast (WCAG AA) in each; test every page in both
- [ ] [Desktop] Accent colour option (default indigo) instead of the Windows accent
- [ ] [Desktop] Quick-paste overlay, lock screen, toasts and tray menu follow the theme
- [ ] ★ [And] Add a light colour scheme, follow system, optional Material You dynamic colour, and a manual override
- [ ] [And] Re-skin the Remote screens (trackpad, air mouse, macros, connection bar) with the same tokens
- [ ] [Web] Keep the toggle; add a "System" option; use the same token names so the site matches the app
- [ ] [All] Screenshot tests in both themes (Phase 13) to catch unreadable text
- [ ] [All] Respect "reduce motion" and "high contrast" system settings

## 9. Work items by order

**Step A: foundation (do first, everything else sits on it)**
- [ ] Design tokens file and the theme system (§8)
- [ ] Reusable controls: Card, SettingRow (label + hint + toggle), Chip filter, TypeBadge, DeliveryRow, StatusDot, EmptyState, Toast
- [ ] Tray + background lifecycle (§4)

**Step B: new shell**
- [ ] Three-pane main window, sidebar, title-bar actions, list/grid, grouped list, detail pane (§3)
- [ ] Device list and Device sheet, Pair-a-device dialog, Layout window (§5)
- [ ] Simple Settings (5 groups, Advanced collapsed): General, Clipboard, Sharing, Hotkeys, Security & Lock, About/Updates
- [ ] Hotkeys editor, Notifications, Activity/diagnostics

**Step C: lock**
- [ ] App lock (§7), then wire encryption at rest (P8) to its key

**Step D: merged features**
- [ ] Delivery tracking and retry, paste-from-device overlay, send-selection hotkey, tag rules, profiles/scenes, unified search, drag-clip-to-device (§6)

**Step E: ShareMouse preference parity** (the table in §2, each as a setting plus a test)

## 10. What the UI work does not change
The networking, KVM and clipboard engines stay as they are. The redesign is a new view layer over the existing
view models, plus the new state they need (devices, delivery, profiles, lock). Existing tests keep guarding the
engines; new view-model tests cover navigation, lock and settings.
