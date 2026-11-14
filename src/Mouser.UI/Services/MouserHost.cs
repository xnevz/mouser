using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Mouser.Core.Files;
using Mouser.Core.Geometry;
using Mouser.Core.Input;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.Core.Services;

namespace Mouser.UI.Services;

public enum PeerLinkStatus { NotPaired, Pairing, Paired, Unreachable }

/// <summary>
/// Owns the network side of the desktop app: pairing, the input receiver, LAN discovery and
/// links to other Mouser computers for desktop-to-desktop control.
/// </summary>
public sealed class MouserHost : IDisposable
{
    private static readonly TimeSpan RepairInterval = TimeSpan.FromSeconds(3);

    private readonly SettingsStore _settings;
    private readonly IInputSink _sink;
    private readonly int _discoveryPort;
    private readonly Dictionary<string, PeerLink> _links = new();
    private readonly Dictionary<string, DateTime> _lastPairAttempt = new();
    private readonly Dictionary<string, DateTime> _lastArpAttempt = new();
    private readonly HashSet<string> _pairing = new();
    // Computers that proved (via a hello frame) they paired with us; lets us sync to them by device id.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _pairedWithUs = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Text, DateTime Timestamp)> _failedClips = new();
    private ScreenBounds? _desktop;
    private IReadOnlyList<Mouser.Core.Geometry.Monitor>? _monitors;
    private bool _locked;

    public PairingGate Pairing { get; }
    public InputInjectionService? Input { get; private set; }
    public PeerDiscoveryService? Discovery { get; private set; }
    public PeerTransferService? Transfer { get; private set; }

    /// <summary>Text copied on a paired computer (sender name, clip). Raised on a background thread.</summary>
    public event Action<string, ClipboardPayload>? ClipReceived;

    /// <summary>A file from a paired computer was verified and saved (sender name, path). Background thread.</summary>
    public event Action<string, string>? FileReceived;

    /// <summary>A lock request arrived from a paired computer (sender device id). Background thread.</summary>
    public event Action<string>? LockRequestedByPeer;

    public static string DefaultDownloadDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Mouser");

    private readonly string _downloadDirectory;
    public KvmController? Kvm { get; }
    public string? InputError { get; private set; }
    public string? DiscoveryError { get; private set; }
    public string? KvmError { get; private set; }
    public bool CanInject => _sink.IsSupported;
    public bool CanShareInput => Kvm != null;
    public bool IsLocked => _locked;

    private DateTime? _lastPowerEventUtc;
    public DateTime? LastPowerEventUtc => _lastPowerEventUtc;

    public string DeviceName => _settings.Current.DeviceName;

    public IReadOnlyCollection<DiscoveredPeer> Peers =>
        Discovery?.ActivePeers ?? (IReadOnlyCollection<DiscoveredPeer>)Array.Empty<DiscoveredPeer>();

    public ActivityLog Log { get; } = new();

    public MouserHost(SettingsStore settings, IInputSink sink, IInputCapture? capture = null,
        int discoveryPort = PeerDiscoveryService.DefaultDiscoveryPort, string? downloadDirectory = null)
    {
        _downloadDirectory = downloadDirectory ?? DefaultDownloadDirectory;
        _settings = settings;
        _sink = sink;
        _discoveryPort = discoveryPort;
        Pairing = new PairingGate(settings.Current.PairingPin);
        if (capture != null)
        {
            Kvm = new KvmController(capture);
            if (OperatingSystem.IsWindows())
            {
                Kvm.ForegroundProbe = ForegroundWindowInfo.Get;
            }
            ApplyKvmOptions();
        }
    }

    /// <summary>(Re)starts or stops networking to match the current settings.</summary>
    public void Restart()
    {
        if (_locked) return;
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
            // TCP on the same port number as the UDP input port; falls back to any free port.
            Transfer = CreateTransfer(_settings.Current.InputPort) ?? CreateTransfer(0);
        }
        catch (SocketException)
        {
            Transfer = null; // clipboard sync and file transfer unavailable, input still works
        }

        try
        {
            Discovery = new PeerDiscoveryService(_settings.Current.DeviceId, _settings.Current.DeviceName,
                OsName, Input.BoundPort, controlPort: Transfer?.BoundPort ?? 0, _discoveryPort);
            Discovery.PeerDiscovered += peer =>
            {
                Log.Record(ActivityCategory.Discovery, $"Found peer {peer.Hostname} ({peer.Address})");
            };
            if (_desktop is { } d) Discovery.UpdateLocalScreen((int)d.X, (int)d.Y, (int)d.Width, (int)d.Height);
            if (_monitors is { } m) Discovery.UpdateLocalMonitors(m);
            Discovery.Start();

            foreach (var peer in _settings.Current.ManualPeers.ToList())
            {
                _ = AddManualPeerAsync(peer);
            }
        }
        catch (SocketException e)
        {
            Discovery?.Dispose();
            Discovery = null;
            DiscoveryError = $"Other devices can't find this PC automatically ({e.Message}). Enter its address manually.";
        }
    }

    public void RestartDiscovery() => Discovery?.UpdateHostname(_settings.Current.DeviceName);

    public void SetLocked(bool locked)
    {
        if (_locked == locked) return;
        _locked = locked;
        if (locked)
        {
            Kvm?.Stop();
            StopServices();
        }
        else
        {
            Restart();
            SyncPeers();
        }
    }

    public async Task<bool> AddManualPeerAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        string hostPart = text;
        int port = _discoveryPort;
        int colon = text.LastIndexOf(':');
        if (colon > 0 && int.TryParse(text[(colon + 1)..], out int customPort))
        {
            hostPart = text[..colon];
            port = customPort;
        }

        try
        {
            IPAddress ip;
            if (!IPAddress.TryParse(hostPart, out ip!))
            {
                var addresses = await Dns.GetHostAddressesAsync(hostPart);
                ip = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                     ?? addresses.FirstOrDefault()!;
                if (ip == null) return false;
            }

            Discovery?.AddTarget(ip, port);

            if (!_settings.Current.ManualPeers.Contains(text))
            {
                _settings.Current.ManualPeers.Add(text);
                _settings.Save();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private PeerTransferService? CreateTransfer(int port)
    {
        try
        {
            var transfer = new PeerTransferService(_settings.Current.DeviceId, ReceiveKeys, _downloadDirectory, port);
            transfer.ClipReceived += (sender, clip) =>
            {
                if (_settings.Current.ClipboardSyncEnabled)
                {
                    if (_settings.Current.Peers.TryGetValue(sender, out var peerSettings) && !peerSettings.ReceiveClipboard)
                        return;

                    string from = PeerName(sender);
                    Log.Record(ActivityCategory.Clipboard, $"Received clip from {from}");
                    ClipReceived?.Invoke(from, clip);
                }
            };
            transfer.FileReceived += (sender, path) =>
            {
                string from = PeerName(sender);
                Log.Record(ActivityCategory.File, $"Received file {Path.GetFileName(path)} from {from}");
                FileReceived?.Invoke(from, path);
            };
            transfer.HelloReceived += (sender, key) =>
            {
                if (key.AsSpan().SequenceEqual(OwnKey)) _pairedWithUs[sender] = true;
            };
            transfer.LockRequested += sender =>
            {
                string from = PeerName(sender);
                Log.Record(ActivityCategory.Security, $"Lock request received from {from}");
                LockRequestedByPeer?.Invoke(sender);
            };
            transfer.PowerReceived += (sender, kind) =>
            {
                if (_settings.Current.Peers.TryGetValue(sender, out var peerSettings))
                {
                    var now = DateTime.UtcNow;
                    if (PowerSyncPolicy.ShouldApply(
                        kind,
                        peerSettings.SyncLock,
                        peerSettings.SyncSleep,
                        peerSettings.SyncDisplayOff,
                        _lastPowerEventUtc,
                        now))
                    {
                        _lastPowerEventUtc = now;
                        string from = PeerName(sender);
                        Log.Record(ActivityCategory.Input, $"Received power event {kind} from {from}");
                        switch (kind)
                        {
                            case PowerEventKind.Lock:
                                WindowsPower.Lock();
                                break;
                            case PowerEventKind.Sleep:
                                WindowsPower.Sleep();
                                break;
                            case PowerEventKind.DisplayOff:
                                WindowsPower.TurnOffDisplay();
                                break;
                        }
                    }
                }
            };
            return transfer;
        }
        catch (SocketException) when (port != 0)
        {
            return null;
        }
    }

    private byte[] OwnKey => PeerTransferService.DeriveKey(Pairing.Pin, _settings.Current.DeviceId);

    /// <summary>Keys a paired computer may use: ours if it paired with us, its own if we paired with it.</summary>
    private IReadOnlyList<byte[]> ReceiveKeys(string senderId, IPAddress senderAddress)
    {
        var keys = new List<byte[]>(2);
        if (Pairing.IsTrusted(senderAddress) || _pairedWithUs.ContainsKey(senderId)) keys.Add(OwnKey);
        if (_settings.Current.Peers.TryGetValue(senderId, out var s) && s.Pin is int pin) keys.Add(PeerTransferService.DeriveKey(pin, senderId));
        return keys;
    }

    private byte[]? SendKey(DiscoveredPeer peer)
    {
        if (_settings.Current.Peers.TryGetValue(peer.DeviceId, out var s) && s.Pin is int pin)
            return PeerTransferService.DeriveKey(pin, peer.DeviceId);
        return _pairedWithUs.ContainsKey(peer.DeviceId) || Pairing.IsTrusted(peer.IpAddress) ? OwnKey : null;
    }

    private string PeerName(string deviceId) =>
        Peers.FirstOrDefault(p => p.DeviceId == deviceId)?.Hostname
        ?? (_settings.Current.Peers.TryGetValue(deviceId, out var s) ? s.Name : "another computer");

    /// <summary>Sends copied text to a specific peer.</summary>
    public async Task SendClipAsync(DiscoveredPeer peer, string text)
    {
        if (Transfer == null) throw new InvalidOperationException("Transfer service not available");
        var key = SendKey(peer) ?? throw new InvalidOperationException($"Not paired with {peer.Hostname}");
        if (peer.ControlPort <= 0) throw new InvalidOperationException($"{peer.Hostname} control port is closed");
        var clip = new ClipboardPayload
        {
            TextContent = text,
            SourceDevice = Environment.MachineName,
            ContentType = ClipboardClassifier.Classify(text)
        };
        await Transfer.SendClipAsync(new IPEndPoint(peer.IpAddress, peer.ControlPort), key, clip);
    }

    /// <summary>Sends copied text directly to a specific peer, ignoring the SendClipboard sync rule.</summary>
    public async Task<ClipDelivery> SendClipToAsync(DiscoveredPeer peer, string text)
    {
        string resolvedName = !string.IsNullOrWhiteSpace(peer.Hostname) ? peer.Hostname : peer.DeviceId;
        try
        {
            await SendClipAsync(peer, text);
            return new ClipDelivery(peer.DeviceId, resolvedName, "Sent", null);
        }
        catch (Exception e)
        {
            return new ClipDelivery(peer.DeviceId, resolvedName, "Failed", e.Message);
        }
    }

    /// <summary>Sends copied text to every reachable paired computer.</summary>
    public async Task<IReadOnlyList<ClipDelivery>> ShareClipboardAsync(string text)
    {
        if (!_settings.Current.ClipboardSyncEnabled)
            return Array.Empty<ClipDelivery>();

        var pairedTargets = new Dictionary<string, string>();

        foreach (var (id, s) in _settings.Current.Peers)
        {
            if (s.Pin != null || _pairedWithUs.ContainsKey(id))
            {
                pairedTargets[id] = !string.IsNullOrWhiteSpace(s.Name) ? s.Name : id;
            }
        }

        foreach (var id in _pairedWithUs.Keys)
        {
            if (!pairedTargets.ContainsKey(id))
            {
                pairedTargets[id] = PeerName(id);
            }
        }

        foreach (var peer in Peers)
        {
            if (SendKey(peer) != null && !pairedTargets.ContainsKey(peer.DeviceId))
            {
                pairedTargets[peer.DeviceId] = peer.Hostname;
            }
        }

        if (pairedTargets.Count == 0)
            return Array.Empty<ClipDelivery>();

        var tasks = pairedTargets.Select(async kvp =>
        {
            var (peerId, peerName) = (kvp.Key, kvp.Value);
            var peer = Peers.FirstOrDefault(p => p.DeviceId == peerId);
            string resolvedName = peer != null && !string.IsNullOrWhiteSpace(peer.Hostname) ? peer.Hostname : peerName;

            if (_settings.Current.Peers.TryGetValue(peerId, out var peerSettings) && !peerSettings.SendClipboard)
            {
                return new ClipDelivery(peerId, resolvedName, "Skipped", "Clipboard sending disabled for this device");
            }

            if (peer == null || peer.ControlPort <= 0)
            {
                return new ClipDelivery(peerId, resolvedName, "Skipped", "Not on the network");
            }

            try
            {
                await SendClipAsync(peer, text);
                return new ClipDelivery(peer.DeviceId, resolvedName, "Sent", null);
            }
            catch (Exception e)
            {
                _failedClips[peer.DeviceId] = (text, DateTime.UtcNow);
                return new ClipDelivery(peer.DeviceId, resolvedName, "Failed", e.Message);
            }
        });

        var results = await Task.WhenAll(tasks);
        return results;
    }

    public bool CanSendFileTo(DiscoveredPeer peer) => Transfer != null && peer.ControlPort > 0 && SendKey(peer) != null;

    public Task SendFileAsync(DiscoveredPeer peer, string path, IProgress<FileTransferProgress>? progress = null)
    {
        var key = SendKey(peer) ?? throw new InvalidOperationException($"Pair with {peer.Hostname} first.");
        if (Transfer == null || peer.ControlPort <= 0) throw new InvalidOperationException($"{peer.Hostname} can't receive files.");
        return Transfer.SendFileAsync(new IPEndPoint(peer.IpAddress, peer.ControlPort), key, path, progress);
    }

    public async Task SendPowerEventAsync(DiscoveredPeer peer, PowerEventKind kind)
    {
        if (Transfer == null) throw new InvalidOperationException("Transfer service not available");
        var key = SendKey(peer) ?? throw new InvalidOperationException($"Not paired with {peer.Hostname}");
        if (peer.ControlPort <= 0) throw new InvalidOperationException($"{peer.Hostname} control port is closed");
        _lastPowerEventUtc = DateTime.UtcNow;
        await Transfer.SendPowerAsync(new IPEndPoint(peer.IpAddress, peer.ControlPort), key, kind);
    }

    public async Task BroadcastPowerEventAsync(PowerEventKind kind)
    {
        _lastPowerEventUtc = DateTime.UtcNow;
        if (Transfer == null) return;

        var tasks = new List<Task>();
        foreach (var peer in Peers)
        {
            if (peer.ControlPort <= 0) continue;
            var key = SendKey(peer);
            if (key == null) continue;

            if (_settings.Current.Peers.TryGetValue(peer.DeviceId, out var peerSettings))
            {
                bool enabled = kind switch
                {
                    PowerEventKind.Lock => peerSettings.SyncLock,
                    PowerEventKind.Sleep => peerSettings.SyncSleep,
                    PowerEventKind.DisplayOff => peerSettings.SyncDisplayOff,
                    _ => false
                };
                if (!enabled) continue;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await Transfer.SendPowerAsync(new IPEndPoint(peer.IpAddress, peer.ControlPort), key, kind);
                    }
                    catch
                    {
                        // Ignore broadcast network failures to unreachable peers
                    }
                }));
            }
        }
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    public async Task LockPeersAsync()
    {
        if (Transfer == null) return;

        var tasks = new List<Task>();
        foreach (var peer in Peers)
        {
            if (peer.ControlPort <= 0) continue;
            var key = SendKey(peer);
            if (key == null) continue;

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await Transfer.SendLockAsync(new IPEndPoint(peer.IpAddress, peer.ControlPort), key);
                    Log.Record(ActivityCategory.Security, $"Sent lock request to {peer.Hostname}");
                }
                catch (Exception e) when (e is SocketException or IOException)
                {
                    // Ignore unreachable peers
                }
            }));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>The bounding box of all local monitors, in physical pixels.</summary>
    public void SetLocalDesktop(ScreenBounds desktop)
    {
        _desktop = desktop;
        Kvm?.SetLocalScreen(desktop);
        Discovery?.UpdateLocalScreen((int)desktop.X, (int)desktop.Y, (int)desktop.Width, (int)desktop.Height);
    }

    public void SetLocalMonitors(IReadOnlyList<Mouser.Core.Geometry.Monitor> monitors)
    {
        _monitors = monitors;
        Kvm?.SetLocalMonitors(monitors);
        Discovery?.UpdateLocalMonitors(monitors);
    }

    public void RegeneratePin()
    {
        _pairedWithUs.Clear();
        int pin = PairingGate.GeneratePin();
        Pairing.Pin = pin;
        _settings.Current.PairingPin = pin;
        _settings.Save();
    }

    public PeerLinkStatus GetPeerStatus(string deviceId)
    {
        if (_pairing.Contains(deviceId)) return PeerLinkStatus.Pairing;
        if (!_links.TryGetValue(deviceId, out var link)) return PeerLinkStatus.NotPaired;
        if (link.IsAlive) return PeerLinkStatus.Paired;
        return _settings.Current.Peers.TryGetValue(deviceId, out var s) && s.Pin != null
            ? PeerLinkStatus.Unreachable
            : PeerLinkStatus.NotPaired;
    }

    /// <summary>Pairs with another Mouser computer using the PIN shown on its Phone Remote page.</summary>
    public async Task<bool> PairPeerAsync(DiscoveredPeer peer, int pin)
    {
        var link = EnsureLink(peer);
        _pairing.Add(peer.DeviceId);
        try
        {
            if (!await link.PairAsync(pin))
            {
                Log.Record(ActivityCategory.Error, $"Pairing failed with {peer.Hostname}");
                return false;
            }
        }
        finally
        {
            _pairing.Remove(peer.DeviceId);
        }

        await SendHelloAsync(peer, pin);
        var saved = PeerSettingsFor(peer);
        saved.Pin = pin;
        if (saved.Placement == PeerPlacement.None) saved.Placement = PeerPlacement.Right;
        _settings.Save();
        SyncPeers();
        Log.Record(ActivityCategory.Pairing, $"Paired with {peer.Hostname}");
        return true;
    }

    public void SetPeerPlacement(string peerId, PeerPlacement placement)
    {
        if (!_settings.Current.Peers.TryGetValue(peerId, out var saved))
        {
            var p = Peers.FirstOrDefault(x => x.DeviceId == peerId);
            saved = new PeerSettings { Name = p?.Hostname ?? peerId };
            _settings.Current.Peers[peerId] = saved;
        }
        saved.Placement = placement;
        _settings.Save();
        SyncPeers();
    }

    public void SetPeerPlacement(DiscoveredPeer peer, PeerPlacement placement) =>
        SetPeerPlacement(peer.DeviceId, placement);

    public void SetPeerOffset(string peerId, int offsetX, int offsetY)
    {
        if (!_settings.Current.Peers.TryGetValue(peerId, out var saved))
        {
            var p = Peers.FirstOrDefault(x => x.DeviceId == peerId);
            saved = new PeerSettings { Name = p?.Hostname ?? peerId };
            _settings.Current.Peers[peerId] = saved;
        }
        saved.OffsetX = offsetX;
        saved.OffsetY = offsetY;
        _settings.Save();
        SyncPeers();
    }

    public void SetPeerMotion(string peerId, double mouseSpeed, double scrollSpeed, bool invert)
    {
        if (!_settings.Current.Peers.TryGetValue(peerId, out var saved))
        {
            var p = Peers.FirstOrDefault(x => x.DeviceId == peerId);
            saved = new PeerSettings { Name = p?.Hostname ?? peerId };
            _settings.Current.Peers[peerId] = saved;
        }
        saved.MouseSpeed = mouseSpeed;
        saved.ScrollSpeed = scrollSpeed;
        saved.InvertScroll = invert;
        _settings.Save();
        Kvm?.SetPeerMotion(peerId, mouseSpeed, scrollSpeed, invert);
    }

    public void ForgetPeer(string deviceId)
    {
        _settings.Current.Peers.Remove(deviceId);
        _settings.Save();
        Kvm?.RemovePeer(deviceId);
        if (_links.Remove(deviceId, out var link)) link.Dispose();
        SyncPeers();
    }

    public void SetKvmEnabled(bool enabled)
    {
        _settings.Current.KvmEnabled = enabled;
        _settings.Save();
        SyncPeers();
    }

    public bool JumpTo(string peerId) => Kvm?.JumpTo(peerId) ?? false;
    public bool JumpNext() => Kvm?.JumpNext() ?? false;

    public void ApplyKvmOptions()
    {
        if (Kvm == null) return;
        var feel = KvmOptions.FromFeel(_settings.Current.SwitchFeel);
        var hotkeys = _settings.Current.Hotkeys;
        var jumpList = new List<HotkeyBinding>();
        string[] jumpStrings =
        [
            hotkeys.Jump1, hotkeys.Jump2, hotkeys.Jump3, hotkeys.Jump4, hotkeys.Jump5,
            hotkeys.Jump6, hotkeys.Jump7, hotkeys.Jump8, hotkeys.Jump9
        ];
        foreach (var s in jumpStrings)
        {
            jumpList.Add(HotkeyBinding.TryParse(s, out var b) ? b : default);
        }

        Kvm.ApplyOptions(new KvmOptions
        {
            EdgeResistanceMs = feel.EdgeResistanceMs,
            EdgePushPixels = feel.EdgePushPixels,
            BlockInFullscreen = _settings.Current.BlockInFullscreen,
            BlockInMaximized = _settings.Current.BlockInMaximized,
            BlockInCorners = _settings.Current.BlockInCorners,
            RequireModifier = _settings.Current.RequireModifier,
            DoubleTapOverride = _settings.Current.DoubleTapOverride,
            WrapAround = _settings.Current.WrapAround,
            ProportionalEntry = _settings.Current.ProportionalEntry,
            ReturnControlHotkey = HotkeyBinding.TryParse(hotkeys.ReturnControl, out var ret) ? ret : (HotkeyBinding.TryParse("Ctrl+Alt+Esc", out var defRet) ? defRet : default),
            SecondaryReturnControlHotkey = HotkeyBinding.TryParse("Scroll Lock", out var sec) ? sec : default,
            LockCursorHotkey = HotkeyBinding.TryParse(hotkeys.LockCursor, out var lockHk) ? lockHk : default,
            PanicDoublePressCtrl = hotkeys.PanicDoublePressCtrl,
            JumpHotkeys = jumpList,
            NextComputerHotkey = HotkeyBinding.TryParse(hotkeys.NextComputer, out var nextHk) ? nextHk : (HotkeyBinding.TryParse("Ctrl+Alt+Right", out var defNext) ? defNext : default)
        });
    }

    public void SetSwitchFeel(string feel)
    {
        if (feel != "Relaxed" && feel != "Normal" && feel != "Careful") feel = "Normal";
        _settings.Current.SwitchFeel = feel;
        _settings.Save();
        ApplyKvmOptions();
    }

    public async Task WakePeerAsync(string peerId)
    {
        if (_settings.Current.Peers.TryGetValue(peerId, out var saved) && WakeOnLan.TryParseMac(saved.Mac, out var mac))
        {
            await WakeOnLan.SendAsync(mac);
            Log.Record(ActivityCategory.Network, $"Sent Wake-on-LAN magic packet to {saved.Name} ({saved.Mac})");
        }
    }

    /// <summary>
    /// Brings links and the KVM in line with discovery and settings. Called once a second from the UI:
    /// re-pairs saved peers after restarts, follows IP changes, and runs the input hooks only when needed.
    /// </summary>
    public void SyncPeers()
    {
        if (_locked) return;
        var visible = Peers.ToDictionary(p => p.DeviceId);

        foreach (var (id, saved) in _settings.Current.Peers)
        {
            if (!visible.TryGetValue(id, out var peer))
            {
                Kvm?.RemovePeer(id); // out of sight: never try to hand control to it
                continue;
            }

            if (peer.IpAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                if (DateTime.UtcNow - _lastArpAttempt.GetValueOrDefault(peer.DeviceId) > TimeSpan.FromMinutes(1))
                {
                    _lastArpAttempt[peer.DeviceId] = DateTime.UtcNow;
                    if (WakeOnLan.TryGetMac(peer.IpAddress, out var macBytes))
                    {
                        var macStr = WakeOnLan.FormatMac(macBytes);
                        if (saved.Mac != macStr)
                        {
                            saved.Mac = macStr;
                            _settings.Save();
                        }
                    }
                }
            }

            if (_failedClips.TryRemove(peer.DeviceId, out var failed) && DateTime.UtcNow - failed.Timestamp < TimeSpan.FromMinutes(10))
            {
                _ = SendClipAsync(peer, failed.Text).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
            }

            var link = EnsureLink(peer);
            if (!link.IsPaired && saved.Pin is int pin && !_pairing.Contains(id)
                && DateTime.UtcNow - _lastPairAttempt.GetValueOrDefault(id) > RepairInterval)
            {
                _lastPairAttempt[id] = DateTime.UtcNow;
                _ = RepairAsync(link, peer, pin);
            }

            var bounds = new ScreenBounds { X = peer.ScreenX, Y = peer.ScreenY, Width = peer.ScreenWidth, Height = peer.ScreenHeight };
            Kvm?.SetPeer(id, peer.Hostname, link, bounds, link.IsPaired ? saved.Placement : PeerPlacement.None,
                saved.MouseSpeed, saved.ScrollSpeed, saved.InvertScroll,
                saved.OffsetX, saved.OffsetY,
                peer.Monitors);
        }

        UpdateKvmRunning();
    }

    private async Task RepairAsync(PeerLink link, DiscoveredPeer peer, int pin)
    {
        if (await link.PairAsync(pin)) await SendHelloAsync(peer, pin);
    }

    private async Task SendHelloAsync(DiscoveredPeer peer, int pin)
    {
        if (Transfer == null || peer.ControlPort <= 0) return;
        try
        {
            await Transfer.SendHelloAsync(new IPEndPoint(peer.IpAddress, peer.ControlPort), PeerTransferService.DeriveKey(pin, peer.DeviceId));
        }
        catch (Exception e) when (e is SocketException or IOException)
        {
            // Input pairing still works; clipboard sync back to us waits for the next re-pair.
        }
    }

    private void UpdateKvmRunning()
    {
        if (Kvm == null) return;
        bool shouldRun = _settings.Current.KvmEnabled && _desktop != null &&
            _settings.Current.Peers.Any(p => p.Value.Placement != PeerPlacement.None && p.Value.Pin != null);
        try
        {
            bool wasRunning = Kvm.IsRunning;
            if (shouldRun) Kvm.Start();
            else Kvm.Stop();
            KvmError = null;
            if (wasRunning != shouldRun)
            {
                Log.Record(ActivityCategory.Input, shouldRun ? "Input capture started" : "Input capture stopped");
            }
        }
        catch (InvalidOperationException e)
        {
            KvmError = $"Couldn't capture the mouse and keyboard: {e.Message}";
        }
    }

    private PeerLink EnsureLink(DiscoveredPeer peer)
    {
        var endpoint = new IPEndPoint(peer.IpAddress, peer.MotionPort);
        if (_links.TryGetValue(peer.DeviceId, out var link) && link.Target.Equals(endpoint)) return link;

        link?.Dispose(); // the peer's address or port changed
        link = new PeerLink(endpoint);
        _links[peer.DeviceId] = link;
        _lastPairAttempt.Remove(peer.DeviceId);
        return link;
    }

    private PeerSettings PeerSettingsFor(DiscoveredPeer peer)
    {
        if (!_settings.Current.Peers.TryGetValue(peer.DeviceId, out var saved))
            _settings.Current.Peers[peer.DeviceId] = saved = new PeerSettings();
        saved.Name = peer.Hostname;
        return saved;
    }

    public static string OsName =>
        OperatingSystem.IsWindows() ? "Windows" :
        OperatingSystem.IsMacOS() ? "macOS" :
        OperatingSystem.IsLinux() ? "Linux" : "Unknown";

    private void StopServices()
    {
        Discovery?.Dispose();
        Discovery = null;
        Transfer?.Dispose();
        Transfer = null;
        Input?.Dispose();
        Input = null;
        InputError = null;
        DiscoveryError = null;
    }

    public void Dispose()
    {
        Kvm?.Dispose();
        foreach (var link in _links.Values) link.Dispose();
        _links.Clear();
        StopServices();
    }
}
