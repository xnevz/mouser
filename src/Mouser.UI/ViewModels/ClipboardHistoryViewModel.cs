using System;
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
    [ObservableProperty] private string _selectedLibrary = "All";   // All, Starred
    [ObservableProperty] private string _selectedFilter = "All";    // All, Text, Link, Code
    [ObservableProperty] private ClipboardItemViewModel? _selectedClip;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _allCount;
    [ObservableProperty] private int _starredCount;

    /// <summary>Shown when the global quick-paste hotkey is available.</summary>
    [ObservableProperty] private string? _quickPasteHint;

    public ObservableCollection<ClipboardItemViewModel> FilteredClips { get; } = new();

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
        if (_clipboard == null || _isPolling) return false;
        _isPolling = true;
        try
        {
            string? text = await _clipboard.GetTextAsync();
            if (text == null || text == _lastSeenText) return false;
            _lastSeenText = text;
            bool captured = await CaptureAsync(text);
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

    public async Task<bool> CaptureAsync(string text)
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

        await _storage.SaveClipAsync(new ClipboardPayload
        {
            ContentType = ClipboardClassifier.Classify(text),
            TextContent = text,
            SourceDevice = Environment.MachineName
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

    public async Task ReloadAsync()
    {
        (AllCount, StarredCount) = await _storage.GetCountsAsync();

        var results = string.IsNullOrWhiteSpace(SearchQuery)
            ? await _storage.GetRecentClipsAsync(PageSize, favoritesOnly: SelectedLibrary == "Starred")
            : await _storage.SearchClipsAsync(SearchQuery, PageSize);

        var items = results.AsEnumerable();
        if (SelectedLibrary == "Starred") items = items.Where(c => c.IsFavorite);
        if (Enum.TryParse<ClipboardContentType>(SelectedFilter, out var kind)) items = items.Where(c => c.ContentType == kind);

        string? selectedId = SelectedClip?.Id;
        FilteredClips.Clear();
        foreach (var payload in items) FilteredClips.Add(new ClipboardItemViewModel(payload));

        SelectedClip = FilteredClips.FirstOrDefault(c => c.Id == selectedId) ?? FilteredClips.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }
}
