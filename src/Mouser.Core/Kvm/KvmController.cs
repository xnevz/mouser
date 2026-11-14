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


    private sealed record Peer(
        string Id,
        string Name,
        PeerLink Link,
        ScreenBounds Remote,
        PeerPlacement Placement,
        ScreenBounds Placed,
        MotionScaler MouseXScaler,
        MotionScaler MouseYScaler,
        MotionScaler VScrollScaler,
        MotionScaler HScrollScaler);

    private readonly IInputCapture _capture;
    private readonly VirtualKvmManager _edges = new();
    private readonly object _lock = new();
    private readonly Dictionary<string, Peer> _peers = new();
    private readonly Dictionary<string, (double MouseSpeed, double ScrollSpeed, bool InvertScroll)> _peerMotions = new();
    private readonly HashSet<ushort> _physicalKeys = new();
    private readonly HashSet<ushort> _remoteKeys = new();
    private byte _remoteButtons;
    private Peer? _active;
    private int _virtualX, _virtualY;
    private int _parkX, _parkY;
    private ScreenBounds? _local;
    private Timer? _watchdog;
    private bool _stateDirty;
    private SwitchPolicy _switchPolicy;
    private Func<(bool Fullscreen, bool Maximized)>? _foregroundProbe;
    private (bool Fullscreen, bool Maximized) _cachedForeground;
    private DateTime _lastForegroundCheck = DateTime.MinValue;
    private static readonly TimeSpan ForegroundCacheDuration = TimeSpan.FromMilliseconds(100);
    private DateTime _lastCtrlPress = DateTime.MinValue;

    public bool IsRunning { get; private set; }
    public bool IsScreenLocked => _edges.IsScreenLocked;
    public string? ActivePeerId { get { lock (_lock) return _active?.Id; } }
    public string? ActivePeerName { get { lock (_lock) return _active?.Name; } }
    public KvmOptions Options { get; private set; } = new();

    public Func<(bool Fullscreen, bool Maximized)>? ForegroundProbe
    {
        get => _foregroundProbe;
        set
        {
            _foregroundProbe = value;
            _lastForegroundCheck = DateTime.MinValue;
        }
    }

    /// <summary>Raised (on the capture or timer thread) when control moves between computers or the lock toggles.</summary>
    public event Action? StateChanged;

    public KvmController(IInputCapture capture)
    {
        _capture = capture;
        _capture.Handler = Handle;
        _switchPolicy = new SwitchPolicy(Options);
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

    public void ApplyOptions(KvmOptions o)
    {
        lock (_lock)
        {
            Options = o;
            _edges.EdgeResistanceMs = o.EdgeResistanceMs;
            _edges.EdgePushDistanceThreshold = o.EdgePushPixels;
            _switchPolicy = new SwitchPolicy(o);
        }
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

    public void SetPeer(string id, string name, PeerLink link, ScreenBounds remoteDesktop, PeerPlacement placement, double mouseSpeed, double scrollSpeed, bool invertScroll)
    {
        lock (_lock)
        {
            _peerMotions[id] = (mouseSpeed, scrollSpeed, invertScroll);
            SetPeerLocked(id, name, link, remoteDesktop, placement);
        }
        RaiseIfDirty();
    }

    public void SetPeerMotion(string peerId, double mouseSpeed, double scrollSpeed, bool invertScroll)
    {
        lock (_lock)
        {
            _peerMotions[peerId] = (mouseSpeed, scrollSpeed, invertScroll);
            if (_peers.TryGetValue(peerId, out var peer))
            {
                peer.MouseXScaler.Speed = mouseSpeed;
                peer.MouseYScaler.Speed = mouseSpeed;
                peer.VScrollScaler.Speed = scrollSpeed;
                peer.VScrollScaler.Invert = invertScroll;
                peer.HScrollScaler.Speed = scrollSpeed;
                peer.HScrollScaler.Invert = invertScroll;
            }
        }
    }

    public void RemovePeer(string id)
    {
        lock (_lock)
        {
            if (_active?.Id == id) ReturnToLocal(null);
            _peers.Remove(id);
            _peerMotions.Remove(id);
            _edges.RemoveRemoteScreen(id);
        }
        RaiseIfDirty();
    }

    public bool JumpTo(string peerId)
    {
        bool result;
        lock (_lock)
        {
            result = JumpToLocked(peerId);
        }
        RaiseIfDirty();
        return result;
    }

    private bool JumpToLocked(string peerId)
    {
        if (_peers.TryGetValue(peerId, out var peer) && peer.Link.IsAlive)
        {
            if (_active?.Id == peerId) return true;
            if (_active != null) ReturnToLocal(null);
            _active = peer;
            peer.MouseXScaler.Reset();
            peer.MouseYScaler.Reset();
            peer.VScrollScaler.Reset();
            peer.HScrollScaler.Reset();
            _virtualX = (int)(peer.Remote.X + peer.Remote.Width / 2);
            _virtualY = (int)(peer.Remote.Y + peer.Remote.Height / 2);
            peer.Link.Send(InputMotionPacket.TypeMoveAbsolute, ClampShort(_virtualX), ClampShort(_virtualY));
            _capture.WarpCursor(_parkX, _parkY);
            _edges.ForceRedirect(peer.Id);
            _stateDirty = true;
            return true;
        }
        return false;
    }

    public bool JumpNext()
    {
        bool result;
        lock (_lock)
        {
            result = JumpNextLocked();
        }
        RaiseIfDirty();
        return result;
    }

    private bool JumpNextLocked()
    {
        var orderedPeers = _peers.Values
            .Where(p => p.Placement != PeerPlacement.None && p.Link.IsAlive)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedPeers.Count == 0) return false;

        if (_active == null)
        {
            return JumpToLocked(orderedPeers[0].Id);
        }

        int index = orderedPeers.FindIndex(p => p.Id == _active.Id);
        if (index < 0 || index == orderedPeers.Count - 1)
        {
            ReturnToLocal(null);
            _stateDirty = true;
            return true;
        }

        return JumpToLocked(orderedPeers[index + 1].Id);
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
        var mouseX = existing?.MouseXScaler ?? new MotionScaler();
        var mouseY = existing?.MouseYScaler ?? new MotionScaler();
        var vscroll = existing?.VScrollScaler ?? new MotionScaler();
        var hscroll = existing?.HScrollScaler ?? new MotionScaler();
        if (_peerMotions.TryGetValue(id, out var motion))
        {
            mouseX.Speed = motion.MouseSpeed;
            mouseY.Speed = motion.MouseSpeed;
            vscroll.Speed = motion.ScrollSpeed;
            vscroll.Invert = motion.InvertScroll;
            hscroll.Speed = motion.ScrollSpeed;
            hscroll.Invert = motion.InvertScroll;
        }
        var updated = new Peer(id, name, link, remote, placement, placed, mouseX, mouseY, vscroll, hscroll);
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

            if (e.IsDown)
            {
                if (Options.PanicDoublePressCtrl && (e.Key is 0x11 or 0xA2 or 0xA3))
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastCtrlPress).TotalMilliseconds <= 300)
                    {
                        _lastCtrlPress = DateTime.MinValue;
                        if (_active != null)
                        {
                            ReturnToLocal(null);
                            _stateDirty = true;
                            return true;
                        }
                    }
                    else
                    {
                        _lastCtrlPress = now;
                    }
                }
                else if (e.Key is not (0x11 or 0xA2 or 0xA3))
                {
                    _lastCtrlPress = DateTime.MinValue;
                }

                if (!Options.NextComputerHotkey.IsEmpty && Options.NextComputerHotkey.Matches(e.Key, _physicalKeys))
                {
                    JumpNextLocked();
                    _stateDirty = true;
                    return true;
                }

                if (Options.JumpHotkeys != null && Options.JumpHotkeys.Count > 0)
                {
                    for (int i = 0; i < Options.JumpHotkeys.Count && i < 9; i++)
                    {
                        var binding = Options.JumpHotkeys[i];
                        if (!binding.IsEmpty && binding.Matches(e.Key, _physicalKeys))
                        {
                            var orderedPeers = _peers.Values
                                .Where(p => p.Placement != PeerPlacement.None && p.Link.IsAlive)
                                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                                .ToList();
                            if (i < orderedPeers.Count)
                            {
                                JumpToLocked(orderedPeers[i].Id);
                            }
                            _stateDirty = true;
                            return true;
                        }
                    }
                }

                bool isReturnHotkey = Options.ReturnControlHotkey.Matches(e.Key, _physicalKeys) ||
                                      Options.SecondaryReturnControlHotkey.Matches(e.Key, _physicalKeys);
                bool isLockHotkey = !Options.LockCursorHotkey.IsEmpty && Options.LockCursorHotkey.Matches(e.Key, _physicalKeys);

                if (_active != null && isReturnHotkey)
                {
                    ReturnToLocal(null);
                    _stateDirty = true;
                    return true;
                }

                if (_active == null && (isLockHotkey || (Options.LockCursorHotkey.IsEmpty && isReturnHotkey)))
                {
                    _edges.ToggleScreenLock();
                    _stateDirty = true;
                    return true;
                }
            }
        }

        if (_active == null)
        {
            if (e.Kind == CaptureKind.Move && _peers.Count > 0 && _local != null)
            {
                if (IsNearEdge(e.X, e.Y, _local))
                {
                    var now = DateTime.UtcNow;
                    var (fullscreen, maximized) = GetForegroundInfo(now);
                    bool ctrl = _physicalKeys.Contains(0x11) || _physicalKeys.Contains(0xA2) || _physicalKeys.Contains(0xA3);
                    bool alt = _physicalKeys.Contains(0x12) || _physicalKeys.Contains(0xA4) || _physicalKeys.Contains(0xA5);
                    bool shift = _physicalKeys.Contains(0x10) || _physicalKeys.Contains(0xA0) || _physicalKeys.Contains(0xA1);

                    var ctx = new SwitchContext(
                        ForegroundFullscreen: fullscreen,
                        ForegroundMaximized: maximized,
                        CursorX: e.X,
                        CursorY: e.Y,
                        Local: _local,
                        CtrlDown: ctrl,
                        AltDown: alt,
                        ShiftDown: shift,
                        Now: now);

                    if (!_switchPolicy.Allows(ctx))
                    {
                        return false;
                    }
                }
                else
                {
                    _switchPolicy.ResetEdgeZone();
                }

                // EnterPeer may refuse an unreachable peer, so only swallow when control really moved.
                return _edges.ProcessLocalMotion(e.X, e.Y, ClampShort(e.Dx), ClampShort(e.Dy))
                    && _active != null;
            }

            return false;
        }

        var link = _active.Link;
        switch (e.Kind)
        {
            case CaptureKind.Move:
                _virtualX += _active.MouseXScaler.Apply(e.Dx);
                _virtualY += _active.MouseYScaler.Apply(e.Dy);
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
                int dy = _active.VScrollScaler.Apply(e.Dy);
                int dx = _active.HScrollScaler.Apply(e.Dx);
                link.Send(InputMotionPacket.TypeScroll, ClampShort(dx), ClampShort(dy));
                return true;

            case CaptureKind.Key:
                if (e.IsDown) _remoteKeys.Add(e.Key);
                else if (!_remoteKeys.Remove(e.Key)) return false; // same rule as buttons
                link.Send(InputMotionPacket.TypeKey, (short)e.Key, (short)(e.IsDown ? 1 : 0));
                return true;
        }
        return false;
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
        peer.MouseXScaler.Reset();
        peer.MouseYScaler.Reset();
        peer.VScrollScaler.Reset();
        peer.HScrollScaler.Reset();
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

        peer.MouseXScaler.Reset();
        peer.MouseYScaler.Reset();
        peer.VScrollScaler.Reset();
        peer.HScrollScaler.Reset();
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

    private (bool Fullscreen, bool Maximized) GetForegroundInfo(DateTime now)
    {
        if (_foregroundProbe == null) return (false, false);
        if (now - _lastForegroundCheck >= ForegroundCacheDuration)
        {
            _cachedForeground = _foregroundProbe();
            _lastForegroundCheck = now;
        }
        return _cachedForeground;
    }

    private static bool IsNearEdge(double x, double y, ScreenBounds local)
    {
        const double margin = 3.0;
        return x <= local.X + margin
            || x >= local.Right - margin
            || y <= local.Y + margin
            || y >= local.Bottom - margin;
    }

    private static short ClampShort(int v) => (short)Math.Clamp(v, short.MinValue, short.MaxValue);

    public void Dispose()
    {
        Stop();
        _capture.Handler = null;
        _capture.Dispose();
    }
}
