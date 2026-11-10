using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mouser.UI.ViewModels;

public partial class ClipboardItemViewModel : ViewModelBase
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private string _contentType = "Text"; // Text, Code, Link, Image, File

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _previewTitle = string.Empty;

    [ObservableProperty]
    private string _sourceDevice = "MacBook Pro";

    [ObservableProperty]
    private string _sourceApp = "VS Code";

    [ObservableProperty]
    private DateTime _timestamp = DateTime.UtcNow;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEncrypted = true;

    public string FormattedTime
    {
        get
        {
            var span = DateTime.UtcNow - Timestamp;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return Timestamp.ToLocalTime().ToString("MMM dd HH:mm");
        }
    }

    public string DeviceIcon => SourceDevice.ToLowerInvariant() switch
    {
        var d when d.Contains("phone") || d.Contains("android") => "📱",
        var d when d.Contains("windows") => "🖥️",
        var d when d.Contains("linux") => "🐧",
        _ => "💻"
    };

    public string TypeIcon => ContentType switch
    {
        "Code" => "💻",
        "Link" => "🔗",
        "Image" => "🖼️",
        "File" => "📁",
        _ => "📝"
    };

    public string BadgeColor => ContentType switch
    {
        "Code" => "#8b5cf6",
        "Link" => "#06b6d4",
        "Image" => "#ec4899",
        "File" => "#f59e0b",
        _ => "#3b82f6"
    };

    public string StarIcon => IsPinned ? "★" : "☆";
    public string StarColor => IsPinned ? "#f59e0b" : "#64748b";

    public int LineCount => string.IsNullOrEmpty(Content) ? 0 : Content.Split('\n').Length;
    public int WordCount => string.IsNullOrEmpty(Content) ? 0 : Content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    public int ByteCount => System.Text.Encoding.UTF8.GetByteCount(Content ?? string.Empty);
    public string FormattedSize => ByteCount > 1024 ? $"{ByteCount / 1024.0:F1} KB" : $"{ByteCount} bytes";

    public string Sha256Checksum
    {
        get
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Content ?? string.Empty));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()[..16] + "...";
        }
    }
}
