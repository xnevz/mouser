using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Mouser.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Mouser.UI.Views.MainWindow
            {
                DataContext = new Mouser.UI.ViewModels.MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}