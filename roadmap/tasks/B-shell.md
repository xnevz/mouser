# Track B: New shell (UniClip-style window, one Devices model, simple Settings)

Read [../AGENT_GUIDE.md](../AGENT_GUIDE.md) first. **Needs track A through A9** (theme tokens in every view).
Design: [../phase-7a-ui-redesign-lock-and-tray.md](../phase-7a-ui-redesign-lock-and-tray.md) §3, §5.
Order: B1 → B2 → B3 → B4 → B5 → B6, then B7 → B8 → B9 → B10, then B11, B12, B13.

Rule for this whole track: **change view models with tests first, then XAML.** Keep the existing nav strings
(`"AndroidRemote"`, `"SmartClipboard"`, `"DisplayMatrix"`, `"Settings"`) working until B9 says otherwise, because
`ViewModelTests.Navigation_SwitchesPagesAndActiveTab` uses them.

---

## B1: Sidebar device list (view model only)

- **Needs:** A9 · **Size:** S · **Files:** create `src/Mouser.UI/ViewModels/SidebarDeviceViewModel.cs`; edit `MainViewModel.cs`; add a test to `ViewModelTests.cs`

**Steps**
1. Create:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
namespace Mouser.UI.ViewModels;

public enum DeviceKind { Computer, Phone }

public partial class SidebarDeviceViewModel : ObservableObject
{
    public string Key { get; }            // device id (computers) or address (phones)
    public DeviceKind Kind { get; }
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isOnline;
    public string Icon => Kind == DeviceKind.Phone ? "📱" : "🖥";
    public string StatusColor => IsOnline ? "#10b981" : "#64748b";

    public SidebarDeviceViewModel(string key, DeviceKind kind) { Key = key; Kind = kind; }
    partial void OnIsOnlineChanged(bool value) => OnPropertyChanged(nameof(StatusColor));
}
```
2. `MainViewModel`: keep the `settings` constructor argument in a field `_settings`. Add `public ObservableCollection<SidebarDeviceViewModel> Devices { get; } = new();` and `[ObservableProperty] private int _onlineDeviceCount;`.
3. In `Refresh()` call a new private `RefreshDevices()` **at the very top** (before any `return`). It must **update in place** (find by `Key`; add if missing; remove if gone; never `Clear()`):
   - Computers online: every `_host.Peers` item → `Key = DeviceId`, `Name = Hostname`, `IsOnline = true`.
   - Computers offline: every `_settings.Current.Peers` entry whose id is **not** in `_host.Peers` → `Name = entry.Value.Name`, `IsOnline = false`.
   - Phones: every `AndroidRemote.Clients` item → `Key = Address`, `Name = Address`, `Kind = Phone`, `IsOnline = IsActive`.
   - `OnlineDeviceCount = Devices.Count(d => d.IsOnline)`.
4. Test `Sidebar_ListsPairedPhoneAsOnline`: use `AppFixture`; pair a `FakePhone` (copy the arrange part of `PhoneRemote_ShowsPinAndListsAPairedPhone`); `app.Main.Refresh()`; assert `Assert.Single(app.Main.Devices)` has `Kind == DeviceKind.Phone`, `IsOnline == true`, and `OnlineDeviceCount == 1`.

**Done when:** build; tests pass (+1).

---

## B2: New sidebar

- **Needs:** B1 · **Size:** M · **Files:** `src/Mouser.UI/Views/MainWindow.axaml`, `src/Mouser.UI/ViewModels/MainViewModel.cs`, `tests/Mouser.Tests/ViewModelTests.cs`

**Target (top to bottom)**
```
[M] Mouser
● All items           492      → opens clipboard, library All
☆ Pinned                0      → opens clipboard, library Starred
DEVICES                  ⌄     (section label)
 ● Laptop-Pro
 ○ Galaxy S22 Ultra
 + Add a phone                 → nav "AndroidRemote"
Manage devices…                → nav "DisplayMatrix"
Settings            Ctrl+,     → nav "Settings"
-------------------------------
● status title / status text   (existing box, unchanged)
```
**Steps**
1. `MainViewModel`: add
```csharp
[RelayCommand]
private void ShowLibrary(string library)
{
    Navigate("SmartClipboard");
    ClipboardHistory.SetLibraryCommand.Execute(library);   // "All" or "Starred"
}
```
2. `MainWindow.axaml`: replace the `StackPanel Grid.Row="1"` buttons with the target above. Reuse `Classes="nav-button"` and `Classes.active="…"`. "All items" is active when `IsClipboardTab` (keep it simple: do not track All vs Pinned active state).
   Devices list: an `ItemsControl ItemsSource="{Binding Devices}"` with an item template `Grid ColumnDefinitions="Auto,*"`: `Ellipse` (`Fill="{Binding StatusColor}"`, 8×8), `TextBlock Text="{Binding Name}"` (`TextTrimming="CharacterEllipsis"`, `Foreground="{DynamicResource TextSecondary}"`). Show `IsVisible="{Binding !Devices.Count}"` hint "No devices yet" when empty (`Classes="hint"`).
3. Add to `<Window>`: 
```xml
<Window.KeyBindings>
  <KeyBinding Gesture="Ctrl+OemComma" Command="{Binding NavigateCommand}" CommandParameter="Settings" />
  <KeyBinding Gesture="Ctrl+F" Command="{Binding FocusSearchCommand}" />
</Window.KeyBindings>
```
   (`FocusSearchCommand` is added in B5; until then omit that line.)
4. Tests: `ShowLibrary("Starred")` → `CurrentPage` is `ClipboardHistoryViewModel` and `ClipboardHistory.SelectedLibrary == "Starred"`.
5. Keep the Phone Remote count badge on "Add a phone".

**Done when:** build; tests pass; app shows the new sidebar in both themes; Ctrl+, opens Settings.

**Do not:** rename `NavigationTab` values or the `Navigate` strings.

---

## B3: Clipboard filters (view model + tests)

- **Needs:** A9 · **Size:** M · **Files:** `src/Mouser.UI/ViewModels/ClipboardHistoryViewModel.cs`, `tests/Mouser.Tests/ViewModelTests.cs` (or `ClipboardTests.cs`)

**Steps**
1. Add to `ClipboardHistoryViewModel`:
```csharp
public static string[] TimeOptions { get; } = { "Any time", "Today", "Yesterday", "Last 7 days", "Last 30 days" };
public ObservableCollection<string> DeviceOptions { get; } = new() { "All devices" };
[ObservableProperty] private string _selectedDevice = "All devices";
[ObservableProperty] private string _selectedTime = "Any time";
partial void OnSelectedDeviceChanged(string value) => _ = ReloadAsync();
partial void OnSelectedTimeChanged(string value) => _ = ReloadAsync();
```
2. In `ReloadAsync()`, after the existing type filter add:
```csharp
if (SelectedDevice != "All devices") items = items.Where(c => c.SourceDevice == SelectedDevice);
var now = DateTime.Now.Date;
items = SelectedTime switch
{
    "Today" => items.Where(c => c.TimestampLocal.Date == now),
    "Yesterday" => items.Where(c => c.TimestampLocal.Date == now.AddDays(-1)),
    "Last 7 days" => items.Where(c => c.TimestampLocal >= now.AddDays(-6)),
    "Last 30 days" => items.Where(c => c.TimestampLocal >= now.AddDays(-29)),
    _ => items
};
```
   Add to `ClipboardItemViewModel`: `public DateTime TimestampLocal => Timestamp.ToLocalTime();` (the existing `Timestamp` is UTC).
3. After building `FilteredClips`, refresh `DeviceOptions`: collect distinct `SourceDevice` from **all** `results` (before device filter), keep `"All devices"` first, add missing, never remove the selected one.
4. Tests: capture two clips with different `SourceDevice` (use `ApplyRemoteClipAsync("Laptop", payload)` for the second) → set `SelectedDevice = "Laptop"` → only that clip remains. Another test: `SelectedTime = "Yesterday"` with only fresh clips → list is empty and `IsEmpty` is true.

**Done when:** build; tests pass (+2).

---

## B4: Day headers (view model + tests)

- **Needs:** B3 · **Size:** S · **Files:** `ClipboardItemViewModel.cs`, `ClipboardHistoryViewModel.cs`, tests

**Steps**
1. `ClipboardItemViewModel`: add
```csharp
public string DayLabel
{
    get
    {
        var d = TimestampLocal.Date; var today = DateTime.Now.Date;
        return d == today ? "Today" : d == today.AddDays(-1) ? "Yesterday" : d.ToString("ddd, MMM d");
    }
}
[ObservableProperty] private bool _showDayHeader;
[ObservableProperty] private int _dayCount;
```
2. In `ReloadAsync()`, after the list is filled: loop `FilteredClips`; for each item whose `DayLabel` differs from the previous item set `ShowDayHeader = true` and `DayCount` = number of consecutive items with that label (count ahead). Others false.
3. Test: three clips: two now, one saved with `TimestampUtc` of two days ago (construct a `ClipboardPayload` and call `_storage.SaveClipAsync` via `app.Storage`). After `ReloadAsync()`: item 0 `ShowDayHeader && DayCount == 2 && DayLabel == "Today"`, item 1 header false, item 2 header true.

**Done when:** build; tests pass (+1).

---

## B5: Clipboard page layout (XAML)

- **Needs:** B3, B4 · **Size:** L (take your time) · **Files:** `src/Mouser.UI/Views/ClipboardHistoryView.axaml`, `ClipboardHistoryView.axaml.cs`, `MainViewModel.cs`

**Target**
```
[ 🔍 Search clips…                              Ctrl+F ]  [Clear history]
[Type ▾] [Device ▾] [Time ▾]
Today · 3 items
 ▣ TXT  69970127                       ASUS · 14:42
 ...                                              | detail pane (340 px, see B6)
```
**Steps**
1. Replace the old header block and the radio-button filter row with:
   - Row 0: `Grid ColumnDefinitions="*,Auto"`: `TextBox x:Name="SearchBox"` (`PlaceholderText="Search clips…"`, `Text="{Binding SearchQuery}"`) and the existing Clear-history button (`Classes="secondary-btn"`).
   - Row 1: three `ComboBox`es, `Width="130"`, `Margin="0,0,8,0"`:
     - Type: items `All, Text, Link, Code` bound with `SelectedItem="{Binding SelectedFilter}"` (these strings already match `SelectedFilter`; "All" = any).
     - Device: `ItemsSource="{Binding DeviceOptions}" SelectedItem="{Binding SelectedDevice}"`.
     - Time: `ItemsSource="{x:Static vm:ClipboardHistoryViewModel.TimeOptions}" SelectedItem="{Binding SelectedTime}"`.
   - Keep Starred/All via sidebar (B2); remove the `lib` radio buttons. Keep the `Starred` logic in the VM.
2. List item template (replace `clip-card` content): a `StackPanel` containing, **first**, the day header
```xml
<StackPanel Orientation="Horizontal" Spacing="8" Margin="4,10,0,4" IsVisible="{Binding ShowDayHeader}">
  <TextBlock Classes="section-title" Text="{Binding DayLabel}" />
  <TextBlock Classes="label" Text="{Binding DayCount, StringFormat='{}{0} items'}" VerticalAlignment="Center" />
</StackPanel>
```
   then the existing row: type badge (30×30, `Background="{Binding BadgeColor}"`, text `{Binding TypeIcon}` in `OnAccent`), title, and a second line `SourceDevice · time` (`{Binding SourceDevice}` + `{Binding FormattedTime}`), star at the right.
   Use short type labels in the badge: add `ClipboardItemViewModel.TypeBadge` returning `"TXT"`, `"URL"`, `"CODE"`, `"IMG"`, `"FILE"` and bind to it instead of `TypeIcon`.
3. Ctrl+F: in `MainViewModel` add `public event Action? FocusSearchRequested;` and `[RelayCommand] private void FocusSearch() { Navigate("SmartClipboard"); FocusSearchRequested?.Invoke(); }`. In `MainWindow.axaml.cs` subscribe in `DataContextChanged` and call `(this.FindControl<…>)` is not possible across views: instead in `ClipboardHistoryView.axaml.cs` expose `FocusSearch()` and have `MainWindow` find the current page content: `if (this.GetVisualDescendants().OfType<ClipboardHistoryView>().FirstOrDefault() is { } v) v.FocusSearch();`. Add the Ctrl+F `KeyBinding` from B2.
4. Add `<TextBlock Classes="hint" … Text="{Binding QuickPasteHint}">` under the filters (existing).
5. Empty state stays (centered hint).

**Done when:** build; 130+ tests pass; screenshot-check in Light and Dark: filters work, day headers show, Ctrl+F focuses search; grep for raw hex in this file prints nothing.

---

## B6: Detail pane

- **Needs:** B5 · **Size:** M · **Files:** `ClipboardHistoryView.axaml`, `ClipboardItemViewModel.cs`

**Target**
```
[ Copy ]  ☆  🗑              (primary button + two icon-style secondary buttons)
TXT · This device · Nov 12, 14:43        (type · source · time, `label` class)
┌──────────────────────────────┐
│ monospace content, wraps      │
└──────────────────────────────┘
[ Characters 8 ] [ Words 1 ] [ Lines 1 ]  (three small cards)
```
**Steps**
1. Action row on **top** of the pane (move buttons from the bottom). Keep commands: `CopyClipCommand`, `ToggleStarCommand`, `DeleteClipCommand`, all with `CommandParameter="{Binding SelectedClip}"`. Star and Delete become compact secondary buttons with `ToolTip.Tip` "Pin (keep forever)" / "Delete" and `AutomationProperties.Name`.
2. Metadata line: `{Binding SelectedClip.TypeBadge}` · `{Binding SelectedClip.SourceDevice}` · `{Binding SelectedClip.FormattedTime}`.
3. Three metadata cards: `Border` with `Background="{DynamicResource BgRaised}"`, `CornerRadius="8"`, `Padding="12,8"`, label (class `label`) over value (`FontWeight="SemiBold"`), bound to `CharCount`, `WordCount`, `LineCount`.
4. Empty selection: whole pane shows a hint "Select an item to see it here." (`IsVisible` on `SelectedClip` null).

**Done when:** build; tests pass; both themes readable.

---

## B7: Editable device name

- **Needs:** A9 · **Size:** S · **Files:** `SettingsStore.cs`, `MouserHost.cs`, `SettingsViewModel.cs`, `SettingsView.axaml`, tests

**Steps**
1. `AppSettings`: `public string DeviceName { get; set; } = "";` (empty = use machine name).
2. `MouserHost`: add `public string DeviceName => string.IsNullOrWhiteSpace(_settings.Current.DeviceName) ? Environment.MachineName : _settings.Current.DeviceName.Trim();` and use it instead of `Environment.MachineName` in `Restart()` (discovery hostname) and `ShareClipboardAsync` (`SourceDevice`). `ClipboardHistoryViewModel.CaptureAsync` also uses `Environment.MachineName` for `SourceDevice`: change to a `Func<string> DeviceName` property set by `MainViewModel` (`ClipboardHistory.DeviceName = () => host.DeviceName;`, default `() => Environment.MachineName`).
3. `SettingsViewModel`: `[ObservableProperty] private string _deviceName;` initialised from `s.DeviceName`; on change (trim, max 40 chars) save and call `_host.Restart()` **only when the user presses an "Apply" button** (so it does not restart on every keystroke): command `ApplyDeviceName`.
4. `SettingsView.axaml`: TextBox + Apply button in a new "This device" card (setting-row pattern) with hint "How this computer appears on your other devices."
5. Test: set `DeviceName = "Desk"`, `ApplyDeviceNameCommand.Execute(null)`; `Host.DeviceName == "Desk"` and `Host.Discovery` beacons carry it (assert via `app.Host.Discovery` local info if exposed; otherwise assert `Host.DeviceName` only).

**Done when:** build; tests pass.

---

## B8: Devices view model

- **Needs:** B1, B7 · **Size:** M · **Files:** create `src/Mouser.UI/ViewModels/DevicesViewModel.cs`; edit `MainViewModel.cs`; test

**Idea:** the Devices page is a left list and a right "sheet". It reuses the existing `MonitorLayoutViewModel` (computers, pairing, placement, files, layout canvas) and `AndroidRemoteViewModel` (PIN, phones); it adds no new network code.

**Steps**
1. Create `DevicesViewModel : ViewModelBase` with constructor `(MouserHost host, MonitorLayoutViewModel layout, AndroidRemoteViewModel remote, SettingsStore settings)` and properties:
   - `public MonitorLayoutViewModel Layout { get; }`, `public AndroidRemoteViewModel Remote { get; }`
   - `public string ThisDeviceName => host.DeviceName;` (refresh on `Refresh()`)
   - `[ObservableProperty] private object? _selected;` (a `PeerViewModel`, a `RemoteClientItem`, or the string `"this"`)
   - `public bool IsThisDeviceSelected => Selected is string;` etc. (use computed bool properties + `[NotifyPropertyChangedFor]`)
   - `[RelayCommand] private void Select(object? item) => Selected = item;`
2. `MainViewModel`: create `public DevicesViewModel DevicesPage { get; }` **after** `AndroidRemote` and `MonitorLayout` exist. Do not change `Navigate` yet.
3. Test: `Select` a peer from `MonitorLayout.Peers` (create via `app.Main.MonitorLayout.SetPeers(new[]{ new DiscoveredPeer{ DeviceId="x", Hostname="Laptop", IpAddress=IPAddress.Loopback, MotionPort=1 } })`) and check `Selected` updates.

**Done when:** build; tests pass.

---

## B9: Devices page (XAML) and navigation switch

- **Needs:** B2, B8 · **Size:** L · **Files:** create `src/Mouser.UI/Views/DevicesView.axaml` + `.axaml.cs`; edit `MainViewModel.cs`, `MainWindow.axaml`, `tests/Mouser.Tests/ViewModelTests.cs`

**Target sheet (right side) for a computer:**
```
Laptop-Pro                         ● Paired
Windows · 192.168.1.14
Share mouse & keyboard    [on/off]       (on = placement != None)
Position            [ Right ▾ ]          (only when on)
[Send file…]  [Forget]
▸ Advanced
```
For an unpaired computer: "Enter the PIN shown on that computer" + PIN box + **Pair** button (existing `PinText`/`PairCommand`).
For a phone: address, last seen, last event, **Unpair** button (existing `ForgetCommand`).
For "This device": name (B7), the PIN box (`Remote.PairingPinText` + New PIN), addresses, accept-phones toggle (`Remote.IsAccepting` + `ToggleAcceptingCommand`).

**Steps**
1. `DevicesView.axaml` (`x:DataType="vm:DevicesViewModel"`): `Grid ColumnDefinitions="260,*"`.
   Left: "This device" row, then "Computers" (`ItemsControl` over `Layout.Peers`), "Phones" (`ItemsControl` over `Remote.Clients`). Each row is a `Button Classes="nav-button"` with status dot + name, `Command="{Binding $parent[UserControl].((vm:DevicesViewModel)DataContext).SelectCommand}" CommandParameter="{Binding}"`.
   Right: a `ContentControl Content="{Binding Selected}"` with `ContentControl.DataTemplates` for `vm:PeerViewModel`, `vm:RemoteClientItem`, and a `x:String` template for `"this"`. Bind to the members listed above.
   Bottom (collapsed): `<Expander Header="Screen layout">` hosting `<views:MonitorLayoutView DataContext="{Binding Layout}" />` (add `xmlns:views="using:Mouser.UI.Views"`).
2. Follow the checklist in guide §4.3: ≤ 6 visible controls per sheet; put placement details under **Advanced**.
3. `MainViewModel.Navigate`: map `"DisplayMatrix"` **and** `"AndroidRemote"` to `DevicesPage` (and select the phone section for `"AndroidRemote"`). `CurrentTab` values stay; set `CurrentTab` to `DisplayMatrix` for both.
4. **Update the existing test** `Navigation_SwitchesPagesAndActiveTab`: `"AndroidRemote"` now yields `DevicesViewModel` (`Assert.IsType<DevicesViewModel>`), and `IsRemoteTab` assertions change to `IsDisplaysTab`. Also the initial page: `CurrentPage` starts as `ClipboardHistory` (clipboard is the main screen): change constructor to `_currentPage = ClipboardHistory; _currentTab = NavigationTab.SmartClipboard;` and update `NoFakeDataOnFirstRun` only if it fails. These are the **only** test edits allowed in this task.
5. Remove the now-unused `AndroidRemoteView` from the nav only (keep the file; its XAML content is reused in the "This device" sheet by copying the PIN card). Delete nothing.

**Done when:** build; tests pass (edited ones included); manual: pair the two instances (`scripts\test-kvm-single-pc.ps1`) through the new page; placement change works; Forget works; phone listed after pairing.

---

## B10: Add a computer by IP (works across subnets and VPN)

- **Needs:** B9 · **Size:** L · **Risk:** medium (touches Core discovery) · **Files:** `src/Mouser.Core/Networking/PeerDiscoveryService.cs`, `src/Mouser.UI/Services/MouserHost.cs`, `SettingsStore.cs`, `DevicesViewModel.cs`, `DevicesView.axaml`, `tests/Mouser.Tests/PeerDiscoveryTests.cs`

**Why:** discovery is UDP broadcast only. There is no reply mechanism, so computers on a different subnet, VLAN, guest Wi-Fi or Tailscale are never found.

**Design (small):** each node can send its beacon by **unicast** to extra addresses. A node that receives a beacon from an address it does not know adds that sender to its own unicast list, so adding the other computer on one side is enough.

**Steps**
1. `PeerDiscoveryService`: add `private readonly ConcurrentDictionary<IPEndPoint, byte> _manualTargets = new();` and `public void AddTarget(IPAddress address, int port = DefaultDiscoveryPort)`; in `BroadcastBeaconLoopAsync` after the broadcast loop, also `SendAsync` the same payload to each manual target (catch `SocketException`, ignore). In the receive loop, when a beacon parses and `peer.DeviceId` is not our own id, call `AddTarget(peer.IpAddress, _discoveryPort)` once per sender address (keep a `HashSet<IPAddress>` of learned senders so it is added only once). This makes discovery two-way even if only one side was told the other's address. Sending a unicast beacon to a computer that is already reached by broadcast is harmless (one extra small packet per second).
   (shortcut: learned targets are never expired; fine for a LAN, revisit if it leaks memory.)
2. `AppSettings`: `public List<string> ManualPeers { get; set; } = new();` (`"host-or-ip"` or `"host:port"`). `SettingsStore.Load`: `settings.ManualPeers ??= new();`.
3. `MouserHost`: `public async Task<bool> AddManualPeerAsync(string text)`: parse `host[:port]`; resolve with `Dns.GetHostAddressesAsync` (IPv4 first); on failure return false; `Discovery?.AddTarget(ip, port)`; add to `ManualPeers` (no duplicates), save. In `Restart()` after `Discovery.Start()` re-add every saved manual peer.
4. `DevicesViewModel`: `[ObservableProperty] private string _manualAddress = ""; [ObservableProperty] private string? _manualError;` and `[RelayCommand] private async Task AddByAddressAsync()` calling the host; message on failure: "Couldn't find that address. Check the name or IP and that Mouser is running there."; on success: "Added. It appears in the list within a few seconds."
5. `DevicesView.axaml`: under the left list add a row: `TextBox PlaceholderText="Add by IP or name (e.g. 192.168.1.20)"` + **Add** button (`Classes="secondary-btn"`).
6. Test (`PeerDiscoveryTests.cs`): copy an existing two-service test; give service A and B **different** discovery ports, call `a.AddTarget(IPAddress.Loopback, bPort)`; assert B discovers A within 3 s and (learned target) A discovers B within 3 s.

**Done when:** build; tests pass (+1); manual on two real PCs with different subnets or Tailscale: Add by IP on one side → both list each other.

---

## B11: Simple Settings

- **Needs:** A10, A12, B7 · **Size:** M · **Files:** `src/Mouser.UI/Views/SettingsView.axaml` (and VM only if a property is missing)

**Target (5 cards, Advanced collapsed in each that needs it)**
1. **Appearance**: Theme combo.
2. **General**: Start Mouser when I sign in · Keep running in the tray when I close the window · Hide to the tray when I minimize the window · Run Mouser as administrator (E18; shows status and **Restart as administrator**) · Warn me when a window can't receive input (E16) · Show notifications. *Advanced:* Start minimized.
3. **Clipboard**: Save what I copy · Share with my other devices · Skip passwords and secrets. *Advanced:* delete unstarred clips after N days.
4. **Sharing**: Accept phones (the existing `AcceptRemoteInput`, via `AndroidRemoteViewModel.ToggleAcceptingCommand` or a mirrored property). *Advanced:* connection port + Apply.
5. **This device**: name + Apply (B7) · data folder (selectable text).

**Steps:** rebuild `SettingsView.axaml` from the **Setting row** and **Card** patterns (guide §4.4). Every row has a one-line hint. Move the port control and the retention control into `<Expander Header="Advanced">`. Keep every existing binding name.

**Done when:** build; tests pass; at default window size the page shows ≤ 6 controls per card without scrolling; both themes.

---

## B12: Activity page and "Copy diagnostics"

- **Needs:** B9 · **Size:** M · **Files:** create `src/Mouser.UI/Services/AppLog.cs`, `src/Mouser.UI/ViewModels/ActivityViewModel.cs`, `src/Mouser.UI/Views/ActivityView.axaml` (+ `.cs`); edit `MouserHost.cs`, `MainViewModel.cs`, `MainWindow.axaml`; test

**Steps**
1. `AppLog`: static class with a `ConcurrentQueue<string>` capped at 500 and `public static void Write(string message)` (prefix `DateTime.Now:HH:mm:ss`), plus `public static event Action<string>? Written;` and `public static string[] Snapshot()`.
2. Add `AppLog.Write(...)` at: peer found/lost (compare `Peers` between `SyncPeers` calls), pairing success/failure (`PairPeerAsync`, `RepairAsync`), KVM enter/return (`Kvm.StateChanged` handler in `App.axaml.cs`), file sent/received, clipboard sent/received, `InputError`/`DiscoveryError`/`KvmError` when they change. One line each, plain words.
3. `ActivityViewModel`: `ObservableCollection<string> Lines` filled from `Snapshot()` and appended via `Written` (`Dispatcher.UIThread.Post`); `[RelayCommand] CopyDiagnosticsAsync` builds a text block: app version (`Assembly` version), OS, device name, ports (`Host.Input?.BoundPort`, `Host.Transfer?.BoundPort`), local IPs, peers with status, last 50 log lines; puts it on the clipboard through `IClipboardAccess` (inject; use `ClipboardHistoryViewModel.CopyTextAsync`).
4. `ActivityView.axaml`: a `ListBox` (monospace) + **Copy diagnostics** button + a hint "Share this when asking for help." Reachable from Settings → Advanced link **"Open activity log"** (no sidebar entry: keeps the sidebar simple). Navigation string `"Activity"` in `MainViewModel.Navigate`.
5. Test: `AppLog.Write("x")` then `Snapshot()` contains it; the diagnostics text contains the device name and the word "Ports".

**Done when:** build; tests pass (+2).

---

## B13: First-run wizard

- **Needs:** B9, B11 · **Size:** M · **Files:** create `OnboardingViewModel.cs`, `Views/OnboardingView.axaml`; edit `SettingsStore.cs` (`bool FirstRunDone`), `MainViewModel.cs`, `MainWindow.axaml`; test

**Behaviour:** shown over the main window the first time only. 3 steps, big text, one button each:
1. **Name this computer** (TextBox prefilled with machine name) → *Next*.
2. **What will you use?** three toggles: Share clipboard · Share mouse and keyboard · Use my phone as a remote → *Next*. Maps to `ClipboardSyncEnabled`, `KvmEnabled`, `AcceptRemoteInput`.
3. **Pair a device**: shows the PIN (large) and "Open Mouser on your other computer or phone on the same Wi-Fi." with live count of discovered devices → *Done* (sets `FirstRunDone = true`).
A **Skip** link on every step sets `FirstRunDone = true`.

**Steps:** `MainViewModel.Onboarding` (null after done); `MainWindow.axaml` overlays `<ContentControl Content="{Binding Onboarding}" IsVisible="{Binding IsOnboarding}" />` on the content grid (`Grid.ColumnSpan="2"`, `Background="{DynamicResource BgApp}"`). Existing installs: `FirstRunDone` defaults **false**, so in `SettingsStore.Load` set `FirstRunDone = true` when the settings file already existed (do not nag existing users).

**Test:** new store → `Onboarding` not null; Next ×2 then Done → `FirstRunDone` saved true and `Onboarding` null; existing settings file → no onboarding.

**Done when:** build; tests pass; manual run with a fresh `--data-dir`.
