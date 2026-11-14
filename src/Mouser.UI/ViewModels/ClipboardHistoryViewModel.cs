using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Clipboard;
using Mouser.Core.Networking;
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
    private string? _lastSeenImageHash;
    private string? _lastSeenFilesText;
    private bool _isPolling;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string? _selectedTag;
    [ObservableProperty] private string _newTagName = string.Empty;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _selectedLibrary = "All";   // All, Starred
    [ObservableProperty] private string _selectedFilter = "All types";    // All types, Text, Link, Code
    [ObservableProperty] private ClipboardItemViewModel? _selectedClip;
    [ObservableProperty] private string _deliverySummaryHeader = "Delivery";
    [ObservableProperty] private bool _hasDeliveries;
    public ObservableCollection<ClipDeliveryViewModel> SelectedClipDeliveries { get; } = new();
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _allCount;
    [ObservableProperty] private int _starredCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTransformResult))]
    private string? _transformResult;

    public bool HasTransformResult => !string.IsNullOrEmpty(TransformResult);

    public static IReadOnlyList<string> AvailableTransforms => TextTransforms.All.Select(t => t.Name).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageStretch))]
    private bool _isImageFitToWindow = true;

    public Stretch ImageStretch => IsImageFitToWindow ? Stretch.Uniform : Stretch.None;

    public string? LastSeenImageHash => _lastSeenImageHash;
    public string? LastSeenText => _lastSeenText;
    public string? LastSeenFilesText => _lastSeenFilesText;

    /// <summary>Invoked to select a destination file path when saving an image clip.</summary>
    public Func<Task<string?>>? PickSavePath { get; set; }

    /// <summary>Invoked to open an image with the default system viewer.</summary>
    public Action<string>? ImageOpener { get; set; }

    /// <summary>Invoked to send a file to a connected peer.</summary>
    public Func<string, Task>? SendFileToPeerAction { get; set; }

    /// <summary>Shown when the global quick-paste hotkey is available.</summary>
    [ObservableProperty] private string? _quickPasteHint;

    /// <summary>Probes the foreground application and window title.</summary>
    public Func<(string App, string Title)> AppProbe { get; set; } = ForegroundApp.Get;

    /// <summary>Probes whether the clipboard content is marked to be excluded by its owner (e.g. password manager).</summary>
    public Func<bool> ExcludedByOwnerProbe { get; set; } = ClipboardFormats.IsExcludedByOwner;

    public static string[] TypeOptions { get; } = { "All types", "Text", "Link", "Code", "Image", "Files" };
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

    /// <summary>Reads the system clipboard and records new text, image, or files. Returns true when a clip was added.</summary>
    public async Task<bool> PollClipboardAsync()
    {
        if (IsLocked || IsPaused || _clipboard == null || _isPolling) return false;
        _isPolling = true;
        try
        {
            var snapshot = await _clipboard.GetSnapshotAsync();

            if (snapshot.Files is { Count: > 0 })
            {
                string filesText = string.Join("\n", snapshot.Files);
                if (filesText == _lastSeenFilesText) return false;

                _lastSeenFilesText = filesText;
                _lastSeenText = null;
                _lastSeenImageHash = null;

                var (fileApp, _) = AppProbe?.Invoke() ?? ("", "");
                if ((ExcludedByOwnerProbe?.Invoke() ?? false) || ClipboardPrivacyFilter.IsIgnoredApp(fileApp, _settings.Current.IgnoredApps))
                {
                    StatusMessage = "Skipped a copy from a password manager.";
                    return false;
                }

                if (!_settings.Current.ClipboardHistoryEnabled) return false;

                var payload = new ClipboardPayload
                {
                    ContentType = ClipboardContentType.FileUri,
                    TextContent = filesText,
                    SourceDevice = string.IsNullOrWhiteSpace(_settings.Current.DeviceName) ? Environment.MachineName : _settings.Current.DeviceName,
                    SourceApp = fileApp ?? string.Empty
                };

                await _storage.SaveClipAsync(payload);
                await ReloadAsync();
                return true;
            }

            if (_settings.Current.SaveImages && snapshot.ImagePng is { Length: > 0 } && string.IsNullOrEmpty(snapshot.Text))
            {
                if (snapshot.ImagePng.Length > 20 * 1024 * 1024)
                {
                    StatusMessage = "Skipped a very large image.";
                    return false;
                }

                var hash = Convert.ToHexString(SHA256.HashData(snapshot.ImagePng)).ToLowerInvariant();
                if (hash == _lastSeenImageHash) return false;

                _lastSeenImageHash = hash;
                _lastSeenText = null;
                _lastSeenFilesText = null;

                var (imgApp, _) = AppProbe?.Invoke() ?? ("", "");
                if ((ExcludedByOwnerProbe?.Invoke() ?? false) || ClipboardPrivacyFilter.IsIgnoredApp(imgApp, _settings.Current.IgnoredApps))
                {
                    StatusMessage = "Skipped a copy from a password manager.";
                    return false;
                }

                var mediaDir = Path.Combine(_settings.DataDirectory, "media");
                Directory.CreateDirectory(mediaDir);
                var mediaPath = Path.Combine(mediaDir, $"{Guid.NewGuid():N}.png");
                await File.WriteAllBytesAsync(mediaPath, snapshot.ImagePng);

                var (w, h) = PngHelper.GetDimensions(snapshot.ImagePng);
                var payload = new ClipboardPayload
                {
                    ContentType = ClipboardContentType.Image,
                    TextContent = $"Image {w}×{h}",
                    MediaPath = mediaPath,
                    MediaHash = hash,
                    Width = w,
                    Height = h,
                    MediaBytes = snapshot.ImagePng.Length,
                    SourceDevice = Environment.MachineName,
                    SourceApp = imgApp ?? string.Empty
                };

                await _storage.SaveClipAsync(payload);
                await ReloadAsync();
                return true;
            }

            string? text = snapshot.Text;
            if (text == null || text == _lastSeenText) return false;
            _lastSeenText = text;
            _lastSeenImageHash = null;
            _lastSeenFilesText = null;
            var (app, _) = AppProbe?.Invoke() ?? ("", "");
            if ((ExcludedByOwnerProbe?.Invoke() ?? false) || ClipboardPrivacyFilter.IsIgnoredApp(app, _settings.Current.IgnoredApps))
            {
                StatusMessage = "Skipped a copy from a password manager.";
                return false;
            }
            var capturedPayload = await CaptureInternalAsync(text, app, snapshot.Html);
            if (capturedPayload == null) return false;

            if (ShareCapturedText != null && IsShareable(text))
            {
                var deliveries = await ShareCapturedText(text);
                if (deliveries != null && deliveries.Count > 0)
                {
                    await _storage.SaveDeliveriesAsync(capturedPayload.Id, deliveries);
                    await LoadDeliveriesForSelectedClipAsync();
                }
            }
            return true;
        }
        finally
        {
            _isPolling = false;
        }
    }

    /// <summary>Set by the app to send newly copied text to paired computers.</summary>
    public Func<string, Task<IReadOnlyList<ClipDelivery>>>? ShareCapturedText { get; set; }

    public ObservableCollection<DiscoveredPeer> SendablePeers { get; } = new();

    /// <summary>Delegate for manually sending a clip directly to a peer.</summary>
    public Func<DiscoveredPeer, string, Task<ClipDelivery>>? SendClipToPeerHandler { get; set; }

    public void UpdateSendablePeers(IEnumerable<DiscoveredPeer> peers)
    {
        var list = peers.ToList();
        if (SendablePeers.SequenceEqual(list)) return;
        SendablePeers.Clear();
        foreach (var p in list)
        {
            SendablePeers.Add(p);
        }
    }

    [RelayCommand]
    private async Task SendClipToPeerAsync(DiscoveredPeer peer)
    {
        if (SelectedClip == null || peer == null || SendClipToPeerHandler == null) return;
        var delivery = await SendClipToPeerHandler(peer, SelectedClip.Content);
        if (delivery != null)
        {
            await _storage.SaveDeliveriesAsync(SelectedClip.Id, new[] { delivery });
            await LoadDeliveriesForSelectedClipAsync();
            StatusMessage = $"Sent clip to {peer.DeviceName}.";
        }
    }

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
        await _clipboard.SetSnapshotAsync(new ClipboardSnapshot(text, null, null, null));
    }

    public async Task<bool> CaptureAsync(string text, string? sourceApp = null, string? html = null)
    {
        var payload = await CaptureInternalAsync(text, sourceApp, html);
        return payload != null;
    }

    private async Task<ClipboardPayload?> CaptureInternalAsync(string text, string? sourceApp = null, string? html = null)
    {
        OnPropertyChanged(nameof(IsCaptureEnabled));
        if (!_settings.Current.ClipboardHistoryEnabled || string.IsNullOrWhiteSpace(text)) return null;

        if (text.Length > MaxClipLength)
        {
            StatusMessage = "Skipped a very large copy (over 1 MB of text).";
            return null;
        }

        if (_settings.Current.SkipSensitiveClips && ClipboardPrivacyFilter.IsSensitive(text))
        {
            StatusMessage = "Skipped a copied secret (looks like a key, token or card number).";
            return null;
        }

        sourceApp ??= AppProbe?.Invoke().App ?? string.Empty;

        var payload = new ClipboardPayload
        {
            ContentType = ClipboardClassifier.Classify(text),
            TextContent = text,
            HtmlContent = html,
            SourceDevice = Environment.MachineName,
            SourceApp = sourceApp ?? string.Empty
        };

        await _storage.SaveClipAsync(payload);
        await ReloadAsync();
        return payload;
    }

    partial void OnSelectedClipChanged(ClipboardItemViewModel? value)
    {
        TransformResult = null;
        _ = LoadDeliveriesForSelectedClipAsync();
    }

    public async Task LoadDeliveriesForSelectedClipAsync()
    {
        SelectedClipDeliveries.Clear();
        if (SelectedClip != null)
        {
            var dels = await _storage.GetDeliveriesAsync(SelectedClip.Id);
            foreach (var d in dels) SelectedClipDeliveries.Add(new ClipDeliveryViewModel(d));
            HasDeliveries = SelectedClipDeliveries.Count > 0;
            int sentCount = SelectedClipDeliveries.Count(d => d.IsDeliveredOrSent);
            DeliverySummaryHeader = HasDeliveries
                ? $"Delivery · {sentCount} of {SelectedClipDeliveries.Count} devices"
                : "Delivery";
        }
        else
        {
            HasDeliveries = false;
            DeliverySummaryHeader = "Delivery";
        }
    }

    partial void OnSearchQueryChanged(string value) => _ = ReloadAsync();
    partial void OnSelectedFilterChanged(string value) => _ = ReloadAsync();
    partial void OnSelectedLibraryChanged(string value) => _ = ReloadAsync();

    public event Func<Task>? TagsChanged;

    [RelayCommand] private void SetLibrary(string library) => SelectedLibrary = library;
    [RelayCommand] private void SetFilter(string filter) => SelectedFilter = filter;

    [RelayCommand]
    public void SetTag(string? tag)
    {
        SelectedTag = tag;
        _ = ReloadAsync();
    }

    [RelayCommand]
    public void ClearTag()
    {
        SelectedTag = null;
        _ = ReloadAsync();
    }

    [RelayCommand]
    public async Task AddTagAsync(string? tagName)
    {
        var name = !string.IsNullOrWhiteSpace(tagName) ? tagName : NewTagName;
        if (SelectedClip == null || string.IsNullOrWhiteSpace(name)) return;
        NewTagName = string.Empty;
        await _storage.AddTagAsync(SelectedClip.Id, name);
        var updated = await _storage.GetClipTagsAsync(SelectedClip.Id);
        SelectedClip.SetTags(updated.Select(t => new TagViewModel(t)));
        if (TagsChanged != null) await TagsChanged.Invoke();
    }

    [RelayCommand]
    public async Task RemoveTagAsync(TagViewModel? tag)
    {
        if (SelectedClip == null || tag == null || tag.IsSystem) return;
        await _storage.RemoveTagAsync(SelectedClip.Id, tag.Id);
        var updated = await _storage.GetClipTagsAsync(SelectedClip.Id);
        SelectedClip.SetTags(updated.Select(t => new TagViewModel(t)));
        if (TagsChanged != null) await TagsChanged.Invoke();
    }

    [RelayCommand] private void ToggleImageZoom() => IsImageFitToWindow = !IsImageFitToWindow;
    [RelayCommand] private void SetImageFit() => IsImageFitToWindow = true;
    [RelayCommand] private void SetImageActualSize() => IsImageFitToWindow = false;

    [RelayCommand]
    private async Task ToggleStarAsync(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        clip.IsPinned = await _storage.ToggleFavoriteAsync(clip.Id);
        StatusMessage = clip.IsPinned ? "Starred. Starred clips are kept forever." : "Removed star.";
        await ReloadAsync();
    }

    [RelayCommand]
    private void ApplyTransform(string? transformName)
    {
        if (string.IsNullOrWhiteSpace(transformName) || SelectedClip == null || !SelectedClip.IsTextClip || string.IsNullOrEmpty(SelectedClip.Content))
        {
            return;
        }

        var transform = TextTransforms.All.FirstOrDefault(t =>
            string.Equals(t.Name, transformName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Name.Replace(" ", ""), transformName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ||
            (transformName.Equals("Uppercase", StringComparison.OrdinalIgnoreCase) && t.Name == "Upper") ||
            (transformName.Equals("Lowercase", StringComparison.OrdinalIgnoreCase) && t.Name == "Lower"));

        if (transform.Apply != null)
        {
            TransformResult = transform.Apply(SelectedClip.Content);
        }
    }

    [RelayCommand]
    private void ClearTransform()
    {
        TransformResult = null;
    }

    [RelayCommand]
    private async Task CopyTransformResultAsync()
    {
        if (string.IsNullOrEmpty(TransformResult)) return;
        await CopyTextAsync(TransformResult);
        StatusMessage = "Transformed text copied to clipboard.";
    }

    [RelayCommand]
    private async Task CopyClipAsync(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        if (clip.IsImage)
        {
            await CopyImageAsync(clip);
            StatusMessage = "Copied to clipboard.";
            return;
        }
        if (clip.IsFileClip)
        {
            await CopyFilesAsync(clip);
            return;
        }
        await CopyTextAsync(clip.Content);
        StatusMessage = "Copied to clipboard.";
    }

    [RelayCommand]
    private async Task CopyFilesAsync(ClipboardItemViewModel? clip)
    {
        var target = clip ?? SelectedClip;
        if (target == null || !target.IsFileClip || !target.HasFiles) return;
        if (_clipboard != null)
        {
            await _clipboard.SetSnapshotAsync(new ClipboardSnapshot(null, null, null, target.FilePaths));
        }
        _lastSeenFilesText = target.Content;
        _lastSeenText = null;
        _lastSeenImageHash = null;
        StatusMessage = target.FilePaths.Count == 1 ? "File copied to clipboard." : $"{target.FilePaths.Count} files copied to clipboard.";
    }

    [RelayCommand]
    private async Task SendFileToPeerAsync(ClipboardItemViewModel? clip)
    {
        var target = clip ?? SelectedClip;
        if (target == null || !target.IsFileClip || !target.HasFiles) return;
        if (SendFileToPeerAction != null)
        {
            await SendFileToPeerAction(target.FilePaths[0]);
            var fileName = Path.GetFileName(target.FilePaths[0]);
            if (string.IsNullOrEmpty(fileName)) fileName = target.FilePaths[0];
            StatusMessage = $"Sending {fileName} to peer…";
        }
        else
        {
            StatusMessage = "No connected computer found to receive files.";
        }
    }

    [RelayCommand]
    private async Task CopyImageAsync(ClipboardItemViewModel? clip)
    {
        var target = clip ?? SelectedClip;
        if (target == null || !target.IsImage || string.IsNullOrEmpty(target.MediaPath) || !File.Exists(target.MediaPath)) return;
        var bytes = await File.ReadAllBytesAsync(target.MediaPath);
        if (bytes is { Length: > 0 })
        {
            if (_clipboard != null)
            {
                await _clipboard.SetSnapshotAsync(new ClipboardSnapshot(null, null, bytes, null));
            }
            _lastSeenImageHash = target.MediaHash;
            _lastSeenText = null;
            StatusMessage = "Image copied to clipboard.";
        }
    }

    [RelayCommand]
    private async Task SaveImageAsAsync(ClipboardItemViewModel? clip)
    {
        var target = clip ?? SelectedClip;
        if (target == null || !target.IsImage || string.IsNullOrEmpty(target.MediaPath) || !File.Exists(target.MediaPath)) return;
        if (PickSavePath == null) return;
        var targetPath = await PickSavePath();
        if (!string.IsNullOrEmpty(targetPath))
        {
            File.Copy(target.MediaPath, targetPath, overwrite: true);
            StatusMessage = $"Image saved to {Path.GetFileName(targetPath)}.";
        }
    }

    [RelayCommand]
    private void OpenImage(ClipboardItemViewModel? clip)
    {
        var target = clip ?? SelectedClip;
        if (target == null || !target.IsImage || string.IsNullOrEmpty(target.MediaPath) || !File.Exists(target.MediaPath)) return;
        if (ImageOpener != null)
        {
            ImageOpener(target.MediaPath);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(target.MediaPath) { UseShellExecute = true });
        }
        catch { }
    }

    [RelayCommand]
    private async Task DeleteClipAsync(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        clip.Dispose();
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

    private static bool MatchesType(ClipboardContentType contentType, string typeFilter)
    {
        return typeFilter.ToLowerInvariant() switch
        {
            "image" => contentType == ClipboardContentType.Image,
            "text" => contentType == ClipboardContentType.Text,
            "code" => contentType == ClipboardContentType.Code,
            "link" => contentType == ClipboardContentType.Link,
            "file" or "folder" => contentType == ClipboardContentType.FileUri,
            _ => false
        };
    }

    public async Task ReloadAsync()
    {
        (AllCount, StarredCount) = await _storage.GetCountsAsync();

        var parsed = Mouser.Core.Clipboard.SearchQuery.Parse(SearchQuery);
        string? activeTag = !string.IsNullOrEmpty(parsed.Tag) ? parsed.Tag : SelectedTag;

        List<ClipboardPayload> results;
        if (!string.IsNullOrWhiteSpace(parsed.Text))
        {
            results = await _storage.SearchClipsAsync(parsed.Text, PageSize);
        }
        else if (!string.IsNullOrEmpty(activeTag))
        {
            results = (await _storage.GetClipsByTagAsync(activeTag, PageSize)).ToList();
        }
        else
        {
            results = await _storage.GetRecentClipsAsync(PageSize, favoritesOnly: SelectedLibrary == "Starred");
        }

        PopulateDeviceOptions(results);

        var items = results.AsEnumerable();
        if (SelectedLibrary == "Starred") items = items.Where(c => c.IsFavorite);

        // Operator filters
        if (!string.IsNullOrEmpty(parsed.Type))
        {
            items = items.Where(c => MatchesType(c.ContentType, parsed.Type));
        }
        if (!string.IsNullOrEmpty(parsed.App))
        {
            items = items.Where(c => !string.IsNullOrEmpty(c.SourceApp) && c.SourceApp.Contains(parsed.App, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrEmpty(parsed.Device))
        {
            items = items.Where(c => !string.IsNullOrEmpty(c.SourceDevice) && c.SourceDevice.Contains(parsed.Device, StringComparison.OrdinalIgnoreCase));
        }
        if (parsed.Before.HasValue)
        {
            items = items.Where(c => c.CreatedTime < parsed.Before.Value);
        }
        if (parsed.After.HasValue)
        {
            items = items.Where(c => c.CreatedTime >= parsed.After.Value);
        }

        // Dropdown UI filters
        if (SelectedFilter != "All types" && SelectedFilter != "All")
        {
            if (SelectedFilter is "Files" or "File") items = items.Where(c => c.ContentType == ClipboardContentType.FileUri);
            else if (Enum.TryParse<ClipboardContentType>(SelectedFilter, out var kind)) items = items.Where(c => c.ContentType == kind);
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

        var clipList = items.ToList();

        var tagsCache = new Dictionary<string, IReadOnlyList<TagInfo>>();
        if (!string.IsNullOrEmpty(activeTag))
        {
            var tagFiltered = new List<ClipboardPayload>();
            foreach (var clip in clipList)
            {
                var clipTags = await _storage.GetClipTagsAsync(clip.Id);
                tagsCache[clip.Id] = clipTags;
                bool matchesTag = clipTags.Any(t =>
                    string.Equals(t.Name, activeTag, StringComparison.OrdinalIgnoreCase) ||
                    (t.Name == "File" && (string.Equals(activeTag, "files", StringComparison.OrdinalIgnoreCase) || string.Equals(activeTag, "folder", StringComparison.OrdinalIgnoreCase))));
                if (matchesTag)
                {
                    tagFiltered.Add(clip);
                }
            }
            clipList = tagFiltered;
        }

        string? selectedId = SelectedClip?.Id;
        foreach (var oldItem in FilteredClips) oldItem.Dispose();
        FilteredClips.Clear();
        foreach (var payload in clipList)
        {
            var itemVm = new ClipboardItemViewModel(payload);
            if (!tagsCache.TryGetValue(itemVm.Id, out var clipTags))
            {
                clipTags = await _storage.GetClipTagsAsync(itemVm.Id);
            }
            itemVm.SetTags(clipTags.Select(t => new TagViewModel(t)));
            FilteredClips.Add(itemVm);
        }

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

        if (TagsChanged != null)
        {
            try
            {
                await TagsChanged.Invoke();
            }
            catch { }
        }
    }
}
