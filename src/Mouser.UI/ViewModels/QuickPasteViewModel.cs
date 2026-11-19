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

/// <summary>The quick-paste overlay: type to filter recent clips, Enter pastes the selected one.</summary>
public partial class QuickPasteViewModel : ViewModelBase
{
    public const int MaxItems = 12;

    private readonly ClipboardStorageService _storage;
    private readonly IClipboardAccess? _clipboard;

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private ClipboardItemViewModel? _selected;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveTransform))]
    private string? _activeTransformName;

    public bool HasActiveTransform => !string.IsNullOrEmpty(ActiveTransformName);
    [ObservableProperty] private string _selectedTab = "All";
    [ObservableProperty] private string? _deviceFilter;

    public bool HasDeviceFilter => !string.IsNullOrWhiteSpace(DeviceFilter);
    public string DeviceFilterText => $"From {DeviceFilter}";

    [RelayCommand]
    public void ClearDeviceFilter()
    {
        DeviceFilter = null;
    }

    partial void OnDeviceFilterChanged(string? value)
    {
        OnPropertyChanged(nameof(HasDeviceFilter));
        OnPropertyChanged(nameof(DeviceFilterText));
        _ = LoadAsync();
    }

    public ObservableCollection<QuickPasteTabViewModel> Tabs { get; } = new();
    public ObservableCollection<ClipboardItemViewModel> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0;

    public string CurrentClipboard { get; set; } = string.Empty;
    public int? LastCursorOffset { get; private set; }
    public ClipboardItemViewModel? LastPastedItem { get; private set; }

    /// <summary>The user picked a clip; the host puts it on the clipboard and pastes it.</summary>
    public event Action<string, bool>? PasteRequested;
    public event Action? Cancelled;

    public QuickPasteViewModel(ClipboardStorageService storage, IClipboardAccess? clipboard = null)
    {
        _storage = storage;
        _clipboard = clipboard;
    }

    partial void OnQueryChanged(string value) => _ = LoadAsync();

    partial void OnSelectedTabChanged(string value)
    {
        foreach (var tab in Tabs)
        {
            tab.IsSelected = string.Equals(tab.Name, value, StringComparison.OrdinalIgnoreCase);
        }
        _ = LoadAsync();
    }

    [RelayCommand]
    public void SelectTab(string? tabName)
    {
        if (string.IsNullOrEmpty(tabName)) tabName = "All";
        SelectedTab = tabName;
    }

    public async Task LoadTabsAsync()
    {
        var current = SelectedTab;
        var tags = await _storage.GetTagsAsync();
        var list = new List<QuickPasteTabViewModel>
        {
            new("All", string.Equals(current, "All", StringComparison.OrdinalIgnoreCase)),
            new("Pinned", string.Equals(current, "Pinned", StringComparison.OrdinalIgnoreCase))
        };

        foreach (var tag in tags.Where(t => t.Count > 0))
        {
            list.Add(new(tag.Name, string.Equals(current, tag.Name, StringComparison.OrdinalIgnoreCase)));
        }

        Tabs.Clear();
        foreach (var tab in list)
        {
            Tabs.Add(tab);
        }

        if (!Tabs.Any(t => string.Equals(t.Name, SelectedTab, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedTab = "All";
            if (Tabs.Count > 0) Tabs[0].IsSelected = true;
        }
    }

    public async Task LoadAsync()
    {
        if (Tabs.Count == 0)
        {
            await LoadTabsAsync();
        }

        if (string.IsNullOrEmpty(CurrentClipboard))
        {
            if (_clipboard != null)
            {
                var snap = await _clipboard.GetSnapshotAsync();
                CurrentClipboard = snap.Text ?? string.Empty;
            }
            else
            {
                var recent = await _storage.GetRecentClipsAsync(1);
                if (recent.Count > 0 && !string.IsNullOrEmpty(recent[0].TextContent))
                {
                    CurrentClipboard = recent[0].TextContent;
                }
            }
        }

        if (Query.StartsWith('!'))
        {
            string filter = Query.Length > 1 ? Query[1..].Trim() : string.Empty;
            var allSnippets = await _storage.GetSnippetsAsync();
            var matchedSnippets = string.IsNullOrEmpty(filter)
                ? allSnippets.Take(MaxItems).ToList()
                : allSnippets.Where(s =>
                    s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    s.Body.Contains(filter, StringComparison.OrdinalIgnoreCase)
                  ).Take(MaxItems).ToList();

            Items.Clear();
            foreach (var snippet in matchedSnippets)
            {
                Items.Add(new SnippetQuickPasteItemViewModel(snippet));
            }
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].ShortcutDigit = i < 9 ? (i + 1).ToString() : null;
            }
            Selected = Items.FirstOrDefault();
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        List<ClipboardPayload> clips;
        int fetchCount = HasDeviceFilter ? 100 : MaxItems;

        if (string.Equals(SelectedTab, "Pinned", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Query))
            {
                clips = (await _storage.GetRecentClipsAsync(fetchCount, favoritesOnly: true)).ToList();
            }
            else
            {
                var hits = await _storage.SearchClipsAsync(Query, HasDeviceFilter ? 100 : 50);
                clips = hits.Where(c => c.IsFavorite).ToList();
            }
        }
        else if (!string.Equals(SelectedTab, "All", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(SelectedTab))
        {
            var tagClips = await _storage.GetClipsByTagAsync(SelectedTab, HasDeviceFilter ? 100 : 50);
            if (string.IsNullOrWhiteSpace(Query))
            {
                clips = tagClips.ToList();
            }
            else
            {
                clips = tagClips.Where(c =>
                    (c.TextContent != null && c.TextContent.Contains(Query, StringComparison.OrdinalIgnoreCase)) ||
                    (c.SourceApp != null && c.SourceApp.Contains(Query, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
        }
        else
        {
            // Starred clips first when not searching: they're the ones people paste over and over.
            clips = string.IsNullOrWhiteSpace(Query)
                ? (await _storage.GetRecentClipsAsync(fetchCount, favoritesOnly: true))
                    .Concat(await _storage.GetRecentClipsAsync(fetchCount)).DistinctBy(c => c.Id).ToList()
                : (await _storage.SearchClipsAsync(Query, HasDeviceFilter ? 100 : MaxItems)).ToList();
        }

        if (HasDeviceFilter)
        {
            clips = clips.Where(c => string.Equals(c.SourceDevice, DeviceFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        clips = clips.Take(MaxItems).ToList();

        Items.Clear();
        foreach (var clip in clips)
        {
            Items.Add(new ClipboardItemViewModel(clip));
        }
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].ShortcutDigit = i < 9 ? (i + 1).ToString() : null;
        }
        Selected = Items.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void CycleTransform(int delta = 1)
    {
        if (TextTransforms.All.Count == 0) return;
        int currentIndex = ActiveTransformName == null
            ? -1
            : TextTransforms.All.ToList().FindIndex(t => string.Equals(t.Name, ActiveTransformName, StringComparison.OrdinalIgnoreCase));
        int total = TextTransforms.All.Count + 1;
        int nextIndex = (currentIndex + 1 + delta) % total;
        if (nextIndex < 0) nextIndex += total;
        int transformIndex = nextIndex - 1;
        ActiveTransformName = transformIndex >= 0 ? TextTransforms.All[transformIndex].Name : null;
    }

    public void ApplyNextTransform() => CycleTransform(1);

    public void MoveSelection(int delta)
    {
        if (Items.Count == 0) return;
        int index = Selected == null ? 0 : Items.IndexOf(Selected) + delta;
        Selected = Items[Math.Clamp(index, 0, Items.Count - 1)];
    }

    public void PasteSelected(bool rich = false)
    {
        if (Selected is { } clip) DoPaste(clip, rich);
    }

    public bool PasteDigit(int digit, bool rich = false)
    {
        if (!string.IsNullOrEmpty(Query)) return false;
        int index = digit - 1;
        if (index >= 0 && index < Items.Count)
        {
            DoPaste(Items[index], rich);
            return true;
        }
        return false;
    }

    [RelayCommand]
    private void Paste(ClipboardItemViewModel? item)
    {
        if ((item ?? Selected) is { } clip) DoPaste(clip, false);
    }

    private void DoPaste(ClipboardItemViewModel clip, bool rich)
    {
        Selected = clip;
        LastPastedItem = clip;
        if (clip is SnippetQuickPasteItemViewModel snippetVm)
        {
            var (expanded, cursorOffset) = SnippetExpander.Expand(snippetVm.Snippet.Body, CurrentClipboard);
            LastCursorOffset = cursorOffset;
            PasteRequested?.Invoke(ApplyActiveTransform(expanded), rich);
        }
        else
        {
            LastCursorOffset = null;
            PasteRequested?.Invoke(ApplyActiveTransform(clip.Content), rich);
        }
    }

    public string ApplyActiveTransform(string text)
    {
        if (string.IsNullOrEmpty(ActiveTransformName) || string.IsNullOrEmpty(text))
        {
            return text;
        }
        var transform = TextTransforms.All.FirstOrDefault(t =>
            string.Equals(t.Name, ActiveTransformName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Name.Replace(" ", ""), ActiveTransformName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ||
            (ActiveTransformName.Equals("Uppercase", StringComparison.OrdinalIgnoreCase) && t.Name == "Upper") ||
            (ActiveTransformName.Equals("Lowercase", StringComparison.OrdinalIgnoreCase) && t.Name == "Lower"));
        return transform.Apply != null ? transform.Apply(text) : text;
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}

public partial class QuickPasteTabViewModel : ObservableObject
{
    public string Name { get; }
    public string DisplayName => Name.StartsWith('#') || Name == "All" || Name == "Pinned" ? Name : $"#{Name}";
    [ObservableProperty] private bool _isSelected;

    public QuickPasteTabViewModel(string name, bool isSelected = false)
    {
        Name = name;
        _isSelected = isSelected;
    }
}

public sealed class SnippetQuickPasteItemViewModel : ClipboardItemViewModel
{
    public Snippet Snippet { get; }

    public SnippetQuickPasteItemViewModel(Snippet snippet)
        : base(new ClipboardPayload
        {
            Id = snippet.Id,
            TimestampUtc = snippet.CreatedUtc,
            ContentType = ClipboardContentType.Text,
            TextContent = string.IsNullOrWhiteSpace(snippet.Body) ? snippet.Name : $"{snippet.Name} — {snippet.Body}",
            SourceDevice = "Snippet",
            IsFavorite = snippet.IsFavorite
        })
    {
        Snippet = snippet;
    }
}
