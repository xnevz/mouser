using Avalonia;
using Avalonia.Media;
using System;
using Mouser.UI.Services;

namespace Mouser.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        CrashReporter.Install();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Mouser.UI.App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            // Inter everywhere, including popups, tooltips and menus that don't inherit the window's font.
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
            // shortcut: Binding area muted because the clipboard detail pane binds to a null SelectedClip while hidden; re-root that pane's DataContext to restore it.
            .LogToTrace(Avalonia.Logging.LogEventLevel.Warning, Avalonia.Logging.LogArea.Property, Avalonia.Logging.LogArea.Layout, Avalonia.Logging.LogArea.Visual, Avalonia.Logging.LogArea.Control);
}
