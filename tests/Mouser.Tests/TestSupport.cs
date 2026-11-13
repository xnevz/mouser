using System.Net;
using System.Net.Sockets;
using Mouser.Core.Input;
using Mouser.Core.Networking;

namespace Mouser.Tests;

/// <summary>Records injected input instead of touching the real OS.</summary>
public sealed class RecordingSink : IInputSink
{
    public List<string> Events { get; } = new();
    public bool IsSupported => true;

    public void MoveRelative(int dx, int dy) { lock (Events) Events.Add($"move {dx},{dy}"); }
    public void MoveAbsolute(int x, int y) { lock (Events) Events.Add($"at {x},{y}"); }
    public void SetButton(MouseButton button, bool isDown) { lock (Events) Events.Add($"{button} {(isDown ? "down" : "up")}"); }
    public void Scroll(int horizontal, int vertical) { lock (Events) Events.Add($"scroll {horizontal},{vertical}"); }
    public void Key(ushort virtualKey, bool isDown) { lock (Events) Events.Add($"key 0x{virtualKey:X2} {(isDown ? "down" : "up")}"); }

    public string[] Snapshot() { lock (Events) return Events.ToArray(); }
}

/// <summary>Behaves like the Android app: one UDP socket that sends packets and reads host replies.</summary>
public sealed class FakePhone : IDisposable
{
    private readonly UdpClient _udp = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly IPEndPoint _host;
    private ushort _seq;

    public FakePhone(int hostPort) => _host = new IPEndPoint(IPAddress.Loopback, hostPort);

    public void Send(byte type, short dx = 0, short dy = 0, byte buttons = 0) =>
        SendPacket(InputMotionPacket.Create(type, ++_seq, dx, dy, buttons));

    public void SendPacket(InputMotionPacket packet)
    {
        var bytes = new byte[InputMotionPacket.PacketSize];
        packet.TryWrite(bytes);
        _udp.Send(bytes, bytes.Length, _host);
    }

    public void ResetSequence(ushort value = 0) => _seq = value;

    public async Task<InputMotionPacket> ReceiveAsync(int timeoutMs = 2000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var result = await _udp.ReceiveAsync(cts.Token);
        Assert.True(InputMotionPacket.TryRead(result.Buffer, out var packet));
        return packet;
    }

    public async Task<bool> PairAsync(int pin)
    {
        SendPacket(InputMotionPacket.CreatePair(++_seq, pin));
        var ack = await ReceiveAsync();
        Assert.Equal(InputMotionPacket.TypePairAck, ack.PacketType);
        return ack.ButtonFlags == 1;
    }

    public void Dispose() => _udp.Dispose();
}

public static class Wait
{
    public static async Task UntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition not met in time.");
            await Task.Delay(10);
        }
    }
}

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Windows only";
    }
}
