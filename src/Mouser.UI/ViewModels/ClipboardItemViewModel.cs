using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Mouser.Core.Clipboard;

namespace Mouser.UI.ViewModels;

public partial class ClipboardItemViewModel : ViewModelBase
{
    public string Id { get; }
    public ClipboardContentType Kind { get; }
    public string Content { get; }
    public string PreviewTitle { get; }
    public string SourceDevice { get; }
    public DateTime Timestamp { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarIcon), nameof(StarColor))]
    private bool _isPinned;

    public ClipboardItemViewModel(ClipboardPayload payload)
    {
        Id = payload.Id;
        Kind = payload.ContentType;
        Content = payload.TextContent;
        PreviewTitle = ClipboardClassifier.Title(payload.TextContent);
        SourceDevice = payload.SourceDevice;
        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(payload.TimestampUtc).UtcDateTime;
        _isPinned = payload.IsFavorite;
    }

    public string ContentType => Kind.ToString();

    public string FormattedTime
    {
        get
        {
            var span = DateTime.UtcNow - Timestamp;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return Timestamp.ToLocalTime().ToString("MMM d, HH:mm");
        }
    }

    public string TypeIcon => Kind switch
    {
        ClipboardContentType.Code => "</>",
        ClipboardContentType.Link => "🔗",
        _ => "¶"
    };

    public string BadgeColor => Kind switch
    {
        ClipboardContentType.Code => "#8b5cf6",
        ClipboardContentType.Link => "#06b6d4",
        _ => "#3b82f6"
    };

    public string StarIcon => IsPinned ? "★" : "☆";
    public string StarColor => IsPinned ? "#f59e0b" : "#64748b";

    public int LineCount => string.IsNullOrEmpty(Content) ? 0 : Content.Split('\n').Length;
    public int WordCount => Content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    public int CharCount => Content.Length;
}
