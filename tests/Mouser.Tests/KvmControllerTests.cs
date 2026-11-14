using System.Net;
using Mouser.Core.Geometry;
using Mouser.Core.Input;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.Core.Services;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;

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
    public bool Key(ushort vk, bool down, char? text = null) => Handler!(new CapturedInput(CaptureKind.Key, Key: vk, IsDown: down, Text: text));
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

    [Theory]
    [InlineData("Careful", 300, 60.0)]
    [InlineData("Relaxed", 0, 1.0)]
    [InlineData("Normal", 80, 15.0)]
    [InlineData("unknown", 80, 15.0)]
    [InlineData(null, 80, 15.0)]
    public void KvmOptions_FromFeel_ReturnsExpectedPresets(string? feel, int expectedMs, double expectedPixels)
    {
        var options = KvmOptions.FromFeel(feel);
        Assert.Equal(expectedMs, options.EdgeResistanceMs);
        Assert.Equal(expectedPixels, options.EdgePushPixels);
    }

    [Fact]
    public async Task KvmController_ApplyOptions_UpdatesOptionsAndEdgeResistance()
    {
        await using var rig = await KvmRig.CreateAsync();
        var careful = KvmOptions.FromFeel("Careful");
        rig.Kvm.ApplyOptions(careful);

        Assert.Same(careful, rig.Kvm.Options);
        Assert.Equal(300, rig.Kvm.Options.EdgeResistanceMs);
        Assert.Equal(60, rig.Kvm.Options.EdgePushPixels);

        var relaxed = KvmOptions.FromFeel("Relaxed");
        rig.Kvm.ApplyOptions(relaxed);

        Assert.Same(relaxed, rig.Kvm.Options);
        Assert.Equal(0, rig.Kvm.Options.EdgeResistanceMs);
        Assert.Equal(1, rig.Kvm.Options.EdgePushPixels);
    }

    [Fact]
    public async Task RelaxedFeel_CrossesImmediatelyOnEdgeContact()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.Kvm.ApplyOptions(KvmOptions.FromFeel("Relaxed"));

        // A single pixel push at the edge crosses immediately with Relaxed preset (threshold = 1 px, 0 ms)
        Assert.True(rig.Capture.Move(1920, 540, 1, 0));
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task CarefulFeel_RequiresHigherPushThresholdToCross()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.Kvm.ApplyOptions(KvmOptions.FromFeel("Careful"));

        // Push of 30px is below Careful threshold (60px, 300ms) - should NOT cross yet
        Assert.False(rig.Capture.Move(1925, 540, 30, 0));
        Assert.Null(rig.Kvm.ActivePeerId);

        // Additional push bringing accumulated total to 65px (>= 60px) - should cross now!
        Assert.True(rig.Capture.Move(1925, 540, 35, 0));
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task CustomReturnHotkey_SwitchesBackFromPeer()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        Assert.True(HotkeyBinding.TryParse("Alt+X", out var altX));
        rig.Kvm.ApplyOptions(new KvmOptions
        {
            ReturnControlHotkey = altX
        });

        rig.Capture.Key(0xA4, true); // Alt
        Assert.True(rig.Capture.Key((ushort)'X', true)); // X
        Assert.Null(rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task SecondaryReturnControlHotkey_ScrollLock_StillWorksWithCustomPrimary()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        Assert.True(HotkeyBinding.TryParse("F12", out var f12));
        rig.Kvm.ApplyOptions(new KvmOptions
        {
            ReturnControlHotkey = f12
        });

        // Scroll Lock (secondary default) still returns control
        Assert.True(rig.Capture.Key(0x91, true));
        Assert.Null(rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task PanicDoublePressCtrl_Within300ms_ReturnsControlFromPeer()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        // First Ctrl press and release
        rig.Capture.Key(0x11, true);
        rig.Capture.Key(0x11, false);

        // Second Ctrl press immediately (< 300 ms) triggers panic return
        bool swallowed = rig.Capture.Key(0x11, true);
        Assert.True(swallowed);
        Assert.Null(rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task PanicDoublePressCtrl_Over300ms_DoesNotReturnControl()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        // First Ctrl press and release
        rig.Capture.Key(0x11, true);
        rig.Capture.Key(0x11, false);

        await Task.Delay(350);

        // Second Ctrl press after >300 ms forwards to peer, does not return control
        rig.Capture.Key(0x11, true);
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task CustomLockCursorHotkey_WhileLocal_TogglesScreenLock()
    {
        await using var rig = await KvmRig.CreateAsync();
        Assert.True(HotkeyBinding.TryParse("Ctrl+Alt+L", out var ctrlAltL));
        rig.Kvm.ApplyOptions(new KvmOptions
        {
            LockCursorHotkey = ctrlAltL
        });

        // Press Ctrl+Alt+L while local
        rig.Capture.Key(0xA2, true); // left Ctrl
        rig.Capture.Key(0xA4, true); // left Alt
        Assert.True(rig.Capture.Key((ushort)'L', true));
        Assert.True(rig.Kvm.IsScreenLocked);

        // Toggle again
        rig.Capture.Key((ushort)'L', true);
        Assert.False(rig.Kvm.IsScreenLocked);
    }

    private sealed class TwoPeerRig : IAsyncDisposable
    {
        public KvmRig Rig { get; }
        public RecordingSink AlphaSink => Rig.PeerSink;
        public RecordingSink BetaSink { get; }
        public InputInjectionService HostBeta { get; }
        public PeerLink LinkBeta { get; }

        public TwoPeerRig(KvmRig rig, RecordingSink betaSink, InputInjectionService hostBeta, PeerLink linkBeta)
        {
            Rig = rig;
            BetaSink = betaSink;
            HostBeta = hostBeta;
            LinkBeta = linkBeta;
        }

        public static async Task<TwoPeerRig> CreateAsync()
        {
            var rig = await KvmRig.CreateAsync();
            rig.Kvm.RemovePeer("peer");
            rig.Kvm.SetPeer("alpha", "Alpha PC", rig.Link, KvmRig.Remote, PeerPlacement.Left);

            var betaSink = new RecordingSink();
            var hostBeta = new InputInjectionService(betaSink, new PairingGate(KvmRig.Pin), 0);
            hostBeta.Start();
            var linkBeta = new PeerLink(new IPEndPoint(IPAddress.Loopback, hostBeta.BoundPort));
            Assert.True(await linkBeta.PairAsync(KvmRig.Pin));
            rig.Kvm.SetPeer("beta", "Beta PC", linkBeta, KvmRig.Remote, PeerPlacement.Right);

            return new TwoPeerRig(rig, betaSink, hostBeta, linkBeta);
        }

        public async ValueTask DisposeAsync()
        {
            await Rig.DisposeAsync();
            LinkBeta.Dispose();
            HostBeta.Dispose();
        }
    }

    [Fact]
    public async Task JumpTo_ActivatesPeer_AndSendsMoveAbsoluteToCenter()
    {
        await using var rig = await TwoPeerRig.CreateAsync();

        bool jumped = rig.Rig.Kvm.JumpTo("alpha");
        Assert.True(jumped);
        Assert.Equal("alpha", rig.Rig.Kvm.ActivePeerId);

        // Center of Remote (X=-1280, Y=0, W=2560, H=1440) is (0, 720)
        await Wait.UntilAsync(() => rig.AlphaSink.Snapshot().Length >= 1);
        Assert.Contains("at 0,720", rig.AlphaSink.Snapshot());
    }

    [Fact]
    public async Task JumpTo_CallingBeta_ReturnsToLocalFirstThenActivatesBeta()
    {
        await using var rig = await TwoPeerRig.CreateAsync();

        Assert.True(rig.Rig.Kvm.JumpTo("alpha"));
        Assert.Equal("alpha", rig.Rig.Kvm.ActivePeerId);

        // Press a key while on alpha so we can check it gets released when leaving
        rig.Rig.Capture.Key(0x41, true); // 'A' down
        await Wait.UntilAsync(() => rig.AlphaSink.Snapshot().Contains("key 0x41 down"));

        // Now JumpTo beta
        Assert.True(rig.Rig.Kvm.JumpTo("beta"));
        Assert.Equal("beta", rig.Rig.Kvm.ActivePeerId);

        // Alpha should have seen key release
        await Wait.UntilAsync(() => rig.AlphaSink.Snapshot().Contains("key 0x41 up"));
        // Beta should have received MoveAbsolute at center (0, 720)
        await Wait.UntilAsync(() => rig.BetaSink.Snapshot().Contains("at 0,720"));
    }

    [Fact]
    public async Task JumpNext_CyclesFromLocalToAlphaToBetaToLocal()
    {
        await using var rig = await TwoPeerRig.CreateAsync();

        // Start local
        Assert.Null(rig.Rig.Kvm.ActivePeerId);

        // Next -> alpha
        Assert.True(rig.Rig.Kvm.JumpNext());
        Assert.Equal("alpha", rig.Rig.Kvm.ActivePeerId);

        // Next -> beta
        Assert.True(rig.Rig.Kvm.JumpNext());
        Assert.Equal("beta", rig.Rig.Kvm.ActivePeerId);

        // Next -> returns to local
        Assert.True(rig.Rig.Kvm.JumpNext());
        Assert.Null(rig.Rig.Kvm.ActivePeerId);

        // Next -> cycles back to alpha
        Assert.True(rig.Rig.Kvm.JumpNext());
        Assert.Equal("alpha", rig.Rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task NextComputerHotkey_InvokesJumpNext_AndSwallowsKey()
    {
        await using var rig = await TwoPeerRig.CreateAsync();

        // Default NextComputer is Ctrl+Alt+Right (VK 0x27)
        rig.Rig.Capture.Key(0xA2, true); // Ctrl
        rig.Rig.Capture.Key(0xA4, true); // Alt

        // Press Right -> jumps to alpha, swallowed
        bool swallowed = rig.Rig.Capture.Key(0x27, true);
        Assert.True(swallowed);
        Assert.Equal("alpha", rig.Rig.Kvm.ActivePeerId);

        // Press Right again -> jumps to beta, swallowed
        swallowed = rig.Rig.Capture.Key(0x27, true);
        Assert.True(swallowed);
        Assert.Equal("beta", rig.Rig.Kvm.ActivePeerId);

        // Press Right again -> returns to local, swallowed
        swallowed = rig.Rig.Capture.Key(0x27, true);
        Assert.True(swallowed);
        Assert.Null(rig.Rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task Jump1Hotkey_JumpsDirectlyToFirstAlphabeticalPeer()
    {
        await using var rig = await TwoPeerRig.CreateAsync();
        Assert.True(HotkeyBinding.TryParse("Ctrl+Alt+1", out var j1));
        Assert.True(HotkeyBinding.TryParse("Ctrl+Alt+2", out var j2));
        rig.Rig.Kvm.ApplyOptions(new KvmOptions
        {
            JumpHotkeys = new[] { j1, j2 }
        });

        // Press Ctrl+Alt+2 while local -> jumps to beta ("Beta PC" is second)
        rig.Rig.Capture.Key(0xA2, true); // Ctrl
        rig.Rig.Capture.Key(0xA4, true); // Alt
        bool swallowed = rig.Rig.Capture.Key((ushort)'2', true);
        Assert.True(swallowed);
        Assert.Equal("beta", rig.Rig.Kvm.ActivePeerId);

        // Press Ctrl+Alt+1 while on beta -> jumps to alpha ("Alpha PC" is first)
        swallowed = rig.Rig.Capture.Key((ushort)'1', true);
        Assert.True(swallowed);
        Assert.Equal("alpha", rig.Rig.Kvm.ActivePeerId);
    }

    [Fact]
    public void MouserHost_ExposesJumpToAndJumpNext()
    {
        using var capture = new FakeCapture();
        using var host = new MouserHost(new SettingsStore(), new RecordingSink(), capture);
        Assert.False(host.JumpTo("nonexistent"));
        Assert.False(host.JumpNext());
    }

    [Fact]
    public async Task MotionScaler_ScalesMouseMovements_AccordingToPeerMouseSpeed()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.Kvm.SetPeerMotion("peer", mouseSpeed: 2.0, scrollSpeed: 1.0, invertScroll: false);
        rig.CrossRight();

        // Entered at -1280, 720
        Assert.Equal(new[] { "at -1280,720" }, await rig.PeerEventsAsync(1));

        // Move 50, 20 with speed 2.0 -> dx: 100, dy: 40 -> -1280 + 100 = -1180, 720 + 40 = 760
        Assert.True(rig.Capture.Move(960, 540, 50, 20));

        var events = await rig.PeerEventsAsync(2);
        Assert.Equal("at -1180,760", events[1]);
    }

    [Fact]
    public async Task MotionScaler_ScalesScrollAndInvertsScroll_AccordingToPeerSettings()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.Kvm.SetPeerMotion("peer", mouseSpeed: 1.0, scrollSpeed: 0.5, invertScroll: true);
        rig.CrossRight();

        Assert.Equal(new[] { "at -1280,720" }, await rig.PeerEventsAsync(1));

        // Wheel 0, -120 with scroll speed 0.5 and invert -> dy: -60 -> inverted: +60
        Assert.True(rig.Capture.Wheel(0, -120));

        var events = await rig.PeerEventsAsync(2);
        Assert.Equal("scroll 0,60", events[1]);
    }

    [Fact]
    public void SettingsStore_ClampsPeerMotionSettings()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_peer_motion_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            store.Current.Peers["test"] = new PeerSettings
            {
                MouseSpeed = 10.0,
                ScrollSpeed = 0.05,
                InvertScroll = true
            };
            store.Save();

            var loaded = new SettingsStore(tmp);
            Assert.True(loaded.Current.Peers.TryGetValue("test", out var peer));
            Assert.Equal(4.0, peer.MouseSpeed);
            Assert.Equal(0.25, peer.ScrollSpeed);
            Assert.True(peer.InvertScroll);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void DeviceDetailViewModel_UpdatesMotionSettings_AndNotifiesHost()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_device_detail_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            using var capture = new FakeCapture();
            using var host = new MouserHost(store, new RecordingSink(), capture);

            var vm = new DeviceDetailViewModel("dev1", DeviceKind.Computer, store, host);
            Assert.Equal(1.0, vm.MouseSpeed);
            Assert.Equal(1.0, vm.ScrollSpeed);
            Assert.False(vm.InvertScroll);

            vm.MouseSpeed = 1.75;
            vm.ScrollSpeed = 2.5;
            vm.InvertScroll = true;

            var reloaded = new SettingsStore(tmp);
            Assert.True(reloaded.Current.Peers.TryGetValue("dev1", out var peer));
            Assert.Equal(1.75, peer.MouseSpeed);
            Assert.Equal(2.5, peer.ScrollSpeed);
            Assert.True(peer.InvertScroll);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public async Task RapidMoves_AreCoalesced_AndFlushedByTimer()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();

        // Entered at -1280, 720
        Assert.Equal(new[] { "at -1280,720" }, await rig.PeerEventsAsync(1));

        // First move sends immediately
        rig.Capture.Move(960, 540, 10, 10);
        // Second move immediately after (within <2ms) is coalesced into pending
        rig.Capture.Move(960, 540, 20, 20);

        // Within a short delay, the 4ms coalesce timer flushes the pending move
        var events = await rig.PeerEventsAsync(3);
        Assert.Equal("at -1280,720", events[0]);
        Assert.Equal("at -1270,730", events[1]);
        Assert.Equal("at -1250,750", events[2]);
    }

    [Fact]
    public async Task WrapAround_WhenEnabledAndEdgeHasNoPeer_WarpsToOppositeEdge_AndActivePeerIdStaysNull()
    {
        await using var rig = await KvmRig.CreateAsync(PeerPlacement.Right);

        // Right edge has a peer, Left edge has NO peer.
        rig.Kvm.ApplyOptions(new KvmOptions { WrapAround = true });

        // Push past left edge (-5, 540 with dx = -30)
        bool swallowed = rig.Capture.Move(-5, 540, -30, 0);

        Assert.True(swallowed);
        Assert.Null(rig.Kvm.ActivePeerId);
        // Exiting left edge warps to right edge (local.Right - 4, y) = (1920 - 4, 540) = (1916, 540)
        Assert.Equal((1916, 540), rig.Capture.Cursor);

        // When WrapAround is disabled, pushing past empty edge does not warp
        rig.Kvm.ApplyOptions(new KvmOptions { WrapAround = false });
        Assert.False(rig.Capture.Move(-5, 540, -30, 0));
    }

    [Fact]
    public async Task WrapAround_TopAndBottomEdges_WarpsVertically()
    {
        await using var rig = await KvmRig.CreateAsync(PeerPlacement.Right);
        rig.Kvm.ApplyOptions(new KvmOptions { WrapAround = true });

        // Push past top edge (960, -5 with dy = -30): warps to bottom (960, local.Bottom - 4) = (960, 1076)
        Assert.True(rig.Capture.Move(960, -5, 0, -30));
        Assert.Null(rig.Kvm.ActivePeerId);
        Assert.Equal((960, 1076), rig.Capture.Cursor);

        // Push past bottom edge (960, 1085 with dy = 30): warps to top (960, local.Y + 3) = (960, 3)
        Assert.True(rig.Capture.Move(960, 1085, 0, 30));
        Assert.Null(rig.Kvm.ActivePeerId);
        Assert.Equal((960, 3), rig.Capture.Cursor);
    }

    [Fact]
    public async Task ReturningToLocal_WhenProportionalEntryFalse_UsesPixelAlignedPosition()
    {
        await using var rig = await KvmRig.CreateAsync(PeerPlacement.Right);
        rig.Kvm.ApplyOptions(new KvmOptions { ProportionalEntry = false });

        // Cross to the peer at y = 540
        rig.CrossRight(540);
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        // Virtual cursor is at (-1280, 540). Moving left with dx = -10 pushes virtualX < -1280
        Assert.True(rig.Capture.Move(960, 540, -10, 0));

        // Returns to local machine, pixel-aligned at y=540, and local.Right - 5 = 1915
        Assert.Null(rig.Kvm.ActivePeerId);
        Assert.Equal((1915, 540), rig.Capture.Cursor);
    }

    [Fact]
    public async Task StackedLocalMonitors_CrossingToPeer_OnlyBesideMatchingMonitor()
    {
        await using var rig = await KvmRig.CreateAsync();

        var top = new Mouser.Core.Geometry.Monitor(0, 0, 1920, 1080);
        var bottom = new Mouser.Core.Geometry.Monitor(0, 1080, 1920, 1080);
        rig.Kvm.SetLocalMonitors(new[] { top, bottom });

        // Peer is to the right, placed only beside top monitor (y=0..1080)
        var peerRemote = new ScreenBounds { X = 0, Y = 0, Width = 1920, Height = 1080 };
        var peerMonitors = new[] { new Mouser.Core.Geometry.Monitor(0, 0, 1920, 1080) };
        rig.Kvm.SetPeer("peer", "Studio PC", rig.Link, peerRemote, PeerPlacement.Right, monitors: peerMonitors);

        // 1. Move across inner edge between top and bottom: should stay local
        Assert.False(rig.Capture.Move(960, 1080, 0, 30));
        Assert.Null(rig.Kvm.ActivePeerId);

        // 2. Push right at y = 1500 (in bottom monitor where no peer exists): should stay local
        Assert.False(rig.Capture.Move(1925, 1500, 30, 0));
        Assert.Null(rig.Kvm.ActivePeerId);

        // 3. Push right at y = 500 (in top monitor beside peer): crosses to peer
        Assert.True(rig.Capture.Move(1925, 500, 30, 0));
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task PeerWithMultipleMonitors_ClampsVirtualCursorToMonitors()
    {
        await using var rig = await KvmRig.CreateAsync();

        // Remote peer has 2 monitors:
        // Mon 1: (0, 0, 1920, 1080)
        // Mon 2: (1920, 1080, 1920, 1080) -- diagonal layout
        var peerMon1 = new Mouser.Core.Geometry.Monitor(0, 0, 1920, 1080);
        var peerMon2 = new Mouser.Core.Geometry.Monitor(1920, 1080, 1920, 1080);
        var peerRemote = new ScreenBounds { X = 0, Y = 0, Width = 3840, Height = 2160 };

        rig.Kvm.SetPeer("peer", "Studio PC", rig.Link, peerRemote, PeerPlacement.Right, monitors: new[] { peerMon1, peerMon2 });

        // Enter peer at y = 500
        Assert.True(rig.Capture.Move(1925, 500, 30, 0));
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        // Move into empty notch at (2500, 500) where peer has no monitor:
        // Input should be swallowed and virtual cursor clamped to nearest monitor
        Assert.True(rig.Capture.Move(960, 540, 2500, 0));
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task PeerWithOffset_OnlyCrossesWithinOffsetBoundary()
    {
        await using var rig = await KvmRig.CreateAsync();

        // Remote peer placed to the right with vertical offset of 200px (Y=200..1640)
        rig.Kvm.SetPeer("peer", "Studio PC", rig.Link, KvmRig.Remote, PeerPlacement.Right, offsetX: 0, offsetY: 200);

        // 1. Cursor at y=50 (above offset peer) does NOT cross
        Assert.False(rig.Capture.Move(1925, 50, 30, 0));
        Assert.Null(rig.Kvm.ActivePeerId);

        // 2. Cursor at y=250 (inside offset peer) crosses
        Assert.True(rig.Capture.Move(1925, 250, 30, 0));
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
    }

    [Fact]
    public async Task KeyWithText_SendsTypeChar_AndUpMatchesDown()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        // Key down with Text sends TypeChar
        Assert.True(rig.Capture.Key(0x45, true, text: 'é'));
        // Key up without Text still releases as TypeChar
        Assert.True(rig.Capture.Key(0x45, false));

        var events = await rig.PeerEventsAsync(3);
        Assert.Contains("char 0x00E9 down", events);
        Assert.Contains("char 0x00E9 up", events);
    }

    [Fact]
    public async Task KeyWithoutText_SendsTypeKey()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        // Shortcut key (e.g. Ctrl+C: C has text = null)
        Assert.True(rig.Capture.Key(0x43, true, text: null));
        Assert.True(rig.Capture.Key(0x43, false));

        var events = await rig.PeerEventsAsync(3);
        Assert.Contains("key 0x43 down", events);
        Assert.Contains("key 0x43 up", events);
    }

    [Fact]
    public async Task ReturnToLocal_ReleasesHeldCharKeys()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        // Press char key down, but don't release it before returning
        Assert.True(rig.Capture.Key(0x45, true, text: 'é'));

        // Scroll Lock returns control to local screen
        Assert.True(rig.Capture.Key(0x91, true));
        Assert.Null(rig.Kvm.ActivePeerId);

        var events = await rig.PeerEventsAsync(3);
        Assert.Contains("char 0x00E9 down", events);
        Assert.Contains("char 0x00E9 up", events);
    }

    [Fact]
    public async Task LastPeerName_RecordsNameOnEdgeTransition_AndPersistsAfterReturn()
    {
        await using var rig = await KvmRig.CreateAsync();
        Assert.Null(rig.Kvm.LastPeerName);

        // Edge transition
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
        Assert.Equal("Studio PC", rig.Kvm.LastPeerName);

        // Return to local (Scroll Lock returns control)
        Assert.True(rig.Capture.Key(0x91, true));
        Assert.Null(rig.Kvm.ActivePeerId);
        Assert.Equal("Studio PC", rig.Kvm.LastPeerName);
    }

    [Fact]
    public async Task LastPeerName_RecordsNameOnJumpTo_AndPersistsAfterReturn()
    {
        await using var rig = await KvmRig.CreateAsync();
        Assert.Null(rig.Kvm.LastPeerName);

        // JumpTo
        Assert.True(rig.Kvm.JumpTo("peer"));
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
        Assert.Equal("Studio PC", rig.Kvm.LastPeerName);

        // Return to local
        Assert.True(rig.Capture.Key(0x91, true));
        Assert.Null(rig.Kvm.ActivePeerId);
        Assert.Equal("Studio PC", rig.Kvm.LastPeerName);
    }
}
