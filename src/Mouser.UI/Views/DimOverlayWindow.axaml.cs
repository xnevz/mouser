using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Mouser.UI.Services;

namespace Mouser.UI.Views;

public partial class DimOverlayWindow : Window, IDimOverlayWindow
{
    public Border? DimBorderControl => this.FindControl<Border>("DimBackground");
    public TextBlock? BannerTextControl => this.FindControl<TextBlock>("BannerText");

    public DimOverlayWindow()
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
            // Fallback when instantiated outside an Avalonia XAML application context (e.g. tests)
        }
    }

    public void SetPeerName(string name)
    {
        var text = BannerTextControl;
        if (text != null)
        {
            text.Text = $"Controlling {name}";
        }
    }

    public void SetDimOpacity(double opacity)
    {
        var border = DimBorderControl;
        if (border != null)
        {
            border.Opacity = Math.Clamp(opacity, 0.0, 0.7);
        }
    }
}
