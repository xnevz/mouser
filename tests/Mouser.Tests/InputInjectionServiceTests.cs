using System.Net;
using Mouser.Core.Input;
using Mouser.Core.Networking;
using Mouser.Core.Services;

namespace Mouser.Tests;

public class InputInjectionServiceTests
{
    private const int Pin = 482_913;
    private static readonly IPEndPoint Phone = new(IPAddress.Parse("192.168.1.50"), 40000);

    private static (InputInjectionService Service, RecordingSink Sink, PairingGate Gate) Create(Func<DateTime>? clock = null)
    {
        var sink = new RecordingSink();
        var gate = new PairingGate(Pin, clock);
        return (new InputInjectionService(sink, gate, 0, clock), sink, gate);
    }

    private static InputMotionPacket P(byte type, ushort seq, short dx = 0, short dy = 0, byte buttons = 0) =>
        InputMotionPacket.Create(type, seq, dx, dy, buttons);

    [Fact]
    public void UnpairedSender_IsIgnored()
    {
        var (service, sink, _) = Create();
        using var _s = service;

        service.HandlePacket(P(InputMotionPacket.TypeMotion, 1, 10, 10), Phone);
        service.HandlePacket(P(InputMotionPacket.TypeKey, 2, 0x41, 1), Phone);

        Assert.Empty(sink.Snapshot());
        Assert.Equal(2, service.RejectedPackets);
    }

    [Fact]
    public void WrongPin_DoesNotPair_CorrectPinDoes()
    {
        var (service, sink, gate) = Create();
        using var _s = service;

        service.HandlePacket(InputMotionPacket.CreatePair(1, 111_111), Phone);
        Assert.False(gate.IsTrusted(Phone.Address));

        service.HandlePacket(InputMotionPacket.CreatePair(2, Pin), Phone);
        Assert.True(gate.IsTrusted(Phone.Address));

        service.HandlePacket(P(InputMotionPacket.TypeMotion, 3, 15, -25), Phone);
        Assert.Equal(new[] { "move 15,-25" }, sink.Snapshot());
    }

    [Fact]
    public void ButtonMask_IsDiffed_SoEveryPressGetsReleased()
    {
        var (service, sink, gate) = Create();
        using var _s = service;
        gate.TryPair(Phone.Address, Pin);

        service.HandlePacket(P(InputMotionPacket.TypeButton, 1, buttons: 0x02), Phone); // right down
        service.HandlePacket(P(InputMotionPacket.TypeButton, 2, buttons: 0x00), Phone); // right up
        service.HandlePacket(P(InputMotionPacket.TypeButton, 3, buttons: 0x01), Phone); // left down (drag)
        service.HandlePacket(P(InputMotionPacket.TypeMotion, 4, 5, 0, buttons: 0x01), Phone); // drag keeps left held
        service.HandlePacket(P(InputMotionPacket.TypeMotion, 5, 5, 0, buttons: 0x00), Phone); // release via motion

        Assert.Equal(new[]
        {
            "Right down", "Right up", "Left down", "move 5,0", "move 5,0", "Left up"
        }, sink.Snapshot());
    }

    [Fact]
    public void KeyAndScrollPackets_AreInjected()
    {
        var (service, sink, gate) = Create();
        using var _s = service;
        gate.TryPair(Phone.Address, Pin);

        service.HandlePacket(P(InputMotionPacket.TypeKey, 1, 0xB3, 1), Phone); // play/pause down
        service.HandlePacket(P(InputMotionPacket.TypeKey, 2, 0xB3, 0), Phone);
        service.HandlePacket(P(InputMotionPacket.TypeScroll, 3, 0, -240), Phone);

        Assert.Equal(new[] { "key 0xB3 down", "key 0xB3 up", "scroll 0,-240" }, sink.Snapshot());
        Assert.Equal(2, service.KeyEventsProcessed);
        Assert.Equal(1, service.ScrollEventsProcessed);
    }

    [Fact]
    public void DuplicateAndReorderedPackets_AreDropped()
    {
        var (service, sink, gate) = Create();
        using var _s = service;
        gate.TryPair(Phone.Address, Pin);

        service.HandlePacket(P(InputMotionPacket.TypeMotion, 10, 1, 0), Phone);
        service.HandlePacket(P(InputMotionPacket.TypeMotion, 10, 1, 0), Phone); // duplicate
        service.HandlePacket(P(InputMotionPacket.TypeMotion, 9, 1, 0), Phone);  // late
        service.HandlePacket(P(InputMotionPacket.TypeMotion, 11, 1, 0), Phone);

        Assert.Equal(2, sink.Snapshot().Length);
        Assert.Equal(2, service.DroppedOutOfOrderPackets);
    }

    [Fact]
    public void HeldInput_IsReleased_WhenClientGoesSilent()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (service, sink, gate) = Create(() => now);
        using var _s = service;
        gate.TryPair(Phone.Address, Pin);

        service.HandlePacket(P(InputMotionPacket.TypeButton, 1, buttons: 0x01), Phone);
        service.HandlePacket(P(InputMotionPacket.TypeKey, 2, 0x10, 1), Phone); // shift held

        now += TimeSpan.FromSeconds(1);
        Assert.False(service.ReleaseHeldInputIfIdle());

        now += InputInjectionService.StuckInputTimeout;
        Assert.True(service.ReleaseHeldInputIfIdle());
        Assert.Contains("Left up", sink.Snapshot());
        Assert.Contains("key 0x10 up", sink.Snapshot());

        // Nothing left to release.
        Assert.False(service.ReleaseHeldInputIfIdle());
    }

    [Fact]
    public void ForgetClient_RevokesTrust()
    {
        var (service, sink, gate) = Create();
        using var _s = service;
        gate.TryPair(Phone.Address, Pin);
        service.HandlePacket(P(InputMotionPacket.TypeMotion, 1, 1, 1), Phone);
        Assert.Single(service.Clients);

        service.ForgetClient(Phone.Address);
        service.HandlePacket(P(InputMotionPacket.TypeMotion, 2, 1, 1), Phone);

        Assert.Single(sink.Snapshot());
        Assert.Empty(service.Clients);
    }

    [Fact]
    public async Task EndToEnd_PhoneOverRealUdp_PairsAndControlsHost()
    {
        var sink = new RecordingSink();
        var gate = new PairingGate(Pin);
        using var service = new InputInjectionService(sink, gate, listenPort: 0);
        service.Start();
        using var phone = new FakePhone(service.BoundPort);

        // Unpaired ping: host answers but reports "not paired".
        phone.Send(InputMotionPacket.TypePing, dx: 1234);
        var pong = await phone.ReceiveAsync();
        Assert.Equal(InputMotionPacket.TypePong, pong.PacketType);
        Assert.Equal(0, pong.ButtonFlags);
        Assert.Equal(1234, pong.DeltaX);

        Assert.False(await phone.PairAsync(123_456));
        Assert.True(await phone.PairAsync(Pin));

        phone.Send(InputMotionPacket.TypePing);
        Assert.Equal(1, (await phone.ReceiveAsync()).ButtonFlags);

        phone.Send(InputMotionPacket.TypeMotion, 12, -7);
        phone.Send(InputMotionPacket.TypeButton, buttons: 0x01);
        phone.Send(InputMotionPacket.TypeButton, buttons: 0x00);
        phone.Send(InputMotionPacket.TypeScroll, 0, 120);
        await Wait.UntilAsync(() => sink.Snapshot().Length >= 4);

        Assert.Equal(new[] { "move 12,-7", "Left down", "Left up", "scroll 0,120" }, sink.Snapshot());

        // Phone app restarts: its sequence starts over, re-pairing resets ordering on the host.
        phone.ResetSequence();
        Assert.True(await phone.PairAsync(Pin));
        phone.Send(InputMotionPacket.TypeMotion, 1, 1);
        await Wait.UntilAsync(() => sink.Snapshot().Length >= 5);
        Assert.Equal("move 1,1", sink.Snapshot()[4]);
    }
}
