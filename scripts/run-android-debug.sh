#!/usr/bin/env bash
set -euo pipefail

# Auto-configure environment
export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
export ANDROID_SDK_ROOT="${ANDROID_SDK_ROOT:-$ANDROID_HOME}"
if [ -d "$HOME/.local/share/jdk-17/Contents/Home" ]; then
    export JAVA_HOME="${JAVA_HOME:-$HOME/.local/share/jdk-17/Contents/Home}"
fi
export PATH="${JAVA_HOME:-}/bin:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$ANDROID_HOME/cmdline-tools/latest/bin:$PATH"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT/android"

echo "=========================================================="
echo "  Mouser Android Companion: Run in DEBUG Mode             "
echo "=========================================================="

# Check if a device or emulator is running
DEVICES=$(adb devices | grep -v "List of devices" | grep "device$" || true)
if [ -z "$DEVICES" ]; then
    AVD_NAME=$(emulator -list-avds | head -n 1 || true)
    if [ -n "$AVD_NAME" ]; then
        echo "[*] No active Android device detected. Launching emulator '$AVD_NAME'..."
        emulator -avd "$AVD_NAME" -no-boot-anim -netdelay none -netspeed full >/dev/null 2>&1 &
        echo "[*] Waiting for emulator to boot up..."
        adb wait-for-device
        # Wait for system boot completed
        while [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" != "1" ]; do
            sleep 1
        done
        echo "[✓] Emulator is online and ready."
    else
        echo "[!] Error: No Android device connected and no AVD found."
        echo "    Create one using: avdmanager create avd -n MouserPixel -k 'system-images;android-34;google_apis;x86_64'"
        exit 1
    fi
else
    echo "[✓] Connected Android device detected."
fi

echo "[*] Building and installing Debug APK..."
./gradlew installDebug

echo "[*] Launching Mouser Companion app..."
adb shell am start -n com.mouser.companion/.MainActivity

echo "=========================================================="
echo "  [✓] Mouser Android Companion (Debug) running successfully! "
echo "=========================================================="
