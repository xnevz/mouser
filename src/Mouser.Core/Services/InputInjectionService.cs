using System.Net;
using Mouser.Core.Interop;
using Mouser.Core.Networking;

namespace Mouser.Core.Services;

/// <summary>
/// Service that receives remote input motion packets (from Android Companion or remote desktop)
/// and injects synthetic OS input events using the native driver.
/// </summary>
public interface IInputInjectionService : IDisposable
{
    bool IsRunning { get; }
    int BoundPort { get; }
    long MotionEventsProcessed { get; }
    long ButtonEventsProcessed { get; }
    long KeyEventsProcessed { get; }
    IPEndPoint? LastRemoteEndPoint { get; }

    void Start();
    void Stop();
    void HandlePacket(InputMotionPacket packet, IPEndPoint remoteEndPoint);
}

public sealed class InputInjectionService : IInputInjectionService
{
    private readonly UdpInputReceiver _receiver;
    private readonly bool _injectToNativeOs;
    private bool _isDisposed;

    public bool IsRunning { get; private set; }
    public int BoundPort => _receiver.BoundPort;
    public long MotionEventsProcessed { get; private set; }
    public long ButtonEventsProcessed { get; private set; }
    public long KeyEventsProcessed { get; private set; }
    public IPEndPoint? LastRemoteEndPoint { get; private set; }

    public event Action<InputMotionPacket, IPEndPoint>? PacketProcessed;

    public InputInjectionService(int listenPort = 0, bool injectToNativeOs = true)
    {
        _receiver = new UdpInputReceiver(listenPort);
        _receiver.MotionReceived += HandlePacket;
        _injectToNativeOs = injectToNativeOs;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (IsRunning) return;

        _receiver.Start();
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _receiver.Stop();
        IsRunning = false;
    }

    public void HandlePacket(InputMotionPacket packet, IPEndPoint remoteEndPoint)
    {
        LastRemoteEndPoint = remoteEndPoint;

        switch (packet.PacketType)
        {
            case 0x01: // Motion
                ProcessMotionPacket(packet);
                break;

            case 0x02: // Button
                ProcessButtonPacket(packet);
                break;

            case 0x03: // Key
                ProcessKeyPacket(packet);
                break;
        }

        PacketProcessed?.Invoke(packet, remoteEndPoint);
    }

    private void ProcessMotionPacket(InputMotionPacket packet)
    {
        MotionEventsProcessed++;

        if (_injectToNativeOs)
        {
            try
            {
                if (packet.DeltaX != 0 || packet.DeltaY != 0)
                {
                    NativeDriver.InjectMouseDelta(packet.DeltaX, packet.DeltaY);
                }

                if (packet.ButtonFlags != 0)
                {
                    bool isLeftDown = (packet.ButtonFlags & 0x01) != 0;
                    bool isRightDown = (packet.ButtonFlags & 0x02) != 0;
                    bool isMiddleDown = (packet.ButtonFlags & 0x04) != 0;

                    if (isLeftDown) NativeDriver.InjectMouseButton(1, true);
                    if (isRightDown) NativeDriver.InjectMouseButton(2, true);
                    if (isMiddleDown) NativeDriver.InjectMouseButton(3, true);
                }
            }
            catch (DllNotFoundException)
            {
                // Native driver not loaded in non-desktop environments or unit tests
            }
        }
    }

    private void ProcessButtonPacket(InputMotionPacket packet)
    {
        ButtonEventsProcessed++;

        if (_injectToNativeOs)
        {
            try
            {
                bool isLeftDown = (packet.ButtonFlags & 0x01) != 0;
                bool isRightDown = (packet.ButtonFlags & 0x02) != 0;
                bool isMiddleDown = (packet.ButtonFlags & 0x04) != 0;

                if (isLeftDown) NativeDriver.InjectMouseButton(1, true);
                else if (packet.ButtonFlags == 0) NativeDriver.InjectMouseButton(1, false);

                if (isRightDown) NativeDriver.InjectMouseButton(2, true);
                if (isMiddleDown) NativeDriver.InjectMouseButton(3, true);
            }
            catch (DllNotFoundException)
            {
                // Native driver stub
            }
        }
    }

    private void ProcessKeyPacket(InputMotionPacket packet)
    {
        KeyEventsProcessed++;

        if (_injectToNativeOs)
        {
            try
            {
                uint keyCode = (uint)packet.DeltaX;
                bool isDown = packet.DeltaY != 0;
                NativeDriver.InjectKey(keyCode, packet.Modifiers, isDown);
            }
            catch (DllNotFoundException)
            {
                // Native driver stub
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _receiver.MotionReceived -= HandlePacket;
        _receiver.Dispose();
    }
}
