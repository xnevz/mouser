using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Mouser.Core.Clipboard;

namespace Mouser.UI.ViewModels;

public sealed record FileClipItemInfo(string Path, string Name, string SizeOrType, bool Exists, bool IsDirectory)
{
    public string Icon => !Exists ? "⚠️" : (IsDirectory ? "📁" : "📄");
}

public partial class ClipboardItemViewModel : ViewModelBase, IDisposable
{
    private readonly ClipboardPayload _payload;
    private Bitmap? _thumbnail;
    private bool _thumbnailLoaded;
    private Bitmap? _previewImage;
    private bool _previewImageLoaded;

    public string Id { get; }
    public ClipboardContentType Kind { get; }
    public string Content { get; }
    public string SourceDevice { get; }
    public DateTime Timestamp { get; }
    public string SourceApp => _payload.SourceApp;
    public bool HasSourceApp => !string.IsNullOrWhiteSpace(SourceApp);
    public string? HtmlContent => _payload.HtmlContent;
    public bool HasHtml => !string.IsNullOrEmpty(HtmlContent);
    public string? MediaPath => _payload.MediaPath;
    public string? MediaHash => _payload.MediaHash;
    public int? Width => _payload.Width;
    public int? Height => _payload.Height;
    public long? MediaBytes => _payload.MediaBytes;
    public bool IsImage => _payload.ContentType == ClipboardContentType.Image;

    public bool IsFileClip => _payload.ContentType == ClipboardContentType.FileUri;

    public IReadOnlyList<string> FilePaths =>
        IsFileClip && !string.IsNullOrWhiteSpace(_payload.TextContent)
            ? _payload.TextContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

    public bool HasFiles => FilePaths.Count > 0;

    public bool IsDirectoryOnly => HasFiles && FilePaths.All(p => Directory.Exists(p));

    public bool IsTextClip => !IsImage && !IsFileClip;

    public bool IsImagePlaceholder => IsImage && Thumbnail == null;

    public string FileBadgeText => IsDirectoryOnly ? "FOLDER" : "FILE";

    public string BadgeText => IsFileClip ? FileBadgeText : TypeIcon;

    public string FileTitle
    {
        get
        {
            if (!IsFileClip || !HasFiles) return string.Empty;
            var first = FilePaths[0];
            var firstFileName = Path.GetFileName(first);
            if (string.IsNullOrEmpty(firstFileName))
            {
                firstFileName = first;
            }
            return FilePaths.Count > 1
                ? $"{firstFileName} and {FilePaths.Count - 1} more"
                : firstFileName;
        }
    }

    public string DisplayTitle => IsFileClip && HasFiles ? FileTitle : Content;

    public string PreviewTitle => IsFileClip && HasFiles ? FileTitle : ClipboardClassifier.Title(_payload.TextContent);

    public string FileSubtitle => FilePaths.Count == 1 ? (IsDirectoryOnly ? "1 folder" : "1 file") : $"{FilePaths.Count} items";

    public IReadOnlyList<FileClipItemInfo> FileClipItems
    {
        get
        {
            if (!IsFileClip || !HasFiles) return Array.Empty<FileClipItemInfo>();
            var items = new List<FileClipItemInfo>(FilePaths.Count);
            foreach (var path in FilePaths)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        var name = Path.GetFileName(trimmed);
                        if (string.IsNullOrEmpty(name)) name = path;
                        items.Add(new FileClipItemInfo(path, name, "Folder", true, true));
                    }
                    else if (File.Exists(path))
                    {
                        var fi = new FileInfo(path);
                        var name = Path.GetFileName(path);
                        if (string.IsNullOrEmpty(name)) name = path;
                        items.Add(new FileClipItemInfo(path, name, FormatFileSize(fi.Length), true, false));
                    }
                    else
                    {
                        var name = Path.GetFileName(path);
                        if (string.IsNullOrEmpty(name)) name = path;
                        items.Add(new FileClipItemInfo(path, name, "Missing", false, false));
                    }
                }
                catch
                {
                    items.Add(new FileClipItemInfo(path, path, "Missing", false, false));
                }
            }
            return items;
        }
    }

    public string DimensionsText => Width.HasValue && Height.HasValue ? $"{Width.Value} × {Height.Value}" : "";

    public string FormattedSizeText
    {
        get
        {
            if (!MediaBytes.HasValue || MediaBytes.Value <= 0) return string.Empty;
            return FormatFileSize(MediaBytes.Value);
        }
    }

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return FormattableString.Invariant($"{bytes:0} B");
        if (bytes < 1024 * 1024) return FormattableString.Invariant($"{bytes / 1024.0:0.#} KB");
        if (bytes < 1024 * 1024 * 1024) return FormattableString.Invariant($"{bytes / (1024.0 * 1024.0):0.#} MB");
        return FormattableString.Invariant($"{bytes / (1024.0 * 1024.0 * 1024.0):0.#} GB");
    }

    public string ImageFormatText => !string.IsNullOrEmpty(FormattedSizeText) ? $"PNG · {FormattedSizeText}" : "PNG";

    public Bitmap? Thumbnail
    {
        get
        {
            if (_thumbnailLoaded) return _thumbnail;
            _thumbnailLoaded = true;
            if (!IsImage || string.IsNullOrEmpty(MediaPath) || !File.Exists(MediaPath)) return null;
            try
            {
                using var stream = File.OpenRead(MediaPath);
                _thumbnail = Bitmap.DecodeToWidth(stream, 96);
            }
            catch
            {
                _thumbnail = null;
            }
            return _thumbnail;
        }
    }

    public Bitmap? PreviewImage
    {
        get
        {
            if (_previewImageLoaded) return _previewImage;
            _previewImageLoaded = true;
            if (!IsImage || string.IsNullOrEmpty(MediaPath) || !File.Exists(MediaPath)) return null;
            try
            {
                using var stream = File.OpenRead(MediaPath);
                _previewImage = new Bitmap(stream);
            }
            catch
            {
                _previewImage = null;
            }
            return _previewImage;
        }
    }

    public void Dispose()
    {
        _thumbnail?.Dispose();
        _thumbnail = null;
        _previewImage?.Dispose();
        _previewImage = null;
        Tags.Clear();
        GC.SuppressFinalize(this);
    }

    public ObservableCollection<TagViewModel> Tags { get; } = new();
    public bool HasTags => Tags.Count > 0;

    public void SetTags(IEnumerable<TagViewModel> tags)
    {
        Tags.Clear();
        foreach (var tag in tags)
        {
            Tags.Add(tag);
        }
        OnPropertyChanged(nameof(HasTags));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarIcon), nameof(StarColor))]
    private bool _isPinned;

    [ObservableProperty] private bool _showDayHeader;
    [ObservableProperty] private int _dayCount;

    public ClipboardItemViewModel(ClipboardPayload payload)
    {
        _payload = payload;
        Id = payload.Id;
        Kind = payload.ContentType;
        Content = payload.TextContent;
        SourceDevice = payload.SourceDevice;
        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(payload.TimestampUtc).UtcDateTime;
        _isPinned = payload.IsFavorite;
    }

    public DateTime TimestampLocal => Timestamp.ToLocalTime();

    public string DayLabel
    {
        get
        {
            var d = TimestampLocal.Date;
            var today = DateTime.Now.Date;
            return d == today ? "Today" : d == today.AddDays(-1) ? "Yesterday" : d.ToString("ddd, MMM d");
        }
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
        ClipboardContentType.Image => "🖼️",
        ClipboardContentType.FileUri => IsDirectoryOnly ? "FOLDER" : "FILE",
        _ => "¶"
    };

    public string BadgeColor => Kind switch
    {
        ClipboardContentType.Code => "#8b5cf6",
        ClipboardContentType.Link => "#06b6d4",
        ClipboardContentType.Image => "#10b981",
        ClipboardContentType.FileUri => "#f59e0b",
        _ => "#3b82f6"
    };

    public string StarIcon => IsPinned ? "★" : "☆";
    public string StarColor => IsPinned ? "#f59e0b" : "#64748b";

    public int LineCount => string.IsNullOrEmpty(Content) ? 0 : Content.Split('\n').Length;
    public int WordCount => Content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    public int CharCount => Content.Length;
}
