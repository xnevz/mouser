# Mouser

Share one mouse and keyboard across computers, control a computer from your Android phone, and keep a searchable clipboard history that syncs between your computers. Everything stays on your local network: no accounts, no cloud.

[![Support Mouser on PayPal](https://img.shields.io/badge/Donate-PayPal-00457C?logo=paypal&logoColor=white)](https://www.paypal.com/ncp/payment/UDVRJ98JJ2MBJ)

If Mouser is useful to you, you can [support its development with PayPal](https://www.paypal.com/ncp/payment/UDVRJ98JJ2MBJ).

## Contents

**Using Mouser**

1. [Features](#features)
2. [Platform support](#platform-support)
3. [Install](#install)
4. [Getting started](#getting-started)
5. [Using the app](#using-the-app)
6. [Settings and data](#settings-and-data)
7. [Privacy and security](#privacy-and-security)
8. [Troubleshooting](#troubleshooting)
9. [Known limitations](#known-limitations)

**Developing Mouser**

10. [Repository layout](#repository-layout)
11. [Build from source](#build-from-source)
12. [Run and test](#run-and-test)
13. [How it works](#how-it-works)
14. [Publish a release](#publish-a-release)
15. [Website](#website)
16. [Roadmap and project docs](#roadmap-and-project-docs)
17. [Support the project](#support-the-project)

---

## Features

- **Mouse and keyboard sharing.** Push the cursor past a screen edge and your mouse and keyboard control the other computer. Move back through the same edge to return.
- **Phone remote (Android).** Use your phone as a trackpad, a gyroscope air mouse, and a media and number-pad remote.
- **Clipboard history.** Text you copy is saved to a searchable history with starring, and anything that looks like a password, API key, private key or card number is skipped. Quick paste with **Win+Alt+V** (Windows).
- **Clipboard sync and file sending.** Copied text syncs between paired computers, and you can send files to a paired computer. Files land in `Downloads/Mouser`.
- **Local only.** Discovery, pairing and transfers happen on your network.

## Platform support

| Platform | Status | What works |
| --- | --- | --- |
| Windows 10/11 (x64) | **Main target** | Everything above. |
| Android 8.0+ | Companion app | Trackpad, air mouse, media keys, arrows and number pad, auto-discovery, saved PIN. No typing from the phone keyboard and no clipboard bridge yet. |
| macOS 12+ | Builds, **not tested on a Mac** | Phone remote, clipboard history and sync, mouse and keyboard sharing (implemented, unverified). No phone media keys, no quick-paste hotkey. |
| Linux (x64) | Partial | Clipboard history and sync, receiving files. **No input injection or capture**, so no phone remote or sharing yet. |

## Install

Download the file for your system from the [latest release](https://github.com/xnevz/mouser/releases/latest).

| Platform | File | Setup |
| --- | --- | --- |
| Windows 10/11 | `Mouser-windows-x64.exe` | Run it and allow it through Windows Defender Firewall for **private** networks. No installer; it is a single portable file. |
| Linux (x86_64) | `Mouser-linux-x64.tar.gz` | `tar xzf Mouser-linux-x64.tar.gz && ./Mouser` |
| macOS | `Mouser-macos-*.tar.gz` | Unpack, open `Mouser.app`, allow it under System Settings → Privacy & Security → Accessibility, then restart it. |
| Android 8.0+ | `Mouser-android.apk` | Allow installs from unknown sources, install, open the app. |

The builds are not code-signed, so Windows SmartScreen and macOS Gatekeeper will warn on first run. Verify downloads against `SHA256SUMS.txt` in the release:

```bash
sha256sum -c SHA256SUMS.txt --ignore-missing      # Linux / Git Bash
certutil -hashfile Mouser-windows-x64.exe SHA256  # Windows, compare by eye
```

There is no auto-update yet; install a newer release over the old one.

## Getting started

Every computer and phone must be on the **same network** (same Wi-Fi or LAN, no client isolation).

1. **Install and start Mouser** on each computer. Allow it through the firewall on private networks.
2. **Find the PIN.** Open the **Phone Remote** page; it shows a 6-digit pairing PIN for this computer.
3. **Connect a phone.** Open the Android app, pick your computer from the list and type the PIN. The app remembers it.
4. **Connect another computer.** On the computer whose keyboard and mouse you want to share, open **Displays & Network**, enter the other computer's PIN, and choose which side of your screen it sits on. It shows as **Paired** when connected.

## Using the app

### Share mouse and keyboard between computers

Push the cursor past the edge you placed the other computer on. Input now goes to that computer until the cursor comes back through the same edge. The other cursor is driven with absolute positions, so the two never drift apart.

| Action | How |
| --- | --- |
| Take control back immediately | **Ctrl+Alt+Esc** or **Scroll Lock** |
| Lock the cursor to this screen | Press the same hotkey while control is local; press again to unlock |
| If the other computer goes quiet | After 2 seconds (sleep, crash, Wi-Fi drop) control comes back on its own |

Buttons and keys held while switching are released on the computer that received the press. Input hooks only run while at least one paired computer is placed, so Mouser adds no input latency the rest of the time.

Windows cannot inject input into **elevated (administrator) windows** such as Task Manager or an admin terminal unless Mouser itself runs as administrator. It also cannot control the lock screen or UAC prompts.

### Phone remote

| Gesture or control | Result |
| --- | --- |
| Drag one finger | Move the pointer |
| Tap | Left click |
| Two-finger tap | Right click |
| Two-finger drag | Scroll |
| Hold, then drag | Click and drag |
| Air mouse | Move the phone like a pointer (uses the gyroscope) |
| Media, arrow and number keys | Play/pause, volume, arrows, digits |

If your phone cannot find the computer, check that both are on the same Wi-Fi and that the firewall allows Mouser.

### Clipboard history, sync and files

- Open **Clipboard History** to search, star, copy back or delete clips. Starred clips are never auto-deleted.
- **Win+Alt+V** opens a search-and-paste popup over whatever app is in front (Windows). If another app already owns the hotkey, the history page says so.
- Text copied on one paired computer shows up on the others (pairing one way is enough). You can turn this off in Settings.
- To send a file, use **Send file…** on a paired computer. Received files are saved to `Downloads/Mouser`.

## Settings and data

Open **Settings** in the app:

| Setting | What it does |
| --- | --- |
| Phone connection port | UDP port phones send input to (default 58491). Phones find a new port automatically. |
| Save clipboard history | Turns history on or off. |
| Skip secrets | Don't save things that look like passwords, API keys, private keys or card numbers. |
| Share copied text | Sync copied text with paired computers. |
| Delete unstarred clips after N days | Automatic cleanup. |
| Start Mouser when I sign in | Launch at login. |

Data is stored only on your computer. On Windows the default folder is `%APPDATA%\Mouser`, holding `settings.json` and `clipboard.db`; **Settings → Data** shows the exact path. To run a second, separate instance (useful for testing), start it with `--data-dir <folder>`.

Network ports in use:

| Port | Protocol | Purpose |
| --- | --- | --- |
| 58490 | UDP broadcast | Finding other Mouser computers and phones |
| 58491 | UDP | Input from phones and paired computers (configurable) |
| dynamic | TCP | File transfer and clipboard sync between computers |

## Privacy and security

- No accounts, no telemetry, no cloud. Input and clipboard traffic never leaves your network.
- Clipboard history stays on the computer. Clipboard sync and file transfers between computers are encrypted with AES-256-GCM using a key derived from the PIN of the computer you paired to.
- Pairing uses a 6-digit PIN, and a computer is then trusted by its IP address. Five wrong tries lock an address out for 60 seconds.
- **Phone input is not encrypted**, and the PIN is sent in clear text on the network. This stops casual misuse on a home LAN, but someone who can sniff your traffic could recover the PIN. Use Mouser on networks you trust. A proper key exchange during pairing is the next security step.

## Troubleshooting

| Problem | Try this |
| --- | --- |
| Phone or other computer is not found | Same network? Allow Mouser through the firewall on **private** networks (Windows asks on first start). Guest Wi-Fi and some routers isolate devices from each other. |
| Windows says the app is unrecognised | SmartScreen warns about unsigned apps. Choose *More info → Run anyway* if you trust the download and checksum. |
| Input does nothing over some windows | Those windows run as administrator. Run Mouser as administrator too. |
| Cursor stays on the other computer | Press **Ctrl+Alt+Esc** or **Scroll Lock**. |
| Win+Alt+V does nothing | Another app owns the hotkey; the Clipboard History page tells you. |
| Pairing keeps failing | Make sure you typed the PIN of the computer you are connecting **to**. After 5 wrong tries wait 60 seconds. |
| macOS shows no input | Grant Accessibility permission to Mouser in System Settings, then restart it. |
| Port already in use | Change the phone connection port in Settings. |

## Known limitations

- Multi-monitor layouts are treated as one rectangle (their bounding box) for edge detection.
- No dragging files across the screen edge; use **Send file…**.
- Android: no typing from the phone keyboard, no clipboard bridge, no three-finger gestures.
- Linux has no input backend; macOS is untested on real hardware.
- No signed installers, notarization or auto-update.
- Discovery uses UDP broadcast, not mDNS, so it only works within one subnet.

---

## Repository layout

| Path | What it is |
| --- | --- |
| `src/Mouser.Core/` | Platform logic: networking, pairing, KVM state machine, input capture and injection, clipboard, file transfer. |
| `src/Mouser.UI/` | Avalonia views and view models (Phone Remote, Clipboard History, Displays & Network, Settings), tray and hotkey services. |
| `src/Mouser.Desktop/` | The executable that starts the UI (`Mouser.exe`). |
| `src/Mouser.Native/` | C++/CMake native library for macOS input capture and injection. |
| `tests/Mouser.Tests/` | xUnit tests, including two full app instances talking to each other. |
| `android/` | Kotlin + Jetpack Compose companion app. |
| `web/` | Next.js download site. |
| `scripts/` | Packaging, release and test scripts. |
| `roadmap/`, `ROADMAP.md` | Status, task cards and design notes. |

Stack: .NET 8, Avalonia 12 with CommunityToolkit.Mvvm, SQLite (FTS5) for history, Kotlin and Compose on Android (min SDK 26), Next.js for the website.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Run the scripts from a bash shell (Git Bash on Windows).

```bash
bash scripts/package-windows.sh                 # bin/dist/windows/Mouser.exe
bash scripts/package-linux.sh                   # bin/dist/linux/Mouser
bash scripts/package-macos-app.sh osx-arm64     # bin/dist/macos/Mouser.app (on a Mac, needs Xcode tools and CMake)
cd android && ./gradlew assembleDebug           # needs the Android SDK and JDK 17
```

The Android debug APK ends up in `android/app/build/outputs/apk/debug/app-debug.apk`.

## Run and test

```bash
dotnet run --project src/Mouser.Desktop     # run the desktop app
dotnet test tests/Mouser.Tests              # desktop tests (also run in CI on Windows)
cd android && ./gradlew :app:testDebugUnitTest   # Android unit tests (also run in CI)
```

`scripts/run-android-debug.sh` and `scripts/run-android-release.sh` build the Android app and install it on a connected device or emulator.

**Testing mouse sharing on one PC.** Build, then run the script, which starts two Mouser instances with separate data and ports:

```powershell
dotnet build src\Mouser.Desktop
powershell -ExecutionPolicy Bypass -File scripts\test-kvm-single-pc.ps1
```

Wait until the first window shows the other as **Paired**, then push the cursor off the right edge of your screen. It reappears at the left edge, now driven through the network by the second instance. Click and type to check buttons and keys, then go back through the left edge or press Ctrl+Alt+Esc. On two real computers, just start Mouser on both and pair them.

## How it works

- **Discovery.** Each instance broadcasts a beacon on UDP 58490 on every network adapter and listens for others.
- **Input protocol.** Moves, buttons, scrolls, keys and pings are 14-byte UDP packets with a per-sender sequence number so stale or duplicate packets are dropped (`InputMotionPacket`, `SequenceTracker`).
- **Pairing.** A `Pair` packet carries the PIN. `PairingGate` checks it and locks out an address after repeated failures.
- **Mouse sharing.** `VirtualKvmManager` is a pure state machine (edges, placement, return edge). `KvmController` drives it from the OS input hooks, sends absolute positions to the active peer, releases held keys and buttons on switch and enforces the 2-second watchdog. Windows uses low-level hooks and `SendInput`; macOS goes through `libmouser_native`.
- **Clipboard.** `ClipboardStorageService` keeps history in SQLite with FTS5 search, `ClipboardPrivacyFilter` drops secrets, `ClipboardCryptoEngine` encrypts synced clips with AES-256-GCM.
- **Files.** `FileStreamingService` and `PeerTransferService` stream chunks over TCP to a paired computer.

Deeper design notes are in [SYSTEM_DESIGN_AND_ROADMAP.md](SYSTEM_DESIGN_AND_ROADMAP.md) (historical) and the phase documents under [roadmap/](roadmap/).

## Publish a release

Releases are built on your own machine and uploaded with the [GitHub CLI](https://cli.github.com) (`gh auth login` once).

```bash
git push
bash scripts/release.sh v0.1.0          # add --draft to review on GitHub before publishing
```

The script requires a clean, pushed tree. It builds Windows and Linux (plus macOS when run on a Mac) and the Android APK, writes `SHA256SUMS.txt`, then creates the tag and release with auto-generated notes. The Android file is a debug build, because release builds are unsigned and Android will not install unsigned APKs.

## Website

The download site lives in `web/` (Next.js, Tailwind) and deploys to GitHub Pages from `main` through `.github/workflows/deploy-web.yml`.

```bash
cd web
npm install
npm run dev      # http://localhost:3000
npm run build
```

Platform copy and download links are in `web/src/data/`. Keep it in sync with what the app really does.

## Roadmap and project docs

- [ROADMAP.md](ROADMAP.md): live checklist of every task and its status.
- [roadmap/README.md](roadmap/README.md): what works today, per feature and phase.
- [roadmap/AGENT_GUIDE.md](roadmap/AGENT_GUIDE.md): rules for contributors and AI agents working on the tasks in [roadmap/tasks/](roadmap/tasks/).
- [web/AGENTS.md](web/AGENTS.md): conventions for the website.

Planned next: tray and close-to-tray, light/dark theme, app lock, a proper key-exchange pairing, Android typing and clipboard bridge, Linux backends, signed installers.

## Support the project

Mouser is free. If it saves you time, you can [donate with PayPal](https://www.paypal.com/ncp/payment/UDVRJ98JJ2MBJ).
