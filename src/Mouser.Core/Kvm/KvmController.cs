using Mouser.Core.Geometry;
using Mouser.Core.Input;
using Mouser.Core.Networking;

namespace Mouser.Core.Kvm;

/// <summary>Where a peer computer sits relative to this one.</summary>
public enum PeerPlacement { None, Left, Right, Above, Below }

/// <summary>
/// Desktop-to-desktop mouse and keyboard sharing. Watches local input; when the cursor is pushed past an
/// edge that has a peer behind it, local input is swallowed and forwarded to that peer as absolute cursor
/// positions plus buttons, wheel and keys. Leaving the peer's screen through the same edge returns control.
/// Ctrl+Alt+Esc (or Scroll Lock) returns control at any time; pressed while local it pins the cursor to this screen.
/// </summary>
public sealed class KvmController : IDisposable
{
    public static readonly TimeSpan WatchdogInterval = TimeSpan.FromMilliseconds(250);

    private const ushort VkEscape = 0x1B;
    private const ushort VkScrollLock = 0x91;

    private sealed record Peer(string Id, string Name, PeerLink Link, ScreenBounds Remote, PeerPlacement Placement, ScreenBounds Placed);

    private readonly IInputCapture _capture;
    private readonly VirtualKvmManager _edges = new();
    private readonly object _lock = new();
    private readonly Dictionary<string, Peer> _peers = new();
    private readonly HashSet<ushort> _physicalKeys = new();
    private readonly HashSet<ushort> _remoteKeys = new();
    private byte _remoteButtons;
    private Peer? _active;
    private int _virtualX, _virtualY;
    private int _parkX, _parkY;
    private ScreenBounds? _local;
    private Timer? _watchdog;
    private bool _stateDirty;

    public bool IsRunning { get; private set; }
    public bool IsScreenLocked => _edges.IsScreenLocked;
    public string? ActivePeerId { get { lock (_lock) return _active?.Id; } }
    public string? ActivePeerName { get { lock (_lock) return _active?.Name; } }

    /// <summary>Raised (on the capture or timer thread) when control moves between computers or the lock toggles.</summary>
    public event Action? StateChanged;

    public KvmController(IInputCapture capture)
    {
        _capture = capture;
        _capture.Handler = Handle;
        _edges.CursorTransitionedToRemote += (_, e) => EnterPeer(e);
    }

    public void Start()
    {
        if (IsRunning) return;
        _capture.Start();
        _watchdog = new Timer(_ => CheckPeerAlive(), null, WatchdogInterval, WatchdogInterval);
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;
        lock (_lock)
        {
            if (_active != null) ReturnToLocal(null);
        }
        _watchdog?.Dispose();
        _watchdog = null;
        _capture.Stop();
        IsRunning = false;
        RaiseIfDirty();
    }

    /// <summary>Bounding box of this computer's whole desktop, in physical pixels.</summary>
    public void SetLocalScreen(ScreenBounds desktop)
    {
        lock (_lock)
        {
            _local = desktop;
            _edges.SetLocalScreen(desktop);
            _parkX = (int)(desktop.X + desktop.Width / 2);
            _parkY = (int)(desktop.Y + desktop.Height / 2);
            // Peers are placed relative to our desktop, so re-place them all.
            var peers = _peers.Values.ToList();
            _peers.Clear();
            foreach (var peer in peers)
            {
                _edges.RemoveRemoteScreen(peer.Id);
                SetPeerLocked(peer.Id, peer.Name, peer.Link, peer.Remote, peer.Placement);
            }
        }
    }

    /// <param name="remoteDesktop">The peer's desktop bounds in the peer's own coordinates (from its beacon).</param>
    public void SetPeer(string id, string name, PeerLink link, ScreenBounds remoteDesktop, PeerPlacement placement)
    {
        lock (_lock) SetPeerLocked(id, name, link, remoteDesktop, placement);
        RaiseIfDirty();
    }

    public void RemovePeer(string id)
    {
        lock (_lock)
        {
            if (_active?.Id == id) ReturnToLocal(null);
            _peers.Remove(id);
            _edges.RemoveRemoteScreen(id);
        }
        RaiseIfDirty();
    }

    private void SetPeerLocked(string id, string name, PeerLink link, ScreenBounds remote, PeerPlacement placement)
    {
        if (_peers.TryGetValue(id, out var existing) && existing.Link == link && existing.Remote == remote
            && existing.Placement == placement && existing.Name == name)
            return; // called every second by the app; nothing changed

        if (placement == PeerPlacement.None && !_peers.ContainsKey(id)) return;

        if (_active?.Id == id && (_active.Link != link || placement == PeerPlacement.None)) ReturnToLocal(null);
        _edges.RemoveRemoteScreen(id);
        _peers.Remove(id);
        if (placement == PeerPlacement.None || _local == null || remote.Width <= 0 || remote.Height <= 0) return;

        var placed = Place(_local, remote, placement) with { HostId = id, Hostname = name };
        var updated = new Peer(id, name, link, remote, placement, placed);
        _peers[id] = updated;
        _edges.AddRemoteScreen(placed);
        if (_active?.Id == id) _active = updated;
    }

    /// <summary>Puts the peer's desktop next to ours in our coordinate space, aligned to our top/left edge.</summary>
    private static ScreenBounds Place(ScreenBounds local, ScreenBounds remote, PeerPlacement placement) => placement switch
    {
        PeerPlacement.Right => new ScreenBounds { X = local.Right, Y = local.Y, Width = remote.Width, Height = remote.Height },
        PeerPlacement.Left => new ScreenBounds { X = local.X - remote.Width, Y = local.Y, Width = remote.Width, Height = remote.Height },
        PeerPlacement.Below => new ScreenBounds { X = local.X, Y = local.Bottom, Width = remote.Width, Height = remote.Height },
        _ => new ScreenBounds { X = local.X, Y = local.Y - remote.Height, Width = remote.Width, Height = remote.Height }
    };

    /// <summary>The capture handler. Returns true to swallow the event locally.</summary>
    internal bool Handle(CapturedInput e)
    {
        bool swallow;
        lock (_lock)
        {
            swallow = HandleLocked(e);
        }
        RaiseIfDirty();
        return swallow;
    }

    private bool HandleLocked(CapturedInput e)
    {
        if (e.Kind == CaptureKind.Key)
        {
            if (e.IsDown) _physicalKeys.Add(e.Key);
            else _physicalKeys.Remove(e.Key);

            if (e.IsDown && IsHotkey(e.Key))
            {
                if (_active != null) ReturnToLocal(null);
                else _edges.ToggleScreenLock();
                _stateDirty = true;
                return true;
            }
        }

        if (_active == null)
        {
            // EnterPeer may refuse an unreachable peer, so only swallow when control really moved.
            return e.Kind == CaptureKind.Move && _peers.Count > 0
                && _edges.ProcessLocalMotion(e.X, e.Y, ClampShort(e.Dx), ClampShort(e.Dy))
                && _active != null;
        }

        var link = _active.Link;
        switch (e.Kind)
        {
            case CaptureKind.Move:
                _virtualX += e.Dx;
                _virtualY += e.Dy;
                if (LeftThroughReturnEdge(out int localX, out int localY))
                {
                    ReturnToLocal((localX, localY));
                    return true;
                }
                var r = _active.Remote;
                _virtualX = Math.Clamp(_virtualX, (int)r.X, (int)r.Right - 1);
                _virtualY = Math.Clamp(_virtualY, (int)r.Y, (int)r.Bottom - 1);
                link.Send(InputMotionPacket.TypeMoveAbsolute, ClampShort(_virtualX), ClampShort(_virtualY));
                return true;

            case CaptureKind.Button:
            {
                byte bit = (byte)e.Button;
                // A release for a press that happened locally belongs to the local OS, or that button stays stuck here.
                if (!e.IsDown && (_remoteButtons & bit) == 0) return false;
                _remoteButtons = e.IsDown ? (byte)(_remoteButtons | bit) : (byte)(_remoteButtons & ~bit);
                link.Send(InputMotionPacket.TypeButton, buttons: _remoteButtons);
                return true;
            }

            case CaptureKind.Wheel:
                link.Send(InputMotionPacket.TypeScroll, ClampShort(e.Dx), ClampShort(e.Dy));
                return true;

            case CaptureKind.Key:
                if (e.IsDown) _remoteKeys.Add(e.Key);
                else if (!_remoteKeys.Remove(e.Key)) return false; // same rule as buttons
                link.Send(InputMotionPacket.TypeKey, (short)e.Key, (short)(e.IsDown ? 1 : 0));
                return true;
        }
        return false;
    }

    private bool IsHotkey(ushort key)
    {
        if (key == VkScrollLock) return true;
        if (key != VkEscape) return false;
        bool ctrl = _physicalKeys.Contains(0x11) || _physicalKeys.Contains(0xA2) || _physicalKeys.Contains(0xA3);
        bool alt = _physicalKeys.Contains(0x12) || _physicalKeys.Contains(0xA4) || _physicalKeys.Contains(0xA5);
        return ctrl && alt;
    }

    /// <summary>True when the virtual cursor crossed the peer's edge that faces this computer.</summary>
    private bool LeftThroughReturnEdge(out int localX, out int localY)
    {
        var r = _active!.Remote;
        var l = _local!;
        double ratioX = (_virtualX - r.X) / r.Width, ratioY = (_virtualY - r.Y) / r.Height;
        localX = (int)(l.X + Math.Clamp(ratioX, 0, 1) * (l.Width - 1));
        localY = (int)(l.Y + Math.Clamp(ratioY, 0, 1) * (l.Height - 1));
        // Land a few pixels inside our edge so we don't bounce straight back.
        switch (_active.Placement)
        {
            case PeerPlacement.Right when _virtualX < r.X: localX = (int)l.Right - 5; return true;
            case PeerPlacement.Left when _virtualX > r.Right - 1: localX = (int)l.X + 4; return true;
            case PeerPlacement.Below when _virtualY < r.Y: localY = (int)l.Bottom - 5; return true;
            case PeerPlacement.Above when _virtualY > r.Bottom - 1: localY = (int)l.Y + 4; return true;
            default: return false;
        }
    }

    private void EnterPeer(CursorTransitionEventArgs e)
    {
        // Runs inside Handle (under _lock) via VirtualKvmManager.ProcessLocalMotion.
        if (!_peers.TryGetValue(e.TargetHostId, out var peer) || !peer.Link.IsAlive)
        {
            _edges.SwitchBackToLocal(); // peer unreachable: stay here
            return;
        }

        _active = peer;
        _virtualX = (int)Math.Clamp(e.EntryX - peer.Placed.X + peer.Remote.X, peer.Remote.X, peer.Remote.Right - 1);
        _virtualY = (int)Math.Clamp(e.EntryY - peer.Placed.Y + peer.Remote.Y, peer.Remote.Y, peer.Remote.Bottom - 1);
        peer.Link.Send(InputMotionPacket.TypeMoveAbsolute, ClampShort(_virtualX), ClampShort(_virtualY));
        // Park the real cursor mid-screen so local edges never clamp the deltas we forward.
        _capture.WarpCursor(_parkX, _parkY);
        _stateDirty = true;
    }

    private void ReturnToLocal((int X, int Y)? cursor)
    {
        var peer = _active;
        if (peer == null) return;

        if (_remoteButtons != 0) peer.Link.Send(InputMotionPacket.TypeButton, buttons: 0);
        foreach (var key in _remoteKeys) peer.Link.Send(InputMotionPacket.TypeKey, (short)key, 0);
        _remoteButtons = 0;
        _remoteKeys.Clear();

        _active = null;
        _edges.SwitchBackToLocal();
        if (cursor is { } c) _capture.WarpCursor(c.X, c.Y);
        _stateDirty = true;
    }

    private void CheckPeerAlive()
    {
        lock (_lock)
        {
            if (_active != null && !_active.Link.IsAlive) ReturnToLocal(null);
        }
        RaiseIfDirty();
    }

    private void RaiseIfDirty()
    {
        bool raise;
        lock (_lock)
        {
            raise = _stateDirty;
            _stateDirty = false;
        }
        if (raise) StateChanged?.Invoke();
    }

    private static short ClampShort(int v) => (short)Math.Clamp(v, short.MinValue, short.MaxValue);

    public void Dispose()
    {
        Stop();
        _capture.Handler = null;
        _capture.Dispose();
    }
}
