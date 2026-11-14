using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace Mouser.UI.Services;

public static class ThemeService
{
    public static readonly string[] Options = { "System", "Light", "Dark" };

    public record DesignThemeInfo(string Id, string Name, string Description, string AccentColor, string DarkBg, string LightBg);

    public static readonly DesignThemeInfo[] Designs =
    {
        new("ModernMinimalist", "Modern Minimalist", "Precision slate with electric indigo accent", "#4f46e5", "#0c0d12", "#f5f6fa"),
        new("CyberpunkTerminal", "Cyberpunk Terminal", "Matrix obsidian with neon emerald HUD", "#00e599", "#05070a", "#eaf0ed"),
        new("NordicFrost", "Nordic Frost", "Clean Scandinavian arctic mist with glacier ice", "#38bdf8", "#0b0f17", "#f0f4f9"),
        new("TokyoNight", "Tokyo Night", "Midnight Shibuya dusk with synthwave fuchsia", "#ec4899", "#0f0d1a", "#fbf7fd"),
        new("IndustrialBrutalist", "Industrial Brutalist", "Tactile Braun charcoal with safety amber", "#f97316", "#121214", "#f4f4f2"),
        new("Catppuccin", "Catppuccin", "Cozy velvety espresso with pastel lavender", "#cba6f7", "#11111b", "#dce0e8"),
        new("SolarizedStudio", "Solarized Studio", "Warm creative darkroom with golden bronze", "#d97706", "#0f0e0c", "#fbf7ee")
    };

    public static string CurrentDesign { get; private set; } = "ModernMinimalist";
    public static string CurrentMode { get; private set; } = "System";

    public static void Apply(string theme, string? design = null)
    {
        CurrentMode = theme;
        if (!string.IsNullOrEmpty(design))
        {
            CurrentDesign = design;
        }

        if (Application.Current is not { } app) return; // null in unit tests

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Apply(theme, design));
            }
            catch
            {
            }
            return;
        }

        try
        {
            var uri = new Uri($"avares://Mouser.UI/Styles/Themes/{CurrentDesign}.axaml");
            var dict = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);
            if (app.Resources.MergedDictionaries.Count > 0)
            {
                app.Resources.MergedDictionaries[0] = dict;
            }
            else
            {
                app.Resources.MergedDictionaries.Add(dict);
            }

            var info = Designs.FirstOrDefault(d => d.Id == CurrentDesign);
            if (info != null && Color.TryParse(info.AccentColor, out var accentColor))
            {
                app.Resources["SystemAccentColor"] = accentColor;
            }
        }
        catch
        {
            // Fall back gracefully in headless or unit test contexts
        }

        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default // follows the operating system
        };
    }
}
