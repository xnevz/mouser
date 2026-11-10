# Phase 2: Native Interop & Networking Engine

> **Objective**: Implement the low-level C++ native libraries (`libmouser_native`), source-generated C# interop bindings (`[LibraryImport]`), and the zero-allocation UDP/TCP network engine for high-frequency input streaming and peer discovery.

---

## 1. Native C++ Library Architecture (`Mouser.Native`)

A single CMake project compiles the platform-specific native hooks into shared libraries exposing an `extern "C"` ABI.

```
Mouser.Native/
├── CMakeLists.txt
├── include/
│   └── mouser_native.h            # Unified C-ABI header
└── src/
    ├── platform_mac.mm            # macOS Objective-C++ implementation
    ├── platform_win.cpp           # Windows Win32 implementation
    ├── platform_linux.cpp         # Linux libevdev/uinput implementation
    └── platform_android.cpp       # Android NDK JNI implementation
```

### 1.1 Unified C-ABI Header (`mouser_native.h`)
```c
#ifndef MOUSER_NATIVE_H
#define MOUSER_NATIVE_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

// Function pointer signatures for C# callbacks
typedef void (*NativeMouseMoveCallback)(int16_t dx, int16_t dy);
typedef void (*NativeMouseButtonCallback)(uint8_t button, bool isDown);
typedef void (*NativeKeyCallback)(uint32_t keyCode, uint8_t modifiers, bool isDown);

// Platform Lifecycle & Permission APIs
bool mouser_check_accessibility_permission(void);
int32_t mouser_init_input_capture(NativeMouseMoveCallback onMove, 
                                 NativeMouseButtonCallback onButton, 
                                 NativeKeyCallback onKey);
void mouser_stop_input_capture(void);

// Synthetic Event Injection APIs
void mouser_inject_mouse_move(int32_t targetX, int32_t targetY);
void mouser_inject_mouse_delta(int16_t dx, int16_t dy);
void mouser_inject_mouse_button(uint8_t button, bool isDown);
void mouser_inject_key(uint32_t keyCode, uint8_t modifiers, bool isDown);

#ifdef __cplusplus
}
#endif

#endif // MOUSER_NATIVE_H
```

---

## 2. High-Performance C# `[LibraryImport]` Layer

Using .NET source generators avoids runtime reflection and marshaling overhead:

```csharp
// Mouser.Core/Interop/NativeDriver.cs
using System.Runtime.InteropServices;

namespace Mouser.Core.Interop;

public static partial class NativeDriver
{
    private const string LibraryName = "mouser_native";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MouseMoveDelegate(short dx, short dy);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MouseButtonDelegate(byte button, [MarshalAs(UnmanagedType.Bool)] bool isDown);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void KeyDelegate(uint keyCode, byte modifiers, [MarshalAs(UnmanagedType.Bool)] bool isDown);

    [LibraryImport(LibraryName, EntryPoint = "mouser_check_accessibility_permission")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CheckAccessibilityPermission();

    [LibraryImport(LibraryName, EntryPoint = "mouser_init_input_capture")]
    public static partial int InitInputCapture(
        MouseMoveDelegate onMove,
        MouseButtonDelegate onButton,
        KeyDelegate onKey);

    [LibraryImport(LibraryName, EntryPoint = "mouser_stop_input_capture")]
    public static partial void StopInputCapture();

    [LibraryImport(LibraryName, EntryPoint = "mouser_inject_mouse_delta")]
    public static partial void InjectMouseDelta(short dx, short dy);

    [LibraryImport(LibraryName, EntryPoint = "mouser_inject_mouse_button")]
    public static partial void InjectMouseButton(byte button, [MarshalAs(UnmanagedType.Bool)] bool isDown);
}
```

---

## 3. High-Frequency UDP Network Engine

Mouse streaming operates at up to 1,000Hz (1 packet per millisecond). Packet processing must produce zero GC allocations.

### 3.1 Packed Binary Packet Layout
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InputMotionPacket
{
    public uint Magic;       // 'MOUS' (0x53554F4D)
    public byte Version;     // 0x01
    public byte PacketType;  // 0x01 = Motion
    public ushort SequenceId;
    public short DeltaX;
    public short DeltaY;
    public byte ButtonFlags;
}
```

### 3.2 High-Throughput UDP Socket Service
```csharp
// Mouser.Core/Networking/UdpInputTransmitter.cs
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mouser.Core.Networking;

public sealed class UdpInputTransmitter : IDisposable
{
    private readonly Socket _socket;
    private readonly IPEndPoint _targetEndPoint;
    private ushort _sequenceCounter;

    public UdpInputTransmitter(IPAddress remoteIp, int port)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            SendBufferSize = 64 * 1024
        };
        _targetEndPoint = new IPEndPoint(remoteIp, port);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SendMotion(short dx, short dy, byte buttons)
    {
        var packet = new InputMotionPacket
        {
            Magic = 0x53554F4D,
            Version = 0x01,
            PacketType = 0x01,
            SequenceId = unchecked(++_sequenceCounter),
            DeltaX = dx,
            DeltaY = dy,
            ButtonFlags = buttons
        };

        // Zero-allocation Span cast and send
        ReadOnlySpan<byte> span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref packet, 1));
        _socket.SendTo(span, SocketFlags.None, _targetEndPoint);
    }

    public void Dispose() => _socket.Dispose();
}
```

---

## 4. Phase 2 Implementation Steps

- [ ] **Step 2.1: Native CMake Build Pipeline**
  - Create `Mouser.Native/CMakeLists.txt` with targets for macOS (`.dylib`), Windows (`.dll`), and Linux (`.so`).
  - Implement CMake install rules copying built binaries to the .NET project's output directory.
- [ ] **Step 2.2: Implement `[LibraryImport]` Bindings**
  - Create `Mouser.Core.Interop` with source-generated P/Invoke methods.
  - Implement unit tests verifying memory layout matches C structures.
- [ ] **Step 2.3: Zero-Allocation UDP Pipeline**
  - Build `UdpInputReceiver` and `UdpInputTransmitter` using `SocketAsyncEventArgs` and `ReadOnlySpan<byte>`.
  - Implement sequence-number tracking and jitter-buffer smoothing.
- [ ] **Step 2.4: Local mDNS Discovery**
  - Broadcast presence using multicast DNS service `_mouser._udp.local`.
  - Automatically detect and pair with peer hosts on the local network.

---

## 5. Phase 2 Deliverables & Exit Criteria

- [ ] `libmouser_mac.dylib` compiles cleanly via CMake and is loaded by .NET on macOS.
- [ ] Benchmark test sends 1,000 motion packets/second with zero GC allocations (verified via `dotnet-dump` or Rider Memory Profiler).
- [ ] Round-trip network ping between two machines is $<2\text{ms}$ over LAN.
