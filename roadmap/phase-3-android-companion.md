# Phase 3: Android Companion & Remote Input Hub

> **Status (verified):** see [README](./README.md) for what the Android app does today.

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

- [x] **Step 3.1: Android Project Configuration (Native Android Studio Project)**
  - Setup dedicated `android/` project with Kotlin 2.0+ and Jetpack Compose (Material3).
  - Configure `AndroidManifest.xml` with permissions: `INTERNET`, `ACCESS_NETWORK_STATE`, `ACCESS_WIFI_STATE`, `VIBRATE`, `HIGH_SAMPLING_RATE_SENSORS`.
  - Configured Gradle Version Catalog (`libs.versions.toml`) targeting Android SDK 35.
- [x] **Step 3.2: Multi-Touch Trackpad Surface**
  - Built `TrackpadSurface.kt` with 1-finger relative dragging, tap-to-click, double-click, and long-press right click.
  - Dedicated hardware Left and Right Click button strip.
- [x] **Step 3.3: 3D Gyroscope Air Mouse Engine**
  - Built `GyroAirMouse.kt` subscribing to `Sensor.TYPE_GYROSCOPE` with `SENSOR_DELAY_GAME`.
  - Implemented deadzone tremor filter (`0.04f`) and dynamic sensitivity scaling.
  - Built `AirMouseSurface.kt` with ergonomic "HOLD TO POINT" trigger button.
- [x] **Step 3.4: Binary Protocol & UDP Transmission**
  - Implemented `InputMotionPacket.kt` matching desktop 14-byte little-endian format byte-for-byte.
  - Implemented `UdpClient.kt` for zero-allocation UDP datagram transmission with sequence counters.
  - Built `HostDiscoveryClient.kt` for auto-discovering desktop Mouser hosts on UDP 58490.
- [x] **Step 3.5: Media Macros & Numeric Keypad**
  - Built `MediaMacrosView.kt` with Play/Pause, Next, Prev, Vol+, Vol-, Mute, and a full 16-key Numpad.
- [x] **Step 3.6: Desktop Input Injection Service**
  - Implemented `InputInjectionService.cs` in `Mouser.Core.Services` handling remote UDP packets and dispatching to `NativeDriver`.
  - Added unit test suite in `tests/Mouser.Tests/InputInjectionServiceTests.cs` (28/28 tests passing).

---

## 6. Android Emulator & Testing Environment

An Android Virtual Device (AVD) has been provisioned on macOS for testing the Android Companion:

- **SDK Location**: `~/Library/Android/sdk`
- **AVD Name**: `MouserPixel` (Pixel 7 configuration with Google APIs)
- **Target OS**: Android 14.0 (API Level 34, `x86_64`)
- **CLI Binaries**: `adb`, `emulator`, `avdmanager`, `sdkmanager` (configured in PATH via `~/.zshrc`)

### Running the Emulator & Installing Mouser Android

```bash
# 1. Start the emulator
emulator -avd MouserPixel &

# 2. Wait for device to boot
adb wait-for-device

# 3. Build the companion app
cd /Users/abdelkarim/Documents/mouser/android
./gradlew assembleDebug

# 4. Install onto the running emulator
adb install -r app/build/outputs/apk/debug/app-debug.apk
```
