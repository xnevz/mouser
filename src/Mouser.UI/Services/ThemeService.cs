using Avalonia;
using Avalonia.Styling;

namespace Mouser.UI.Services;

/// <summary>
/// Mouser has one fixed design (Styles/Tokens.axaml + App.axaml). The only user choice is whether it
/// renders light, dark, or follows the operating system.
/// </summary>
public static class ThemeService
{
    public static readonly string[] Options = { "System", "Light", "Dark" };

    public static string CurrentMode { get; private set; } = "System";

    public static void Apply(string theme)
    {
        CurrentMode = theme;
        if (Application.Current is not { } app) return;

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            try { Avalonia.Threading.Dispatcher.UIThread.Post(() => Apply(theme)); }
            catch { }
            return;
        }

        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
