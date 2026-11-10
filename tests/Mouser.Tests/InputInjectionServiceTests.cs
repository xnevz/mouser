using System.Net;
using Mouser.Core.Networking;
using Mouser.Core.Services;
using Xunit;

namespace Mouser.Tests;

public class InputInjectionServiceTests
{
    [Fact]
    public void InputInjectionService_InitializesAndBindsPort()
    {
        using var service = new InputInjectionService(listenPort: 0, injectToNativeOs: false);

        Assert.False(service.IsRunning);
        Assert.True(service.BoundPort > 0);
        Assert.Equal(0, service.MotionEventsProcessed);
        Assert.Equal(0, service.ButtonEventsProcessed);
        Assert.Equal(0, service.KeyEventsProcessed);
        Assert.Null(service.LastRemoteEndPoint);
    }

    [Fact]
    public void InputInjectionService_StartAndStop_TransitionsRunningState()
    {
        using var service = new InputInjectionService(listenPort: 0, injectToNativeOs: false);

        service.Start();
        Assert.True(service.IsRunning);

        service.Stop();
        Assert.False(service.IsRunning);
    }

    [Fact]
    public void InputInjectionService_HandlePacket_ProcessesMotionCorrectly()
    {
        using var service = new InputInjectionService(listenPort: 0, injectToNativeOs: false);
        var remoteEp = new IPEndPoint(IPAddress.Loopback, 54321);

        var packet = InputMotionPacket.CreateMotion(sequenceId: 1, dx: 15, dy: -25, buttons: 0x01);
        service.HandlePacket(packet, remoteEp);

        Assert.Equal(1, service.MotionEventsProcessed);
        Assert.Equal(0, service.ButtonEventsProcessed);
        Assert.Equal(0, service.KeyEventsProcessed);
        Assert.Equal(remoteEp, service.LastRemoteEndPoint);
    }

    [Fact]
    public void InputInjectionService_HandlePacket_ProcessesButtonAndKeyEvents()
    {
        using var service = new InputInjectionService(listenPort: 0, injectToNativeOs: false);
        var remoteEp = new IPEndPoint(IPAddress.Loopback, 54321);

        var buttonPacket = new InputMotionPacket
        {
            Magic = InputMotionPacket.ExpectedMagic,
            Version = InputMotionPacket.CurrentVersion,
            PacketType = 0x02, // Button
            SequenceId = 2,
            ButtonFlags = 0x02
        };
        service.HandlePacket(buttonPacket, remoteEp);

        var keyPacket = new InputMotionPacket
        {
            Magic = InputMotionPacket.ExpectedMagic,
            Version = InputMotionPacket.CurrentVersion,
            PacketType = 0x03, // Key
            SequenceId = 3,
            DeltaX = 0x20, // Space key
            DeltaY = 1     // Down
        };
        service.HandlePacket(keyPacket, remoteEp);

        Assert.Equal(1, service.ButtonEventsProcessed);
        Assert.Equal(1, service.KeyEventsProcessed);
    }

    [Fact]
    public void InputInjectionService_PacketProcessedEvent_FiresOnEveryPacket()
    {
        using var service = new InputInjectionService(listenPort: 0, injectToNativeOs: false);
        var remoteEp = new IPEndPoint(IPAddress.Loopback, 54321);

        InputMotionPacket? receivedPacket = null;
        IPEndPoint? receivedEp = null;

        service.PacketProcessed += (p, ep) =>
        {
            receivedPacket = p;
            receivedEp = ep;
        };

        var motion = InputMotionPacket.CreateMotion(10, 5, 5);
        service.HandlePacket(motion, remoteEp);

        Assert.NotNull(receivedPacket);
        Assert.Equal(10, receivedPacket.Value.SequenceId);
        Assert.Equal(remoteEp, receivedEp);
    }
}
