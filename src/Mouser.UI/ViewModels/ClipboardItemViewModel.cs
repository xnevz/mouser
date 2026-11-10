using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mouser.UI.ViewModels;

public partial class ClipboardItemViewModel : ViewModelBase
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private string _contentType = "Text"; // Text, Code, Link, Image

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _previewTitle = string.Empty;

    [ObservableProperty]
    private string _sourceDevice = "MacBook Pro";

    [ObservableProperty]
    private DateTime _timestamp = DateTime.UtcNow;

    [ObservableProperty]
    private bool _isPinned;

    public string FormattedTime => Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public string BadgeColor => ContentType switch
    {
        "Code" => "#8b5cf6",
        "Link" => "#06b6d4",
        "Image" => "#ec4899",
        _ => "#3b82f6"
    };
}
