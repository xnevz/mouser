using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mouser.UI.ViewModels;

public class PairedDeviceItem
{
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Icon { get; init; } = "💻";
    public bool IsOnline { get; init; } = true;
    public string PingText { get; init; } = "<1ms";
}

public partial class ClipboardHistoryViewModel : ViewModelBase
{
    private readonly ObservableCollection<ClipboardItemViewModel> _allClips = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedLibrary = "All"; // All, Starred, Encrypted, Trash

    [ObservableProperty]
    private string _selectedFilter = "All"; // All, Text, Code, Link, Image, File

    [ObservableProperty]
    private string _selectedDevice = "All";

    [ObservableProperty]
    private ClipboardItemViewModel? _selectedClip;

    [ObservableProperty]
    private string _statusMessage = "Local-First End-to-End Encrypted Sync Active (ChaCha20-Poly1305)";

    [ObservableProperty]
    private int _allCount;

    [ObservableProperty]
    private int _starredCount;

    [ObservableProperty]
    private int _encryptedCount;

    public ObservableCollection<ClipboardItemViewModel> FilteredClips { get; } = new();
    public ObservableCollection<PairedDeviceItem> PairedDevices { get; } = new();

    public ClipboardHistoryViewModel()
    {
        // Seed Paired Devices (UniClipboard Multi-Device Mesh)
        PairedDevices.Add(new PairedDeviceItem { Name = "MacBook Pro 16\"", Role = "Host • macOS Sonoma", Icon = "💻", IsOnline = true, PingText = "Local" });
        PairedDevices.Add(new PairedDeviceItem { Name = "Windows Workstation", Role = "Peer • Windows 11", Icon = "🖥️", IsOnline = true, PingText = "0.8ms" });
        PairedDevices.Add(new PairedDeviceItem { Name = "Linux ThinkPad", Role = "Peer • Ubuntu 24", Icon = "🐧", IsOnline = true, PingText = "1.2ms" });
        PairedDevices.Add(new PairedDeviceItem { Name = "Android Galaxy S24", Role = "Mobile • Wireless LAN", Icon = "📱", IsOnline = true, PingText = "2.1ms" });

        // Seed initial history items with UniClipboard rich payload metadata
        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Code",
            PreviewTitle = "Zero-Allocation UDP Packet Struct",
            Content = "[StructLayout(LayoutKind.Sequential, Pack = 1)]\npublic struct InputMotionPacket\n{\n    public byte Magic;\n    public byte PacketType;\n    public float DeltaX;\n    public float DeltaY;\n    public uint Buttons;\n}",
            SourceDevice = "Windows Workstation",
            SourceApp = "VS Code",
            Timestamp = DateTime.UtcNow.AddMinutes(-2),
            IsPinned = true
        });

        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Link",
            PreviewTitle = "Apple Developer: CGEventTap Documentation",
            Content = "https://developer.apple.com/documentation/coregraphics/cgeventtap",
            SourceDevice = "MacBook Pro",
            SourceApp = "Safari",
            Timestamp = DateTime.UtcNow.AddMinutes(-12),
            IsPinned = true
        });

        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Text",
            PreviewTitle = "Meeting notes & KVM specs",
            Content = "ShareMouse alternative with cross-platform clipboard, UniClipboard design system, and Android air mouse.",
            SourceDevice = "Linux ThinkPad",
            SourceApp = "Obsidian",
            Timestamp = DateTime.UtcNow.AddMinutes(-45),
            IsPinned = false
        });

        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Code",
            PreviewTitle = "Objective-C++ Hook callback",
            Content = "CGEventRef eventTapCallback(CGEventTapProxy proxy, CGEventType type, CGEventRef event, void *refcon)\n{\n    if (type == kCGEventMouseMoved) {\n        CGPoint loc = CGEventGetLocation(event);\n        dispatch_motion(loc.x, loc.y);\n    }\n    return event;\n}",
            SourceDevice = "MacBook Pro",
            SourceApp = "Xcode",
            Timestamp = DateTime.UtcNow.AddHours(-1),
            IsPinned = false
        });

        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Link",
            PreviewTitle = "UniClipboard GitHub Repository",
            Content = "https://github.com/UniClipboard/UniClipboard",
            SourceDevice = "Linux ThinkPad",
            SourceApp = "Firefox",
            Timestamp = DateTime.UtcNow.AddHours(-2),
            IsPinned = true
        });

        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "File",
            PreviewTitle = "mouser-network-topology.drawio.png",
            Content = "/Users/abdelkarim/Documents/mouser/specs/mouser-network-topology.drawio.png (Binary PNG image, 342.1 KB)",
            SourceDevice = "MacBook Pro",
            SourceApp = "Finder",
            Timestamp = DateTime.UtcNow.AddHours(-3),
            IsPinned = false
        });

        _allClips.Add(new ClipboardItemViewModel
        {
            ContentType = "Text",
            PreviewTitle = "Wi-Fi Hotspot Passphrase",
            Content = "Mouser-UltraMesh-WPA3-Secured-2026",
            SourceDevice = "Android Galaxy S24",
            SourceApp = "Settings",
            Timestamp = DateTime.UtcNow.AddHours(-4),
            IsPinned = false
        });

        UpdateCounts();
        RefreshFilter();
    }

    partial void OnSearchQueryChanged(string value) => RefreshFilter();
    partial void OnSelectedFilterChanged(string value) => RefreshFilter();
    partial void OnSelectedLibraryChanged(string value) => RefreshFilter();
    partial void OnSelectedDeviceChanged(string value) => RefreshFilter();

    [RelayCommand]
    private void SetLibrary(string library)
    {
        SelectedLibrary = library;
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        SelectedFilter = filter;
    }

    [RelayCommand]
    private void SetDeviceFilter(string device)
    {
        SelectedDevice = device;
    }

    [RelayCommand]
    private void SelectClip(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        foreach (var c in FilteredClips)
        {
            c.IsSelected = (c == clip);
        }
        SelectedClip = clip;
    }

    [RelayCommand]
    private void ToggleStar(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        clip.IsPinned = !clip.IsPinned;
        UpdateCounts();
        StatusMessage = clip.IsPinned
            ? $"⭐ Starred: {clip.PreviewTitle}"
            : $"Unstarred: {clip.PreviewTitle}";

        if (SelectedLibrary == "Starred")
        {
            RefreshFilter();
        }
    }

    [RelayCommand]
    private void CopyClip(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        StatusMessage = $"✓ Copied to clipboard ({clip.ContentType} from {clip.SourceDevice})";
    }

    [RelayCommand]
    private void SendToDevice(string deviceName)
    {
        if (SelectedClip == null) return;
        StatusMessage = $"✓ Dispatched \"{SelectedClip.PreviewTitle}\" securely to {deviceName} via LAN mesh";
    }

    [RelayCommand]
    private void DeleteClip(ClipboardItemViewModel? clip)
    {
        if (clip == null) return;
        _allClips.Remove(clip);
        FilteredClips.Remove(clip);
        UpdateCounts();
        StatusMessage = "Item removed from clipboard history";
        if (SelectedClip == clip)
        {
            SelectedClip = FilteredClips.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        _allClips.Clear();
        FilteredClips.Clear();
        SelectedClip = null;
        UpdateCounts();
        StatusMessage = "Clipboard history cleared";
    }

    private void UpdateCounts()
    {
        AllCount = _allClips.Count;
        StarredCount = _allClips.Count(c => c.IsPinned);
        EncryptedCount = _allClips.Count(c => c.IsEncrypted);
    }

    private void RefreshFilter()
    {
        FilteredClips.Clear();
        var items = _allClips.AsEnumerable();

        // 1. Library Filter (All, Starred, Encrypted)
        if (SelectedLibrary == "Starred")
        {
            items = items.Where(c => c.IsPinned);
        }
        else if (SelectedLibrary == "Encrypted")
        {
            items = items.Where(c => c.IsEncrypted);
        }

        // 2. Type Filter (All, Text, Code, Link, Image, File)
        if (!string.IsNullOrWhiteSpace(SelectedFilter) && SelectedFilter != "All")
        {
            items = items.Where(c => c.ContentType.Equals(SelectedFilter, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Device Filter
        if (!string.IsNullOrWhiteSpace(SelectedDevice) && SelectedDevice != "All")
        {
            items = items.Where(c => c.SourceDevice.Contains(SelectedDevice, StringComparison.OrdinalIgnoreCase));
        }

        // 4. Search Query (Full-text matching title, content, device, or app)
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            items = items.Where(c =>
                c.PreviewTitle.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                c.Content.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                c.SourceDevice.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                c.SourceApp.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
        {
            FilteredClips.Add(item);
        }

        SelectedClip = FilteredClips.FirstOrDefault();
        if (SelectedClip != null)
        {
            SelectedClip.IsSelected = true;
        }
    }
}
