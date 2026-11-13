# Track E: Mouse and keyboard sharing (ShareMouse level)

Read [../AGENT_GUIDE.md](../AGENT_GUIDE.md) first. Feature list: [../phase-7-feature-parity-gaps.md](../phase-7-feature-parity-gaps.md) Phase 9.
Option mapping from the ShareMouse screenshots: [../phase-7a-ui-redesign-lock-and-tray.md](../phase-7a-ui-redesign-lock-and-tray.md) §2.
**Needs:** A9 (settings pages exist) and B9 (Devices page). Everything here is **Windows first**; macOS stays in the backlog.

Facts about today's code (verified by reading it):
- `KvmController` (`src/Mouser.Core/Kvm/KvmController.cs`) owns a `VirtualKvmManager _edges`, a `_peers` dictionary, and the hooked input handler `Handle(CapturedInput)`. It runs on the **hook thread**: keep work in it tiny; never block, never allocate in the mouse-move path.
- `VirtualKvmManager` has `EdgeResistanceMs = 80` and `EdgePushDistanceThreshold = 15`. A crossing happens when the cursor has pushed for ≥ 80 ms **or** ≥ 15 px (OR, not AND). Edge margin is 3 px.
- `FindAdjacentScreen` picks the **first** remote screen on that side and does **not** check that it overlaps the exit point along the edge. With different heights you can cross where the other computer has no screen. E9 fixes this.
- `CoordinateTransformationEngine.CalculateEntryPosition` maps **proportionally** (ratio of the edge), so screens of different size are already "equalised".
- Hotkeys are constants in `KvmController`: Ctrl+Alt+Esc or Scroll Lock (`IsHotkey`).
- Local desktop = bounding box of all monitors (`MonitorLayoutViewModel.LocalDesktop`).
- Remote input is applied by `InputInjectionService` (cases `TypeButton`, `TypeKey`, `TypeScroll`, `TypeMoveAbsolute`) → `IInputSink`.
- Tests: `KvmControllerTests.cs`, `VirtualKvmTests.cs`, `TwoComputerTests.cs`. Look at them before changing `KvmController`; add tests in the same style.

Order: E1 → E2 → E3 → E4, then E5–E8 any order, E9 → E10 → E11 (geometry chain), E12, then E13–E18. **E16 → E18 fix the "doesn't work in administrator windows" problem and can be done early (right after A12 and A11).**

---

## E1: Switching feel (presets) and a `KvmOptions` object

- **Needs:** B11 · **Size:** M · **Files:** create `src/Mouser.Core/Kvm/KvmOptions.cs`; edit `KvmController.cs`, `SettingsStore.cs`, `MouserHost.cs`, `SettingsViewModel.cs`, `SettingsView.axaml` (Sharing card), tests in `KvmControllerTests.cs`

**Steps**
1. Core class (mutable, thread-safe by replacing the whole object):
```csharp
namespace Mouser.Core.Kvm;

public sealed class KvmOptions
{
    public int EdgeResistanceMs { get; init; } = 80;
    public double EdgePushPixels { get; init; } = 15;

    public static KvmOptions FromFeel(string feel) => feel switch
    {
        "Relaxed" => new() { EdgeResistanceMs = 0, EdgePushPixels = 1 },
        "Careful" => new() { EdgeResistanceMs = 300, EdgePushPixels = 60 },
        _ => new() // "Normal"
    };
}
```
2. `KvmController.ApplyOptions(KvmOptions o)`: under `_lock` copy values into `_edges.EdgeResistanceMs` / `_edges.EdgePushDistanceThreshold`.
3. `AppSettings`: `public string SwitchFeel { get; set; } = "Normal";` (validate in `Load` against the 3 names). `MouserHost` calls `Kvm?.ApplyOptions(KvmOptions.FromFeel(...))` in its constructor and in a new `SetSwitchFeel(string)` that also saves.
4. UI: Settings → **Sharing** card: a segmented control made of three `RadioButton`s (GroupName `feel`) "Relaxed / Normal / Careful" with hint "How hard you must push the cursor against the edge before it moves to the other computer." Bound through `SettingsViewModel.SwitchFeel` + `OnSwitchFeelChanged` → `_host.SetSwitchFeel`.
5. Test: `FromFeel("Careful").EdgeResistanceMs == 300`; unknown string → Normal; with `Relaxed` a single move past the edge crosses at once (copy the setup of an existing crossing test).

**Done when:** build; tests pass (+3).

---

## E2: Prevent switching when it would be annoying

- **Needs:** E1 · **Size:** L · **Files:** create `src/Mouser.Core/Kvm/SwitchPolicy.cs`, `src/Mouser.UI/Services/ForegroundWindowInfo.cs`; edit `KvmController.cs`, `KvmOptions.cs`, `SettingsStore.cs`, Settings VM/view; tests `SwitchPolicyTests.cs`

**Options (all default off except the first)** — taken from the ShareMouse "Prevent switching between displays" box:
`BlockInFullscreen` (default **on**), `BlockInMaximized`, `BlockInCorners`, `RequireModifier` (`None|Ctrl|Alt|Shift`), `DoubleTapOverride` (default on).

**Pure policy first (testable):**
```csharp
public readonly record struct SwitchContext(
    bool ForegroundFullscreen, bool ForegroundMaximized,
    double CursorX, double CursorY, ScreenBounds Local,
    bool CtrlDown, bool AltDown, bool ShiftDown, DateTime Now);

public sealed class SwitchPolicy
{
    public SwitchPolicy(KvmOptions options);
    /// <summary>True when the cursor may leave through this edge now.</summary>
    public bool Allows(in SwitchContext c);
}
```
Rules: `BlockInFullscreen && c.ForegroundFullscreen` → deny; `BlockInMaximized && c.ForegroundMaximized` → deny; `BlockInCorners` and the cursor is within **80 px** of a corner of `Local` (both |dx|<80 and |dy|<80 to a corner) → deny; `RequireModifier != None` and that modifier is not down → deny. **Double-tap override:** when a crossing was denied, remember `Now`; if another crossing attempt (a new contact: the cursor left the edge zone and came back) occurs within **400 ms** of a denied one → allow. Implement with a `DateTime _lastDenied` field; `Allows` updates it.
Tests (≥ 8): each rule alone, combinations, corner geometry on 1920×1080, double-tap inside and outside 400 ms, modifier cases.

**Windows probe:** `ForegroundWindowInfo.Get()` returns `(bool Fullscreen, bool Maximized)`: `GetForegroundWindow` → `IsZoomed(hwnd)` (maximized), and fullscreen = `GetWindowRect` equals the monitor rect from `MonitorFromWindow` + `GetMonitorInfo` **and** the window has no `WS_CAPTION` style (`GetWindowLong GWL_STYLE & 0x00C00000 == 0`). Exclude the desktop/shell windows (class names `Progman`, `WorkerW`, `Shell_TrayWnd`).

**Integration:** in `KvmController.HandleLocked` (the `_active == null` branch) call the policy **only when the cursor is within 3 px of an edge** (the same margin `VirtualKvmManager` uses): evaluate `ForegroundWindowInfo.Get()` lazily and **at most every 100 ms** (cache), because it is a syscall in a hook. If `Allows` is false, do not call `ProcessLocalMotion`. Expose a `Func<(bool,bool)> ForegroundProbe` on the controller so tests can fake it.

**UI:** Settings → Sharing → **Advanced** expander: five checkboxes with plain hints, e.g. "Don't switch while a full-screen app or game is in front".

**Done when:** build; tests pass (+8); manual: open a full-screen video, push the cursor to the edge: it does not switch; double-tap override works.

---

## E3: Hotkeys page and configurable hotkeys

- **Needs:** D15's `HotkeyParser` (create it here if D15 is not ✅) · **Size:** L · **Files:** create `src/Mouser.Core/Input/HotkeyBinding.cs` (+ `HotkeyParser` if missing), `src/Mouser.UI/ViewModels/HotkeysViewModel.cs`, `Views/HotkeysView.axaml`; edit `KvmController.cs`, `SettingsStore.cs`, `QuickPasteHotkey.cs`; tests

**Hotkey model**
```csharp
public readonly record struct HotkeyBinding(ushort Key, bool Ctrl, bool Alt, bool Shift, bool Win)
{
    public static bool TryParse(string text, out HotkeyBinding b);   // "Ctrl+Alt+Esc", "Scroll Lock", "Win+Alt+V", "F13"
    public override string ToString();
    public bool Matches(ushort key, IReadOnlySet<ushort> physicalKeysDown);
}
```
Key names: A–Z, 0–9, F1–F24, `Esc, Tab, Space, Enter, Insert, Delete, Home, End, PageUp, PageDown, Up, Down, Left, Right, Scroll Lock, Pause`. Case-insensitive. Tests: round trip, invalid strings, modifier-only → false.

**Settings (`AppSettings.Hotkeys` = `Dictionary<string,string>`)** with defaults:
| id | default | meaning |
| :--- | :--- | :--- |
| `panic` | `Esc` (double press, see below) | return control at once |
| `returnLock` | `Ctrl+Alt+Esc` | return control; when local, lock cursor to this screen |
| `lockCursor` | `Scroll Lock` | same as above |
| `toggleSharing` | *(none)* | pause/resume sharing |
| `quickPaste` | `Win+Alt+V` | quick-paste overlay |
| `jump1`…`jump9` | `Ctrl+Alt+1`…`9` | jump to the Nth computer (E4) |
| `nextComputer` | `Ctrl+Alt+Right` | jump to the next one (E4) |

**Panic key:** "Double-press required" (ShareMouse): the panic binding fires only when pressed twice within 600 ms (setting `PanicDoublePress`, default true). Only while **controlling a peer** (never swallow Esc locally).

**Steps**
1. Replace `IsHotkey` in `KvmController` with a lookup in a `Dictionary<string,HotkeyBinding>` set through `KvmController.SetHotkeys(...)` (under `_lock`). The check stays on the hook thread: just compare, no allocation. Keep the old defaults so `KvmControllerTests` (hotkey tests) still pass **unchanged**.
2. `QuickPasteHotkey`: read the binding at registration; changing it re-registers (`Dispose` + `new`) from `App.axaml.cs`.
3. `HotkeysViewModel` + `HotkeysView`: a list of rows `Name · hint · [button showing the current hotkey]`. Clicking the button enters "press the new hotkey" mode: a `KeyDown` handler on the button builds a `HotkeyBinding` from `e.Key` + `e.KeyModifiers`, rejects modifier-only presses, shows **conflict** text if the binding is already used by another row ("Already used by Quick paste"), and has an ✕ to clear. **Reset to defaults** button. Reached from Settings → Sharing → "Hotkeys…" link (nav string `"Hotkeys"`; no sidebar entry).
4. Convert Avalonia `Key` → Windows VK with a small switch (letters, digits, F-keys, the named keys above); write that as a pure function with tests.

**Done when:** build; tests pass; manual: change the return hotkey, verify it returns control; conflicts are refused.

---

## E4: Jump hotkeys ("QuickJump") and "next computer"

- **Needs:** E3 · **Size:** M · **Files:** `KvmController.cs`, `MouserHost.cs`, `HotkeysViewModel.cs`, tests in `KvmControllerTests.cs`

**Steps**
1. `KvmController.JumpTo(string peerId)`: under `_lock`; if the peer exists and `Link.IsAlive`: if another peer is active, `ReturnToLocal(null)` first; set `_active = peer`; `_virtualX/Y` = centre of `peer.Remote`; send `TypeMoveAbsolute`; `_capture.WarpCursor(_parkX, _parkY)`; mark state dirty; also call `_edges` so its `IsRedirected` is true: add `public void ForceRedirect(string hostId)` to `VirtualKvmManager` that sets `IsRedirected = true; CurrentFocusedHostId = hostId`.
2. `KvmController.JumpNext()`: peers ordered by name; next after the active one (from local → first).
3. In `HandleLocked`'s key branch (before the hotkey for return): check `jump1..9` and `nextComputer` bindings **on key down only**, swallow the key.
4. Peer order for `jumpN`: alphabetical by `Name` among peers with placement ≠ None (document this in the Hotkeys page hint).
5. Tests: with two fake peers, `JumpTo("a")` → `ActivePeerId == "a"` and a `MoveAbsolute` packet at the centre was sent; `JumpNext` cycles a → b → local.

**Done when:** build; tests pass (+3).

---

## E5: Mouse speed, scroll speed, invert scroll (per device)

- **Needs:** B9 · **Size:** M · **Files:** create `src/Mouser.Core/Kvm/MotionScaler.cs`, tests; edit `KvmController.cs`, `SettingsStore.cs` (`PeerSettings`), `MouserHost.cs`, `DevicesView.axaml` (Advanced)

**Pure first**
```csharp
public sealed class MotionScaler
{
    public double Speed { get; set; } = 1.0;       // 0.25 .. 4.0
    public bool Invert { get; set; }
    /// <summary>Scales a delta, carrying the fractional remainder so slow movement is not lost.</summary>
    public int Apply(int delta);
}
```
`Apply`: `double v = delta * Speed + _remainder; int r = (int)Math.Truncate(v); _remainder = v - r; return Invert ? -r : r;`. Tests: speed 0.5 over 10 deltas of 1 sums to 5; 2.0 doubles; invert flips sign; remainder never accumulates past 1.

**Integration**
1. `PeerSettings`: `double MouseSpeed = 1, ScrollSpeed = 1; bool InvertScroll;` (clamp in `Load`).
2. `KvmController` keeps three scalers per active peer (move X/Y share one; vertical scroll; horizontal scroll). Move path: `_virtualX += mouse.Apply(e.Dx)` (replace the raw delta), scroll path: `ClampShort(vscroll.Apply(e.Dy))`.
3. Device sheet → Advanced: three controls: Mouse speed `Slider` (0.25–4, step 0.25, with the value shown), Scroll speed `Slider`, `CheckBox` "Reverse scroll direction". Applied live: `MouserHost.SetPeerMotion(deviceId, speed, scrollSpeed, invert)` → controller.

**Done when:** build; tests pass (+4).

---

## E6: Coalesce mouse moves (smooth with 1000 Hz mice)

- **Needs:** none · **Size:** M · **Files:** create `src/Mouser.Core/Kvm/MoveCoalescer.cs`, tests; edit `KvmController.cs`

**Why:** every hook event sends one UDP packet. Because moves are **absolute**, only the latest position matters.

**Design:** `MoveCoalescer(Action<short,short> send, int minIntervalMs = 2, Func<long>? clock = null)`:
- `Post(x, y)`: if `now - lastSent >= minInterval` → send now; else remember as pending.
- `Flush()`: if pending → send it. Call `Flush()` **before** sending any button/key/scroll packet (keeps order) and on return to local.
- A 4 ms `System.Threading.Timer` (owned by the controller, started on `EnterPeer`, stopped on return) calls `Flush()` so the last position is never stuck.
Tests (fake clock): 1000 posts within 100 ms → ≤ 60 sends; last position always eventually sent; `Flush` before button preserves order.

**Integration:** in `KvmController` Move case replace `link.Send(TypeMoveAbsolute…)` with `_coalescer.Post(...)`; call `_coalescer.Flush()` at the top of Button/Wheel/Key cases and in `ReturnToLocal`. Add a lock-free path: the coalescer is only touched under `_lock` (already held).

**Done when:** build; tests pass (+3); `KvmControllerTests` still green (they assert packets: update an expectation **only** if it counted one packet per move and say so in the notes).

---

## E7: Dim inactive displays (who has control)

- **Needs:** A12 · **Size:** M · **Files:** create `src/Mouser.UI/Views/DimOverlayWindow.axaml(.cs)`, `src/Mouser.UI/Services/ClickThrough.cs`; edit `App.axaml.cs`, `SettingsStore.cs`, Settings Advanced

**Behaviour:** while this computer is **controlling a peer**, every local monitor is covered by a semi-transparent black overlay (default 40 %) and an "Controlling {name}" pill at the top centre; it disappears when control returns. Input must pass through the overlay.

**Steps**
1. `DimOverlayWindow`: `WindowDecorations="None" ShowInTaskbar="False" Topmost="True" CanResize="False" Background="Black" Opacity="0.4"`, `IsHitTestVisible="False"`.
2. `ClickThrough.Apply(Window w)` (Windows): get the HWND via `w.TryGetPlatformHandle()?.Handle`, then `SetWindowLongPtr(h, GWL_EXSTYLE, old | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE)`; call after `Show()`.
3. `App.axaml.cs`: on `Kvm.StateChanged` (already hooked) → `Dispatcher.UIThread.Post`: if `Kvm.ActivePeerName != null` and the option is on → create one overlay per `window.Screens.All` (position/size from `Screen.Bounds`; mind `Scaling`: set `Position = new PixelPoint(b.X, b.Y)` and `Width/Height = b.Width/scaling`), else close them all.
4. Settings → Sharing → Advanced: toggle "Dim this computer's screens while controlling another" (default on) and a `Slider` 0–70 %.
5. Never take focus: `ShowActivated="False"`.

**Done when:** build; tests pass; manual: hop to the other PC, screens dim, mouse clicks do not hit the overlay; return, overlay gone.

---

## E8: Wrap-around and "keep pixel alignment"

- **Needs:** E1 · **Size:** S · **Files:** `KvmController.cs`, `KvmOptions.cs`, `CoordinateTransformationEngine.cs`, tests

**Steps**
1. Wrap (`KvmOptions.WrapAround`, default off): in `HandleLocked` when `_active == null`, a `Move` event leaves the local desktop at an edge that has **no peer**: warp the cursor to the opposite edge (`_capture.WarpCursor`), x or y mirrored (`Right - 4`, `X + 3`). Only when the push exceeds the E1 resistance.
2. Alignment (`KvmOptions.ProportionalEntry`, default true = today's behaviour): when false, `EntryY = sourceExitY - source.Y + target.Y` (clamped into the target) instead of the ratio. Add an optional parameter `bool proportional = true` to `CalculateEntryPosition` (existing tests call it without the parameter).
3. Settings → Sharing → Advanced: "Wrap the pointer around at the screen edges" and "Scale the cursor position between different-size screens" (default on).
4. Tests: wrap sends the cursor to the opposite edge and never enters the peer; pixel alignment case: source 1080 high, target 1440 high, exit y=540 → target y=540 (not 720).

**Done when:** build; tests pass (+3).

---

## E9: Per-monitor geometry (pure model first)

- **Needs:** E1 · **Size:** L (split into E9a–E9d; do them in order, each is a separate commit)

### E9a: Pure edge map
- **Files:** create `src/Mouser.Core/Geometry/EdgeMap.cs`, `tests/Mouser.Tests/EdgeMapTests.cs`
- **API:**
```csharp
public sealed record Monitor(double X, double Y, double Width, double Height);

public static class EdgeMap
{
    /// <summary>
    /// Local monitors and a peer's placed monitors share one coordinate plane. Given a cursor at (x,y) moving
    /// toward <paramref name="edge"/>, returns the peer monitor that is directly across that edge at that
    /// position (overlapping along the edge), or null when the cursor would leave into empty space.
    /// </summary>
    public static (int PeerMonitorIndex, double EntryX, double EntryY)? FindCrossing(
        IReadOnlyList<Monitor> local, IReadOnlyList<Monitor> peerPlaced, double x, double y, ScreenEdge edge);

    /// <summary>True when (x,y) lies on an outer edge of some local monitor (not an edge shared with another local monitor).</summary>
    public static bool IsOuterEdge(IReadOnlyList<Monitor> local, double x, double y, ScreenEdge edge);
}
```
- **Tests (≥ 8):** one monitor each side by side; peer taller than local (cursor in the top half crosses, bottom half too, entry y proportional to nothing: **pixel-aligned in the plane**); peer shorter (cursor below the peer's bottom → null); two local monitors stacked and the right-most only has a peer next to the upper one; inner edge between two local monitors → `IsOuterEdge` false; gap of 0–2 px counts as adjacent (tolerance 2 px), a 10 px gap does not.

### E9b: Send all monitors in the discovery beacon
- **Files:** `src/Mouser.Core/Networking/PeerDiscoveryService.cs` (`DiscoveryDto`, `CreateBeaconPayload`, `TryParseBeaconPayload`), `DiscoveredPeer.cs`, `MouserHost.SetLocalDesktop`, `App.axaml.cs`/`MonitorLayoutViewModel.SetDisplays`, tests in `PeerDiscoveryTests.cs`
- Add `Mon: int[][]` (each `[x,y,w,h]`) to the DTO. **Keep** `X,Y,W,H` (the bounding box) so older builds still parse. `DiscoveredPeer.Monitors` (`IReadOnlyList<Monitor>`, empty when absent). Local monitors come from `window.Screens.All` bounds (already collected in `UpdateDisplays`): extend `SetDisplays` to also call `_host.SetLocalMonitors(list)`.
- Test: beacon round trip with 2 monitors; a beacon without `Mon` still parses (`Monitors` empty).

### E9c: Use it in the KVM
- **Files:** `KvmController.cs`, `VirtualKvmManager.cs`, tests in `KvmControllerTests.cs`/`VirtualKvmTests.cs`
- `KvmController.SetLocalMonitors(IReadOnlyList<Monitor>)`; `SetPeer` receives the peer's monitors (fallback: one monitor = bbox when empty). Place each peer's monitors = peer monitors shifted by the placement offset used today (`Place`).
- Replace `VirtualKvmManager.FindAdjacentScreen` + `DetectScreenEdgeCollision` use with `EdgeMap.IsOuterEdge` + `EdgeMap.FindCrossing`. **Keep behaviour identical for single-monitor setups** (all current tests must pass unchanged).
- When the virtual cursor on the peer leaves **its own monitor** inside the peer's desktop (between peer monitors), it must follow the peer's real layout: clamp to the union of the peer's monitors (nearest point) instead of the bounding box.
- New tests: local 1080p + 1440p stacked, peer to the right; cursor at y in the 1440p part crosses, at a y with no peer monitor beside it does not.

### E9d: Layout view draws real monitors
- **Files:** `MonitorLayoutViewModel.cs` (`Relayout` draws one node per peer monitor), view unchanged. Test: a peer with 2 monitors gives 2 peer nodes.

**Done when (each sub-card):** build; tests pass.

---

## E10: Draggable layout with offsets

- **Needs:** E9d · **Size:** L · **Files:** `SettingsStore.cs` (`PeerSettings.OffsetX/OffsetY`), `KvmController.Place`, `MonitorLayoutViewModel.cs`, `MonitorLayoutView.axaml(.cs)`, `ScreenNodeViewModel.cs`, tests

**Steps**
1. `PeerSettings`: `int OffsetX, OffsetY` (pixels in this computer's coordinate space; for a Left/Right peer only `OffsetY` is used, for Above/Below only `OffsetX`). `KvmController.SetPeer(..., placement, offset)`; `Place` adds it.
2. **Fix in `VirtualKvmManager`/E9c path:** crossing now requires overlap along the edge (done by `EdgeMap`).
3. Canvas dragging: the peer `Button`s become draggable: on `PointerPressed` capture the pointer, on `PointerMoved` move the node (`PositionX/PositionY` already drive `Canvas.Left/Top`), on `PointerReleased` convert the canvas delta back to pixels (`delta / scale`; `scale` is computed inside `Relayout`: store it in a field) and **snap**: if the peer's near edge is within 24 px (canvas) of the local desktop edge, snap to it; choose the placement (`Left/Right/Above/Below`) from which side the node is on; snap its offset to 0 when within 12 px of aligned top/left, or centre.
   *Note from history (commit ba4a78d): an earlier pointer-capture bug was fixed in this view; read that diff before editing `MonitorLayoutView`.*
4. Persist via `_host.SetPeerPlacement` + new `SetPeerOffset`; call `Relayout()`.
5. Tests (view-model level): `ChangePlacement` + offset persisted in settings; offset shifts the crossing zone (KvmController test: peer offset 200 px down, cursor above the offset zone does not cross).

**Done when:** build; tests pass; manual: drag the other computer's block to sit lower; the cursor only crosses where blocks touch.

---

## E11: Several computers per side and chains (A–B–C)

- **Needs:** E10 · **Size:** XL · **Risk:** HIGH. Do a design review with the user before coding.

**Idea:** put **all** computers in one plane in *this* computer's coordinates (peers can be anchored to another peer, not just to this PC). While controlling B, the virtual cursor moves in the plane; when it leaves B's rectangle into C's rectangle, this computer (which must be paired with C too) switches the **active link** from B to C directly: no forwarding through B.
- `PeerSettings.AnchorId` (null = this PC) + `Placement` relative to the anchor + offset. A peer whose anchor is missing is hidden from the plane.
- Pure `VirtualPlane` class in `Mouser.Core/Kvm`: rectangles by id, `Move(currentId, x, y, dx, dy) → (id, x, y)`; tests: 3 in a row, moving right through B into C, then back; gaps stop the cursor; overlapping rectangles are rejected on insert.
- `KvmController.HandleLocked` Move case calls the plane instead of `LeftThroughReturnEdge`; `ReturnToLocal` is just "plane says id == local".
- **Stuck keys and buttons on every switch** (reuse the release logic in `ReturnToLocal` for B before activating C).
- UI: the layout view allows dropping a peer next to another peer; Device sheet gets "Next to: [This computer ▾ / other computer]".

**Done when:** build; all `KvmControllerTests` green; new plane tests green; manual with three instances (`--data-dir a|b|c`).

---

## E12: Layout-aware typing (Unicode path)

- **Needs:** none (protocol change allowed here) · **Size:** L · **Risk:** medium · **Files:** `src/Mouser.Core/Networking/InputMotionPacket.cs`, `src/Mouser.Core/Input/IInputCapture.cs` (`CapturedInput`), `Win32InputCapture.cs`, `KvmController.cs`, `src/Mouser.Core/Input/IInputSink.cs`, `Win32InputSink.cs`, `MacInputSink.cs`, `InputInjectionService.cs`, `NullInputSink`, tests `RecordingSink` (`TestSupport.cs`), `InputMotionPacketTests.cs`, `KvmControllerTests.cs`, Android `Protocol.kt` (constants only)

**Problem:** the wire carries virtual-key codes. If the two computers use different keyboard layouts (US vs Arabic vs French AZERTY) the same VK types a different character on the far side.

**Design:** when the user types a **printable character with no Ctrl/Alt/Win held**, send the **character** (`TypeChar = 0x0A`, `dx` = UTF-16 code unit, `buttonFlags` bit 0 = key down/up). Everything else (shortcuts, arrows, function keys) keeps sending VK codes.

**Steps**
1. `InputMotionPacket.TypeChar = 0x0A`; allow it in `TryRead` validation (look at how `TypeKey` is accepted). Add tests in `InputMotionPacketTests`.
2. `IInputSink`: add `void Char(ushort utf16Unit, bool isDown)`. `NullInputSink`: empty. `Win32InputSink`: `SendInput` with `KEYEVENTF_UNICODE` (+`KEYEVENTF_KEYUP` for up), `wVk = 0`, `wScan = unit`. `MacInputSink`: leave a `// backlog-macos: needs CGEventKeyboardSetUnicodeString` and do nothing. `RecordingSink`: record `"char 0x{unit:X4} down|up"`.
3. `InputInjectionService`: handle `TypeChar` like `TypeKey` (same pairing/sequence rules; same cases for held-key bookkeeping: look at how `TypeKey` tracks held keys so a dropped release is cleaned up and mirror it).
4. `Win32InputCapture.KeyboardHook`: for key **down** (and the matching up) compute the character with `ToUnicodeEx(vk, scan, keyState, buf, buf.Length, flags: 0x4 /*don't change keyboard state*/, hkl)` using the foreground thread's layout (`GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow()))`). Add `char? Text` to `CapturedInput` (default null). Skip when Ctrl or Alt (without AltGr semantics: treat Ctrl+Alt as AltGr, allowed to produce text) or Win is down, and for dead keys (`ToUnicodeEx` returns negative): fall back to VK.
5. `KvmController` Key case: if `e.Text` is not null send `TypeChar` for down and **remember** that this VK was sent as a char so its **up** is sent as a char-up (keep a `Dictionary<ushort, ushort> _charKeys`); otherwise send `TypeKey`. Release-on-return must release both kinds.
6. Tests: `KvmControllerTests`: key 'a' with `Text='a'` sends TypeChar; Ctrl+C sends VK; up matches down kind. `Win32InputSinkTests`: a `[WindowsFact]` that injects a Unicode char into a hidden `TextBox`-less target is hard; instead test the `INPUT` struct construction in a pure helper `Win32InputSink.BuildCharInput(unit, down)`.

**Done when:** build; tests pass; manual: set the other computer to a different layout and type `é`, `ا`: correct characters appear; Ctrl+C still copies.

---

## E13: Lock, sleep and display-off sync

- **Needs:** C4 (`WindowsSession.IsWorkstationLocked`), B9 · **Size:** M · **Protocol change allowed.** **Files:** `PeerTransferService.cs` (`FrameType.PowerEvent = 7`, `SendPowerAsync`, `PowerReceived` event), `MouserHost.cs`, `App.axaml.cs`, `SettingsStore.cs` (`PeerSettings.SyncLock`, `SyncSleep`), `DevicesView.axaml` (Advanced), tests

**Behaviour:** when this PC locks (detected in the 1-second tick by `WindowsSession.IsWorkstationLocked()` going false→true) send `Lock` to every paired peer with `SyncLock` on; a receiving PC with "Lock when {other} locks" enabled calls `LockWorkStation()`. Sleep and display-off use the same frame with other codes; **display off** = `SendMessage(HWND_BROADCAST=0xFFFF, WM_SYSCOMMAND=0x0112, SC_MONITORPOWER=0xF170, 2)`; **sleep** is `SetSuspendState(false,true,false)` from `powrprof.dll` (off by default, confirmation text in the UI).
- **Safety rules:** all options default **off**; only peers that are paired (frame authenticated) can send; ignore a second event within 5 s (no ping-pong between two PCs that both sync lock: each ignores events for 5 s after it caused or sent one).
- Test: frame round trip in `PeerTransferTests.cs` (copy the Hello test); policy function `PowerSyncPolicy.ShouldApply(eventKind, settings, lastEventUtc, now)` pure with tests for the 5-second guard.

**Done when:** build; tests pass (+3); manual: Win+L on A locks B when enabled.

---

## E14: Wake a sleeping computer (Wake-on-LAN)

- **Needs:** B9 · **Size:** M · **Files:** create `src/Mouser.Core/Networking/WakeOnLan.cs`, tests; edit `SettingsStore.cs` (`PeerSettings.Mac`), `MouserHost.cs`, `DevicesView.axaml`

**Steps**
1. `WakeOnLan.BuildMagicPacket(byte[] mac)` → 6×`0xFF` then the MAC ×16 (102 bytes). `WakeOnLan.TryParseMac(string)` accepts `aa:bb:cc:dd:ee:ff` / `-` / no separators. `SendAsync(mac)` sends the packet to UDP port 9 on every address of `NetworkInfo.GetBroadcastAddresses()`. Tests for packet bytes and the parser.
2. Learn the MAC while the peer is online: `SendARP` (`iphlpapi.dll`, `SendARP(uint destIp, uint srcIp, byte[] mac, ref uint len)`) for IPv4; store in `PeerSettings.Mac` when it changes (inside `MouserHost.SyncPeers`, at most once per minute per peer; ignore failures).
3. Device sheet for an **offline** saved computer: **Wake** button (visible only when `Mac` is known) with hint "Works only if Wake-on-LAN is enabled in that computer's BIOS and network adapter settings."

**Done when:** build; tests pass (+3).

---

## E15: Paste from the other computer (hotkey)

- **Needs:** D15, E3 · **Size:** S · **Files:** `KvmController.cs` (remember `LastPeerName`), `App.axaml.cs`, `QuickPasteViewModel.cs`, hotkey setting `pasteFromPeer` (default `Ctrl+Shift+V`)

**Behaviour:** pressing the hotkey opens the quick-paste overlay already **filtered to clips whose `SourceDevice` is the computer you last came from** (or the one you are controlling), and Enter pastes into the focused window. (ShareMouse's "insert remote clipboard", but with history, search and images.)
- `QuickPasteViewModel.DeviceFilter` string; `LoadAsync` applies it (`SourceDevice == filter`); header chip "From Laptop-Pro ✕" clears it.
- If no peer was used yet, behave like the normal quick paste.
- Test: with clips from two devices, `DeviceFilter` shows only one.

**Done when:** build; tests pass (+1).

---

## E16: Detect and warn about elevated (administrator) windows

- **Needs:** A12 · **Size:** S · **Files:** create `src/Mouser.UI/Services/ElevationInfo.cs`; edit `ForegroundWindowInfo.cs` (E2 creates it; create it here if missing), `App.axaml.cs`, `SettingsStore.cs`, Settings VM/view, tests

**Why (this is a reported bug):** when the foreground window runs as administrator, Windows (UIPI, "User Interface Privilege Isolation") blocks input that comes from a **normal** process. `src/Mouser.Desktop/app.manifest` has no `requestedExecutionLevel`, so Mouser runs as a normal user and its phone input, KVM injection, quick-paste (Ctrl+V) and capture hooks can be silently ignored over admin windows (Task Manager, an elevated terminal, installers, the registry editor). Today nothing tells the user why. The real fix is E18 (run Mouser as administrator) and H8; this card makes the problem **visible**.

**Pure helper:** `ElevationInfo.IsCurrentProcessElevated()` using `new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)` (Windows only; other OS → false). Test: returns a bool without throwing.

**Steps**
1. `ForegroundWindowInfo.IsElevated()`: `GetWindowThreadProcessId` → `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION = 0x1000)` → `OpenProcessToken(TOKEN_QUERY = 8)` → `GetTokenInformation(TokenElevation = 20)`. Return `true` only when the foreground process is elevated **and** Mouser is not. If `OpenProcess` fails with access denied (typical for elevated or SYSTEM processes), treat that as "elevated" when Mouser is not elevated. Always close handles in `finally`.
2. In the 1-second tick (do not add a new timer) evaluate it **only when input is being injected into this PC** (a phone client active in the last 5 s, or a computer is controlling this one) and at most once per second; toast at most once per minute: "That window runs as administrator, so Mouser can't type or click in it. Turn on Settings → General → Run Mouser as administrator."
3. Also expose `bool ForegroundIsElevated` on `MainViewModel` and show an inline `banner-warning` at the top of the Devices page while it is true and a phone or computer is active.
4. Settings → General (B11 pattern): **"Warn me when a window can't receive input"** (`AppSettings.WarnElevatedWindows`, default on). Hint: "Windows blocks Mouser from typing into administrator windows unless Mouser runs as administrator too."
5. Tests: the pure helper; a view-model test that the warning flag follows a fake probe (`Func<bool> ElevatedProbe` property on `MainViewModel`, default `ForegroundWindowInfo.IsElevated`).

**Done when:** build; tests pass; manual: open an elevated Notepad and move the phone remote over it → toast and banner appear; close it → they stop.

---

## E18: "Run Mouser as administrator" (setting, restart button, start at login elevated)

- **Needs:** E16, A11 · **Size:** L · **Risk:** medium (UAC, scheduled task, single instance)
- **Files:** create `src/Mouser.UI/Services/ElevatedLaunch.cs`; edit `PlatformServices.cs` (`StartupRegistration`), `App.axaml.cs`, `SingleInstance.cs` (A11), `SettingsStore.cs`, `SettingsViewModel.cs`, `SettingsView.axaml`, tests

**Goal:** one clear switch that makes Mouser work in administrator windows with the least UAC annoyance.

**How it works**
- An **elevated Mouser** is not blocked by UIPI, so it can inject into and hook admin windows. It still cannot reach the UAC secure desktop or the lock screen (that is H5).
- A **Run-key** autostart entry cannot silently start an elevated app (Windows skips apps that need a UAC prompt). The supported way is a **scheduled task** with "Run with highest privileges" triggered at logon: no prompt at login after the one-time setup.

**Setting:** `AppSettings.RunAsAdministrator` (bool, default **off**).

**Steps**
1. `ElevatedLaunch` (Windows only; elsewhere every method returns false and does nothing):
   - `bool IsElevated` → `ElevationInfo.IsCurrentProcessElevated()`.
   - `bool RestartElevated(string extraArgs)`: `Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { Arguments = extraArgs, UseShellExecute = true, Verb = "runas" })`; catch `Win32Exception` with `NativeErrorCode == 1223` (the user pressed **No** in the UAC prompt) and return false. On success the **caller** shuts the current instance down (release the single-instance mutex first so the new one can start).
   - `bool InstallLogonTask()`: only valid when elevated. Run `schtasks.exe` with `ProcessStartInfo.ArgumentList` (never string concatenation): `/Create`, `/TN`, `Mouser`, `/TR`, `"<exe path in quotes>" --minimized`, `/SC`, `ONLOGON`, `/RL`, `HIGHEST`, `/F`. Return `ExitCode == 0`.
   - `bool RemoveLogonTask()`: `/Delete /TN Mouser /F`. `bool HasLogonTask()`: `/Query /TN Mouser`, exit code 0.
   - Toggle flow: turn on → if not elevated, `RestartElevated("--enable-admin-startup")`; the elevated instance sees that argument, calls `InstallLogonTask()`, removes the Run-key entry, and keeps running elevated.
2. `StartupRegistration.Apply(enabled)`: when `RunAsAdministrator` is on, **do not** write the Run key; use the task. Never leave **both** a Run entry and the task (two instances). Keep today's Run-key behaviour when it is off.
3. Settings → General → **Run Mouser as administrator** toggle (Setting-row pattern):
   - hint "Lets Mouser type and click in administrator windows such as Task Manager. Windows asks for permission once.";
   - status line: "Mouser is running as administrator" or "Mouser is running normally";
   - buttons: **Restart as administrator** (visible when not elevated) and **Restart normally** (visible when elevated: relaunch through `explorer.exe "<exe>"`, which starts it non-elevated);
   - when the user declines UAC: message "Windows permission was declined. Nothing changed." and the toggle goes back to off.
4. Tray tooltip and the About page show "(administrator)" when elevated. If `RunAsAdministrator` is on but the process is **not** elevated (the task failed), toast "Mouser couldn't start as administrator."
5. **Single instance (A11) and elevation:** a normal instance cannot open the elevated instance's named mutex/event (access denied). In `SingleInstance`, catch `UnauthorizedAccessException` when opening and treat it as "another (elevated) instance is running": toast "Mouser is already running as administrator. Use its tray icon." and exit. When an **elevated** process creates the mutex/event, give them a security descriptor that lets the same user's normal process open them (`MutexAcl.Create` / `EventWaitHandleAcl.Create` with `SecurityIdentifier(WellKnownSidType.WorldSid, null)` and rights `Synchronize | Modify`), so a second click on the shortcut can still signal the first.
6. **Data folder trap:** if UAC asks for a *different* administrator's password, `%APPDATA%` differs and Mouser looks like it lost its settings and pairings. Add one hint line under the toggle: "If Windows asks for a different administrator's password, Mouser uses that account's settings."
7. The task is only edited through the `--enable-admin-startup` / `--disable-admin-startup` arguments, never from a non-elevated process.
8. Do **not** put `requestedExecutionLevel level="requireAdministrator"` in `app.manifest` (it would prompt on every launch for everyone and break Run-key start). Elevation stays optional.

**Tests (pure parts)**
- `SchtasksCommand.BuildCreateArguments(exePath)` / `BuildDeleteArguments()` return the argument list; assert a path with spaces (`C:\Program Files\Mouser\Mouser.exe`) stays one argument inside the quoted `/TR` value and that `--minimized` is present.
- Settings round trip for `RunAsAdministrator` (default false).
- `StartupRegistration.ChooseMethod(runAsAdmin, enabled)` → `RunKey | Task | None`: all four cases.

**Done when:** build; tests pass; **manual on Windows:** toggle on → UAC prompt → Mouser restarts as administrator and says so; the phone remote and the KVM now work over an elevated Task Manager and an elevated terminal; sign out and in → Mouser starts elevated with **no** prompt (`schtasks /Query /TN Mouser` finds it); toggle off → task removed and the normal Run entry is back; pressing **No** on UAC changes nothing.

**Do not:** run the whole app elevated by default; store an administrator password; touch HKLM.

---

## E17: Cross-screen drag and drop (design spike, ask the user first)

- **Needs:** E9c, D11 · **Size:** XL · **Do not start coding before the user approves the design below.**

**Realistic design (no OLE hooks):** when the cursor crosses to a peer **with the left button held**, and the source foreground window is Explorer: (1) the controller injects Ctrl+C **locally before swallowing input** to copy the selection, (2) reads the file list via D4/D8, (3) sends the files with `SendFileAsync` to the peer while the user keeps dragging, (4) on button release on the peer, if the foreground window there is Explorer, the receiving side pastes into the folder under the cursor (inject Ctrl+V after focusing it), otherwise leaves the files in `Downloads\Mouser` and toasts.
**Spike questions to answer in a note before building:** is the Ctrl+C trick acceptable (it overwrites the clipboard: save and restore it)? Which folder does Explorer paste into when the cursor is over a folder vs a blank area? What is the size limit for a drag (suggest 500 MB, otherwise refuse with a toast)?
**Tests:** policy functions only (when to start, size limit, source detection) as pure code.
