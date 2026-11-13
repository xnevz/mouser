# Track C: App lock

Read [../AGENT_GUIDE.md](../AGENT_GUIDE.md) first. Design and the "honest limits": [../phase-7a-ui-redesign-lock-and-tray.md](../phase-7a-ui-redesign-lock-and-tray.md) §7.
**Needs:** A10 (tray) and B11 (settings page). Order: C1 → C2 → C3 → C4 → C5; C6–C8 are independent afterwards.

**What "lock" means here:** with a passphrase set, Mouser can be *locked*. While locked: the window shows only a
lock screen, Quit and quick-paste are blocked until the passphrase is entered, clipboard capture and sharing are
**off**, and the network services are stopped. There is **no backdoor**: a forgotten passphrase is recoverable only
with the recovery key shown once at setup.

Cryptography used (all in the .NET base library; **no new packages**):
- passphrase → key: `Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, 600_000, SHA256, 32)`
  (shortcut: PBKDF2 instead of Argon2id because it needs no package; upgrade if a vetted Argon2 library is added.)
- wrapping: reuse `ClipboardCryptoEngine.Encrypt/Decrypt(plain, key)` (AES-256-GCM, already tested).

---

## C1: `AppLockService` in Core (no UI) with tests

- **Needs:** none · **Size:** M · **Files:** create `src/Mouser.Core/Security/AppLockService.cs`, `tests/Mouser.Tests/AppLockTests.cs`

**API to build (exact)**
```csharp
namespace Mouser.Core.Security;

public sealed class AppLockService
{
    public AppLockService(string filePath, Func<DateTime>? clock = null);

    public bool IsConfigured { get; }          // a passphrase has been set up
    public bool IsLocked { get; }              // starts true when IsConfigured and the app just started
    public byte[]? MasterKey { get; }          // 32 bytes while unlocked, null while locked

    public event Action? Locked;
    public event Action? Unlocked;

    /// <summary>First-time setup. Returns the recovery key (show once, never stored).</summary>
    public string Setup(string passphrase);

    public bool TryUnlock(string passphrase);              // false on wrong passphrase or while backing off
    public bool TryUnlockWithRecoveryKey(string recoveryKey);
    public TimeSpan BackoffRemaining { get; }              // > 0 while further attempts are refused
    public void Lock();                                    // wipes MasterKey from memory, raises Locked
    public bool ChangePassphrase(string oldPassphrase, string newPassphrase);
    public void Remove(string passphrase);                 // turn the lock off (needs the passphrase)
}
```
**File format** (`lock.json`, written atomically with temp file + move like `SettingsStore.Save`): `{ "Version":1, "Salt":"<b64 16 bytes>", "Iterations":600000, "WrappedByPassphrase":"<b64>", "RecoverySalt":"<b64>", "WrappedByRecovery":"<b64>", "Failures":0, "NextAttemptUtc":"…" }`.

**Rules**
1. `Setup`: random 32-byte master key (`ClipboardCryptoEngine.GenerateKey()`); KEK = PBKDF2(passphrase, salt); `WrappedByPassphrase = Encrypt(master, KEK)`. Recovery key = 20 random bytes shown as 8 groups of 5 chars from `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` joined by `-` (say 32 chars + 7 dashes); `WrappedByRecovery = Encrypt(master, PBKDF2(recoveryKey, recoverySalt))`. Reject passphrases shorter than 8 characters with `ArgumentException`.
2. `TryUnlock`: if `BackoffRemaining > 0` return false **without** trying. Decrypt failure (GCM tag mismatch → exception, catch `CryptographicException`) = wrong passphrase.
3. Backoff after consecutive failures (persist `Failures`/`NextAttemptUtc` so restart does not reset): 1–3 failures: none; 4th: 5 s; 5th: 15 s; 6th: 60 s; 7th and later: 5 min. A success resets to 0.
4. `Lock()` zeroes the master key array (`CryptographicOperations.ZeroMemory`) and sets `MasterKey = null`.
5. Constructing the service with an existing `lock.json` → `IsLocked == true`. With no file → `IsConfigured == false`, `IsLocked == false`.
6. Corrupt `lock.json`: keep it as `lock.json.corrupt`, treat as not configured **and** log nothing secret. (Do not silently unlock a previously locked app: if the file exists but is unreadable, throw `InvalidDataException` and let the UI show "Lock file damaged; use the recovery key or delete lock.json to reset (history stays unreadable)".)

**Tests (`AppLockTests`, use a temp folder and an injected clock)**
- setup → restart (new instance) is locked → right passphrase unlocks, `MasterKey` length 32 and **same bytes** as before.
- wrong passphrase returns false; 4th wrong attempt sets `BackoffRemaining > 0`; correct passphrase during backoff still returns false; after advancing the clock past the backoff it works.
- recovery key unlocks; `ChangePassphrase` then old passphrase fails, new works, master key unchanged.
- `Lock()` nulls the key and raises the event once.
- passphrase shorter than 8 chars throws.

**Done when:** build; tests pass (≈ +6).

---

## C2: Host respects the lock

- **Needs:** C1 · **Size:** M · **Files:** `src/Mouser.UI/Services/MouserHost.cs`, `src/Mouser.UI/ViewModels/ClipboardHistoryViewModel.cs`, `tests/Mouser.Tests/ViewModelTests.cs`

**Behaviour:** `MouserHost.SetLocked(bool locked)`.
- `locked == true`: `StopServices()` (this stops phone input, discovery and transfers) **and** `Kvm?.Stop()` (releases hooks and returns control). Remember `_locked = true`.
- `locked == false`: `_locked = false; Restart(); SyncPeers();`.
- While `_locked`: `Restart()` must return early (so a settings change cannot restart services behind the lock).
- `ClipboardHistoryViewModel`: add `public bool IsLocked { get; set; }`. In `PollClipboardAsync` return `false` immediately when `IsLocked`. In `ApplyRemoteClipAsync` return immediately when `IsLocked`.

**Tests**
- Fixture host running: `SetLocked(true)` → `Host.Input == null`, `Host.Discovery == null`; `SetLocked(false)` → `Host.Input != null`.
- With `FakeClipboard.Text = "secret"` and `ClipboardHistory.IsLocked = true`: `PollClipboardAsync()` returns false and nothing is stored.

**Done when:** build; tests pass (+2).

---

## C3: Lock screen and setup UI

- **Needs:** C2, B11 · **Size:** L · **Files:** create `src/Mouser.UI/ViewModels/LockViewModel.cs`, `src/Mouser.UI/Views/LockView.axaml` (+ `.cs`); edit `MainViewModel.cs`, `MainWindow.axaml`, `SettingsViewModel.cs`, `SettingsView.axaml`, `App.axaml.cs`

**Steps**
1. `App.axaml.cs`: create `var appLock = new AppLockService(Path.Combine(settings.DataDirectory, "lock.json"));` and pass it to `MainViewModel` (extra constructor parameter `AppLockService? appLock = null`; tests that call `new MainViewModel(host, settings, storage)` keep working because it is optional).
2. `MainViewModel`: `public bool IsLocked => _lock?.IsLocked ?? false;` `public LockViewModel? Lock { get; }`; subscribe to `Locked`/`Unlocked`: `OnPropertyChanged(nameof(IsLocked))`, `_host.SetLocked(...)`, `ClipboardHistory.IsLocked = …`. If the app **starts** locked, call `_host.SetLocked(true)` **before** `_host.Restart()` in `App.axaml.cs` (so no sockets open at all).
3. `LockViewModel`: `[ObservableProperty] string _passphrase`, `string? _error`, `bool _isBackingOff`; commands `UnlockCommand` (calls `TryUnlock`; on false sets `Error` = "That passphrase is wrong." or "Too many tries. Wait N seconds."), `UseRecoveryKeyCommand` (a second TextBox, shown after pressing "Forgot?"). Clear `Passphrase` after every attempt.
4. `LockView.axaml` (full-window overlay, `Background="{DynamicResource BgApp}"`): centered column: large 🔒, title "Mouser is locked", `TextBox PasswordChar="•"` bound to `Passphrase` with `Watermark`, **Unlock** (`Classes="primary-btn"`, `IsDefault="True"` so Enter works), error text (`Danger`), a small "Forgot passphrase?" link that reveals the recovery-key box and a hint **"Without the passphrase or recovery key, saved history cannot be opened."**
5. `MainWindow.axaml`: overlay `<ContentControl Grid.ColumnSpan="2" Content="{Binding Lock}" IsVisible="{Binding IsLocked}" />` **last** in the root `Grid` so it covers everything. Also set `IsEnabled="{Binding !IsLocked}"` on the content below.
6. Settings → new card **Security** (B11 pattern): if not configured: "Set a passphrase" button opens an inline form (passphrase + confirm, ≥ 8 chars) and then shows the recovery key in a `SelectableTextBlock` with a **"I saved it"** button that must be pressed before the form closes. If configured: **Lock now**, **Change passphrase**, **Remove lock** (each asks for the current passphrase). Advanced: "Lock after N minutes of inactivity" (0 = never; default 0), "Lock when Windows locks" (default on).
7. `AppSettings`: `int AutoLockMinutes`, `bool LockWithWindows = true`.

**Tests**
- Set up the lock in the fixture (pass an `AppLockService` into a `MainViewModel`); `Lock.Passphrase = "wrong"`; `UnlockCommand.Execute(null)` → `IsLocked` stays true and `Error` not null; right passphrase → `IsLocked == false` and `Host.Input != null`.
- `Lock()` → `Host.Input == null`.

**Done when:** build; tests pass; manual: set passphrase, click Lock now: window shows only the lock screen; wrong passphrase shows an error; Enter unlocks; restart the app while locked: starts locked and no port is open (`netstat -ano | findstr 58490` shows nothing for the process).

---

## C4: Triggers (Ctrl+L, tray, idle, Windows lock) and blocking Quit

- **Needs:** C3 · **Size:** M · **Files:** `App.axaml.cs`, `App.axaml` (tray menu), `MainWindow.axaml` (KeyBinding), create `src/Mouser.UI/Services/WindowsSession.cs`

**Steps**
1. Ctrl+L: `KeyBinding Gesture="Ctrl+L"` → `MainViewModel.LockNowCommand` (calls `_lock.Lock()` only if `IsConfigured`).
2. Tray menu (A10): add **Lock** (`OnTrayLock`) above Quit; hide it unless `IsConfigured`.
3. `WindowsSession.cs`:
```csharp
using System.Runtime.InteropServices;
namespace Mouser.UI.Services;

public static class WindowsSession
{
    [DllImport("user32.dll")] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    /// <summary>True while the lock screen (secure desktop) is showing.</summary>
    public static bool IsWorkstationLocked()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var h = OpenInputDesktop(0, false, 0x0100 /*DESKTOP_SWITCHDESKTOP*/);
        if (h == IntPtr.Zero) return true;
        CloseDesktop(h);
        return false;
    }

    public static TimeSpan IdleTime()
    {
        if (!OperatingSystem.IsWindows()) return TimeSpan.Zero;
        var i = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref i) ? TimeSpan.FromMilliseconds((uint)Environment.TickCount - i.dwTime) : TimeSpan.Zero;
    }
}
```
4. In the existing 1-second timer tick (`App.axaml.cs`) add, before `vm.Refresh()`: `if (appLock.IsConfigured && !appLock.IsLocked && ((settings.Current.LockWithWindows && WindowsSession.IsWorkstationLocked()) || (settings.Current.AutoLockMinutes > 0 && WindowsSession.IdleTime() >= TimeSpan.FromMinutes(settings.Current.AutoLockMinutes)))) appLock.Lock();`
   (Note: idle time is measured system-wide, so remote input from a phone or the KVM counts as activity only if Windows sees it: injected input does reset the idle timer; acceptable.)
5. **Block Quit while locked:** `OnTrayQuit`: if `appLock.IsLocked` → `ShowMainWindow(); _quitAfterUnlock = true; return;` and in the `Unlocked` handler: `if (_quitAfterUnlock) Quit();`. Also the window close button while locked just hides to tray (A10 behaviour) even if `CloseToTray` is off.
6. Quick-paste hotkey handler: `if (appLock.IsLocked) return;`.

**Done when:** build; tests pass; manual: Ctrl+L locks; Win+L then unlock Windows → Mouser is locked; tray Quit while locked shows the lock screen and quits only after the right passphrase; Win+Alt+V does nothing while locked.

---

## C5: Encrypt clipboard history at rest (SQLCipher spike, then switch)

- **Needs:** C3 · **Size:** L · **Risk:** HIGH (package swap). **Do the spike first; stop and report if it fails.**
- **Files:** `src/Mouser.Core/Mouser.Core.csproj`, `src/Mouser.Core/Clipboard/ClipboardStorageService.cs`, `src/Mouser.UI/App.axaml.cs`, tests

**Why a package is needed:** FTS5 search stores a plaintext copy of every clip, so encrypting individual columns is not enough. The whole database file must be encrypted: SQLCipher.

**Spike (10 lines, in a scratch console project outside the repo)**
1. Reference `Microsoft.Data.Sqlite.Core` and `SQLitePCLRaw.bundle_e_sqlcipher` (latest stable 2.1.x). Call `SQLitePCL.Batteries_V2.Init();`.
2. Open a file DB, run `PRAGMA key = "x'<64 hex chars>'";`, create an FTS5 table, insert, query with `MATCH`. Reopen with no key → expect `SqliteException` "file is not a database".
3. If FTS5 `MATCH` works with the key: proceed. If not: **stop**, write the result in ROADMAP.md under Blocked.

**Steps (after a good spike)**
1. `Mouser.Core.csproj`: replace `Microsoft.Data.Sqlite` 8.0.10 with `Microsoft.Data.Sqlite.Core` 8.0.10 + `SQLitePCLRaw.bundle_e_sqlcipher` (version from the spike). Add `SQLitePCL.Batteries_V2.Init()` once (static constructor of `ClipboardStorageService`).
2. `ClipboardStorageService`: new constructor parameter `byte[]? key` (null = unencrypted, used by existing tests). `InitializeAsync` runs `PRAGMA key = "x'{hex}'";` **immediately after open, before any other command**.
3. **Migration:** if `clipboard.db` exists and opens **without** a key (plaintext) and a key is supplied: use `ATTACH DATABASE 'clipboard.enc.db' AS enc KEY "x'{hex}'"; SELECT sqlcipher_export('enc'); DETACH DATABASE enc;` then replace the file (keep the old as `clipboard.db.bak` until the next successful start, then delete it).
4. `App.axaml.cs`: when the lock is configured, the storage is created/opened **only after unlock** with `appLock.MasterKey`; on `Locked` dispose the storage (`DisposeAsync`) and clear `SqliteConnection` pools; on `Unlocked` reopen. When the lock is not configured, behave as today (no key).
5. Tests: encrypted DB cannot be opened without the key (expect `SqliteException`); FTS search works with the key; the plaintext → encrypted migration keeps all rows and favourites.

**Done when:** build; all tests pass; manual: with the lock on, open `clipboard.db` in a SQLite viewer: unreadable.

---

## C6: Android app lock

- **Needs:** A13 · **Size:** M · **Package:** `androidx.biometric:biometric:1.1.0` in `android/app/build.gradle.kts` (check the file for the existing dependency style first)
- **Files:** `android/app/build.gradle.kts`, `MainActivity.kt` (must extend `FragmentActivity` for BiometricPrompt: check what it extends today), `MouserViewModel.kt`, a new `ui/components/LockScreen.kt`

**Steps**
1. Preference `appLockEnabled` (`prefs`, key `"app_lock"`). Toggle in the connection/settings area.
2. When enabled, on `onStart` after the app was in the background more than 30 s, show `LockScreen` (full-screen composable) and call `BiometricPrompt` with `BIOMETRIC_STRONG or DEVICE_CREDENTIAL`; on success hide the lock.
3. While locked: the `MouserConnection` must not send input (guard in the view model: `if (locked) return`), and the screen content is not composed (so it is not visible in the app switcher; also set `window.setFlags(FLAG_SECURE, FLAG_SECURE)` while the lock is enabled).
4. Unit test (JVM): the lock state machine (a small pure class `LockState(timeoutMs, clock)` with `onBackgrounded()`, `onForegrounded()`, `isLocked`) in `android/app/src/test/...`.

**Done when:** `.\gradlew.bat testDebugUnitTest` and `assembleDebug` pass; manual on emulator with a PIN set.

---

## C7: Lock all paired devices

- **Needs:** C4 · **Size:** M · **Protocol change allowed here.** **Files:** `src/Mouser.Core/Networking/PeerTransferService.cs`, `MouserHost.cs`, `DevicesViewModel.cs` (or tray), tests in `PeerTransferTests.cs`

**Steps**
1. `PeerTransferService.FrameType`: add `LockRequest = 6`. Add `public async Task SendLockAsync(IPEndPoint target, byte[] key, CancellationToken ct = default)` (copy `SendHelloAsync`, use `FrameType.LockRequest`) and event `public event Action<string>? LockRequested;` (sender device id), raised in `HandleConnectionAsync` next to the `Hello` branch (authentication is the same frame encryption: only a paired computer can produce a valid frame).
2. `MouserHost`: `public Task LockPeersAsync()` sends to every paired reachable peer (same selection as `ShareClipboardAsync`; ignore failures) and `public event Action<string>? LockRequestedByPeer`.
3. `App.axaml.cs`: on `LockRequestedByPeer` → if `AppSettings.AllowRemoteLock` (default **off**; explicit opt-in in Settings → Security → Advanced "Let my other devices lock this computer") and the lock is configured → `appLock.Lock()`.
4. Tray item and Devices button **"Lock all devices"** (only when configured).
5. Test (`PeerTransferTests`): copy a clip test; send `SendLockAsync` and assert `LockRequested` fires with the sender id; with a wrong key it does not.

**Done when:** build; tests pass (+1).

---

## C8: Windows Hello (optional, spike)

- **Needs:** C4 · **Size:** M · **Risk:** HIGH (needs the WinRT target framework)
- Goal: a "Use Windows Hello" button on the lock screen that unlocks without typing. Hello does not hold the key itself: store the master key wrapped with a DPAPI key (`ProtectedData.Protect(masterKey, null, DataProtectionScope.CurrentUser)`, file `lock.hello`) and release it only after `UserConsentVerifier.RequestVerificationAsync` succeeds.
- **Spike first:** `Windows.Security.Credentials.UI.UserConsentVerifier` needs `<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>` on `Mouser.UI`, which also changes how `Mouser.Desktop` and the tests reference it and breaks Linux builds. **If the TFM change breaks the build or Linux/mac targeting, STOP and move this item to the backlog.** Otherwise implement behind `OperatingSystem.IsWindows()`.
- Note the security trade-off in the UI hint: DPAPI protects against other Windows users, not against malware running as you.
