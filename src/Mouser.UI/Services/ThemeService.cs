using Avalonia;
using Avalonia.Styling;

namespace Mouser.UI.Services;

public static class ThemeService
{
    public static readonly string[] Options = { "System", "Light", "Dark" };

    public static void Apply(string theme)
    {
        if (Application.Current is not { } app) return; // null in unit tests
        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default // follows the operating system
        };
    }
}
