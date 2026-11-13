using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Mouser.Core.Networking;

/// <summary>
/// Local subnet peer discovery service using UDP broadcast/multicast beacons.
/// Enables zero-configuration discovery of Mouser nodes on the LAN.
/// </summary>
public sealed class PeerDiscoveryService : IDisposable
{
    public const int DefaultDiscoveryPort = 58490;
    private static readonly TimeSpan DefaultPeerTimeout = TimeSpan.FromSeconds(5);

    private readonly UdpClient _udpClient;
    private readonly DiscoveredPeer _localPeerInfo;
    private readonly ConcurrentDictionary<string, DiscoveredPeer> _peers = new();
    private readonly int _discoveryPort;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _beaconTask;
    private Task? _pruneTask;
    private bool _isDisposed;

    public event Action<DiscoveredPeer>? PeerDiscovered;
    public event Action<DiscoveredPeer>? PeerLost;

    public IReadOnlyCollection<DiscoveredPeer> ActivePeers => _peers.Values.ToList();

    public PeerDiscoveryService(string deviceId, string hostname, string os, int motionPort, int controlPort, int discoveryPort = DefaultDiscoveryPort)
    {
        _discoveryPort = discoveryPort;
        _localPeerInfo = new DiscoveredPeer
        {
            DeviceId = deviceId,
            Hostname = hostname,
            OperatingSystem = os,
            MotionPort = motionPort,
            ControlPort = controlPort
        };

        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.EnableBroadcast = true;
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _discoveryPort));
    }

    /// <summary>Publishes this computer's desktop bounds so peers can place it next to their screens.</summary>
    public void UpdateLocalScreen(int x, int y, int width, int height)
    {
        lock (_localPeerInfo)
        {
            _localPeerInfo.ScreenX = x;
            _localPeerInfo.ScreenY = y;
            _localPeerInfo.ScreenWidth = width;
            _localPeerInfo.ScreenHeight = height;
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_cts != null) return; // Already running

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        _beaconTask = Task.Run(() => BroadcastBeaconLoopAsync(_cts.Token));
        _pruneTask = Task.Run(() => PruneExpiredPeersLoopAsync(_cts.Token));
    }

    private async Task BroadcastBeaconLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            byte[] beaconPayload;
            lock (_localPeerInfo) beaconPayload = CreateBeaconPayload(_localPeerInfo);

            // Re-resolved every beat so Wi-Fi reconnects and adapter changes are picked up.
            foreach (var address in NetworkInfo.GetBroadcastAddresses())
            {
                try
                {
                    await _udpClient.SendAsync(beaconPayload, beaconPayload.Length, new IPEndPoint(address, _discoveryPort));
                }
                catch (SocketException)
                {
                    // Adapter went down between enumeration and send.
                }
            }

            try
            {
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(token);
                if (TryParseBeaconPayload(result.Buffer, out var peer))
                {
                    // Ignore our own broadcast
                    if (peer.DeviceId == _localPeerInfo.DeviceId) continue;

                    peer.IpAddress = result.RemoteEndPoint.Address;
                    peer.LastSeenUtc = DateTime.UtcNow;

                    // A peer beacons from every adapter (Wi-Fi, Ethernet, Hyper-V/WSL…). Keep the address we first
                    // heard while it stays alive, so links to it don't flip between adapters every second.
                    bool isNew = !_peers.TryGetValue(peer.DeviceId, out var known);
                    if (!isNew) peer.IpAddress = known!.IpAddress;
                    _peers[peer.DeviceId] = peer;

                    if (isNew)
                    {
                        PeerDiscovered?.Invoke(peer);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Transient socket exception
            }
        }
    }

    private async Task PruneExpiredPeersLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, token);

                foreach (var kvp in _peers)
                {
                    if (kvp.Value.IsExpired(DefaultPeerTimeout))
                    {
                        if (_peers.TryRemove(kvp.Key, out var expiredPeer))
                        {
                            PeerLost?.Invoke(expiredPeer);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Non-fatal
            }
        }
    }

    public static byte[] CreateBeaconPayload(DiscoveredPeer peer)
    {
        var dto = new DiscoveryDto
        {
            Id = peer.DeviceId,
            Host = peer.Hostname,
            Os = peer.OperatingSystem,
            MPort = peer.MotionPort,
            CPort = peer.ControlPort,
            X = peer.ScreenX,
            Y = peer.ScreenY,
            W = peer.ScreenWidth,
            H = peer.ScreenHeight
        };
        string json = JsonSerializer.Serialize(dto);
        return Encoding.UTF8.GetBytes("MOUS_DISC:" + json);
    }

    public static bool TryParseBeaconPayload(ReadOnlySpan<byte> data, out DiscoveredPeer peer)
    {
        peer = new DiscoveredPeer();
        string text = Encoding.UTF8.GetString(data);
        if (!text.StartsWith("MOUS_DISC:")) return false;

        string json = text.Substring("MOUS_DISC:".Length);
        try
        {
            var dto = JsonSerializer.Deserialize<DiscoveryDto>(json);
            if (dto == null || string.IsNullOrEmpty(dto.Id)) return false;

            peer.DeviceId = dto.Id;
            peer.Hostname = dto.Host;
            peer.OperatingSystem = dto.Os;
            peer.MotionPort = dto.MPort;
            peer.ControlPort = dto.CPort;
            peer.ScreenX = dto.X;
            peer.ScreenY = dto.Y;
            peer.ScreenWidth = dto.W;
            peer.ScreenHeight = dto.H;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try { Task.WaitAll(new[] { _listenTask, _beaconTask, _pruneTask }.Where(t => t != null).ToArray()!, 300); } catch { }
            _cts.Dispose();
            _cts = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _udpClient.Dispose();
    }

    private sealed class DiscoveryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string Os { get; set; } = string.Empty;
        public int MPort { get; set; }
        public int CPort { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }
}
