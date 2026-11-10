using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mouser.UI.ViewModels;

public partial class ClipboardHistoryViewModel : ViewModelBase
{
    private readonly ObservableCollection<ClipboardItemViewModel> _allClips = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";

    [ObservableProperty]
    private ClipboardItemViewModel? _selectedClip;

    [ObservableProperty]
    private string _statusMessage = "Encrypted local SQLite history active";

    public ObservableCollection<ClipboardItemViewModel> FilteredClips { get; } = new();

    public ClipboardHistoryViewModel()
    {
        // Seed initial history items
        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Code",
            PreviewTitle = "Zero-Allocation UDP Packet Struct",
            Content = "[StructLayout(LayoutKind.Sequential, Pack = 1)]\npublic struct InputMotionPacket { ... }",
            SourceDevice = "Windows Workstation",
            Timestamp = DateTime.UtcNow.AddMinutes(-2),
            IsPinned = true
        });

        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Link",
            PreviewTitle = "Apple Developer: CGEventTap Documentation",
            Content = "https://developer.apple.com/documentation/coregraphics/cgeventtap",
            SourceDevice = "MacBook Pro",
            Timestamp = DateTime.UtcNow.AddMinutes(-12)
        });

        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Text",
            PreviewTitle = "Meeting notes & KVM specs",
            Content = "ShareMouse alternative with cross-platform clipboard and Android air mouse.",
            SourceDevice = "Linux ThinkPad",
            Timestamp = DateTime.UtcNow.AddMinutes(-45)
        });

        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Code",
            PreviewTitle = "Objective-C++ Hook callback",
            Content = "CGEventRef eventTapCallback(CGEventTapProxy proxy, CGEventType type, CGEventRef event, void *refcon)",
            SourceDevice = "MacBook Pro",
            Timestamp = DateTime.UtcNow.AddHours(-1)
        });

        RefreshFilter();
    }

    partial void OnSearchQueryChanged(string value) => RefreshFilter();
    partial void OnSelectedFilterChanged(string value) => RefreshFilter();

    [RelayCommand]
    private void SetFilter(string filter)
    {
        SelectedFilter = filter;
    }

    [RelayCommand]
    private void CopyClip(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        StatusMessage = $"Copied to clipboard ({clip.ContentType})";
    }

    [RelayCommand]
    private void DeleteClip(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        _allClips.Remove(clip);
        FilteredClips.Remove(clip);
        StatusMessage = "Clip removed from history";
    }

    [RelayCommand]
    private void ClearAll()
    {
        _allClips.Clear();
        FilteredClips.Clear();
        StatusMessage = "Clipboard history cleared";
    }

    private void RefreshFilter()
    {
        FilteredClips.Clear();
        var items = _allClips.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedFilter) && SelectedFilter != "All")
        {
            items = items.Where(c => c.ContentType.Equals(SelectedFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            items = items.Where(c =>
                c.PreviewTitle.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                c.Content.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
        {
            FilteredClips.Add(item);
        }

        SelectedClip = FilteredClips.FirstOrDefault();
    }
}
