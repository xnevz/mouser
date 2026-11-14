using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mouser.UI.Views;

public partial class LaserOverlayWindow : Window
{
    public LaserOverlayWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        try
        {
            AvaloniaXamlLoader.Load(this);
        }
        catch
        {
            // Fallback outside XAML loader (e.g., in unit tests)
        }
    }

    public void UpdatePosition(int screenX, int screenY)
    {
        Position = new PixelPoint(screenX - 20, screenY - 20);
    }
}
