# Mouser Android Companion App

Native Android companion application for **Mouser** — Virtual KVM and Universal Smart Clipboard.
Transforms any Android smartphone into an ultra-low latency wireless trackpad, 3D gyro air-mouse, and media remote for desktop operating systems (macOS, Windows, Linux).

---

## Architecture & Features

- **Native Kotlin + Jetpack Compose**: Modern declarative UI with custom zinc dark theme (`#18181B`).
- **Precision Multi-Touch Trackpad**:
  - 1-finger drag with dynamic ballistic velocity curves.
  - 1-finger tap for Left Click, double-tap for Double Click.
  - Long press for Right Click.
  - Hardware Left and Right Click strip.
- **3D Gyroscope Air Mouse**:
  - Uses Android `SensorManager` (`Sensor.TYPE_GYROSCOPE` with `SENSOR_DELAY_GAME`).
  - Hand tremor deadzone filter (`0.04f` threshold).
  - Ergonomic "HOLD TO POINT" trigger button (reposition without moving mouse).
- **Media Remote & Macros**:
  - Media controls (Play/Pause, Next, Prev, Volume +, Volume -, Mute).
  - Numeric Keypad (Numpad) for entering numbers into desktop spreadsheets and apps.
- **Ultra-Low Latency Binary Protocol**:
  - Transmits 14-byte `InputMotionPacket` over UDP datagrams (`ByteOrder.LITTLE_ENDIAN`).
  - Sequence-number tracking.
- **Peer Discovery**:
  - Auto-discovery on local network over UDP port `58490` (`MOUSER_DISCOVER_QUERY`).
  - Manual host IP and port configuration dialog.

---

## Opening in Android Studio

1. Open **Android Studio** (Koala / Ladybug or newer recommended).
2. Click **File -> Open...** and select the `/android` folder:
   ```
   /Users/abdelkarim/Documents/mouser/android
   ```
3. Allow Gradle to sync dependencies (requires JDK 17+).
4. Connect an Android device (Android 8.0 / API 26+) with USB debugging or start an Android Virtual Device (AVD).
5. Click **Run 'app'** (`Shift + F10`).

---

## Build Requirements

- Android Studio Koala / Ladybug or newer
- JDK 17
- Android SDK Platform 35 (`compileSdk = 35`, `minSdk = 26`)
- Android Gradle Plugin 8.7.2
- Kotlin 2.0.21 with Compose Compiler Plugin
