using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Networking;
using Mouser.Core.Services;
using Mouser.UI.Services;

namespace Mouser.UI.ViewModels;

public sealed record RemoteClientItem(string Address, string LastSeenText, string LastEvent, bool IsActive, RemoteClient Source)
{
    public string StatusColor => IsActive ? "#10b981" : "#64748b";
}

/// <summary>Pairing and live status for phones (and other devices) controlling this computer.</summary>
public partial class AndroidRemoteViewModel : ViewModelBase
{
    /// <summary>A phone that stays silent this long is shown as idle (the app pings every second while open).</summary>
    public static readonly TimeSpan ActiveWindow = TimeSpan.FromSeconds(5);

    private readonly MouserHost _host;
    private readonly SettingsStore _settings;
    private readonly Func<DateTime> _clock;

    [ObservableProperty] private string _pairingPinText = string.Empty;
    [ObservableProperty] private string _addressText = string.Empty;
    [ObservableProperty] private int _port;
    [ObservableProperty] private bool _isAccepting;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private int _activeClientCount;

    public ObservableCollection<RemoteClientItem> Clients { get; } = new();

    public bool CanInject => _host.CanInject;
    public bool HasClients => Clients.Count > 0;

    public AndroidRemoteViewModel(MouserHost host, SettingsStore settings, Func<DateTime>? clock = null)
    {
        _host = host;
        _settings = settings;
        _clock = clock ?? (() => DateTime.UtcNow);
        Refresh();
    }

    public void Refresh()
    {
        PairingPinText = PairingGate.FormatPin(_host.Pairing.Pin);
        IsAccepting = _host.Input != null;
        Port = _host.Input?.BoundPort ?? _settings.Current.InputPort;
        ErrorMessage = _host.InputError ?? _host.DiscoveryError;

        var addresses = NetworkInfo.GetLocalIPv4Addresses();
        AddressText = addresses.Count == 0
            ? "No network connection"
            : string.Join("   ", addresses.Select(a => a.ToString()));

        var now = _clock();
        var items = (_host.Input?.Clients ?? Array.Empty<RemoteClient>())
            .Where(c => _host.Pairing.IsTrusted(c.Address))
            .Select(c => new RemoteClientItem(
                c.Address.ToString(),
                FormatAgo(now - c.LastSeenUtc),
                c.LastEvent,
                now - c.LastSeenUtc < ActiveWindow,
                c))
            .ToList();

        Clients.Clear();
        foreach (var item in items) Clients.Add(item);
        ActiveClientCount = items.Count(i => i.IsActive);
        OnPropertyChanged(nameof(HasClients));
    }

    [RelayCommand]
    private void RegeneratePin()
    {
        _host.RegeneratePin();
        StatusMessage = "New PIN created. Paired phones must enter it again.";
        Refresh();
    }

    [RelayCommand]
    private void ToggleAccepting()
    {
        _settings.Current.AcceptRemoteInput = !_settings.Current.AcceptRemoteInput;
        _settings.Save();
        _host.Restart();
        StatusMessage = _settings.Current.AcceptRemoteInput ? "Accepting input from paired phones." : "Remote input is off.";
        Refresh();
    }

    [RelayCommand]
    private void Forget(RemoteClientItem? client)
    {
        if (client == null) return;
        _host.Input?.ForgetClient(client.Source.Address);
        StatusMessage = $"{client.Address} was unpaired.";
        Refresh();
    }

    internal static string FormatAgo(TimeSpan span) =>
        span.TotalSeconds < 5 ? "Active now" :
        span.TotalMinutes < 1 ? $"{(int)span.TotalSeconds}s ago" :
        span.TotalHours < 1 ? $"{(int)span.TotalMinutes}m ago" :
        $"{(int)span.TotalHours}h ago";
}
