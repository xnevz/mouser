# Agent guide: how to execute Mouser roadmap tasks

**Read this whole file before every task.** It is written for a small or fast AI model (Gemini Flash class) that
works best with short, literal instructions. If a rule here conflicts with your own habit, the rule wins.

Status board: [../ROADMAP.md](../ROADMAP.md). Task cards: [tasks/](./tasks/). Big-picture design: [phase-7a](./phase-7a-ui-redesign-lock-and-tray.md).

---

## 0. The loop (do exactly this, one task at a time)

1. Open [../ROADMAP.md](../ROADMAP.md). Pick the **first ⬜ task whose "Needs" are all ✅**. Do not pick by taste.
2. Open its card in `roadmap/tasks/`. Read **Files**, **Steps**, **Done when**, **Do not**.
3. Run the baseline (section 1) and write down the passing test count.
4. Do the Steps in order. Make the smallest change that finishes the step. Build after each step.
5. Run every command in **Done when**. All must pass.
6. Tick the task in ROADMAP.md (⬜ → ✅) and add one line of notes if anything surprised you.
7. **Stop.** Do not start the next task in the same session unless asked.

If you cannot finish: do not half-finish. Revert your edits for that task (`git checkout -- <files>` and delete files
you created), write the exact error in ROADMAP.md under **Blocked**, and stop.

## 1. Commands (Windows, PowerShell, run from `d:\projects\mouser`)

| Purpose | Command | Expected today |
| :--- | :--- | :--- |
| Build everything | `dotnet build Mouser.sln` | `Build succeeded`, 0 errors (CA1416 warnings are normal) |
| Run all tests | `dotnet test tests/Mouser.Tests --nologo -v q` | `Passed! Failed: 0, Passed: 130, Skipped: 3` (verified 2026-11-13) |
| Run one test | `dotnet test tests/Mouser.Tests --filter "FullyQualifiedName~ViewModelTests"` | passes |
| Run the app | `dotnet run --project src/Mouser.Desktop` | window opens |
| Run a second instance | `dotnet run --project src/Mouser.Desktop -- --data-dir C:\temp\mouser-b` | second window |
| Android unit tests | `cd android; .\gradlew.bat testDebugUnitTest` | passes |
| Web build / lint | `cd web; npm run build` and `npm run lint` | passes |

The test count only goes **up**. If it goes down, you deleted or broke a test: undo.
`Skipped: 3` is correct (native-library tests need a Mac build).

## 2. Hard rules

1. **Touch only the files listed in the card.** If you need another file, stop and add it to the card's Notes first.
2. **Never delete, skip, or weaken a test to make it pass.** You may add tests and you may update a test only when the card says its expected value changes.
3. **No new NuGet / npm / Gradle packages** unless the card names the package and version.
4. **No raw colour hex in `.axaml` files you write or edit** once task A1 is ✅ (tasks A2–A8 remove the old ones). Use theme resources (section 4). Grep check is in every UI card.
5. **Core never references UI.** `Mouser.Core` has no Avalonia. `Mouser.UI` may reference Core.
6. **Do not change the wire protocol** (UDP 14-byte packet, TCP `MCH1` frames) unless the card says "Protocol change allowed" (cards C7, E12, E13, F3, F4, G6).
7. **Git:** follow section 3 of [EXECUTOR_PROMPT.md](./EXECUTOR_PROMPT.md): one branch per task, small commits, never push or merge to main.
8. **Do not invent APIs.** If you are unsure an Avalonia / Compose / Next.js API exists in the installed version, open a similar existing usage in this repo, or build a 10-line spike, before writing the feature.
9. **Keep it small.** No new abstractions, interfaces or "future-proof" options that the card does not ask for.
10. **Comment only the "why"**, one line. For a known limit write: `// shortcut: <limit>, <when to upgrade>`.

## 3. Repo map (what lives where)

```
src/Mouser.Core/      engine, NO UI. Clipboard/ Files/ Geometry/ Input/ Interop/ Kvm/ Networking/ Services/
src/Mouser.UI/        Avalonia app: App.axaml(.cs), Views/, ViewModels/, Services/ (MouserHost, SettingsStore, ...)
src/Mouser.Desktop/   tiny exe: Program.cs only
src/Mouser.Native/    C++/ObjC for macOS (parked)
tests/Mouser.Tests/   xUnit, ~130 tests. Helpers: TestSupport.cs (RecordingSink, FakePhone, Wait, WindowsFact),
                      ViewModelTests.cs (AppFixture = a full app on temp folders, FakeClipboard)
android/app/src/main/java/com/mouser/companion/   Kotlin + Compose
web/src/              Next.js site
roadmap/              plans. ROADMAP.md at repo root is the status board.
```

Key classes you will touch:

| Class | File | What it is |
| :--- | :--- | :--- |
| `MouserHost` | `src/Mouser.UI/Services/MouserHost.cs` | owns networking: pairing, input receiver, discovery, peer links, KVM, file/clip transfer |
| `SettingsStore` / `AppSettings` / `PeerSettings` | `src/Mouser.UI/Services/SettingsStore.cs` | JSON settings in `%APPDATA%\Mouser\settings.json` |
| `MainViewModel` | `src/Mouser.UI/ViewModels/MainViewModel.cs` | owns the 4 page view models, nav, status text |
| `ClipboardHistoryViewModel` | `.../ClipboardHistoryViewModel.cs` | capture (1 s poll), search, filters, stars |
| `ClipboardStorageService` | `src/Mouser.Core/Clipboard/ClipboardStorageService.cs` | SQLite + FTS5 |
| `KvmController` | `src/Mouser.Core/Kvm/KvmController.cs` | edge hop logic |
| `App` | `src/Mouser.UI/App.axaml.cs` | wiring, 1 s `DispatcherTimer`, quick-paste hotkey |

## 4. UI rules (apply to every UI task)

### 4.1 Theme resources instead of hex
After task A1, colours come only from `src/Mouser.UI/Styles/Tokens.axaml`. Use them like this:

```xml
<Border Background="{DynamicResource BgSurface}" BorderBrush="{DynamicResource BorderSubtle}" BorderThickness="1" />
<TextBlock Foreground="{DynamicResource TextMuted}" />
```

* **Always `DynamicResource`**, never `StaticResource` (static does not follow the Light/Dark switch).
* Saturated status/badge colours that come from a view model as strings (`"#10b981"`, `BadgeColor`) are allowed: they read fine on both themes.
* Need a colour that has no token? Add a token to **both** variants in `Tokens.axaml`; never write a raw hex in a view.

### 4.2 Sizes (use only these)
Spacing 4 / 8 / 12 / 16 / 24. Corner radius 8 (controls), 12 (cards). Font sizes 11 (label), 12 (hint), 13 (body), 14 (section title), 20 (page title).
Sidebar width 232. Detail pane 340. One accent colour: `Accent`.

### 4.3 The "super simple" checklist (every page must pass it)
- [ ] At most **6 visible controls** (buttons, toggles, inputs, combos) without scrolling or expanding.
- [ ] Anything beyond that lives inside `<Expander Header="Advanced">` (collapsed by default).
- [ ] No raw numbers to type except port and days; use a Slider or a 3-choice preset instead.
- [ ] Every toggle has a one-line plain-language hint under it. No words like UDP, PIN hash, FTS, hook.
- [ ] Every empty list shows an empty state: one sentence saying what to do next.
- [ ] Every error text says how to fix it.
- [ ] Every button has a `ToolTip.Tip`; every icon-only button also has `AutomationProperties.Name`.
- [ ] Works at the minimum window size 900 x 600 with no horizontal scrollbar.

### 4.4 Copy-paste XAML patterns
Setting row (title + hint on the left, control on the right):
```xml
<Grid ColumnDefinitions="*,Auto" Margin="0,4">
  <StackPanel Spacing="2">
    <TextBlock Classes="section-title" FontSize="13" Text="Share clipboard" />
    <TextBlock Classes="hint" Text="Copied text and images appear on your other devices." />
  </StackPanel>
  <ToggleSwitch Grid.Column="1" IsChecked="{Binding ClipboardSyncEnabled}" OnContent="" OffContent="" />
</Grid>
```
Card: `<Border Classes="glass-card" Padding="20"><StackPanel Spacing="12"> ... </StackPanel></Border>`
Advanced group: `<Expander Header="Advanced" IsExpanded="False"><StackPanel Spacing="12" Margin="0,8,0,0"> ... </StackPanel></Expander>`
Status dot: `<Ellipse Width="8" Height="8" Fill="{Binding StatusColor}" />`
Empty state: `<TextBlock Classes="hint" HorizontalAlignment="Center" TextAlignment="Center" IsVisible="{Binding IsEmpty}" Text="Nothing here yet.&#10;Copy something and it shows up." />`

### 4.5 MVVM with CommunityToolkit (how this repo does it)
```csharp
public partial class FooViewModel : ViewModelBase            // must be partial
{
    [ObservableProperty] private string _title = "";          // generates property Title
    [RelayCommand] private void DoIt() { }                    // generates DoItCommand
    [RelayCommand] private async Task LoadAsync() { }         // generates LoadCommand (Async suffix dropped)
    partial void OnTitleChanged(string value) { }             // runs when Title changes
}
```
* **View lookup is by name:** `Mouser.UI.ViewModels.FooViewModel` is shown with `Mouser.UI.Views.FooView` (a `UserControl` with `x:DataType="vm:FooViewModel"`). Both names must match or the page says "Not Found".
* Events raised by Core on **background threads** must hop to the UI thread: `Dispatcher.UIThread.Post(() => ...)`.
* New setting? (1) add a property with a default to `AppSettings`, (2) validate it in `SettingsStore.Load()`, (3) add a test that saves, reloads and checks the value.

## 5. Known traps (read before you start)

1. **The 1-second timer in `App.axaml.cs` is not only UI.** `vm.Refresh()` → `MonitorLayout.SetPeers()` → `host.SyncPeers()`, which re-pairs saved computers and starts/stops the KVM. Clipboard polling is in the same tick. **Never pause, skip or stop this timer when the window is hidden.**
2. **Closing the main window currently ends the app.** Do not "fix" background behaviour anywhere except task A10.
3. **Input hooks and `SendInput` only exist on Windows** (`[SupportedOSPlatform("windows")]`). Call them behind `OperatingSystem.IsWindows()`. CA1416 warnings already exist in `App.axaml.cs`: fine.
4. **Tests must never touch the real clipboard, real ports or `%APPDATA%`.** Use `AppFixture` (temp folder, port 0, random discovery port) and `FakeClipboard`.
5. **`FluentTheme` already styles controls for both variants.** Only override what Tokens.axaml needs. The accent override (`SystemAccentColor` etc.) in `App.axaml` stays.
6. **The wire carries Windows virtual-key codes.** Do not change key handling without reading `KeyTranslationService.cs`.
7. **Clipboard is text only today** (`IClipboardAccess` has Get/SetText). Image/HTML work is track D and starts by widening that interface.
8. **Avalonia version is 12.1.6.** If an API from Avalonia 11 docs does not compile, search the repo for how a similar control is used and adapt; do not downgrade packages.
9. **Next.js in `web/` is a newer version than you know.** Read `web/AGENTS.md` and `web/node_modules/next/dist/docs/` before changing web code.
10. **macOS is parked.** Do not edit `MacInput*.cs`, `platform_mac.mm`, or `backlog-macos.md` items unless a card says so.

## 6. Stop-and-ask list
Stop and report (do not guess) when: the card's file does not exist or looks different from what the card says;
a build error survives two fix attempts; a test you did not touch starts failing; the task seems to need a package, a
protocol change or a file outside the card; you are tempted to delete something.

## 7. Executor prompt
The full strict prompt (git, tests, UI, reporting) is in [EXECUTOR_PROMPT.md](./EXECUTOR_PROMPT.md). Short version:

```
You are executing ONE task from the Mouser repo at d:\projects\mouser.
1. Read roadmap/AGENT_GUIDE.md fully.
2. Read roadmap/tasks/<FILE>.md, section "<TASK ID>".
3. Follow its Steps in order. Touch only the listed files. Build after each step.
4. Run its "Done when" commands. Paste their output tails into your final message.
5. Tick the task in ROADMAP.md. Do not start another task.
If blocked, revert your edits, write the error under "Blocked" in ROADMAP.md, and stop.
```
