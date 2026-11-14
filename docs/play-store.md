# Mouser Companion — Google Play Store Release Checklist

This guide covers everything required to package, sign, and publish the Mouser Companion Android application to the Google Play Store.

---

## 1. Store Listing Metadata

### App Details
- **App Name:** Mouser Companion
- **Short Description (≤ 80 characters):**
  > Ultra-low-latency trackpad, air mouse, presentation remote & macros for PC.
- **Category:** Tools / Productivity
- **Content Rating:** Everyone (3+)

### Full Description
```text
Turn your Android phone or tablet into a precision input companion for your PC.

Mouser Companion connects seamlessly over your local Wi-Fi network to provide ultra-low latency mouse tracking, presentation control, and custom shortcut buttons.

FEATURES:
• Multi-touch Trackpad: 1-finger smooth cursor movement, 2-finger scroll and pinch-to-zoom, 3-finger desktop swipe gestures, and tap-to-click.
• Gyroscopic Air Mouse: Wave your phone to point across your screens with sub-millisecond motion processing.
• Slide Remote: Control PowerPoint, Keynote, and Google Slides with Next/Previous buttons, presentation timer, speaker notes, and an on-screen laser pointer overlay.
• Macro Pad: Customizable grid of single-key, multi-key combo, and text snippet macros with JSON export/import.
• Direct Typing: Send text and dictate directly to your PC with instant diff synchronization.
• Clipboard Sync: Effortlessly push and pull clipboard text to and from your computer.
• File Sharing: Send photos, documents, and files directly to your PC via Android's native share sheet.
• Biometric App Lock: Secure companion controls with fingerprint or facial authentication.
• Landscape & Tablet Optimized: Ergonomic side-rail controls designed for two-handed tablet use.

PRIVACY & SECURITY:
• 100% Local: No cloud servers, no account registration, no remote telemetry.
• Cryptographically Secure: Pairing uses P-256 ECDH key exchange with 6-digit confirmation codes; all sessions are protected by AES-256-GCM encryption with replay protection.
```

---

## 2. Graphic Assets Checklist

| Asset | Specifications | Status |
| :--- | :--- | :--- |
| **App Icon** | 512 × 512 px, 32-bit PNG with alpha, max 1024 KB | Prepared (`mipmap-xxxhdpi`) |
| **Feature Graphic** | 1024 × 500 px, JPG or 24-bit PNG (no alpha), max 15 MB | Prepared |
| **Phone Screenshots** | Min 2, max 8. Recommended 1080 × 2400 px (Trackpad, Air Mouse, Slides, Macros, Typing) | Prepared |
| **7-Inch Tablet Screenshots** | Min 1. Recommended 1200 × 1920 px (Landscape Trackpad layout) | Prepared |
| **10-Inch Tablet Screenshots**| Min 1. Recommended 1600 × 2560 px (Landscape Trackpad layout) | Prepared |

---

## 3. Permissions & Declarations Rationale

Google Play requires explicit declarations for sensitive permissions:

1. **`FOREGROUND_SERVICE_CONNECTED_DEVICE`**:
   - *Rationale:* Mouser Companion connects to a local PC as a physical input peripheral. The foreground service maintains active UDP motion packet streaming and TCP event handling when the screen is dimmed or other apps are momentarily accessed.
2. **`POST_NOTIFICATIONS`**:
   - *Rationale:* Required by Android 13+ to display the ongoing connection status notification with a one-tap **Disconnect** action.
3. **`ACCESS_NETWORK_STATE` & `INTERNET`**:
   - *Rationale:* Required for UDP multicast host discovery (port 58490) and point-to-point encrypted session transmission (port 58491) within the local Wi-Fi network.
4. **`VIBRATE`**:
   - *Rationale:* Provides haptic feedback on trackpad clicks, button releases, and slide advance.
5. **`REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`**:
   - *Rationale:* Optional prompt to prevent aggressive vendor OS battery managers from throttling motion sensors mid-presentation.

---

## 4. Privacy Policy Declaration

- **URL:** `https://mouser.local/privacy` (or project repo URL)
- **Data Collection:**
  - Zero personal data collected.
  - Zero analytics / tracking SDKs.
  - Zero advertising.
  - All communication is strictly peer-to-peer on the local network using AES-256-GCM.

---

## 5. Release Build & Signing Instructions

### 1. Generate Release Keystore (One-time)
Run the following command in a secure environment:
```bash
keytool -genkey -v -keystore mouser-release.keystore -alias mouser-companion -keyalg RSA -keysize 2048 -validity 10000
```
> [!IMPORTANT]
> Store `mouser-release.keystore` in a safe, backed-up location. Never commit it to git (verified ignored in `.gitignore`).

### 2. Configure `local.properties`
Add signing credentials to `android/local.properties`:
```properties
KEYSTORE_FILE=../mouser-release.keystore
KEYSTORE_PASSWORD=your_keystore_password
KEY_ALIAS=mouser-companion
KEY_PASSWORD=your_key_password
```

### 3. Build Signed Android App Bundle (AAB)
```bash
cd android
./gradlew bundleRelease
```
The resulting bundle is generated at:
`android/app/build/outputs/bundle/release/app-release.aab`

---

## 6. Play Console Deployment Steps

1. **Create Application:** In Google Play Console, select **Create app**, title "Mouser Companion", default language "English (US)".
2. **Set up Store Presence:** Fill in Short Description, Full Description, upload Icon, Feature Graphic, and Screenshots.
3. **App Content Questionnaire:**
   - Privacy Policy link.
   - Ads: Select "No, my app does not contain ads".
   - App Access: Select "All functionality is available without special access".
   - Target Audience: 13+ or Everyone.
4. **Internal Testing Track:**
   - Create a new release in **Testing > Internal testing**.
   - Upload `app-release.aab`.
   - Add internal testers and verify installation via Google Play Store link.
5. **Promote to Production:** Once verified, promote through Closed Testing to Production.
