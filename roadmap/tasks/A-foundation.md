# Track A: Foundation (theme, tray, background)

Read [../AGENT_GUIDE.md](../AGENT_GUIDE.md) first. Order matters: **A1 → A2 → A3…A8 → A9** (theme tokens, then every
view, then the switch), and A10–A13 can follow in any order after A9. Status: [../../ROADMAP.md](../../ROADMAP.md).

Design background: [../phase-7a-ui-redesign-lock-and-tray.md](../phase-7a-ui-redesign-lock-and-tray.md) §4 and §8.

---

## A1: Create the colour token file

- **Needs:** none · **Size:** S · **Risk:** low (nothing uses the tokens yet, so the app looks the same)
- **Files:** create `src/Mouser.UI/Styles/Tokens.axaml`; edit `src/Mouser.UI/App.axaml` (only `Application.Resources`)

**Steps**
1. Create folder `src/Mouser.UI/Styles/` and the file `Tokens.axaml` with exactly this content:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ResourceDictionary.ThemeDictionaries>

    <ResourceDictionary x:Key="Dark">
      <SolidColorBrush x:Key="BgApp">#0c0d12</SolidColorBrush>
      <SolidColorBrush x:Key="BgSidebar">#12131b</SolidColorBrush>
      <SolidColorBrush x:Key="BgSurface">#13141d</SolidColorBrush>
      <SolidColorBrush x:Key="BgRaised">#181922</SolidColorBrush>
      <SolidColorBrush x:Key="BgInput">#161722</SolidColorBrush>
      <SolidColorBrush x:Key="BgListItem">#15161f</SolidColorBrush>
      <SolidColorBrush x:Key="BgSunken">#0f1018</SolidColorBrush>
      <SolidColorBrush x:Key="BgHover">#1c1d28</SolidColorBrush>
      <SolidColorBrush x:Key="BgActive">#1f2133</SolidColorBrush>
      <SolidColorBrush x:Key="BgPressed">#14151e</SolidColorBrush>
      <SolidColorBrush x:Key="BorderSubtle">#262938</SolidColorBrush>
      <SolidColorBrush x:Key="BorderStrong">#2e3248</SolidColorBrush>
      <SolidColorBrush x:Key="TextPrimary">#f4f4f5</SolidColorBrush>
      <SolidColorBrush x:Key="TextSecondary">#cbd5e1</SolidColorBrush>
      <SolidColorBrush x:Key="TextMuted">#94a3b8</SolidColorBrush>
      <SolidColorBrush x:Key="TextFaint">#64748b</SolidColorBrush>
      <SolidColorBrush x:Key="Accent">#4f46e5</SolidColorBrush>
      <SolidColorBrush x:Key="AccentHover">#4338ca</SolidColorBrush>
      <SolidColorBrush x:Key="AccentPressed">#3730a3</SolidColorBrush>
      <SolidColorBrush x:Key="AccentSoft">#6366f1</SolidColorBrush>
      <SolidColorBrush x:Key="OnAccent">#ffffff</SolidColorBrush>
      <SolidColorBrush x:Key="Success">#10b981</SolidColorBrush>
      <SolidColorBrush x:Key="Warning">#f59e0b</SolidColorBrush>
      <SolidColorBrush x:Key="Danger">#f43f5e</SolidColorBrush>
      <SolidColorBrush x:Key="BannerErrorBg">#2a1218</SolidColorBrush>
      <SolidColorBrush x:Key="BannerErrorText">#fecdd3</SolidColorBrush>
      <SolidColorBrush x:Key="BannerWarningBg">#2a2112</SolidColorBrush>
      <SolidColorBrush x:Key="BannerWarningText">#fde68a</SolidColorBrush>
      <SolidColorBrush x:Key="BannerInfoBg">#141a2e</SolidColorBrush>
      <SolidColorBrush x:Key="BannerInfoBorder">#3b4a7a</SolidColorBrush>
      <SolidColorBrush x:Key="BannerInfoText">#c7d2fe</SolidColorBrush>
      <SolidColorBrush x:Key="PeerBg">#112a22</SolidColorBrush>
      <SolidColorBrush x:Key="PeerBorder">#1f6b53</SolidColorBrush>
      <SolidColorBrush x:Key="PillBg">#11261d</SolidColorBrush>
      <SolidColorBrush x:Key="BadgeBg">#1c2030</SolidColorBrush>
      <SolidColorBrush x:Key="BadgeViolet">#a78bfa</SolidColorBrush>
    </ResourceDictionary>

    <ResourceDictionary x:Key="Light">
      <SolidColorBrush x:Key="BgApp">#f5f6fa</SolidColorBrush>
      <SolidColorBrush x:Key="BgSidebar">#eceef5</SolidColorBrush>
      <SolidColorBrush x:Key="BgSurface">#ffffff</SolidColorBrush>
      <SolidColorBrush x:Key="BgRaised">#f3f4f9</SolidColorBrush>
      <SolidColorBrush x:Key="BgInput">#ffffff</SolidColorBrush>
      <SolidColorBrush x:Key="BgListItem">#ffffff</SolidColorBrush>
      <SolidColorBrush x:Key="BgSunken">#eef0f6</SolidColorBrush>
      <SolidColorBrush x:Key="BgHover">#e6e9f2</SolidColorBrush>
      <SolidColorBrush x:Key="BgActive">#dfe3f0</SolidColorBrush>
      <SolidColorBrush x:Key="BgPressed">#d5daea</SolidColorBrush>
      <SolidColorBrush x:Key="BorderSubtle">#dde1ec</SolidColorBrush>
      <SolidColorBrush x:Key="BorderStrong">#c5cbdc</SolidColorBrush>
      <SolidColorBrush x:Key="TextPrimary">#14151f</SolidColorBrush>
      <SolidColorBrush x:Key="TextSecondary">#384056</SolidColorBrush>
      <SolidColorBrush x:Key="TextMuted">#5b6478</SolidColorBrush>
      <SolidColorBrush x:Key="TextFaint">#6b7389</SolidColorBrush>
      <SolidColorBrush x:Key="Accent">#4f46e5</SolidColorBrush>
      <SolidColorBrush x:Key="AccentHover">#4338ca</SolidColorBrush>
      <SolidColorBrush x:Key="AccentPressed">#3730a3</SolidColorBrush>
      <SolidColorBrush x:Key="AccentSoft">#6366f1</SolidColorBrush>
      <SolidColorBrush x:Key="OnAccent">#ffffff</SolidColorBrush>
      <SolidColorBrush x:Key="Success">#059669</SolidColorBrush>
      <SolidColorBrush x:Key="Warning">#d97706</SolidColorBrush>
      <SolidColorBrush x:Key="Danger">#e11d48</SolidColorBrush>
      <SolidColorBrush x:Key="BannerErrorBg">#fdecef</SolidColorBrush>
      <SolidColorBrush x:Key="BannerErrorText">#9f1239</SolidColorBrush>
      <SolidColorBrush x:Key="BannerWarningBg">#fef3e2</SolidColorBrush>
      <SolidColorBrush x:Key="BannerWarningText">#92400e</SolidColorBrush>
      <SolidColorBrush x:Key="BannerInfoBg">#eaf0ff</SolidColorBrush>
      <SolidColorBrush x:Key="BannerInfoBorder">#9db4f0</SolidColorBrush>
      <SolidColorBrush x:Key="BannerInfoText">#3730a3</SolidColorBrush>
      <SolidColorBrush x:Key="PeerBg">#e3f6ee</SolidColorBrush>
      <SolidColorBrush x:Key="PeerBorder">#6fbf9f</SolidColorBrush>
      <SolidColorBrush x:Key="PillBg">#e3f6ee</SolidColorBrush>
      <SolidColorBrush x:Key="BadgeBg">#e6e9f2</SolidColorBrush>
      <SolidColorBrush x:Key="BadgeViolet">#7c3aed</SolidColorBrush>
    </ResourceDictionary>

  </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```

2. In `App.axaml`, replace the whole `<Application.Resources>…</Application.Resources>` block with:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceInclude Source="avares://Mouser.UI/Styles/Tokens.axaml" />
        </ResourceDictionary.MergedDictionaries>
        <!-- Brand accent instead of the user's Windows accent colour (toggles, checkboxes, focus). -->
        <Color x:Key="SystemAccentColor">#4f46e5</Color>
        <Color x:Key="SystemAccentColorLight1">#6366f1</Color>
        <Color x:Key="SystemAccentColorLight2">#818cf8</Color>
        <Color x:Key="SystemAccentColorDark1">#4338ca</Color>
    </ResourceDictionary>
</Application.Resources>
```

**Done when**
- `dotnet build Mouser.sln` succeeds.
- `dotnet run --project src/Mouser.Desktop` opens the window and it looks **identical** to before (nothing uses the tokens yet).
- `dotnet test tests/Mouser.Tests --nologo -v q` still `Passed: 130`.

**Do not:** change `RequestedThemeVariant` (stays `Dark` until A9). Do not edit any view.

---

## A2: Convert `App.axaml` styles to tokens

- **Needs:** A1 · **Size:** M · **Files:** `src/Mouser.UI/App.axaml`

**Steps**
1. In the `<Application.Styles>` block, replace every raw hex in a `<Setter Value="#…">` with the token from the **mapping table at the bottom of this file**, written as `Value="{DynamicResource TokenName}"`.
   Example, before → after:
   ```xml
   <Setter Property="Background" Value="#0c0d12" />        →  <Setter Property="Background" Value="{DynamicResource BgApp}" />
   <Setter Property="Foreground" Value="#94a3b8" />        →  <Setter Property="Foreground" Value="{DynamicResource TextMuted}" />
   <Setter Property="BorderBrush" Value="#4f46e5" />       →  <Setter Property="BorderBrush" Value="{DynamicResource Accent}" />
   ```
2. Banner borders: `banner-error` border → `Danger`; `banner-warning` border → `Warning`; `banner-info` border → `BannerInfoBorder`. Banner backgrounds → `BannerErrorBg` / `BannerWarningBg` / `BannerInfoBg`.
3. `Border.status-pill`: background `PillBg`, border `Success`.
4. `TextBox` style: `Background` → `BgInput`, `Foreground` → `TextPrimary`, `BorderBrush` → `BorderSubtle`, `SelectionBrush` → `Accent`. `TextBox:focus` border → `AccentSoft`.
5. Leave the four `SystemAccentColor*` `<Color>` lines alone.

**Done when**
- Run `grep -nE "#[0-9a-fA-F]{6}\b" src/Mouser.UI/App.axaml` (PowerShell: `Select-String -Path src\Mouser.UI\App.axaml -Pattern '#[0-9a-fA-F]{6}\b'`). **Only the four `SystemAccentColor*` lines may match.**
- Build passes. The app in Dark looks the same as before (spot check: sidebar, a button, a card).
- 130 tests pass.

**Do not:** use `StaticResource`. Do not change `RequestedThemeVariant`.

---

## A3: Migrate `MainWindow.axaml`

- **Needs:** A2 · **Size:** S · **Files:** `src/Mouser.UI/Views/MainWindow.axaml`
- Hex found today: `#0c0d12 #10b981 #12131b #161722 #232635 #272a38 #4f46e5 #94a3b8 #a78bfa #e2e8f0 #f8fafc #ffffff`

**Steps**
1. Replace each hex using the mapping table (attribute form: `Foreground="{DynamicResource TextMuted}"`).
2. Special cases: the logo tile `Background="#4f46e5"` → `Accent`; the "M" `Foreground="#ffffff"` → `OnAccent`; nav-badge TextBlock colours `#10b981` → `Success`, `#a78bfa` → `BadgeViolet`; the sidebar border `#232635` → `BorderSubtle`; `Window Background` → `BgApp`.

**Done when:** the grep from A2 run on `Views/MainWindow.axaml` prints nothing; build + 130 tests pass; app looks unchanged in Dark.

---

## A4: Migrate `AndroidRemoteView.axaml`
- **Needs:** A2 · **Size:** S · **Files:** `src/Mouser.UI/Views/AndroidRemoteView.axaml`
- Hex today: `#0f1018 #181922 #272a38 #2e3248 #94a3b8 #e2e8f0 #f1f5f9 #f8fafc #fde68a #fecdd3`
- Special: banner-error text `#fecdd3` → `BannerErrorText`; banner-warning text `#fde68a` → `BannerWarningText`; PIN box `#0f1018` → `BgSunken`; `<Separator Background="#272a38">` → `BorderSubtle`.
- **Done when:** grep prints nothing for this file; build + tests pass.

## A5: Migrate `ClipboardHistoryView.axaml`
- **Needs:** A2 · **Size:** S · **Files:** `src/Mouser.UI/Views/ClipboardHistoryView.axaml`
- Hex today: `#15161f #1a1b28 #262938 #2e3248 #6366f1 #64748b #f1f5f9 #fde68a #ffffff`
- Special: the `#ffffff` on the type badge (text sitting on a coloured fill) → `OnAccent`. `Rectangle Fill="#2e3248"` → `BorderStrong`. Selected clip card: background `BgActive`, border `AccentSoft`.
- **Done when:** grep prints nothing; build + tests pass.

## A6: Migrate `MonitorLayoutView.axaml`
- **Needs:** A2 · **Size:** M · **Files:** `src/Mouser.UI/Views/MonitorLayoutView.axaml`
- Hex today: `#0f1015 #112a22 #181922 #1c1d26 #1f6b53 #232535 #272a38 #2e3248 #6366f1 #64748b #94a3b8 #c7d2fe #cbd5e1 #e2e8f0 #f1f5f9 #fda4af #ffffff`
- Special: canvas `#0f1015` → `BgSunken`; screen button bg `#1c1d26` → `BgRaised`; peer screen `#112a22`/`#1f6b53` → `PeerBg`/`PeerBorder`; the screen **label `#ffffff` → `TextPrimary`** (it sits on a light button in Light mode, so NOT `OnAccent`); peer error text `#fda4af` → `BannerErrorText`; the info banner text `#c7d2fe` → `BannerInfoText`.
- **Done when:** grep prints nothing; build + tests pass.

## A7: Migrate `SettingsView.axaml`
- **Needs:** A2 · **Size:** XS · **Files:** `src/Mouser.UI/Views/SettingsView.axaml`
- Hex today: `#cbd5e1 #e2e8f0` → `TextSecondary`, `TextPrimary`. **Done when:** grep prints nothing; build passes.

## A8: Migrate `QuickPasteWindow.axaml`
- **Needs:** A2 · **Size:** XS · **Files:** `src/Mouser.UI/Views/QuickPasteWindow.axaml`
- Hex today: `#13141d #4f46e5 #64748b #f1f5f9` → `BgSurface`, `Accent`, `TextFaint`, `TextPrimary`.
- **Done when:** grep prints nothing; build + tests pass (`QuickPasteTests` covers the view model).

---

## A9: Theme setting (System / Light / Dark) and switch the default

- **Needs:** A3, A4, A5, A6, A7, A8 all ✅ · **Size:** M
- **Files:** edit `src/Mouser.UI/Services/SettingsStore.cs`, `src/Mouser.UI/ViewModels/SettingsViewModel.cs`, `src/Mouser.UI/Views/SettingsView.axaml`, `src/Mouser.UI/App.axaml`, `src/Mouser.UI/App.axaml.cs`; create `src/Mouser.UI/Services/ThemeService.cs`; add tests to `tests/Mouser.Tests/ViewModelTests.cs`

**Steps**
1. `ThemeService.cs`:
```csharp
using Avalonia;
using Avalonia.Styling;

namespace Mouser.UI.Services;

public static class ThemeService
{
    public static readonly string[] Options = { "System", "Light", "Dark" };

    public static void Apply(string theme)
    {
        if (Application.Current is not { } app) return; // null in unit tests
        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default // follows the operating system
        };
    }
}
```
2. `AppSettings`: add `public string Theme { get; set; } = "System";`. In `SettingsStore.Load()` after the other checks add: `if (!ThemeService.Options.Contains(settings.Theme)) settings.Theme = "System";` (add `using System.Linq;`).
3. `SettingsViewModel`: add `[ObservableProperty] private string _theme;`, set `_theme = s.Theme;` in the constructor, add `public string[] ThemeOptions => ThemeService.Options;` and `partial void OnThemeChanged(string value) { ThemeService.Apply(value); Save(s => s.Theme = value); }`.
4. `SettingsView.axaml`: add a new card at the top (copy the structure of an existing card) titled **Appearance** with one `ComboBox` bound as `ItemsSource="{Binding ThemeOptions}" SelectedItem="{Binding Theme}" Width="160"` and a hint "System follows your Windows setting."
5. `App.axaml`: change `RequestedThemeVariant="Dark"` to `RequestedThemeVariant="Default"`.
6. `App.axaml.cs`: right after `var settings = new SettingsStore(...)` add `ThemeService.Apply(settings.Current.Theme);`.
7. Tests (in `ViewModelTests`): (a) new `AppFixture` → `Settings.Current.Theme == "System"`; (b) set `app.Main.Settings.Theme = "Light"`, create `new SettingsStore(app.Settings.FilePath)`, assert `Current.Theme == "Light"`; (c) write `"Theme": "Banana"` into a temp settings file, load it, assert it becomes `"System"`.

**Done when:** build + all tests pass (count rises by 3). Run the app: switch Appearance to Light and then Dark; **every page, the quick-paste window (Win+Alt+V) and all banners must be readable in both**. If something is unreadable, add or fix a token; do not hard-code a colour.

**Do not:** write colours in C#. Do not remove the `SystemAccentColor*` lines.

---

## A10: Tray icon, close-to-tray and minimize-to-tray

- **Needs:** A9 · **Size:** M · **Risk:** medium (Avalonia 12 tray API: test it)
- **Files:** edit `src/Mouser.UI/App.axaml`, `src/Mouser.UI/App.axaml.cs`, `src/Mouser.UI/Services/SettingsStore.cs`, `src/Mouser.UI/ViewModels/SettingsViewModel.cs`, `src/Mouser.UI/Views/SettingsView.axaml`; tests in `ViewModelTests.cs`

**Behaviour to build**
- Closing the main window **hides** it; Mouser keeps running (hooks, clipboard capture, sync, KVM, phone input all continue).
- **Minimizing** the main window also hides it to the tray (no taskbar button left behind).
- Tray menu: **Open Mouser**, **Pause sharing** (toggle), **Pause clipboard** (toggle), separator, **Quit**.
- Left-click on the tray icon opens the window. **Quit** really exits.
- First time the window is hidden, show a toast "Mouser is still running in the tray" (toast arrives in A12; until then skip the toast).
- Two independent settings (Settings → General), both default **on**:
  - **"Keep running in the tray when I close the window"** (`CloseToTray`). When off, closing exits as today.
  - **"Hide to the tray when I minimize the window"** (`MinimizeToTray`). When off, minimize behaves like any app (taskbar).

**Steps**
1. `AppSettings`: add `public bool CloseToTray { get; set; } = true;`, `public bool MinimizeToTray { get; set; } = true;` and `public bool PauseSharing { get; set; }` and `public bool PauseClipboard { get; set; }`.
2. `App.axaml`: add inside `<Application ...>` (after `Application.Styles` is fine):
```xml
<TrayIcon.Icons>
  <TrayIcons>
    <TrayIcon Icon="/Assets/avalonia-logo.ico" ToolTipText="Mouser" Clicked="OnTrayClicked">
      <TrayIcon.Menu>
        <NativeMenu>
          <NativeMenuItem Header="Open Mouser" Click="OnTrayOpen" />
          <NativeMenuItem Header="Pause sharing" ToggleType="CheckBox" Click="OnTrayPauseSharing" />
          <NativeMenuItem Header="Pause clipboard" ToggleType="CheckBox" Click="OnTrayPauseClipboard" />
          <NativeMenuItemSeparator />
          <NativeMenuItem Header="Quit" Click="OnTrayQuit" />
        </NativeMenu>
      </TrayIcon.Menu>
    </TrayIcon>
  </TrayIcons>
</TrayIcon.Icons>
```
   If the XAML does not compile in Avalonia 12.1.6, create the same menu in C# in `OnFrameworkInitializationCompleted` with `TrayIcon.SetIcons(this, new TrayIcons { new TrayIcon { ... } })`. Pick one way and stay with it.
3. `App.axaml.cs`: add fields `private MainWindow? _window; private bool _quitting;`. After `var window = new MainWindow {...}` set `_window = window;` and add:
```csharp
window.Closing += (_, e) =>
{
    if (_quitting || e.CloseReason == WindowCloseReason.OSShutdown || !settings.Current.CloseToTray) return;
    e.Cancel = true;
    window.Hide();
};
```
   Minimize handler (same place):
```csharp
window.PropertyChanged += (_, e) =>
{
    if (e.Property == Window.WindowStateProperty && window.WindowState == WindowState.Minimized
        && settings.Current.MinimizeToTray && !_quitting)
        window.Hide(); // taskbar button disappears; the tray icon stays
};
```
   Add handlers: `OnTrayClicked`/`OnTrayOpen` → `ShowMainWindow()`; `OnTrayQuit` → `_quitting = true; (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();`.
   `ShowMainWindow()`: `_window.Show(); if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal; _window.Activate();`
4. **Pause sharing:** in the pause handler set `settings.Current.PauseSharing`, save, and call `_host.SetKvmEnabled(!paused)` (this stops hooks; already exists in `MouserHost`). Keep `MonitorLayoutViewModel.IsKvmEnabled` in sync: set `vm.MonitorLayout.IsKvmEnabled = !paused` on the UI thread.
   **Pause clipboard:** `vm.ClipboardHistory.IsPaused` (new `bool` property). In `PollClipboardAsync` return `false` at the top when paused. Also skip `ShareCapturedText`.
5. `SettingsViewModel` + `SettingsView.axaml`: add the checkboxes for `CloseToTray` **and** `MinimizeToTray` using the **Setting row** pattern from the guide (§4.4).
6. **Do not touch the 1-second timer** (guide, trap 1).

**Tests**
- `PauseClipboard_DoesNotCapture`: with `FakeClipboard.Text = "hello"` and `IsPaused = true`, `PollClipboardAsync()` returns false and history stays empty; set `IsPaused = false` → returns true.
- `TraySettings_DefaultOnAndPersist` (settings round trip for `CloseToTray` and `MinimizeToTray`: both default true; set each to false, reload, still false).

**Done when:** build + tests pass. **Manual check on Windows:** click the **minimize** button → the window and its taskbar button disappear and the tray icon stays; clicking the tray icon brings the window back **un-minimized**; with `MinimizeToTray` off, minimize goes to the taskbar as normal. Click the window X → the window disappears and `Mouser.exe` is still in Task Manager; tray icon appears (look in the hidden-icons arrow `^`); Win+Alt+V quick paste still works with the window hidden; copy text while hidden, then open the window from the tray: the clip is in history; **Quit** removes the process.

**Do not:** show a dialog on close or minimize. Do not let the lock screen (C3) change tray behaviour.

---

## A11: `--minimized`, start at login minimized, single instance

- **Needs:** A10 · **Size:** S
- **Files:** create `src/Mouser.UI/Services/SingleInstance.cs`; edit `src/Mouser.UI/App.axaml.cs`, `src/Mouser.UI/Services/PlatformServices.cs`

**Steps**
1. `StartupRegistration.ApplyWindows`: change the registry value to `$"\"{exe}\" --minimized"`.
2. `SingleInstance.cs`:
```csharp
using System.Threading;
namespace Mouser.UI.Services;

/// <summary>First process owns the mutex; later launches ping it to show its window, then exit.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show;
    public bool IsFirst { get; }

    public SingleInstance(string name = "Mouser")
    {
        _mutex = new Mutex(true, $@"Local\{name}.Lock", out bool created);
        _show = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Show");
        IsFirst = created;
    }

    /// <summary>Second instance calls this and then exits.</summary>
    public void SignalFirst() => _show.Set();

    /// <summary>First instance: runs onShow on a background thread each time another launch happens.</summary>
    public void Listen(Action onShow) =>
        new Thread(() => { while (_show.WaitOne()) onShow(); }) { IsBackground = true, Name = "Mouser single-instance" }.Start();

    public void Dispose() { if (IsFirst) _mutex.ReleaseMutex(); _mutex.Dispose(); _show.Dispose(); }
}
```
3. In `OnFrameworkInitializationCompleted` (inside the `desktop` branch, **before** creating settings): if `--data-dir` was **not** passed, create `var instance = new SingleInstance();` and `if (!instance.IsFirst) { instance.SignalFirst(); desktop.Shutdown(); return; }` else `instance.Listen(() => Dispatcher.UIThread.Post(ShowMainWindow));`. Dispose it in `desktop.Exit`. (When `--data-dir` is passed skip all of this so the two-instance test script still works.)
4. If `Environment.GetCommandLineArgs()` contains `--minimized` **and** `CloseToTray` is on: do not call `desktop.MainWindow = window` visibility… simplest: set `window.ShowActivated = false; window.WindowState = WindowState.Minimized; window.ShowInTaskbar = false` **or** after `window.Opened` call `window.Hide()`. Use the `Opened`+`Hide()` way.

**Done when:** build + tests pass. Manual: run the exe twice → only one `Mouser.exe` in Task Manager and the first window comes to the front. Run `Mouser.exe --minimized` → no window, tray icon present. `scripts\test-kvm-single-pc.ps1` still starts two windows.

---

## A12: In-app toasts

- **Needs:** A9 · **Size:** M · **Risk:** low
- **Files:** create `src/Mouser.UI/Views/ToastWindow.axaml`, `ToastWindow.axaml.cs`, `src/Mouser.UI/Services/ToastService.cs`; edit `App.axaml.cs`, `src/Mouser.UI/Services/SettingsStore.cs`, Settings VM/view

**Goal:** a small borderless always-on-top card in the bottom-right corner for 4 seconds. Works when the main window is hidden. (shortcut: custom window, not native OS toast; upgrade when the installer exists in Phase 12.)

**Steps**
1. `ToastWindow.axaml`: copy `QuickPasteWindow.axaml`'s window attributes (`WindowDecorations="None" ShowInTaskbar="False" Topmost="True" CanResize="False"`), `Width="320" SizeToContent="Height"`, `ShowActivated="False"`, background `{DynamicResource BgSurface}`. Content: `Border` with `BorderBrush="{DynamicResource BorderSubtle}"`, `CornerRadius="12"`, `Padding="14"`, a bold `TextBlock x:Name="TitleText"` (TextPrimary) and a `TextBlock x:Name="BodyText"` (class `hint`, `TextWrapping="Wrap"`).
2. `ToastService.Show(string title, string body)`: must be called on the UI thread (callers use `Dispatcher.UIThread.Post`). Creates the window, fills the two texts, positions it using `Screens.Primary.WorkingArea` (bottom-right, 16 px margin; with several toasts stack upwards by 8 px + height), shows it, and closes it with a `DispatcherTimer` after 4 s. Skip if `settings.Current.Notifications` is false.
3. `AppSettings`: `public bool Notifications { get; set; } = true;` + checkbox "Show notifications" in Settings (Setting-row pattern).
4. Wire in `App.axaml.cs`:
   - `_host.ClipReceived` → toast `"Clip from {from}"`, body = first 60 chars (not if the text looks sensitive: it is already filtered upstream).
   - `_host.FileReceived` → toast `"File from {from}"`, body = file name.
   - First hide-to-tray (A10) → toast "Mouser is still running in the tray".
   - `_host.Kvm.StateChanged` → toast "Controlling {name}" / "Back on this computer" (only when the main window is hidden).
5. Do not show toasts for events when the main window is visible **and** active.

**Done when:** build + tests pass; manual: hide to tray, copy text on the second instance (`--data-dir`) → a toast appears on the first.

---

## A13: Android light and dark scheme

- **Needs:** none (independent) · **Size:** M
- **Files:** `android/app/src/main/java/com/mouser/companion/ui/theme/Color.kt`, `Theme.kt`, `MainActivity.kt`, `MouserViewModel.kt`, and the 4 component files that hard-code colours: `ui/components/AirMouseSurface.kt`, `ConnectionBar.kt`, `MediaMacrosView.kt`, `TrackpadSurface.kt`

Facts: today only `darkColorScheme` exists; 75 colour references (`Zinc…`, `Color(0x…)`) sit outside the theme files; preferences use `application.getSharedPreferences("mouser", …)` in `MouserViewModel`.

**Steps**
1. `Color.kt`: add light palette constants: `Slate50 = 0xFFF5F6FA`, `SlateSurface = 0xFFFFFFFF`, `SlateRaised = 0xFFF3F4F9`, `SlateText = 0xFF14151F`, `SlateMuted = 0xFF5B6478`, `SlateBorder = 0xFFDDE1EC`.
2. `Theme.kt`: add `lightColorScheme(primary = BluePrimary, secondary = EmeraldAccent, tertiary = IndigoAccent, background = Slate50, surface = SlateSurface, surfaceVariant = SlateRaised, onPrimary = Color.White, onBackground = SlateText, onSurface = SlateText, onSurfaceVariant = SlateMuted, outline = SlateBorder)`. Change `MouserTheme` to `fun MouserTheme(mode: String = "System", content: @Composable () -> Unit)`; `val dark = when (mode) { "Dark" -> true; "Light" -> false; else -> isSystemInDarkTheme() }`; pick the scheme accordingly. Add `import androidx.compose.foundation.isSystemInDarkTheme`.
3. In the four component files and `MainActivity.kt`, **replace every `Zinc…`/`Color(0x…)` used for backgrounds, text and borders** with `MaterialTheme.colorScheme.background / surface / surfaceVariant / onSurface / onSurfaceVariant / outline`. Keep the saturated status colours (`EmeraldAccent`, `RoseAccent`, `AmberAccent`, `BluePrimary`).
4. `MouserViewModel`: add `val themeMode = MutableStateFlow(prefs.getString("theme", "System") ?: "System")` and `fun setThemeMode(m: String) { prefs.edit().putString("theme", m).apply(); themeMode.value = m }`. In `MainActivity` collect it and pass to `MouserTheme(mode)`.
5. Add a small "Theme" chooser (3 `FilterChip`s: System/Light/Dark) in the connection/settings area of `ConnectionBar.kt`.

**Done when:** `cd android; .\gradlew.bat testDebugUnitTest` passes and `.\gradlew.bat assembleDebug` succeeds. Grep check: `grep -rnE "Zinc|Color\(0x" android/app/src/main/java --include=*.kt | grep -v "/theme/"` prints **only** saturated status colours. Visual: install on emulator, toggle all three modes, every screen readable.

---

## A14: Website theme alignment

- **Needs:** none · **Size:** S · **Files:** `web/src/context/ThemeContext.tsx`, `web/src/components/ThemeToggle.tsx`, `web/src/app/globals.css`
- Read `web/AGENTS.md` first. The site already switches Light/Dark and defaults to the OS setting on first visit.
- **Steps:** (1) in `globals.css` replace the two hex pairs by named CSS variables with the **same names and values as Tokens.axaml** (`--bg-app`, `--bg-surface`, `--text-primary`, `--text-muted`, `--accent`, …) for `:root` and `:root.dark`; (2) extend `ThemeContext` with a third state `system` that removes the saved key and follows `prefers-color-scheme` live (listen to `change`); (3) make `ThemeToggle` cycle System → Light → Dark with an accessible label.
- **Done when:** `cd web; npm run lint; npm run build` pass; toggling works and survives reload.

---

# Appendix: hex → token mapping (for A2–A8)

| Raw hex (dark UI today) | Token | Notes |
| :--- | :--- | :--- |
| `#0c0d12` | `BgApp` | window background |
| `#12131b` | `BgSidebar` | |
| `#13141d` | `BgSurface` | cards, quick-paste window |
| `#181922`, `#1c1d26` | `BgRaised` | row cards, secondary buttons, screen buttons |
| `#161722` | `BgInput` | text boxes, sidebar status box |
| `#15161f` | `BgListItem` | clip card |
| `#0f1018`, `#0f1015` | `BgSunken` | PIN box, layout canvas |
| `#1c1d28`, `#232535` | `BgHover` | |
| `#1f2133`, `#1a1b28` | `BgActive` | selected nav item / selected clip |
| `#252838`, `#14151e` | `BgPressed` | |
| `#262938`, `#272a38`, `#232635` | `BorderSubtle` | |
| `#2e3248`, `#3f4562` | `BorderStrong` | |
| `#f4f4f5`, `#f8fafc`, `#f1f5f9`, `#e2e8f0` | `TextPrimary` | |
| `#cbd5e1` | `TextSecondary` | |
| `#94a3b8` | `TextMuted` | |
| `#64748b` | `TextFaint` | timestamps, placeholders |
| `#ffffff` | `OnAccent` **only** when it sits on an accent/badge fill; otherwise `TextPrimary` | |
| `#4f46e5` / `#4338ca` / `#3730a3` / `#6366f1` | `Accent` / `AccentHover` / `AccentPressed` / `AccentSoft` | |
| `#10b981` / `#f59e0b` / `#f43f5e` | `Success` / `Warning` / `Danger` | |
| `#2a1218` / `#fecdd3`, `#fda4af` | `BannerErrorBg` / `BannerErrorText` | |
| `#2a2112` / `#fde68a` | `BannerWarningBg` / `BannerWarningText` | |
| `#141a2e` / `#3b4a7a` / `#c7d2fe` | `BannerInfoBg` / `BannerInfoBorder` / `BannerInfoText` | |
| `#112a22` / `#1f6b53` | `PeerBg` / `PeerBorder` | |
| `#11261d` | `PillBg` | |
| `#1c2030` | `BadgeBg` | |
| `#a78bfa` | `BadgeViolet` | |

Allowed raw hex **after** this track: `Tokens.axaml`; the four `SystemAccentColor*` lines in `App.axaml`; saturated colour strings returned by view models (`"#10b981"`, badge colours).
