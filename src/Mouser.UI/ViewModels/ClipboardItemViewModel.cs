using System;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Mouser.Core.Clipboard;

namespace Mouser.UI.ViewModels;

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
    public string PreviewTitle { get; }
    public string SourceDevice { get; }
    public DateTime Timestamp { get; }
    public string SourceApp => _payload.SourceApp;
    public bool HasSourceApp => !string.IsNullOrWhiteSpace(SourceApp);
    public string? MediaPath => _payload.MediaPath;
    public string? MediaHash => _payload.MediaHash;
    public int? Width => _payload.Width;
    public int? Height => _payload.Height;
    public long? MediaBytes => _payload.MediaBytes;
    public bool IsImage => _payload.ContentType == ClipboardContentType.Image;

    public string DimensionsText => Width.HasValue && Height.HasValue ? $"{Width.Value} × {Height.Value}" : "";

    public string FormattedSizeText
    {
        get
        {
            if (!MediaBytes.HasValue || MediaBytes.Value <= 0) return string.Empty;
            double bytes = MediaBytes.Value;
            if (bytes < 1024) return FormattableString.Invariant($"{bytes:0} B");
            if (bytes < 1024 * 1024) return FormattableString.Invariant($"{bytes / 1024.0:0.#} KB");
            if (bytes < 1024 * 1024 * 1024) return FormattableString.Invariant($"{bytes / (1024.0 * 1024.0):0.#} MB");
            return FormattableString.Invariant($"{bytes / (1024.0 * 1024.0 * 1024.0):0.#} GB");
        }
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
        GC.SuppressFinalize(this);
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
        PreviewTitle = ClipboardClassifier.Title(payload.TextContent);
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
        _ => "¶"
    };

    public string BadgeColor => Kind switch
    {
        ClipboardContentType.Code => "#8b5cf6",
        ClipboardContentType.Link => "#06b6d4",
        ClipboardContentType.Image => "#10b981",
        _ => "#3b82f6"
    };

    public string StarIcon => IsPinned ? "★" : "☆";
    public string StarColor => IsPinned ? "#f59e0b" : "#64748b";

    public int LineCount => string.IsNullOrEmpty(Content) ? 0 : Content.Split('\n').Length;
    public int WordCount => Content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    public int CharCount => Content.Length;
}
