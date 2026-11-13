# Mouser: Roadmap & Status

The phase documents in this folder are the original design notes. This page is the **source of truth for
what actually works**; the phase documents describe intent and may mention things that were never built.

Status was last verified on Windows 11 with `dotnet test tests/Mouser.Tests` (130 passing) and the Android
unit tests. macOS code is written but **has not been run on a Mac**.

| Phase | Area | Status |
| :---: | :--- | :--- |
| [0](./phase-0-macos-dev-setup.md) | macOS toolchain | Done (manual: grant Accessibility to Mouser on each Mac) |
| [1](./phase-1-ui-and-mvvm.md) | Desktop UI & MVVM | Done: Phone Remote, Clipboard History, Displays & Network, Settings, all on real data |
| [2](./phase-2-native-interop-and-networking.md) | Interop & networking | Done: 14-byte UDP protocol, broadcast discovery on every adapter, PIN pairing, per-sender ordering. Discovery is UDP broadcast, not mDNS |
| [3](./phase-3-android-companion.md) | Android companion | Done: trackpad (tap, 2-finger tap/scroll, hold-to-drag), gyro air mouse, media/number keys, auto-discovery, saved PIN. Not built: three-finger gestures, phone clipboard bridge, typing text |
| [4](./phase-4-virtual-kvm-engine.md) | Desktop-to-desktop KVM | **Windows: done.** macOS: implemented, untested. Linux: no capture or injection backend |
| [5](./phase-5-smart-clipboard-engine.md) | Smart clipboard | Done: SQLite FTS5 history, secret filter, encrypted sync between paired computers, quick-paste overlay (Win+Alt+V, Windows) |
| [6](./phase-6-packaging-and-notarization.md) | Files & packaging | File transfer to a paired computer: done (Send file… on a peer). Build scripts: done. Not done: signed installers, Apple notarization, MSIX/Flatpak, cross-screen drag-and-drop |
| [7](./phase-7-feature-parity-gaps.md#phase-7-app-shell-make-it-a-real-background-app--start-here) | App shell | **Not started.** Tray, close-to-tray, notifications, Devices/Files/Hotkeys/Activity pages, onboarding, themes, add-by-IP |
| [7a](./phase-7a-ui-redesign-lock-and-tray.md) | Simple UI redesign, tray/background, app lock, dark + light | **Not started.** UniClip-style 3-pane window, one Device model, close-to-tray on every OS, un-bypassable app lock, light/dark/system theme across desktop, Android and web, ShareMouse option parity, merged clipboard + mouse features |
| [8](./phase-7-feature-parity-gaps.md#phase-8-clipboard-parity-ditto--winv--paste) | Clipboard parity | **Not started.** Event-driven capture, images/rich text/files, snippets, transforms, per-device rules, encrypted history |
| [9](./phase-7-feature-parity-gaps.md#phase-9-sharemouse-level-mouse-and-keyboard-sharing) | ShareMouse-level KVM | **Not started.** Per-monitor layout, chains, edge-resistance and hotkey settings, layout-aware typing, drag-and-drop |
| [10](./phase-7-feature-parity-gaps.md#phase-10-security-rewrite) | Security rewrite | **Not started.** Key-exchange pairing, authenticated encrypted input, approval prompts |
| [11](./phase-7-feature-parity-gaps.md#phase-11-phone-companion-v2-android) | Phone companion v2 | **Not started.** Typing, clipboard bridge, share sheet, macro pad, presentation tools |
| [12](./phase-7-feature-parity-gaps.md#phase-12-windows-platform-linux-and-release) | Windows service, Linux, release | **Not started.** UAC/lock-screen helper, installers, auto-update, Linux backends |
| [13](./phase-7-feature-parity-gaps.md#phase-13-quality-docs-and-website) | Quality, docs, website | **Not started.** UI tests, CI, soak/chaos tests, user guide, protocol spec |
| [Backlog](./backlog-macos.md) | macOS | **Parked.** Code exists but is unverified; first-run checklist, menu bar, clipboard, packaging and notarization are listed for later (a macOS VM can cover most of it) |

Scope for now: Windows + Android. The full item list (about 170 checkboxes) is in
[phase-7-feature-parity-gaps.md](./phase-7-feature-parity-gaps.md), which also has the gap analysis behind it.

## What each feature does today

**Desktop-to-desktop control (KVM).** Open *Displays & Network* on the computer whose mouse and keyboard you
want to share, type the PIN shown on the other computer's *Phone Remote* page, then choose which side of your
screen it is on. Push the cursor past that edge: input goes to the other computer until the cursor comes back
through the same edge. The other computer's cursor is driven with absolute positions, so the two never drift.

- **Ctrl+Alt+Esc** or **Scroll Lock** returns control immediately. Pressed while local, it locks the cursor to
  this screen (press again to unlock).
- If the other computer stops answering for 2 seconds (sleep, crash, Wi-Fi), control comes back automatically.
- Buttons and keys held while switching are released on the computer that received the press.
- Input hooks only run while at least one paired computer is placed, so the rest of the time Mouser adds no
  input latency.

**Clipboard sync and files.** Paired computers share copied text in both directions (one pairing is enough) and
can send each other files, saved to `Downloads/Mouser`. Traffic is AES-256-GCM encrypted with a key derived
from the PIN of the computer that was paired to.

## How to test the KVM

**On one Windows PC** (no second computer needed):

```powershell
dotnet build src\Mouser.Desktop
powershell -ExecutionPolicy Bypass -File scripts\test-kvm-single-pc.ps1
```

The script starts two Mouser instances with their own settings. A is already paired with B and has B placed
to its right. Wait until A's *Displays & Network* page shows B as **Paired**, then push the cursor off the
right edge of your screen. It reappears at the left edge at the same height. That is A capturing your mouse,
sending it over the network, and B moving the cursor. Click and type to check that buttons and keys go through
B too. Move back off the left edge, or press Ctrl+Alt+Esc, to return. Close both windows when done.

**On two computers:** start Mouser on both, on the same network. Windows asks to allow Mouser through the
firewall: allow it on private networks. Then pair as described above. A Windows VM (Hyper-V, VMware) on a
bridged network also works as the second computer.

Automated coverage: `KvmControllerTests` (edge crossing, forwarding, clamping, return, stuck input, hotkeys,
dead peer), `TwoComputerTests` (two full app instances discovering, pairing, syncing and sending files), and
`Win32InputSinkTests` (real hook install and real cursor movement).

## Known limits

- Pairing uses a 6-digit PIN and trust by IP address. It stops casual misuse on a LAN, but someone who can
  sniff traffic could brute-force the PIN offline. A proper key exchange (ECDH during pairing) is the next
  security step.
- Multi-monitor layouts are treated as one rectangle (their bounding box) for edge detection.
- Windows can't inject input into elevated (admin) windows unless Mouser itself runs elevated.
- The Android app has no clipboard sync and can't type text yet.
- No signed installers: Windows builds a portable `Mouser.exe`, macOS an unsigned `.app`.
