using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Clipboard;

namespace Mouser.UI.ViewModels;

/// <summary>The quick-paste overlay: type to filter recent clips, Enter pastes the selected one.</summary>
public partial class QuickPasteViewModel : ViewModelBase
{
    public const int MaxItems = 12;

    private readonly ClipboardStorageService _storage;

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private ClipboardItemViewModel? _selected;

    public ObservableCollection<ClipboardItemViewModel> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0;

    /// <summary>The user picked a clip; the host puts it on the clipboard and pastes it.</summary>
    public event Action<string>? PasteRequested;
    public event Action? Cancelled;

    public QuickPasteViewModel(ClipboardStorageService storage) => _storage = storage;

    partial void OnQueryChanged(string value) => _ = LoadAsync();

    public async Task LoadAsync()
    {
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

    [RelayCommand]
    private void Paste(ClipboardItemViewModel? item)
    {
        if ((item ?? Selected) is { } clip) PasteRequested?.Invoke(clip.Content);
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}
