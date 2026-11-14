using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

/// <summary>Local clipboard history: captures copied text, stores it in SQLite and lets you search and re-copy it.</summary>
public partial class ClipboardHistoryViewModel : ViewModelBase
{
    /// <summary>Clips larger than this are not stored (they are usually accidental file-sized copies).</summary>
    public const int MaxClipLength = 1_000_000;
    private const int PageSize = 200;

    private readonly ClipboardStorageService _storage;
    private readonly SettingsStore _settings;
    private IClipboardAccess? _clipboard;
    private string? _lastSeenText;
    private bool _isPolling;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _selectedLibrary = "All";   // All, Starred
    [ObservableProperty] private string _selectedFilter = "All types";    // All types, Text, Link, Code
    [ObservableProperty] private ClipboardItemViewModel? _selectedClip;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _allCount;
    [ObservableProperty] private int _starredCount;

    /// <summary>Shown when the global quick-paste hotkey is available.</summary>
    [ObservableProperty] private string? _quickPasteHint;

    /// <summary>Probes the foreground application and window title.</summary>
    public Func<(string App, string Title)> AppProbe { get; set; } = ForegroundApp.Get;

    public static string[] TypeOptions { get; } = { "All types", "Text", "Link", "Code" };
    public static string[] TimeOptions { get; } = { "Any time", "Today", "Yesterday", "Last 7 days", "Last 30 days" };
    public ObservableCollection<string> DeviceOptions { get; } = new() { "All devices" };
    [ObservableProperty] private string _selectedDevice = "All devices";
    [ObservableProperty] private string _selectedTime = "Any time";
    partial void OnSelectedDeviceChanged(string value) => _ = ReloadAsync();
    partial void OnSelectedTimeChanged(string value) => _ = ReloadAsync();

    public ObservableCollection<ClipboardItemViewModel> FilteredClips { get; } = new();
    public ObservableCollection<ClipboardItemViewModel> History => FilteredClips;
    public bool IsLocked { get; set; }

    public bool IsCaptureEnabled => _settings.Current.ClipboardHistoryEnabled;
    public bool IsEmpty => FilteredClips.Count == 0;

    public ClipboardHistoryViewModel(ClipboardStorageService storage, SettingsStore settings)
    {
        _storage = storage;
        _settings = settings;
    }

    /// <summary>Connects the system clipboard once the main window exists.</summary>
    public void AttachClipboard(IClipboardAccess clipboard) => _clipboard = clipboard;

    public async Task InitializeAsync()
    {
        await _storage.InitializeAsync();
        await _storage.PurgeClipsOlderThanAsync(_settings.Current.ClipboardRetentionDays);
        await ReloadAsync();
    }

    /// <summary>Reads the system clipboard and records new text. Returns true when a clip was added.</summary>
    public async Task<bool> PollClipboardAsync()
    {
        if (IsLocked || IsPaused || _clipboard == null || _isPolling) return false;
        _isPolling = true;
        try
        {
            string? text = await _clipboard.GetTextAsync();
            if (text == null || text == _lastSeenText) return false;
            _lastSeenText = text;
            var (app, _) = AppProbe?.Invoke() ?? ("", "");
            bool captured = await CaptureAsync(text, app);
            if (ShareCapturedText != null && IsShareable(text)) _ = ShareCapturedText(text);
            return captured;
        }
        finally
        {
            _isPolling = false;
        }
    }

    /// <summary>Set by the app to send newly copied text to paired computers.</summary>
    public Func<string, Task>? ShareCapturedText { get; set; }

    private bool IsShareable(string text) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= MaxClipLength &&
        !(_settings.Current.SkipSensitiveClips && ClipboardPrivacyFilter.IsSensitive(text));

    /// <summary>Puts text copied on a paired computer on this clipboard (and in history) without echoing it back.</summary>
    public async Task ApplyRemoteClipAsync(string fromComputer, ClipboardPayload clip)
    {
        if (IsLocked) return;
        string text = clip.TextContent;
        if (_clipboard == null || !IsShareable(text) || text == _lastSeenText) return;

        _lastSeenText = text; // the next poll sees our own write and must not send it back
        await _clipboard.SetTextAsync(text);
        if (_settings.Current.ClipboardHistoryEnabled)
        {
            await _storage.SaveClipAsync(new ClipboardPayload
            {
                ContentType = ClipboardClassifier.Classify(text),
                TextContent = text,
                SourceDevice = fromComputer
            });
            await ReloadAsync();
        }
        StatusMessage = $"Pasted text copied on {fromComputer}.";
    }

    /// <summary>Puts history text back on the clipboard without capturing or syncing it again.</summary>
    public async Task CopyTextAsync(string text)
    {
        if (_clipboard == null) return;
        _lastSeenText = text;
        await _clipboard.SetTextAsync(text);
    }

    public async Task<bool> CaptureAsync(string text, string? sourceApp = null)
    {
        OnPropertyChanged(nameof(IsCaptureEnabled));
        if (!_settings.Current.ClipboardHistoryEnabled || string.IsNullOrWhiteSpace(text)) return false;

        if (text.Length > MaxClipLength)
        {
            StatusMessage = "Skipped a very large copy (over 1 MB of text).";
            return false;
        }

        if (_settings.Current.SkipSensitiveClips && ClipboardPrivacyFilter.IsSensitive(text))
        {
            StatusMessage = "Skipped a copied secret (looks like a key, token or card number).";
            return false;
        }

        sourceApp ??= AppProbe?.Invoke().App ?? string.Empty;

        await _storage.SaveClipAsync(new ClipboardPayload
        {
            ContentType = ClipboardClassifier.Classify(text),
            TextContent = text,
            SourceDevice = Environment.MachineName,
            SourceApp = sourceApp ?? string.Empty
        });
        await ReloadAsync();
        return true;
    }

    partial void OnSearchQueryChanged(string value) => _ = ReloadAsync();
    partial void OnSelectedFilterChanged(string value) => _ = ReloadAsync();
    partial void OnSelectedLibraryChanged(string value) => _ = ReloadAsync();

    [RelayCommand] private void SetLibrary(string library) => SelectedLibrary = library;
    [RelayCommand] private void SetFilter(string filter) => SelectedFilter = filter;

    [RelayCommand]
    private async Task ToggleStarAsync(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        clip.IsPinned = await _storage.ToggleFavoriteAsync(clip.Id);
        StatusMessage = clip.IsPinned ? "Starred. Starred clips are kept forever." : "Removed star.";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task CopyClipAsync(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        await CopyTextAsync(clip.Content);
        StatusMessage = "Copied to clipboard.";
    }

    [RelayCommand]
    private async Task DeleteClipAsync(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        await _storage.DeleteClipAsync(clip.Id);
        StatusMessage = "Clip deleted.";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        int removed = await _storage.ClearAsync(includeFavorites: false);
        StatusMessage = $"Cleared {removed} clip{(removed == 1 ? "" : "s")}. Starred clips were kept.";
        await ReloadAsync();
    }

    private void PopulateDeviceOptions(IEnumerable<ClipboardPayload> raw)
    {
        foreach (var device in raw.Select(c => c.SourceDevice).Where(d => !string.IsNullOrEmpty(d)).Distinct())
        {
            if (!DeviceOptions.Contains(device))
                DeviceOptions.Add(device);
        }
    }

    public async Task ReloadAsync()
    {
        (AllCount, StarredCount) = await _storage.GetCountsAsync();

        var results = string.IsNullOrWhiteSpace(SearchQuery)
            ? await _storage.GetRecentClipsAsync(PageSize, favoritesOnly: SelectedLibrary == "Starred")
            : await _storage.SearchClipsAsync(SearchQuery, PageSize);

        PopulateDeviceOptions(results);

        var items = results.AsEnumerable();
        if (SelectedLibrary == "Starred") items = items.Where(c => c.IsFavorite);
        if (SelectedFilter != "All types" && SelectedFilter != "All")
        {
            if (Enum.TryParse<ClipboardContentType>(SelectedFilter, out var kind)) items = items.Where(c => c.ContentType == kind);
        }
        if (SelectedDevice != "All devices") items = items.Where(c => c.SourceDevice == SelectedDevice);
        var now = DateTime.Now.Date;
        items = SelectedTime switch
        {
            "Today" => items.Where(c => c.CreatedTime.Date == now),
            "Yesterday" => items.Where(c => c.CreatedTime.Date == now.AddDays(-1)),
            "Last 7 days" => items.Where(c => c.CreatedTime.Date >= now.AddDays(-7)),
            "Last 30 days" => items.Where(c => c.CreatedTime.Date >= now.AddDays(-30)),
            _ => items
        };

        string? selectedId = SelectedClip?.Id;
        FilteredClips.Clear();
        foreach (var payload in items) FilteredClips.Add(new ClipboardItemViewModel(payload));

        string? lastLabel = null;
        for (int i = 0; i < FilteredClips.Count; i++)
        {
            var item = FilteredClips[i];
            if (item.DayLabel != lastLabel)
            {
                item.ShowDayHeader = true;
                lastLabel = item.DayLabel;
                int count = 0;
                for (int j = i; j < FilteredClips.Count && FilteredClips[j].DayLabel == lastLabel; j++)
                    count++;
                item.DayCount = count;
            }
            else
            {
                item.ShowDayHeader = false;
                item.DayCount = 0;
            }
        }

        SelectedClip = FilteredClips.FirstOrDefault(c => c.Id == selectedId) ?? FilteredClips.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }
}
