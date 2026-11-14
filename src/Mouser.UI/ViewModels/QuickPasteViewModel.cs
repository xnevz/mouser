using System;
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

    public ObservableCollection<ClipboardItemViewModel> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0;

    public string CurrentClipboard { get; set; } = string.Empty;
    public int? LastCursorOffset { get; private set; }

    /// <summary>The user picked a clip; the host puts it on the clipboard and pastes it.</summary>
    public event Action<string, bool>? PasteRequested;
    public event Action? Cancelled;

    public QuickPasteViewModel(ClipboardStorageService storage, IClipboardAccess? clipboard = null)
    {
        _storage = storage;
        _clipboard = clipboard;
    }

    partial void OnQueryChanged(string value) => _ = LoadAsync();

    public async Task LoadAsync()
    {
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
            Selected = Items.FirstOrDefault();
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        // Starred clips first when not searching: they're the ones people paste over and over.
        var clips = string.IsNullOrWhiteSpace(Query)
            ? (await _storage.GetRecentClipsAsync(MaxItems, favoritesOnly: true))
                .Concat(await _storage.GetRecentClipsAsync(MaxItems)).DistinctBy(c => c.Id).Take(MaxItems).ToList()
            : await _storage.SearchClipsAsync(Query, MaxItems);

        Items.Clear();
        foreach (var clip in clips) Items.Add(new ClipboardItemViewModel(clip));
        Selected = Items.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

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

    [RelayCommand]
    private void Paste(ClipboardItemViewModel? item)
    {
        if ((item ?? Selected) is { } clip) DoPaste(clip, false);
    }

    private void DoPaste(ClipboardItemViewModel clip, bool rich)
    {
        if (clip is SnippetQuickPasteItemViewModel snippetVm)
        {
            var (expanded, cursorOffset) = SnippetExpander.Expand(snippetVm.Snippet.Body, CurrentClipboard);
            LastCursorOffset = cursorOffset;
            PasteRequested?.Invoke(expanded, rich);
        }
        else
        {
            LastCursorOffset = null;
            PasteRequested?.Invoke(clip.Content, rich);
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
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
