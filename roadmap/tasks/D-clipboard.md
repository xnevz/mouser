# Track D: Clipboard parity (UniClip / Ditto / Win+V level)

Read [../AGENT_GUIDE.md](../AGENT_GUIDE.md) first. Feature list: [../phase-7-feature-parity-gaps.md](../phase-7-feature-parity-gaps.md) Phase 8.
**Needs:** track A through A9 and B5/B6 (the new clipboard page). D cards are mostly independent: take them in the
order below, because later cards use earlier ones.

Facts about today's code (verified): `IClipboardAccess` has only `GetTextAsync/SetTextAsync`
(`src/Mouser.UI/Services/PlatformServices.cs`). `ClipboardPayload` (`src/Mouser.Core/Clipboard/ClipboardPayload.cs`) serialises
id, time, type, text, html, source device, favourite with `BinaryWriter`. SQLite table `clips` + FTS5 table `clips_fts`
(`ClipboardStorageService.InitializeAsync`). Capture is a 1-second poll called from `App.axaml.cs`. Dedupe is by exact
text. Wire frames are capped at 4 MB (`PeerTransferService.MaxFrameBytes`).

**Pure-logic first:** many cards below create a *pure static function* in `Mouser.Core` with unit tests. Do those
first inside a card; wiring into UI is the last step.

---

## D1: Event-driven capture on Windows

- **Needs:** A9 · **Size:** S · **Files:** create `src/Mouser.UI/Services/ClipboardWatcher.cs`; edit `App.axaml.cs`; test `tests/Mouser.Tests/ClipboardWatcherTests.cs`

**Idea (lazy):** Windows increments a counter on every clipboard change (`GetClipboardSequenceNumber`). A tiny background thread checks it every 150 ms (a cheap integer read, no clipboard access) and raises an event only when it changes. No window or message loop needed.

**Steps**
1. `ClipboardWatcher`:
```csharp
using System.Runtime.InteropServices;
namespace Mouser.UI.Services;

public sealed class ClipboardWatcher : IDisposable
{
    private readonly Func<uint> _sequence;
    private readonly CancellationTokenSource _cts = new();
    public event Action? Changed;

    public ClipboardWatcher(Func<uint>? sequence = null, int intervalMs = 150)
    {
        _sequence = sequence ?? DefaultSequence;
        uint last = _sequence();
        new Thread(() =>
        {
            while (!_cts.IsCancellationRequested)
            {
                Thread.Sleep(intervalMs);
                uint now = _sequence();
                if (now != last) { last = now; Changed?.Invoke(); }
            }
        }) { IsBackground = true, Name = "Mouser clipboard watcher" }.Start();
    }

    private static uint DefaultSequence() => OperatingSystem.IsWindows() ? GetClipboardSequenceNumber() : 0; // shortcut: non-Windows keeps the 1 s poll
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    public void Dispose() => _cts.Cancel();
}
```
2. `App.axaml.cs`: create the watcher; `watcher.Changed += () => Dispatcher.UIThread.Post(async () => await vm.ClipboardHistory.PollClipboardAsync());`. **Keep the 1-second timer's `PollClipboardAsync()` call** as a fallback (guide trap 1) — `PollClipboardAsync` already ignores unchanged text, so double calls are harmless. Dispose the watcher in `desktop.Exit`.
3. Test: pass a fake sequence (`uint` field you increment); assert `Changed` fires once per change within 1 s and not when the value is stable.

**Done when:** build; tests pass (+1); manual: copy text, the clip appears in history within ~0.2 s.

---

## D2: Source app and window title

- **Needs:** D1 · **Size:** M · **Files:** `ClipboardPayload.cs`, `ClipboardStorageService.cs`, `ClipboardItemViewModel.cs`, `ClipboardHistoryViewModel.cs`, create `src/Mouser.UI/Services/ForegroundApp.cs`, tests in `ClipboardTests.cs`

**Steps**
1. `ForegroundApp.Get()` (Windows only, else `("", "")`): `GetClipboardOwner()` → fallback `GetForegroundWindow()` → `GetWindowThreadProcessId` → `Process.GetProcessById(pid).ProcessName` (catch all exceptions → `""`) and `GetWindowText` for the title (max 200 chars). Return `(App, Title)`. P/Invoke from `user32.dll`.
2. `ClipboardPayload`: add `public string SourceApp { get; set; } = "";`. **Wire compatibility:** `Serialize` writes it **last**; `Deserialize` reads it only `if (ms.Position < ms.Length)` so payloads from older builds still load. Test both directions with a hand-built old-format byte array.
3. Storage: `InitializeAsync` after the schema: run `PRAGMA table_info(clips)`; if no `source_app` column, `ALTER TABLE clips ADD COLUMN source_app TEXT NOT NULL DEFAULT ''`. Include it in insert and in `ReadPayload` (add it as the **last** selected column in every SELECT: there are 4 SELECT statements: update all four and the reader index).
4. `ClipboardHistoryViewModel`: when capturing a polled clip, set `SourceApp = ForegroundApp.Get().App` (via an injectable `Func<(string App,string Title)> AppProbe`, default `ForegroundApp.Get`, so tests can fake it).
5. UI: detail pane metadata line shows `SourceApp` when not empty.
6. Tests: payload round trip with `SourceApp`; an old-format payload (no trailing string) deserialises with `SourceApp == ""`; storage migration: create a DB with the old schema (use raw SQL in the test), `InitializeAsync`, save and read a clip with `SourceApp`.

**Done when:** build; tests pass (+3).

---

## D3: Respect password managers (exclusion formats + ignore list)

- **Needs:** D2 · **Size:** M · **Files:** `src/Mouser.Core/Clipboard/ClipboardPrivacyFilter.cs`, `SettingsStore.cs`, `ClipboardHistoryViewModel.cs`, `SettingsView.axaml` (+VM), create `src/Mouser.UI/Services/ClipboardFormats.cs`, tests in `ClipboardTests.cs`

**Steps**
1. Core, pure: `public static bool IsIgnoredApp(string? app, IEnumerable<string> ignored)` → case-insensitive exact match on process name without `.exe`. Tests: `("KeePass", ["keepass"])` true; `("notepad", ["keepass"])` false; empty app false.
2. `AppSettings.IgnoredApps` (`List<string>`), default `["keepass","keepassxc","1password","bitwarden","lastpass","dashlane","enpass"]`. `Load()`: `??= default`.
3. `ClipboardFormats.IsExcludedByOwner()` (Windows): `RegisterClipboardFormatW` for `"ExcludeClipboardContentFromMonitorProcessing"` and `"CanIncludeInClipboardHistory"`; excluded when the first format is **available** (`IsClipboardFormatAvailable`) — the second is a DWORD that is `0` when the owner says "do not include"; to keep it simple treat only the first as the signal (shortcut: ignores CanIncludeInClipboardHistory=0; revisit in the spike for D4). Do not open the clipboard; `IsClipboardFormatAvailable` does not require opening it.
4. `ClipboardHistoryViewModel.PollClipboardAsync`: before saving **and before sharing**, skip when `ClipboardFormats.IsExcludedByOwner()` or `IsIgnoredApp(app, settings.IgnoredApps)`. Set `StatusMessage = "Skipped a copy from a password manager."`.
5. Settings → Clipboard → Advanced: a multi-line `TextBox` "Don't save copies from these apps (one per line)" bound to `IgnoredAppsText` (VM converts to/from the list, trims, lowercases, drops blanks).
6. Tests: privacy filter function; a VM test with `AppProbe` returning `"keepass"` → nothing stored, nothing shared.

**Done when:** build; tests pass (+3); manual with the real KeePass or Bitwarden.

---

## D4: Spike: reading and writing images, HTML and files from the clipboard

- **Needs:** D1 · **Size:** S (investigation) · **Output:** a short note in this card + the widened interface; **no UI**
- **Files:** `src/Mouser.UI/Services/PlatformServices.cs`, create `src/Mouser.Core/Clipboard/ClipboardSnapshot.cs`

**Steps**
1. Core record:
```csharp
namespace Mouser.Core.Clipboard;
public sealed record ClipboardSnapshot(string? Text, string? Html, byte[]? ImagePng, IReadOnlyList<string>? Files)
{
    public bool IsEmpty => string.IsNullOrEmpty(Text) && string.IsNullOrEmpty(Html) && ImagePng is not { Length: > 0 } && Files is not { Count: > 0 };
}
```
2. `IClipboardAccess`: add `Task<ClipboardSnapshot> GetSnapshotAsync();` and `Task SetSnapshotAsync(ClipboardSnapshot s);`. Update `AvaloniaClipboardAccess` and the test double `FakeClipboard` (`tests/Mouser.Tests/ViewModelTests.cs`: implement both from its `Text` field so existing tests keep passing).
3. **Find the Avalonia 12.1.6 API** for: bitmap, file list, HTML. Method: in `AvaloniaClipboardAccess`, try `Avalonia.Input.Platform.ClipboardExtensions` (look for `TryGetBitmapAsync`, `TryGetFilesAsync`, `TryGetInProcessDataTransfer` or `GetDataAsync`) using IDE completion or `dotnet build` errors. Write what exists in a comment block at the top of the class.
4. If Avalonia lacks bitmap/HTML read/write: implement Windows-only P/Invoke in a new `Win32Clipboard.cs` (`OpenClipboard`, `GetClipboardData(CF_DIBV5=17 / CF_HDROP=15 / registered "HTML Format")`, `CloseClipboard` always in `finally`; retry open up to 5× with 20 ms sleep because other apps hold it). Convert DIB → PNG with `Avalonia.Media.Imaging.Bitmap` or `System.Drawing` is **not** available: if you cannot encode PNG without a package, **stop and ask** (candidate package: `SixLabors.ImageSharp`, needs the user's approval).
5. Keep `GetTextAsync` behaviour identical.

**Done when:** build; existing tests pass; a short manual harness (temporary `--clip-dump` argument is NOT allowed: use a unit test marked `[WindowsFact]` that is skipped in CI and prints format availability) proves you can read an image copied from the Snipping Tool. Report which approach worked.

---

## D5: Image clips: store

- **Needs:** D4 · **Size:** L · **Files:** `ClipboardStorageService.cs`, `ClipboardPayload.cs`, `ClipboardHistoryViewModel.cs`, `ClipboardItemViewModel.cs`, tests

**Design:** image bytes live in files, not in SQLite: `<DataDir>/media/<id>.png`. The DB row has `content_type = Image(5)`, `text_content = "Image 512×251"` (so search and list titles work), and new columns `media_path TEXT`, `media_hash TEXT`, `width INTEGER`, `height INTEGER`, `bytes INTEGER` (add with the same `PRAGMA table_info` migration pattern as D2).

**Steps**
1. `ClipboardPayload`: `MediaPath`, `MediaHash`, `Width`, `Height`, `MediaBytes` (append to `Serialize` as in D2 with the trailing-field rule; **do not** put image bytes in the payload yet: wire sync is D12).
2. Storage: `SaveClipAsync` dedupes images by `media_hash` (SHA-256 hex) the same way text is deduped by text. `DeleteClipAsync`, `ClearAsync`, `PurgeClipsOlderThanAsync` must also delete the media file (collect the `media_path` values first, delete files after the transaction commits, ignore `IOException`).
3. `ClipboardHistoryViewModel.PollClipboardAsync`: use `GetSnapshotAsync`. If `ImagePng` is present and (`Text` is empty): reject when larger than **20 MB** (status message "Skipped a very large image."), compute hash, write the file (create `media` folder), save the payload.
4. Settings → Clipboard: toggle "Save images" (default on) `AppSettings.SaveImages`.
5. Tests: save an image payload with a temp media folder, read back, delete → file gone; duplicate hash updates timestamp instead of adding a row; purge removes files.

**Done when:** build; tests pass (+3); `media` folder follows retention.

---

## D6: Image clips: list, preview and actions

- **Needs:** D5, B6 · **Size:** M · **Files:** `ClipboardItemViewModel.cs`, `ClipboardHistoryView.axaml`, `ClipboardHistoryViewModel.cs`

**Steps**
1. `ClipboardItemViewModel.Thumbnail` (`Avalonia.Media.Imaging.Bitmap?`): load lazily on first access with `Bitmap.DecodeToWidth(stream, 96)`; cache; dispose when the item is removed. `IsImage` bool.
2. List row: when `IsImage`, show the thumbnail (36×36, `Stretch="UniformToFill"`, rounded) instead of the text badge.
3. Detail pane: when `IsImage` show an `Image` bound to a full bitmap (`PreviewImage`, loaded when selected) inside a `Border` with a checkerboard-free solid `BgSunken`; **Fit / 100%** toggle (two small buttons switching `Stretch` between `Uniform` and `None` inside a `ScrollViewer`). Metadata cards: Dimensions (`512 × 251`), Format (`PNG · 12.5 KB`), Copied (`Nov 12 · 14:43`).
4. Buttons for images: **Copy image** (primary: `SetSnapshotAsync` with the PNG; mark `_lastSeenText` equivalent so it is not re-captured: add `_lastSeenImageHash`), **Save as…** (`StorageProvider.SaveFilePickerAsync`, expose a `Func<Task<string?>> PickSavePath` like `PickFile` in `MonitorLayoutViewModel`), **Open** (`Process.Start(new ProcessStartInfo(path){ UseShellExecute = true })`).
5. Filter chip "Type" gets a new option `Image` (the VM already parses enum names from `SelectedFilter`).

**Done when:** build; tests pass; manual: take a screenshot (Win+Shift+S), it appears with a thumbnail, Copy image pastes into Paint.

---

## D7: HTML/RTF capture and "paste as plain text"

- **Needs:** D4 · **Size:** M · **Files:** `ClipboardHistoryViewModel.cs`, `QuickPasteViewModel.cs`, `App.axaml.cs`, `QuickPasteWindow.axaml(.cs)`, tests in `QuickPasteTests.cs`

**Steps**
1. When the snapshot has `Html`, store it in `HtmlContent` (already a column) and keep `TextContent` (plain) for search. `ContentType` stays `Text` unless the classifier says otherwise (do not add a new visible type).
2. History **Copy** and quick paste: default pastes **plain text** (`SetSnapshotAsync(new(text, null, null, null))`). Holding **Shift** when pressing Enter in quick paste pastes **rich** (`Html` included). Add `PasteRequested(string text, bool rich)` to `QuickPasteViewModel` (update its tests and the handler in `App.axaml.cs`).
3. Detail pane: small "Rich" chip when `HtmlContent` is not empty.

**Done when:** build; tests pass; manual: copy a formatted paragraph from a web page → quick paste inserts plain; with Shift inserts formatted.

---

## D8: File clips (Explorer copy)

- **Needs:** D4, D5 · **Size:** M · **Files:** `ClipboardHistoryViewModel.cs`, `ClipboardItemViewModel.cs`, `ClipboardHistoryView.axaml`

**Steps**
1. Snapshot with `Files`: store `ContentType = FileUri`, `TextContent` = the paths joined with `\n` (search works), no file copies (shortcut: stores paths only; if the file is later deleted the clip becomes a dead reference).
2. Row title: first file name + "and N more"; badge text `FILE`; `Folder` for directories (`Directory.Exists`).
3. Detail: list of names with size when the file still exists; **Copy** puts the files back (`SetSnapshotAsync` with `Files`), **Send to…** uses `MouserHost.SendFileAsync` for the first file (multi-file send is D12).

**Done when:** build; tests pass; manual with a file copied in Explorer.

---

## D9: Tags (system and custom) and the sidebar Tags section

- **Needs:** B2, D5 · **Size:** L · **Files:** `ClipboardStorageService.cs`, `ClipboardHistoryViewModel.cs`, `ClipboardItemViewModel.cs`, `MainViewModel.cs`, `MainWindow.axaml`, `ClipboardHistoryView.axaml`, tests

**Design:** tables `tags(id TEXT PK, name TEXT UNIQUE COLLATE NOCASE, color TEXT, is_system INTEGER)` and `clip_tags(clip_id TEXT, tag_id TEXT, PRIMARY KEY(clip_id, tag_id))`. System tags **Link, Code, Image, Folder** are not stored per clip: they are derived from `ContentType` (so they stay correct). Custom tags are stored.

**Steps**
1. Storage API: `GetTagsAsync()` (name, color, count incl. derived system counts), `AddTagAsync(clipId, name)` (creates the tag with a colour from a fixed 8-colour palette if new), `RemoveTagAsync(clipId, name)`, `GetClipTagsAsync(clipId)`, `DeleteTagAsync(name)`. Delete `clip_tags` rows in `DeleteClipAsync`/`ClearAsync`/`Purge`.
2. VM: `ObservableCollection<TagViewModel> Tags` (Name, Color, Count) on `MainViewModel` refreshed after every reload; `SelectedTag` filter on `ClipboardHistoryViewModel` (`SetTagCommand`).
3. Sidebar: "TAGS" section under Pinned (B2 layout): coloured square + `#Name` + count; click filters the clipboard page.
4. Detail pane: tag chips row with **＋ Add tag** (a small `TextBox` flyout; Enter adds; chips have ✕).
5. Search box: typing `#name` filters by tag (full operator parsing arrives in D10).
6. Tests: add/remove tag, counts, deleting a clip removes its links, derived system tags counted by type.

**Done when:** build; tests pass (+4).

---

## D10: Search operators (pure parser)

- **Needs:** D9 · **Size:** M · **Files:** create `src/Mouser.Core/Clipboard/SearchQuery.cs`, `tests/Mouser.Tests/SearchQueryTests.cs`; edit `ClipboardStorageService.SearchClipsAsync`, `ClipboardHistoryViewModel.ReloadAsync`

**API**
```csharp
public sealed record SearchQuery(string Text, string? Type, string? App, string? Device, string? Tag, DateTime? Before, DateTime? After)
{
    public static SearchQuery Parse(string input);
}
```
**Grammar:** tokens split on whitespace (quoted `"a b"` stays one token). `type:image|text|link|code|file`, `app:chrome`, `device:laptop` or `@laptop`, `#tag`, `before:2026-01-31`, `after:2026-01-01`, `on:2026-01-31` (sets both). Everything else is free text joined by spaces. Unknown `key:` stays free text. Invalid dates are ignored (kept as text).

**Steps**
1. Write `Parse` and **tests first** (≥ 10 cases: each operator, quoted text, combined, junk).
2. `ReloadAsync`: parse `SearchQuery`; free text → existing FTS search; the other fields filter in memory on the loaded page (shortcut: filters apply to the first 200 FTS hits; page properly in D17).
3. Search box placeholder: `Search, or type # @ type: on:`.

**Done when:** build; tests pass (+10).

---

## D11: Delivery status per device

- **Needs:** B9, D2 · **Size:** L · **Files:** `MouserHost.cs`, `ClipboardStorageService.cs`, `ClipboardHistoryViewModel.cs`, `ClipboardHistoryView.axaml`, tests

**Design:** table `deliveries(clip_id TEXT, device_id TEXT, device_name TEXT, status TEXT, error TEXT, updated INTEGER, PRIMARY KEY(clip_id, device_id))`, status ∈ `Queued, Sent, Delivered, Failed, Skipped`.

**Steps**
1. `MouserHost.ShareClipboardAsync(string text)` currently returns `Task` and swallows errors. Change to return `Task<IReadOnlyList<ClipDelivery>>` where `ClipDelivery(string DeviceId, string DeviceName, string Status, string? Error)`; one entry per **paired** peer: `Delivered` after `SendClipAsync` completes (TCP write finished = delivered; there is no app-level ack: say "Sent" in the UI), `Failed` with `e.Message`, `Skipped` when peer rules (D12) exclude it or the peer is offline (`Error = "Not on the network"`).
2. `ClipboardHistoryViewModel.ShareCapturedText` type becomes `Func<string, Task<IReadOnlyList<ClipDelivery>>>`; after sharing, write rows through new `ClipboardStorageService.SaveDeliveriesAsync(clipId, list)`. Update the `MainViewModel` line that assigns it and the tests that use it.
3. Detail pane: **Delivery** section: header "Delivery · 1 of 2 devices", one row per device: status dot, name, status text; failed rows show the reason in a tooltip (ⓘ icon).
4. **Retry:** when a failed device comes back (in `MouserHost.SyncPeers` when a peer becomes visible), resend the latest failed clip (max 1 per device, only if younger than 10 minutes).
5. Tests: fake peers are hard; test the storage (`SaveDeliveriesAsync`/read back) and the VM mapping from `ClipDelivery` to rows.

**Done when:** build; tests pass (+3); manual with the two-instance script: copy on A, B receives, A's detail pane shows "Sent".

---

## D12: Per-device sync rules and "Send to device…"

- **Needs:** D11, B9 · **Size:** M · **Files:** `SettingsStore.cs` (`PeerSettings`), `MouserHost.cs`, `DevicesView.axaml` (sheet), `ClipboardHistoryView.axaml`, tests

**Steps**
1. `PeerSettings`: `bool SendClipboard = true; bool ReceiveClipboard = true; bool SendFiles = true;`.
2. `MouserHost`: `ShareClipboardAsync` skips peers with `SendClipboard == false` (`Skipped`); `ReceiveKeys`/`ClipReceived` handler drops clips from peers with `ReceiveClipboard == false`.
3. Device sheet (B9): two toggles "Send my clipboard to this device" / "Accept clipboard from this device" (Setting-row pattern).
4. `MouserHost.SendClipToAsync(DiscoveredPeer peer, string text)` for **manual push**: sends regardless of the `SendClipboard` rule; returns a `ClipDelivery`.
5. Clipboard detail pane: **Send to ▾** menu (a `MenuFlyout` listing paired online computers); also a global command used later by the tray/hotkey.
6. Tests: with rules set to false `ShareClipboardAsync` returns `Skipped` for that peer (use the existing two-instance fixtures in `TwoComputerTests.cs` as a pattern).

**Done when:** build; tests pass (+2).

---

## D13: Snippets with variables

- **Needs:** B2 · **Size:** L · **Files:** create `src/Mouser.Core/Clipboard/SnippetExpander.cs`, tests `SnippetTests.cs`; storage table; `SnippetsViewModel/View`; nav entry

**Pure function first**
```csharp
public static class SnippetExpander
{
    public static (string Text, int? CursorOffset) Expand(string body, string clipboard, Func<string, string?>? prompt = null, DateTime? now = null);
}
```
Variables: `{{date}}` (yyyy-MM-dd), `{{time}}` (HH:mm), `{{datetime}}`, `{{clipboard}}`, `{{uuid}}`, `{{prompt:Label}}` (calls `prompt("Label")`; null → empty), `{{cursor}}` (removed; its index returned as `CursorOffset` = number of characters **after** it, so the caller can press Left that many times). Unknown `{{x}}` stays literal. Tests (≥ 8): each variable, two clipboards, unknown, cursor offset, nested braces.

**Steps after tests**
1. Table `snippets(id, name, body, folder, favorite, created)`; CRUD in `ClipboardStorageService` (or a new `SnippetStore` sharing the connection string; keep it in the same DB).
2. `SnippetsViewModel`: list + search + editor (Name, Body multi-line TextBox, "Insert variable ▾" menu that appends the tokens). `SnippetsView.axaml` in the 3-pane style (list left, editor right).
3. Sidebar entry **Snippets** under Pinned (nav string `"Snippets"`).
4. Quick paste: typing `!` as the first character switches the overlay to snippets (shows snippet names); Enter expands and pastes (use `ExpandAndPaste`: set the clipboard to the expanded text then Ctrl+V; then send `Left` × `CursorOffset`).
5. (Stretch, separate card later) text abbreviations such as `;sig` need a global key watcher; not in this card.

**Done when:** build; tests pass (+8+); manual: create "Hello {{date}}", paste via quick paste.

---

## D14: Text transforms

- **Needs:** B6 · **Size:** S · **Files:** create `src/Mouser.Core/Clipboard/TextTransforms.cs`, `tests/Mouser.Tests/TextTransformTests.cs`; edit detail pane + quick paste

**Pure functions (each with tests):** `Upper`, `Lower`, `TitleCase`, `Trim`, `RemoveLineBreaks` (join with a space), `SortLines`, `DedupeLines`, `JsonPretty` / `JsonMinify` (`System.Text.Json`, on failure return the input unchanged and expose `TryJsonPretty`), `UrlEncode` / `UrlDecode` (`Uri.EscapeDataString`), `Base64Encode` / `Base64Decode` (invalid input → unchanged), `StripFormatting` (identity on text).
Expose a static `IReadOnlyList<(string Name, Func<string,string> Apply)> All`.

**UI:** detail pane **Transform ▾** menu → applies and shows the result in a "Result" box with **Copy result**; quick paste: pressing **Tab** on a selected item opens the same list; Enter pastes the transformed text.

**Done when:** build; tests pass (+12).

---

## D15: Quick paste v2

- **Needs:** D6, D9, D14 · **Size:** M · **Files:** `QuickPasteWindow.axaml(.cs)`, `QuickPasteViewModel.cs`, `QuickPasteHotkey.cs`, `SettingsStore.cs`, tests in `QuickPasteTests.cs`

**Steps**
1. Rows show thumbnail/badge, title, device and the digit `1`–`9` for the first nine items; pressing a digit pastes that item (when the search box is empty).
2. Tabs across the top: **All / Pinned / each tag** (small `RadioButton`s styled as chips).
3. Image rows paste the image; rich rows follow D7 (Shift = rich).
4. Position the overlay next to the cursor (`GetCursorPos` P/Invoke) clamped to the working area instead of screen centre.
5. Configurable hotkey: `AppSettings.QuickPasteHotkey` string like `"Win+Alt+V"`; `QuickPasteHotkey` takes modifiers and key from a parsed value (write `HotkeyParser.TryParse(string, out uint mods, out uint vk)` as a pure function with tests: letters, digits, F1–F24, modifiers Ctrl/Alt/Shift/Win, invalid → false). Settings shows it in the Hotkeys list (E3 builds the editor; until then it is edited in `settings.json`).
6. Tests: digit selection, tab filtering, `HotkeyParser` cases.

**Done when:** build; tests pass (+6).

---

## D16: Paging, large clips, retention presets, maintenance

- **Needs:** D10 · **Size:** M · **Files:** `ClipboardStorageService.cs`, `ClipboardHistoryViewModel.cs`, `ClipboardHistoryView.axaml`, `SettingsView.axaml`, tests

**Steps**
1. Paging: `GetRecentClipsAsync(limit, offset, favoritesOnly)`; `ClipboardHistoryViewModel.LoadMoreCommand` fires when the list is scrolled within 5 items of the end (a `ScrollViewer.ScrollChanged` handler in the view's code-behind calls the command); search paging the same way (`LIMIT/OFFSET` on the FTS query).
2. Large text: text over 1 MB is stored on disk in `media/<id>.txt` and the DB holds the first 20,000 characters + `media_path` (replace the "Skipped a very large copy" message); cap at 50 MB.
3. Retention presets combo: `1 hour, 24 hours, 7 days, 30 days, 1 year, Forever`. Store as `ClipboardRetentionHours` (keep `ClipboardRetentionDays` readable for old files: migrate once in `Load`). Purge runs at start **and every hour** (in the existing timer: check once per hour with a `DateTime` field).
4. Maintenance buttons (Settings → Clipboard → Advanced): **Clear all history**, **Optimize** (`VACUUM` + `INSERT INTO clips_fts(clips_fts) VALUES('optimize')`), **Rebuild search** (delete and refill `clips_fts` from `clips`), and a size line ("History uses 12.4 MB").
5. **Panic wipe** hotkey (default none) and "Clear history when Mouser quits" toggle (`AppSettings.ClearOnExit`, runs in `desktop.Exit` before dispose; honour starred = keep).
6. Tests: paging returns disjoint pages; preset migration; rebuild keeps search working.

**Done when:** build; tests pass (+4).

---

## D17: More secret detectors (pure)

- **Needs:** none · **Size:** S · **Files:** `src/Mouser.Core/Clipboard/ClipboardPrivacyFilter.cs`, `tests/Mouser.Tests/ClipboardTests.cs`
- **Rule from the repo history (commit f01b60c):** build fake secrets in tests by **string concatenation at runtime** (e.g. `"gh" + "p_" + new string('a', 36)`), never as literals, so secret scanners stay quiet.
- Add regexes + tests: JWT (`eyJ…\.eyJ…\.…`), Slack (`xox[baprs]-…`), Google API key (`AIza[0-9A-Za-z_-]{35}`), Azure storage key (`AccountKey=[A-Za-z0-9+/=]{60,}`), SSH private key already covered, `.env`-style lines where the key name contains `SECRET|TOKEN|PASSWORD|API_KEY` and the value is ≥ 8 chars, IBAN (checksum mod 97), generic "high-entropy single token ≥ 24 chars with mixed classes" (Shannon entropy ≥ 4.0) **only when** it has no spaces.
- Each detector: one positive and one negative test (a normal sentence, a URL, a UUID must stay non-sensitive).
- Also add `Mask` usage: the list row for a sensitive clip (when "Don't save secrets" is off) shows `Mask(text)`; detail pane has a **Reveal** toggle.

**Done when:** build; tests pass (+14).
