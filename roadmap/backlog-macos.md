# Backlog: macOS

**Status: parked.** Windows and Android come first (see [phase-7-feature-parity-gaps.md](./phase-7-feature-parity-gaps.md)).
This file keeps every macOS-specific item so it can be picked up later without re-doing the analysis.

**Starting point.** The code exists but has never run on a Mac: `MacInputSink`, `MacInputCapture`,
`platform_mac.mm` (event tap, injection, warp) and the Cmd↔Ctrl mapping in `KeyTranslationService`. Treat all of
it as *unverified* until section 1 passes.

## What the macOS virtual machine can and cannot test

You have a macOS VM, which covers most of this list. Use the same pattern as the Windows two-instance test: the VM is the second
computer, on a **bridged** network adapter so UDP broadcast discovery reaches it.

| Works in a VM | Limited or not reliable in a VM |
| :--- | :--- |
| Building `libmouser_native.dylib` and the .NET app | Real mouse hardware feel, 1000 Hz mice, trackpad gestures |
| Accessibility / Input Monitoring prompts and grants | Multi-monitor and Retina scaling (VM display is usually one virtual screen) |
| Event-tap capture and `CGEvent` injection, key mapping, edge hop with the Windows PC | GPU-dependent UI smoothness |
| Clipboard (`NSPasteboard`) monitoring and sync | Sleep/wake and lock-screen behaviour (differs from real hardware) |
| LaunchAgent start-at-login, menu-bar item | Notarization needs an Apple Developer account (not the VM itself) |
| Packaging up to a signed `.app` | Final signing on a different machine than the one users have |

Before relying on results, check the VM host's licence terms for running macOS.

## 1. First run (verify what is already written)
- [ ] Build `libmouser_native.dylib` with CMake and load it through `NativeDriver`; run `NativeDriverTests`
- [ ] Launch the Avalonia app on macOS and confirm all four pages render
- [ ] Grant Accessibility and Input Monitoring; verify the "permission missing" message when they are not granted
- [ ] Phone → Mac: trackpad, scroll, keys and media keys through `MacInputSink`
- [ ] Windows ↔ Mac edge hop in both directions, buttons, wheel, keys, return hotkey, dead-peer recovery
- [ ] Mac cursor absolute positioning on Retina displays (points vs pixels, `Scaling` from Avalonia)
- [ ] Keyboard: verify the Cmd↔Ctrl, Option↔Alt mapping on real shortcuts (copy, paste, select all, tab switching)
- [ ] Mac hotkey for "return control": Ctrl+Alt+Esc does not exist on a Mac keyboard; define and test an equivalent
- [ ] Clipboard history and sync with a Windows peer; file send both ways
- [ ] Fix whatever breaks, and add the Mac results to the roadmap README ("macOS: verified" only after this)

## 2. Platform features
- [ ] Menu-bar (status item) icon with the same menu as the Windows tray, and hide the Dock icon when running in the background
- [ ] Start at login through a LaunchAgent (replace the Windows-only `StartupRegistration`)
- [ ] Native notifications (`UNUserNotificationCenter`)
- [ ] Event-driven clipboard capture: poll `NSPasteboard.changeCount` every ~200 ms, not a 1 s UI timer
- [ ] Honour `org.nspasteboard.ConcealedType` / `TransientType` so password managers are skipped
- [ ] Image, RTF, HTML and file-URL clips from `NSPasteboard`; paste them back with the right UTIs
- [ ] Source app name and bundle id for each clip (`NSWorkspace.frontmostApplication`)
- [ ] Quick-paste overlay: global hotkey (e.g. Cmd+Shift+V) with a non-activating panel, then paste into the previous app
- [ ] Permission onboarding screen that deep-links to System Settings → Privacy & Security
- [ ] Detect when permissions were revoked or reset after an update and show a fix-it banner
- [ ] Secure Input detection (password fields disable event taps) and a visible "input blocked" status
- [ ] Media keys (`NX_SYSDEFINED`) capture and injection for volume/play/pause
- [ ] Mission Control / Spaces and full-screen app behaviour while input is redirected
- [ ] Synchronised lock (Ctrl+Cmd+Q) and display sleep across computers
- [ ] Caps Lock quirks (macOS caps lock delay) when forwarding to a Mac
- [ ] Natural-scroll direction setting per peer; pixel vs line scrolling differences
- [ ] Mouse acceleration curve differences and a speed-matching option
- [ ] Menu-bar and Dock hot-corner behaviour at the screen edge when hopping
- [ ] Multi-display layout from `NSScreen` with correct backing scale per display
- [ ] Apple Silicon (arm64) and Intel (x64) builds; universal binary for the native library

## 3. Packaging and release
- [ ] Hardened-runtime entitlements for event taps and the network
- [ ] Developer ID signing with a stable identity so Accessibility grants survive updates
- [ ] Notarization with `notarytool`, stapling, and a DMG with a drag-to-Applications layout
- [ ] Sparkle (or equivalent) auto-update
- [ ] Homebrew cask
- [ ] Gatekeeper first-run test on a clean machine/VM snapshot
- [ ] CI job on a macOS runner that builds the native library, runs unit tests and produces the artifact
- [ ] Update `scripts/` with a Mac equivalent of `test-kvm-single-pc.ps1`

## 4. Phone and Mac
- [ ] Verify the Android app drives a Mac host end to end (gyro, trackpad, media, numpad)
- [ ] Mac-specific macro pad actions (Spotlight, Mission Control, screenshot, Cmd+Tab)
- [ ] Android clipboard bridge with a Mac

## 5. Documentation
- [ ] macOS setup guide (permissions, firewall prompt, bridged VM networking for testing)
- [ ] Update `phase-0-macos-dev-setup.md` once the VM workflow is confirmed
- [ ] List known Mac limits in the README (Secure Input, login window, permission resets)
