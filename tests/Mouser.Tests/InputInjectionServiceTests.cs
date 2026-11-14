using System.Net;
using Mouser.Core.Input;
using Mouser.Core.Networking;
using Mouser.Core.Services;

namespace Mouser.Tests;

public class InputInjectionServiceTests
{
    private const int Pin = 482_913;
    private static readonly IPEndPoint Phone = new(IPAddress.Parse("192.168.1.50"), 40000);

    private static (InputInjectionService Service, RecordingSink Sink) Create(Func<DateTime>? clock = null)
    {
        var sink = new RecordingSink();
        return (new InputInjectionService(sink, 0, clock) { AllowOlderDevices = true }, sink);
    }

    private static InputMotionPacket P(byte type, ushort seq, short dx = 0, short dy = 0, byte buttons = 0) =>
        InputMotionPacket.Create(type, seq, dx, dy, buttons);

    [Fact]
    public void UnpairedSender_IsIgnored()
    {
        var (service, sink) = Create();
        service.AllowOlderDevices = false;
        using var _s = service;

        service.HandlePacket(P(InputMotionPacket.TypeMotion, 1, 10, 10), Phone);
        service.HandlePacket(P(InputMotionPacket.TypeKey, 2, 0x41, 1), Phone);

        Assert.Empty(sink.Snapshot());
        Assert.Equal(2, service.RejectedPackets);
    }

    [Fact]
    public void ButtonMask_IsDiffed_SoEveryPressGetsReleased()
    {
        var (service, sink) = Create();
        using var _s = service;

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
        var (service, sink) = Create();
        using var _s = service;

        service.HandlePacket(P(InputMotionPacket.TypeKey, 1, 0xB3, 1), Phone); // play/pause down
        service.HandlePacket(P(InputMotionPacket.TypeKey, 2, 0xB3, 0), Phone);
        service.HandlePacket(P(InputMotionPacket.TypeScroll, 3, 0, -240), Phone);

        Assert.Equal(new[] { "key 0xB3 down", "key 0xB3 up", "scroll 0,-240" }, sink.Snapshot());
        Assert.Equal(2, service.KeyEventsProcessed);
        Assert.Equal(1, service.ScrollEventsProcessed);
    }

    [Fact]
    public void CharPackets_AreInjectedAndHeldReleasedOnIdle()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (service, sink) = Create(() => now);
        using var _s = service;

        // TypeChar down
        service.HandlePacket(P(InputMotionPacket.TypeChar, 1, dx: 0x00E9, dy: 1, buttons: 1), Phone);
        Assert.Equal(new[] { "char 0x00E9 down" }, sink.Snapshot());
        Assert.Equal(1, service.CharEventsProcessed);

        // TypeChar up
        service.HandlePacket(P(InputMotionPacket.TypeChar, 2, dx: 0x00E9, dy: 0, buttons: 0), Phone);
        Assert.Equal(new[] { "char 0x00E9 down", "char 0x00E9 up" }, sink.Snapshot());

        // Stuck char down released on idle timeout
        service.HandlePacket(P(InputMotionPacket.TypeChar, 3, dx: 0x0627, dy: 1, buttons: 1), Phone);
        now += InputInjectionService.StuckInputTimeout + TimeSpan.FromSeconds(1);
        Assert.True(service.ReleaseHeldInputIfIdle());
        Assert.Contains("char 0x0627 up", sink.Snapshot());
    }

    [Fact]
    public void DuplicateAndReorderedPackets_AreDropped()
    {
        var (service, sink) = Create();
        using var _s = service;

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
        var (service, sink) = Create(() => now);
        using var _s = service;

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
        var (service, sink) = Create();
        using var _s = service;
        service.HandlePacket(P(InputMotionPacket.TypeMotion, 1, 1, 1), Phone);
        Assert.Single(service.Clients);

        service.ForgetClient(Phone.Address);
        Assert.Empty(service.Clients);
    }

    [Fact]
    public async Task EndToEnd_PhoneOverRealUdp_PairsAndControlsHost()
    {
        var sink = new RecordingSink();
        using var service = new InputInjectionService(sink, listenPort: 0) { AllowOlderDevices = true };
        service.Start();
        using var phone = new FakePhone(service.BoundPort);

        // Ping: host answers Pong
        phone.Send(InputMotionPacket.TypePing, dx: 1234);
        var pong = await phone.ReceiveAsync();
        Assert.Equal(InputMotionPacket.TypePong, pong.PacketType);
        Assert.Equal(1234, pong.DeltaX);

        phone.Send(InputMotionPacket.TypeMotion, 12, -7);
        phone.Send(InputMotionPacket.TypeButton, buttons: 0x01);
        phone.Send(InputMotionPacket.TypeButton, buttons: 0x00);
        phone.Send(InputMotionPacket.TypeScroll, 0, 120);
        await Wait.UntilAsync(() => sink.Snapshot().Length >= 4);

        Assert.Equal(new[] { "move 12,-7", "Left down", "Left up", "scroll 0,120" }, sink.Snapshot());
    }
}
