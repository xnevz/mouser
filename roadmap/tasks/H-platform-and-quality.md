# Track H (Windows platform, Linux, release) and Track Q (quality, docs, website)

Read [../AGENT_GUIDE.md](../AGENT_GUIDE.md) first. Feature lists: [../phase-7-feature-parity-gaps.md](../phase-7-feature-parity-gaps.md) Phases 12 and 13.
macOS items are **not** here: see [../backlog-macos.md](../backlog-macos.md).

Already existing (verified): `.github/workflows/ci.yml` runs `dotnet test tests/Mouser.Tests` on `windows-latest` and Android unit tests + `assembleDebug` on Ubuntu; `.github/workflows/deploy-web.yml` deploys `web/`. `scripts/package-windows.sh` publishes a self-contained single-file `Mouser.exe` to `bin/dist/windows`. There is no installer, signing, auto-update or crash reporting.

---

## H1: Windows installer (Inno Setup)

- **Needs:** A10, A11 · **Size:** M · **Files:** create `installer/windows/mouser.iss`, `scripts/build-installer.ps1`, edit `README` (build instructions in `roadmap/README.md`)

**Why Inno Setup:** it is an external free tool (no NuGet/MSIX signing prerequisites), produces one `Mouser-Setup.exe`, and can add firewall rules and cleanup on uninstall.
**Steps**
1. `scripts/build-installer.ps1`: runs `dotnet publish src/Mouser.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin\dist\windows` then `& "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe" installer\windows\mouser.iss` (fail with a clear message if ISCC is missing: "Install Inno Setup 6 from jrsoftware.org").
2. `mouser.iss`: `AppName=Mouser`, `AppVersion` read from a `/DAppVersion=` argument (the script passes the assembly version), `DefaultDirName={autopf}\Mouser`, `PrivilegesRequired=admin`, shortcuts (Start menu, optional desktop), `[Run]` firewall rules:
   `netsh advfirewall firewall add rule name="Mouser" dir=in action=allow program="{app}\Mouser.exe" enable=yes profile=private` and `[UninstallRun]` deleting that rule; option "Start Mouser when I sign in" (writes the same registry value as `StartupRegistration`: `"{app}\Mouser.exe" --minimized` under `HKCU\...\Run`); uninstall removes the Run value but **keeps** `%APPDATA%\Mouser` unless the user ticks a checkbox in the uninstaller.
3. Close a running Mouser on upgrade: `CloseApplications=yes` and `RestartApplications=no`; since Mouser keeps running in the tray, the installer must ask it to quit: use the single-instance named event from A11 or Inno's `CloseApplications` (Restart Manager) and test both.
4. Output `bin\dist\Mouser-Setup-<version>.exe`.

**Done when:** the script produces the installer; install on a clean Windows Sandbox/VM; the app starts, the firewall rule exists (`netsh advfirewall firewall show rule name=Mouser`), uninstall removes it.

---

## H2: Code signing

- **Needs:** H1 · **Size:** S · **Blocked on the user:** a code-signing certificate (OV/EV) or Azure Trusted Signing account. **Do not buy or create anything.**
- Add an optional `-Sign` switch to `build-installer.ps1`: `signtool sign /fd SHA256 /tr <timestamp-url> /td SHA256 /f <pfx> /p <password>` for `Mouser.exe` **and** the installer (`SignTool=` directive in the `.iss`). The pfx path and password come from environment variables `MOUSER_SIGN_PFX` / `MOUSER_SIGN_PASSWORD`; never commit them, never print them. If the variables are missing the script prints "Skipping signing" and continues.

---

## H3: Auto-update

- **Needs:** H1 · **Size:** L · **Package (only after the user agrees):** `Velopack` (NuGet; check the current stable version and record it in the commit message)
- Simplest honest version first (**no package**): on start and every 24 h, `GET https://api.github.com/repos/<owner>/<repo>/releases/latest` (take `REPO_URL` from `web/src/data/`), compare the tag to the assembly version; if newer, show a banner in About ("Version 1.2 is available [Download]") that opens the release page. Settings: "Check for updates" (default on) with a proxy field under Advanced (ShareMouse had one: `HttpClient` with `WebProxy`). Pure `VersionComparer` with tests (`v1.2.0` vs `1.10.0`, pre-release tags).
- Step 2 (only if the user wants silent updates): replace the download link with Velopack's `UpdateManager`.
- **Do not** send telemetry: the only request is the release check, with a generic `User-Agent: Mouser`.

**Done when:** build; tests pass (+4); manual with a fake newer tag via a test hook (`--update-url <file-or-url>` argument for testing only).

---

## H4: Crash and diagnostics logs (local, opt-in sharing)

- **Needs:** B12 · **Size:** S · **Files:** `src/Mouser.UI/Services/AppLog.cs` (extend), `Program.cs`/`App.axaml.cs`
- Persist `AppLog` lines to `%APPDATA%\Mouser\logs\mouser-YYYYMMDD.log`, rotate: keep 7 files, max 2 MB each. Handle `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` and `Dispatcher.UIThread.UnhandledException`: write the exception (type, message, stack; **no clipboard content, no keys**) and show a toast "Mouser hit a problem. Details are in the activity log." Never upload anything automatically. "Open log folder" button in the Activity page.
- Test: rotation keeps 7 files; an exception line never contains the text of the current clipboard (feed a sentinel string through the clipboard and assert it is absent from the log after a forced handler call).

---

## H5: Windows helper service for UAC, lock screen and admin windows (research with the user)

- **Needs:** H1, H2 · **Size:** XL · **Risk:** very high. **Produce a design note `docs/windows-service.md` and stop for approval.**
- Content of the note: today a normal user process cannot inject input into elevated windows, the UAC secure desktop, or the lock screen. The known solution is a Windows service running as `LocalSystem` that starts a helper process in the active session with the **winlogon desktop** (`WTSGetActiveConsoleSessionId`, `DuplicateTokenEx`, `CreateProcessAsUser` with `lpDesktop = "winsta0\\winlogon"`), and a named-pipe link from the helper to the normal app. Describe: installer changes (service registration, needs admin), the pipe ACL (only the interactive user), what the helper does (capture/inject while the secure desktop is active), failure modes (service stopped, session switch), the security impact (SYSTEM process; keep it tiny; validate every pipe message), and a test plan using a VM. Ask the user whether to proceed.

---

## H6: Linux X11 input injection (so phones can control Linux)

- **Needs:** none · **Size:** L · **Files:** create `src/Mouser.Core/Input/X11InputSink.cs`, `src/Mouser.Core/Input/X11KeyMap.cs`; edit `IInputSink.cs` (`InputSinks.CreateForCurrentPlatform`); tests for the key map
- **Cannot be fully tested on Windows.** Put pure logic (VK → X11 keysym table) in `X11KeyMap` with unit tests; the P/Invoke part is verified on a Linux machine or WSL2 with an X server (WSLg).
- P/Invoke `libX11.so.6` (`XOpenDisplay`, `XCloseDisplay`, `XFlush`, `XKeysymToKeycode`, `XWarpPointer`, `XDefaultRootWindow`) and `libXtst.so.6` (`XTestFakeMotionEvent`, `XTestFakeButtonEvent`, `XTestFakeKeyEvent`). Buttons: left = 1, middle = 2, right = 3, wheel up/down = 4/5, wheel left/right = 6/7 (one press+release per 120 units).
- `IsSupported = display != null`; `MoveAbsolute` uses `XTestFakeMotionEvent(display, -1, x, y, 0)`; `MoveRelative` uses `XWarpPointer`-free approach: query pointer (`XQueryPointer`) then absolute.
- Key map: VK letters/digits → ASCII keysyms, F1–F24 → `0xFFBE…`, arrows, Enter, Tab, Esc, Backspace, modifiers, media keys (`XF86AudioPlay` etc.). Tests: 40 representative mappings.
- Wayland sessions (`XDG_SESSION_TYPE=wayland`) → `IsSupported = false` with the reason surfaced in the Phone page banner ("Wayland blocks synthetic input; see docs").
- Update the `InputSinks` shortcut comment and the Phone banner text in `AndroidRemoteView`.

**Done when:** build on Windows; key-map tests pass; a note records the Linux manual result (or "not tested" explicitly).

## H7: Linux capture, tray and packaging (after H6)

- XInput2 raw-event capture for KVM (`Mouser.Core/Input/X11InputCapture.cs`: `XISelectEvents` on the root window with `XI_RawMotion`, `XI_RawKeyPress`...; swallowing events needs a pointer/keyboard **grab**: `XGrabPointer`/`XGrabKeyboard` while controlling a peer). Clipboard watcher via `XFixesSelectSelectionInput` (spike) or polling. Tray via Avalonia `TrayIcon` (A10 note about GNOME). XDG autostart `.desktop` file for `StartupRegistration`. `scripts/package-linux.sh` exists: add AppImage and a Flatpak manifest. **Wayland:** research `libei` / RemoteDesktop portal in a note first.

## H8: `uiAccess` build (works in admin windows without running as administrator) — spike

- **Needs:** H1, H2 · **Size:** M · **Risk:** high, needs a signing certificate. **Do not start without the user's certificate.**
- Windows lets a process with `<requestedExecutionLevel level="asInvoker" uiAccess="true"/>` in its manifest send input to higher-integrity windows (and hook them) **without** being elevated. Conditions: the exe must be **Authenticode-signed with a trusted certificate** and must run from a **secure location** (`C:\Program Files\...`, which the installer H1 uses). If either condition fails Windows refuses to start the app ("A referral was returned from the server").
- Steps: (1) create a second manifest `app.uiaccess.manifest` (copy `src/Mouser.Desktop/app.manifest` and add the `<trustInfo><security><requestedPrivileges><requestedExecutionLevel level="asInvoker" uiAccess="true"/>...` block); (2) an MSBuild property `-p:UiAccess=true` swaps `ApplicationManifest` for it; (3) the **installer build only** passes it and signs the exe (H2); the portable build and `dotnet run` keep the normal manifest; (4) `ElevationInfo`/E16 learns a third state: "uiAccess" (check `GetTokenInformation(TokenUIAccess = 26)`), in which case the elevated-window warning is **not** shown and E18's toggle explains "Not needed: this build can already type into administrator windows".
- Still cannot type on the UAC secure desktop or lock screen (H5).
- Test plan: signed installer in a VM; type into an elevated Notepad from the phone; confirm the unsigned portable build still starts.

---

## Q1: Headless UI tests

- **Needs:** B9 · **Size:** M · **Package (approved name):** `Avalonia.Headless.XUnit` (same version as Avalonia, `12.1.6`) in `tests/Mouser.Tests/Mouser.Tests.csproj`
- Add `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]` with `TestAppBuilder : AppBuilder.Configure<Mouser.UI.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions())`.
- Tests with `[AvaloniaFact]`: create `MainWindow` with an `AppFixture` view model; for each nav string (`SmartClipboard`, `DisplayMatrix`, `Settings`, `Activity`) navigate, `Dispatcher.UIThread.RunJobs()`, assert the content control is **not** the "Not Found:" text block (catches missing view/VM name pairs); for both `ThemeVariant.Light` and `Dark` render a frame (`window.CaptureRenderedFrame()`) and assert it is not blank (check some pixels differ) and save PNGs to `bin/ui-snapshots/` for manual review.
- Keep these tests fast (< 10 s total) and independent of the network (`AppFixture` already uses port 0).

## Q2: Extend CI

- **Needs:** Q1 · **Size:** S · **Files:** `.github/workflows/ci.yml`
- Add a `web` job (`actions/setup-node@v4`, `working-directory: web`, `npm ci`, `npm run lint`, `npm run build`), and make the desktop job also `dotnet build Mouser.sln -c Release` first. Upload `bin/ui-snapshots` as an artifact (`actions/upload-artifact@v4`). Do not add secrets. Keep jobs independent so one failure does not hide another.

## Q3: Two-instance integration job

- **Needs:** B10 · **Size:** M · **Files:** `tests/Mouser.Tests/TwoComputerTests.cs` (extend)
- `TwoComputerTests` already runs two app instances (`AppFixture`) that discover, pair, sync and send files. Extend it with: add-by-IP across two different discovery ports (B10), a KVM crossing between the two fixtures with a `FakeCapture` (see `KvmControllerTests` for the fake), clipboard image sync (after D12), lock-all (C7). Each new scenario is a separate `[Fact]` with a 10 s timeout (`Wait.UntilAsync`).

## Q4: Performance budget and benchmarks

- **Size:** M · **Files:** `tests/Mouser.Tests/PerfTests.cs` (traits `Category=Perf`; excluded by default via `dotnet test --filter "Category!=Perf"` documented in the guide)
- Measurements with `Stopwatch`, asserting generous limits so they only catch regressions: input path (`KvmController.Handle` for 10,000 move events ≤ 50 ms total), clipboard capture to DB (100 clips ≤ 2 s), FTS query over 10,000 clips ≤ 50 ms, `MoveCoalescer` (E6) ≤ 1 µs/post, `SearchQuery.Parse` 10,000 parses ≤ 200 ms, memory: after 1,000 captured clips + refreshes `GC.GetTotalMemory(true)` growth ≤ 20 MB.
- Idle budget (manual, written in the doc): app hidden in the tray should use < 1 % CPU and < 150 MB.

## Q5: Soak and chaos tests (manual scripts + a few automated)

- **Size:** M · **Files:** `scripts/soak-two-instances.ps1`, `tests/Mouser.Tests/ChaosTests.cs`
- Script: starts two instances, uses `SendInput` from PowerShell (`Add-Type` with the `Win32` API) to move/click/type across the edge in a loop for N hours, and checks afterwards: no stuck keys (`GetAsyncKeyState` for Ctrl/Shift/Alt/mouse buttons all up), memory of both processes did not grow > 2×, logs show no unhandled exceptions.
- Automated chaos tests (no OS input): wrap the UDP receiver with a `LossyChannel` (drop 20 %, duplicate 10 %, reorder windows) and assert sequence filtering and the secure replay window behave; kill a peer mid-file-transfer and assert the receiver deletes the partial file and the sender gets an `IOException`; change a peer's IP mid-session (`EnsureLink`) and assert control returns and then resumes.

## Q6: Documentation

- **Size:** M · **Files:** `docs/user-guide.md`, `docs/faq.md`, `docs/protocol.md` (after F1/F4, describes what is built), `docs/architecture.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, `.github/ISSUE_TEMPLATE/bug_report.md`
- User guide: install, first run, pair a computer, pair a phone, share the mouse, clipboard history/sync, snippets, lock, tray, hotkeys, troubleshooting (**firewall**, guest Wi-Fi isolation, VPN, antivirus, elevated windows). Every screenshot must be taken after the UI redesign (so write the text now and add images last).
- Architecture doc: one diagram (ASCII or Mermaid) of the projects and threads (hook thread, UI thread, network threads) and the rule "Core never references UI".
- Contribution guide: how to run tests, code style, how to add a platform backend (`IInputSink`, `IInputCapture`, `IClipboardAccess`).
- `CHANGELOG.md` in Keep-a-Changelog format starting from the current state.

## Q7: Website and old docs reconciliation

- **Size:** M · **Files:** `web/src/components/FeatureGrid.tsx`, `Hero.tsx`, `ArchitecturePresentation.tsx`, `web/src/data/releases.ts`, `roadmap/phase-*.md`, `SYSTEM_DESIGN_AND_ROADMAP.md`, `DOTNET_MVVM_ROADMAP.md`
- Read `web/AGENTS.md` first. For each claim on the site (features, platforms, security wording like "end-to-end encrypted", "mDNS", "ChaCha20-Poly1305"), compare with [../../ROADMAP.md](../../ROADMAP.md) "Done" list; change the text to match reality, label unbuilt items "Coming soon". Update `releases.ts` when an installer exists (H1). Light/dark variables follow A14.
- Old phase documents: add a first line to each `roadmap/phase-N-*.md`: `> Status: see ../ROADMAP.md. This file is the original design; checkboxes may be out of date.` and uncheck anything that is not actually built (examples already known: event-driven clipboard hooks, "Android Accessibility Service", mDNS discovery, ChaCha20-Poly1305, encrypted history). `npm run build` and `npm run lint` must pass.
