using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mouser.UI.Views;

public partial class ToastWindow : Window
{
    public TextBlock TitleBlock => this.FindControl<TextBlock>("TitleText")!;
    public TextBlock BodyBlock => this.FindControl<TextBlock>("BodyText")!;

    public ToastWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Services.WindowChrome.ApplyRoundedPopup(this);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
