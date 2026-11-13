using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Mouser.Core.Input;
using Mouser.Core.Networking;
using Mouser.Core.Services;

namespace Mouser.UI.Services;

/// <summary>
/// Owns the network side of the desktop app: pairing, the input receiver and LAN discovery.
/// </summary>
public sealed class MouserHost : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly IInputSink _sink;
    private readonly int _discoveryPort;

    public PairingGate Pairing { get; }
    public InputInjectionService? Input { get; private set; }
    public PeerDiscoveryService? Discovery { get; private set; }
    public string? InputError { get; private set; }
    public string? DiscoveryError { get; private set; }
    public bool CanInject => _sink.IsSupported;

    public IReadOnlyCollection<DiscoveredPeer> Peers =>
        Discovery?.ActivePeers ?? (IReadOnlyCollection<DiscoveredPeer>)Array.Empty<DiscoveredPeer>();

    public MouserHost(SettingsStore settings, IInputSink sink, int discoveryPort = PeerDiscoveryService.DefaultDiscoveryPort)
    {
        _settings = settings;
        _sink = sink;
        _discoveryPort = discoveryPort;
        Pairing = new PairingGate(settings.Current.PairingPin);
    }

    /// <summary>(Re)starts or stops networking to match the current settings.</summary>
    public void Restart()
    {
        StopServices();
        if (!_settings.Current.AcceptRemoteInput) return;

        try
        {
            Input = new InputInjectionService(_sink, Pairing, _settings.Current.InputPort);
            Input.Start();
        }
        catch (SocketException e)
        {
            Input?.Dispose();
            Input = null;
            InputError = e.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? $"Port {_settings.Current.InputPort} is already in use. Is another copy of Mouser running?"
                : $"Couldn't open port {_settings.Current.InputPort}: {e.Message}";
            return;
        }

        try
        {
            Discovery = new PeerDiscoveryService(_settings.Current.DeviceId, Environment.MachineName,
                OsName, Input.BoundPort, controlPort: 0, _discoveryPort);
            Discovery.Start();
        }
        catch (SocketException e)
        {
            Discovery?.Dispose();
            Discovery = null;
            DiscoveryError = $"Phones can't find this PC automatically ({e.Message}). Enter its address manually.";
        }
    }

    public void RegeneratePin()
    {
        int pin = PairingGate.GeneratePin();
        Pairing.Pin = pin;
        _settings.Current.PairingPin = pin;
        _settings.Save();
    }

    public static string OsName =>
        OperatingSystem.IsWindows() ? "Windows" :
        OperatingSystem.IsMacOS() ? "macOS" :
        OperatingSystem.IsLinux() ? "Linux" : "Unknown";

    private void StopServices()
    {
        Discovery?.Dispose();
        Discovery = null;
        Input?.Dispose();
        Input = null;
        InputError = null;
        DiscoveryError = null;
    }

    public void Dispose() => StopServices();
}
