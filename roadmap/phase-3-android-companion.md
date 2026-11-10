# Phase 3: Android Companion & Remote Input Hub

> **Objective**: Build the Android mobile companion application that turns any Android smartphone into a high-precision, low-latency wireless trackpad, 3D gyro air-mouse, and macro remote for desktop computers.

---

## 1. Mobile Input Capabilities

### 1.1 Precision Multi-Touch Trackpad
- **High-Frequency Sampling**: Samples raw touch events at up to 120Hz / 240Hz display refresh rates.
- **Gesture Engine**:
  - **1-Finger Drag**: Relative cursor motion with customizable ballistic acceleration.
  - **1-Finger Tap**: Left click.
  - **2-Finger Tap**: Right click.
  - **2-Finger Drag**: Vertical and horizontal smooth kinetic scrolling.
  - **Pinch In / Out**: Zoom in / out (translating to `Ctrl + Mouse Wheel` or `Cmd + Mouse Wheel`).
  - **Tap-and-Hold Drag**: Text selection and window dragging.

### 1.2 3D Gyroscope Air Mouse (Spatial Pointer)
- **Sensor Fusion**: Reads Android `Sensor.TYPE_ROTATION_VECTOR` and `Sensor.TYPE_GYROSCOPE` at 100Hz.
- **Deadzone & Jitter Filter**: Filters out involuntary micro-tremors from the user's hand using a low-pass Kalman/complementary filter.
- **Trigger Button**: User holds a soft button on screen to activate pointing mode (pointing like a TV remote or laser pointer).

### 1.3 Quick Macros & Media Remote
- Dedicated remote controls: Play, Pause, Next Track, Previous Track, System Volume, Mute.
- Numpad mode: Full touch numeric keypad for data entry into desktop spreadsheets.

---

## 2. Technical Architecture & Communication

```mermaid
graph LR
    subgraph Android Device (Mouser Mobile)
        Touch[Touch Listener / Compose]
        Gyro[SensorManager: Gyro/IMU]
        Engine[Input Packet Serializer]
        UdpClient[UDP Socket Client]

        Touch --> Engine
        Gyro --> Engine
        Engine --> UdpClient
    end

    subgraph Desktop Host (macOS / Windows / Linux)
        UdpServer[UDP Receiver Service]
        NativeInj[libmouser_native Injection Engine]
        OSCursor[Operating System Cursor]

        UdpClient -->|Wi-Fi: <3ms UDP Packets| UdpServer
        UdpServer --> NativeInj
        NativeInj --> OSCursor
    end
```

---

## 3. Implementation Blueprint

### 3.1 Android Sensor & Touch Processor
```csharp
// Mouser.Android/Sensors/GyroAirMouseService.cs
using Android.Hardware;

namespace Mouser.Android.Sensors;

public sealed class GyroAirMouseService : Java.Lang.Object, ISensorEventListener
{
    private readonly SensorManager _sensorManager;
    private readonly Action<short, short> _onDeltaCalculated;
    private float _lastPitch, _lastYaw;

    public GyroAirMouseService(SensorManager sensorManager, Action<short, short> onDelta)
    {
        _sensorManager = sensorManager;
        _onDeltaCalculated = onDelta;
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Sensor?.Type == SensorType.Gyroscope)
        {
            // Calculate rotational velocity deltas
            float yawVelocity = e.Values![0];
            float pitchVelocity = e.Values![1];

            // Filter out minor hand trembling (deadzone threshold)
            if (Math.Abs(yawVelocity) > 0.05f || Math.Abs(pitchVelocity) > 0.05f)
            {
                short dx = (short)(yawVelocity * -25.0f);
                short dy = (short)(pitchVelocity * -25.0f);
                _onDeltaCalculated(dx, dy);
            }
        }
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }
}
```

### 3.2 Desktop Receiver & Native Injection
On the desktop side, incoming UDP delta packets trigger native C++ injection:
```csharp
// Mouser.Core/Services/InputInjectionService.cs
using Mouser.Core.Interop;
using Mouser.Core.Networking;

namespace Mouser.Core.Services;

public sealed class InputInjectionService
{
    public void HandleRemoteMotion(InputMotionPacket packet)
    {
        // Call low-level native injection driver
        NativeDriver.InjectMouseDelta(packet.DeltaX, packet.DeltaY);

        if (packet.ButtonFlags != 0)
        {
            bool isLeftDown = (packet.ButtonFlags & 0x01) != 0;
            NativeDriver.InjectMouseButton(1, isLeftDown);
        }
    }
}
```

---

## 4. Phase 3 Implementation Steps

- [ ] **Step 3.1: Android Project Configuration**
  - Setup `Mouser.Android` with Avalonia Mobile / .NET Android workload.
  - Configure `AndroidManifest.xml` with permissions: `INTERNET`, `ACCESS_WIFI_STATE`, `VIBRATE`.
- [ ] **Step 3.2: Multi-Touch Trackpad Surface**
  - Build touch control pad with visual feedback rings on finger touch.
  - Implement velocity curve calculation for natural cursor deceleration.
- [ ] **Step 3.3: 3D Gyroscope Air Mouse Engine**
  - Subscribe to `Sensor.TYPE_GYROSCOPE` with `SENSOR_DELAY_GAME` (20ms interval).
  - Implement smoothing filter to prevent cursor jitter.
- [ ] **Step 3.4: Desktop Receiver Verification**
  - Run end-to-end test: control macOS/Windows cursor using physical Android device over Wi-Fi.

---

## 5. Phase 3 Deliverables & Exit Criteria

- [ ] Android application successfully pairs with desktop host via mDNS.
- [ ] Trackpad touch input moves the desktop cursor fluidly without lag or jumps.
- [ ] 3D air-mouse pointer functions smoothly when holding the trigger button.
- [ ] Two-finger scroll operates smoothly in browser and document windows.
