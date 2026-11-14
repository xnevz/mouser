using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Mouser.Core.Clipboard;

namespace Mouser.UI.ViewModels;

public partial class TagViewModel : ViewModelBase
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _color = "#6366f1";
    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isSystem;

    public string DisplayName => Name.StartsWith("#") ? Name : $"#{Name}";

    public IBrush Brush
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Color)) return Brushes.Gray;
            try
            {
                return SolidColorBrush.Parse(Color);
            }
            catch
            {
                return Brushes.Gray;
            }
        }
    }

    public TagViewModel() { }

    public TagViewModel(string id, string name, string color, int count, bool isSystem)
    {
        _id = id;
        _name = name;
        _color = color;
        _count = count;
        _isSystem = isSystem;
    }

    public TagViewModel(TagInfo info)
        : this(info.Id, info.Name, info.Color, info.Count, info.IsSystem)
    {
    }
}
