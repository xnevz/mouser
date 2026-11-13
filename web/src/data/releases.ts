import { PlatformId, PlatformInfo } from '@/types/download';

export const REPO_URL = 'https://github.com/xnevz/mouser';
export const RELEASES_URL = `${REPO_URL}/releases`;

/**
 * What each platform can do today. Keep this in sync with the code: no build is published yet,
 * so every platform points at "build from source" and the GitHub Releases page.
 */
export const PLATFORMS: Record<PlatformId, PlatformInfo> = {
  windows: {
    id: 'windows',
    name: 'Windows',
    badge: 'Works today',
    summary: 'Desktop app that receives input from the Android companion and keeps a searchable clipboard history.',
    requirements: 'Windows 10 or 11, 64-bit',
    works: [
      'Phone remote: pointer, clicks, dragging, scrolling, media and number keys',
      'PIN pairing, so only your phones can control the PC',
      'Automatic discovery on the local network',
      'Clipboard history with search, stars and secret filtering',
    ],
    notYet: [
      'Moving the mouse between two computers',
      'Signed installer (you get a single portable .exe)',
    ],
    buildCommand: 'bash scripts/package-windows.sh',
    buildOutput: 'bin/dist/windows/Mouser.exe',
    setup: [
      'Run Mouser.exe and allow it through Windows Defender Firewall for private networks.',
      'Open the Phone Remote page to see the pairing PIN.',
    ],
  },
  macos: {
    id: 'macos',
    name: 'macOS',
    badge: 'Builds, less tested',
    summary: 'Same desktop app, with input injection through a small native library.',
    requirements: 'macOS 12 or later, Xcode command line tools and CMake to build',
    works: [
      'Phone remote: pointer, clicks, dragging, scrolling, arrow and number keys',
      'Clipboard history',
    ],
    notYet: [
      'Media keys from the phone (macOS needs a different event type)',
      'Moving the mouse between two computers',
      'Notarized, signed app',
    ],
    buildCommand: 'bash scripts/package-macos-app.sh osx-arm64',
    buildOutput: 'bin/dist/macos/Mouser.app',
    setup: [
      'Open Mouser.app, then allow it in System Settings → Privacy & Security → Accessibility.',
      'Restart Mouser after granting the permission.',
    ],
  },
  linux: {
    id: 'linux',
    name: 'Linux',
    badge: 'Clipboard only',
    summary: 'The desktop app runs and keeps clipboard history, but cannot inject input yet.',
    requirements: 'x86_64 desktop with X11 or Wayland',
    works: ['Clipboard history', 'Shows up for phones on the network'],
    notYet: ['Phone remote input (needs an XTest or uinput backend)', 'Moving the mouse between two computers'],
    buildCommand: 'bash scripts/package-linux.sh',
    buildOutput: 'bin/dist/linux/Mouser',
    setup: ['chmod +x Mouser && ./Mouser'],
  },
  android: {
    id: 'android',
    name: 'Android',
    badge: 'Companion app',
    summary: 'Turns your phone into a trackpad, air mouse and media remote for a computer running Mouser.',
    requirements: 'Android 8.0 (API 26) or later, same Wi-Fi network as the computer',
    works: [
      'Trackpad: move, tap, two-finger scroll and right-click, hold-to-drag',
      'Gyroscope air mouse',
      'Media keys, arrows and number pad',
      'Finds computers automatically and remembers the PIN',
    ],
    notYet: ['Typing text from the phone keyboard', 'Signed release on a store'],
    buildCommand: 'cd android && ./gradlew assembleDebug',
    buildOutput: 'android/app/build/outputs/apk/debug/app-debug.apk',
    setup: [
      'Install the APK (allow installs from unknown sources).',
      'Open Mouser, pick your computer and type the PIN it shows.',
    ],
  },
};
