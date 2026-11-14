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

    public record DesignThemeInfo(
        string Id,
        string Name,
        string Description,
        string AccentColor,
        string DarkBg,
        string LightBg,
        double DefaultRadius,
        double DefaultCardRadius,
        double DefaultBorderThickness,
        string DefaultFont = "Segoe UI, Inter, Aptos, Arial, sans-serif"
    );

    public record AccentColorPreset(string Name, string HexColor, string Description);

    public static readonly DesignThemeInfo[] Designs =
    {
        new("ModernMinimalist", "Modern Minimalist", "Precision slate with electric indigo accent", "#4f46e5", "#0c0d12", "#f5f6fa", 8, 12, 1),
        new("CyberpunkTerminal", "Cyberpunk Terminal", "Matrix obsidian with neon emerald HUD", "#00e599", "#05070a", "#eaf0ed", 2, 4, 1.5, "Cascadia Code, Consolas, Courier New, monospace"),
        new("NordicFrost", "Nordic Frost", "Clean Scandinavian arctic mist with glacier ice", "#38bdf8", "#0b0f17", "#f0f4f9", 10, 14, 1),
        new("TokyoNight", "Tokyo Night", "Midnight Shibuya dusk with synthwave fuchsia", "#ec4899", "#0f0d1a", "#fbf7fd", 8, 12, 1),
        new("IndustrialBrutalist", "Industrial Brutalist", "Tactile Braun charcoal with safety amber", "#f97316", "#121214", "#f4f4f2", 0, 0, 2),
        new("Catppuccin", "Catppuccin", "Cozy velvety espresso with pastel lavender", "#cba6f7", "#11111b", "#dce0e8", 12, 16, 1),
        new("SolarizedStudio", "Solarized Studio", "Warm creative darkroom with golden bronze", "#d97706", "#0f0e0c", "#fbf7ee", 6, 10, 1)
    };

    public static readonly string[] CornerRadiusOptions =
    {
        "Theme default",
        "Sharp (0px)",
        "Subtle (4px)",
        "Modern (8px)",
        "Smooth Pill (14px)"
    };

    public static readonly string[] DensityOptions =
    {
        "Compact",
        "Comfortable",
        "Spacious"
    };

    public static readonly string[] FontFamilyOptions =
    {
        "System Default",
        "Modern Sans (Segoe UI, Inter)",
        "Monospace (Cascadia Code)"
    };

    public static readonly AccentColorPreset[] AccentPresets =
    {
        new("Theme Default", "", "Follows active theme's accent"),
        new("Electric Indigo", "#4f46e5", "Modern tech indigo"),
        new("Neon Emerald", "#00e599", "Cyberpunk HUD terminal green"),
        new("Arctic Cyan", "#06b6d4", "Nordic glacier cyan"),
        new("Synthwave Pink", "#ec4899", "Tokyo night vibrant fuchsia"),
        new("Safety Amber", "#f97316", "Industrial high-visibility orange"),
        new("Velvet Lavender", "#cba6f7", "Soft cozy pastel purple"),
        new("Golden Studio", "#d97706", "Warm creative studio bronze"),
        new("Crimson Rose", "#f43f5e", "High impact vivid crimson")
    };

    public static readonly string[] WindowEffectOptions =
    {
        "Solid",
        "Mica / Translucent"
    };

    public static string CurrentDesign { get; private set; } = "ModernMinimalist";
    public static string CurrentMode { get; private set; } = "System";
    public static string CurrentCornerRadius { get; private set; } = "Theme default";
    public static string CurrentDensity { get; private set; } = "Comfortable";
    public static string CurrentFontFamily { get; private set; } = "System Default";
    public static string CurrentAccent { get; private set; } = string.Empty;
    public static string CurrentWindowEffect { get; private set; } = "Solid";
    public static bool CurrentReduceMotion { get; private set; } = false;

    public static void Apply(
        string theme,
        string? design = null,
        string? cornerRadius = null,
        string? density = null,
        string? fontFamily = null,
        string? customAccent = null,
        string? windowEffect = null,
        bool? reduceMotion = null)
    {
        CurrentMode = theme;
        if (!string.IsNullOrEmpty(design)) CurrentDesign = design;
        if (!string.IsNullOrEmpty(cornerRadius)) CurrentCornerRadius = cornerRadius;
        if (!string.IsNullOrEmpty(density)) CurrentDensity = density;
        if (!string.IsNullOrEmpty(fontFamily)) CurrentFontFamily = fontFamily;
        if (customAccent != null) CurrentAccent = customAccent;
        if (!string.IsNullOrEmpty(windowEffect)) CurrentWindowEffect = windowEffect;
        if (reduceMotion.HasValue) CurrentReduceMotion = reduceMotion.Value;

        if (Application.Current is not { } app) return;

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Apply(
                    theme, design, cornerRadius, density, fontFamily, customAccent, windowEffect, reduceMotion));
            }
            catch { }
            return;
        }

        var designInfo = Designs.FirstOrDefault(d => d.Id == CurrentDesign) ?? Designs[0];

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
        }
        catch { }

        // 1. Geometry / Corner Radius
        double ctrlRadius, cardRadius, pillRadius, cardBorder;
        string cr = CurrentCornerRadius.Trim();
        if (cr.StartsWith("Sharp", StringComparison.OrdinalIgnoreCase))
        {
            ctrlRadius = 0; cardRadius = 0; pillRadius = 0; cardBorder = 2;
        }
        else if (cr.StartsWith("Subtle", StringComparison.OrdinalIgnoreCase))
        {
            ctrlRadius = 4; cardRadius = 6; pillRadius = 8; cardBorder = 1;
        }
        else if (cr.StartsWith("Modern", StringComparison.OrdinalIgnoreCase))
        {
            ctrlRadius = 8; cardRadius = 12; pillRadius = 16; cardBorder = 1;
        }
        else if (cr.StartsWith("Smooth", StringComparison.OrdinalIgnoreCase))
        {
            ctrlRadius = 14; cardRadius = 18; pillRadius = 24; cardBorder = 1;
        }
        else // Theme default
        {
            ctrlRadius = designInfo.DefaultRadius;
            cardRadius = designInfo.DefaultCardRadius;
            pillRadius = Math.Max(ctrlRadius * 1.5, 8);
            cardBorder = designInfo.DefaultBorderThickness;
        }

        app.Resources["ControlCornerRadius"] = new CornerRadius(ctrlRadius);
        app.Resources["CardCornerRadius"] = new CornerRadius(cardRadius);
        app.Resources["PillCornerRadius"] = new CornerRadius(pillRadius);
        app.Resources["CardBorderThickness"] = new Thickness(cardBorder);

        // 2. Density & Spacing
        string dens = CurrentDensity.Trim();
        if (dens.StartsWith("Compact", StringComparison.OrdinalIgnoreCase))
        {
            app.Resources["DensityPadding"] = new Thickness(10, 5);
            app.Resources["NavPadding"] = new Thickness(10, 6);
            app.Resources["CardPadding"] = new Thickness(14);
            app.Resources["ListItemPadding"] = new Thickness(8, 4);
            app.Resources["BaseFontSize"] = 12.0;
        }
        else if (dens.StartsWith("Spacious", StringComparison.OrdinalIgnoreCase))
        {
            app.Resources["DensityPadding"] = new Thickness(18, 12);
            app.Resources["NavPadding"] = new Thickness(16, 12);
            app.Resources["CardPadding"] = new Thickness(24);
            app.Resources["ListItemPadding"] = new Thickness(16, 10);
            app.Resources["BaseFontSize"] = 14.0;
        }
        else // Comfortable
        {
            app.Resources["DensityPadding"] = new Thickness(14, 8);
            app.Resources["NavPadding"] = new Thickness(12, 9);
            app.Resources["CardPadding"] = new Thickness(20);
            app.Resources["ListItemPadding"] = new Thickness(12, 8);
            app.Resources["BaseFontSize"] = 13.0;
        }

        // 3. Typography & Font Family
        string font = CurrentFontFamily.Trim();
        if (font.StartsWith("Mono", StringComparison.OrdinalIgnoreCase))
        {
            app.Resources["AppFontFamily"] = new FontFamily("Cascadia Code, Consolas, Courier New, monospace");
        }
        else if (font.StartsWith("Modern", StringComparison.OrdinalIgnoreCase))
        {
            app.Resources["AppFontFamily"] = new FontFamily("Segoe UI, Inter, Aptos, Arial, sans-serif");
        }
        else // System Default
        {
            app.Resources["AppFontFamily"] = new FontFamily(designInfo.DefaultFont);
        }

        // 4. Accent Color
        string accentHex = !string.IsNullOrWhiteSpace(CurrentAccent) ? CurrentAccent : designInfo.AccentColor;
        if (Color.TryParse(accentHex, out var accentColor))
        {
            app.Resources["SystemAccentColor"] = accentColor;
            app.Resources["Accent"] = new SolidColorBrush(accentColor);
            app.Resources["AccentHover"] = new SolidColorBrush(Color.FromRgb(
                (byte)Math.Min(255, accentColor.R + 25),
                (byte)Math.Min(255, accentColor.G + 25),
                (byte)Math.Min(255, accentColor.B + 25)));
            app.Resources["AccentPressed"] = new SolidColorBrush(Color.FromRgb(
                (byte)Math.Max(0, accentColor.R - 25),
                (byte)Math.Max(0, accentColor.G - 25),
                (byte)Math.Max(0, accentColor.B - 25)));
            app.Resources["AccentSoft"] = new SolidColorBrush(Color.FromArgb(200, accentColor.R, accentColor.G, accentColor.B));
        }

        // 5. Theme Variant
        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
