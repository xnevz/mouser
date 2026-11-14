using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Mouser.Core.Clipboard;

namespace Mouser.UI.ViewModels;

public sealed class ClipDeliveryViewModel : ViewModelBase
{
    private readonly ClipDelivery _delivery;

    public ClipDeliveryViewModel(ClipDelivery delivery)
    {
        _delivery = delivery;
    }

    public string DeviceId => _delivery.DeviceId;
    public string DeviceName => _delivery.DeviceName;
    public string Status => _delivery.Status;
    public string? Error => _delivery.Error;
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string StatusDisplay => Status == "Delivered" ? "Sent" : Status;
    public bool IsDeliveredOrSent => Status == "Sent" || Status == "Delivered";

    public IBrush StatusBrush
    {
        get
        {
            string key = IsDeliveredOrSent ? "Success" : Status == "Failed" ? "Danger" : "TextMuted";
            if (Application.Current != null &&
                Application.Current.TryFindResource(key, out var resource) &&
                resource is IBrush brush)
            {
                return brush;
            }

            return key switch
            {
                "Success" => SolidColorBrush.Parse("#10b981"),
                "Danger" => SolidColorBrush.Parse("#f43f5e"),
                _ => SolidColorBrush.Parse("#94a3b8")
            };
        }
    }
}
