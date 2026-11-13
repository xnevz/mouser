using System.Net;
using Mouser.Core.Geometry;
using Mouser.Core.Input;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.Core.Services;

namespace Mouser.Tests;

public sealed class FakeCapture : IInputCapture
{
    public bool IsSupported => true;
    public Func<CapturedInput, bool>? Handler { get; set; }
    public (int X, int Y) Cursor { get; private set; }
    public bool Started { get; private set; }

    public void Start() => Started = true;
    public void Stop() => Started = false;
    public void WarpCursor(int x, int y) => Cursor = (x, y);
    public void Dispose() { }

    public bool Move(int x, int y, int dx, int dy) => Handler!(new CapturedInput(CaptureKind.Move, x, y, dx, dy));
    public bool Button(MouseButton b, bool down) => Handler!(new CapturedInput(CaptureKind.Button, Button: b, IsDown: down));
    public bool Key(ushort vk, bool down) => Handler!(new CapturedInput(CaptureKind.Key, Key: vk, IsDown: down));
    public bool Wheel(int dx, int dy) => Handler!(new CapturedInput(CaptureKind.Wheel, Dx: dx, Dy: dy));
}

/// <summary>
/// This PC (1920×1080) with a peer PC (2560×1440 whose desktop starts at -1280,0) to its right.
/// The peer is a real InputInjectionService over loopback UDP, recording what it would inject.
/// </summary>
public sealed class KvmRig : IAsyncDisposable
{
    public const int Pin = 135_790;
    public static readonly ScreenBounds Local = new() { X = 0, Y = 0, Width = 1920, Height = 1080 };
    public static readonly ScreenBounds Remote = new() { X = -1280, Y = 0, Width = 2560, Height = 1440 };

    public FakeCapture Capture { get; } = new();
    public RecordingSink PeerSink { get; } = new();
    public InputInjectionService PeerHost { get; }
    public PeerLink Link { get; }
    public KvmController Kvm { get; }

    private KvmRig()
    {
        PeerHost = new InputInjectionService(PeerSink, new PairingGate(Pin), 0);
        PeerHost.Start();
        Link = new PeerLink(new IPEndPoint(IPAddress.Loopback, PeerHost.BoundPort));
        Kvm = new KvmController(Capture);
        Kvm.SetLocalScreen(Local);
    }

    public static async Task<KvmRig> CreateAsync(PeerPlacement placement = PeerPlacement.Right)
    {
        var rig = new KvmRig();
        Assert.True(await rig.Link.PairAsync(Pin));
        rig.Kvm.SetPeer("peer", "Studio PC", rig.Link, Remote, placement);
        rig.Kvm.Start();
        return rig;
    }

    /// <summary>Pushes hard against the local right edge, which crosses immediately (push beats edge resistance).</summary>
    public void CrossRight(int y = 540) => Capture.Move(1925, y, 30, 0);

    public async Task<string[]> PeerEventsAsync(int atLeast)
    {
        await Wait.UntilAsync(() => PeerSink.Snapshot().Length >= atLeast);
        await Task.Delay(50); // let any stragglers arrive so "exactly" assertions are meaningful
        return PeerSink.Snapshot();
    }

    public async ValueTask DisposeAsync()
    {
        Kvm.Dispose();
        Link.Dispose();
        PeerHost.Dispose();
        await Task.CompletedTask;
    }
}

public class KvmControllerTests
{
    [Fact]
    public async Task MovingInsideTheScreen_StaysLocal()
    {
        await using var rig = await KvmRig.CreateAsync();

        Assert.False(rig.Capture.Move(1000, 500, 5, 5));
        Assert.False(rig.Capture.Button(MouseButton.Left, true));
        Assert.False(rig.Capture.Key(0x41, true));

        Assert.Null(rig.Kvm.ActivePeerId);
        Assert.Empty(rig.PeerSink.Snapshot());
    }

    [Fact]
    public async Task PushingPastTheEdge_HandsControlToThePeer_AtTheMatchingHeight()
    {
        await using var rig = await KvmRig.CreateAsync();
        bool changed = false;
        rig.Kvm.StateChanged += () => changed = true;

        Assert.True(rig.Capture.Move(1925, 540, 30, 0)); // halfway down our screen

        Assert.Equal("peer", rig.Kvm.ActivePeerId);
        Assert.True(changed);
        Assert.Equal((960, 540), rig.Capture.Cursor); // real cursor parked mid-screen
        // Enters at the peer's left edge (its own coordinates), halfway down its 1440 px height.
        Assert.Equal(new[] { "at -1280,720" }, await rig.PeerEventsAsync(1));
    }

    [Fact]
    public async Task WhileOnThePeer_AllInputIsSwallowedAndForwarded()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();

        Assert.True(rig.Capture.Move(960, 540, 100, -20));
        Assert.True(rig.Capture.Button(MouseButton.Left, true));
        Assert.True(rig.Capture.Button(MouseButton.Left, false));
        Assert.True(rig.Capture.Wheel(0, -120));
        Assert.True(rig.Capture.Key(0x41, true));
        Assert.True(rig.Capture.Key(0x41, false));

        Assert.Equal(new[]
        {
            "at -1280,720", "at -1180,700", "Left down", "Left up", "scroll 0,-120", "key 0x41 down", "key 0x41 up"
        }, await rig.PeerEventsAsync(7));
    }

    [Fact]
    public async Task CursorIsClampedToThePeerScreen()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight(1079);

        rig.Capture.Move(960, 540, 5000, 5000); // way past the peer's bottom-right corner

        Assert.Equal("at 1279,1439", (await rig.PeerEventsAsync(2))[1]);
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task ComingBackThroughTheSameEdge_ReturnsControl_AtTheMatchingHeight()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight(540);

        Assert.True(rig.Capture.Move(960, 540, -10, 0)); // back over the peer's left edge

        Assert.Null(rig.Kvm.ActivePeerId);
        Assert.Equal((1915, 539), rig.Capture.Cursor); // inside our right edge, halfway down
        Assert.False(rig.Capture.Move(1900, 539, -5, 0)); // local again: not swallowed
    }

    [Fact]
    public async Task ReturningReleasesAnythingHeldOnThePeer()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();
        rig.Capture.Button(MouseButton.Left, true);   // dragging on the peer…
        rig.Capture.Key(0x10, true);                   // …with Shift held
        rig.Capture.Move(960, 540, -10, 0);            // and leave mid-drag

        var events = await rig.PeerEventsAsync(5);
        Assert.Contains("Left up", events);
        Assert.Contains("key 0x10 up", events);
    }

    [Fact]
    public async Task ReleasesOfLocallyPressedKeys_GoToTheLocalOs()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.Capture.Key(0x10, true);                   // Shift pressed here…
        rig.Capture.Button(MouseButton.Left, true);    // …and a local drag in progress
        rig.CrossRight();

        // Releases happen while the peer is active, but their presses happened locally.
        Assert.False(rig.Capture.Key(0x10, false));
        Assert.False(rig.Capture.Button(MouseButton.Left, false));
        Assert.Equal(new[] { "at -1280,720" }, await rig.PeerEventsAsync(1));
    }

    [Fact]
    public async Task Hotkey_ReturnsControlImmediately()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();

        rig.Capture.Key(0xA2, true); // left Ctrl
        rig.Capture.Key(0xA4, true); // left Alt
        Assert.True(rig.Capture.Key(0x1B, true)); // Esc: swallowed, never reaches either PC

        Assert.Null(rig.Kvm.ActivePeerId);
        var events = await rig.PeerEventsAsync(5);
        Assert.Contains("key 0xA2 up", events); // modifiers sent to the peer were released there
        Assert.Contains("key 0xA4 up", events);
        Assert.DoesNotContain("key 0x1B down", events);
    }

    [Fact]
    public async Task Hotkey_WhileLocal_LocksTheCursorToThisScreen()
    {
        await using var rig = await KvmRig.CreateAsync();

        Assert.True(rig.Capture.Key(0x91, true)); // Scroll Lock
        Assert.True(rig.Kvm.IsScreenLocked);
        Assert.False(rig.Capture.Move(1925, 540, 30, 0));
        Assert.Null(rig.Kvm.ActivePeerId);

        rig.Capture.Key(0x91, true);
        Assert.False(rig.Kvm.IsScreenLocked);
        Assert.True(rig.Capture.Move(1925, 540, 30, 0));
    }

    [Fact]
    public async Task UnreachablePeer_IsNeverEntered_AndAnActiveSessionFallsBack()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        rig.PeerHost.Stop(); // peer PC goes to sleep

        await Wait.UntilAsync(() => rig.Kvm.ActivePeerId == null, timeoutMs: (int)PeerLink.PongTimeout.TotalMilliseconds + 2000);
        Assert.False(rig.Capture.Move(960, 540, 5, 0)); // input belongs to this PC again
        Assert.False(rig.Capture.Move(1925, 540, 30, 0)); // and the dead peer can't be entered
    }

    [Fact]
    public async Task PeerOnTheLeft_UsesTheLeftEdge()
    {
        await using var rig = await KvmRig.CreateAsync(PeerPlacement.Left);

        Assert.False(rig.Capture.Move(1925, 540, 30, 0)); // right edge has nobody behind it
        Assert.True(rig.Capture.Move(-5, 270, -30, 0));

        Assert.Equal(new[] { "at 1279,360" }, await rig.PeerEventsAsync(1)); // peer's right edge, quarter height
        Assert.True(rig.Capture.Move(960, 540, 10, 0)); // back out through the peer's right edge
        Assert.Null(rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task RemovingThePeerWhileActive_ReturnsControl()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();

        rig.Kvm.RemovePeer("peer");

        Assert.Null(rig.Kvm.ActivePeerId);
        Assert.False(rig.Capture.Move(1925, 540, 30, 0));
    }
}
