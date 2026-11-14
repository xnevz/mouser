# Wayland Input Capture and Emulation Architecture

## 1. The Wayland Security Model

Unlike the X11 windowing system where any client can register global keyboard/pointer hooks and inject events via `XTest` or `XSendEvent`, Wayland compositors intentionally enforce strict client isolation:

- **No Global Hooks:** No client can snoop keystrokes or mouse movement across other application windows.
- **No Global Warping / Synthetic Injections:** Standard Wayland protocols do not allow an unprivileged application to inject arbitrary input events into the compositor or warp the cursor to a physical coordinate.
- **Implication for Mouser:** Traditional desktop KVM capture (`XGrabPointer`, `XGrabKeyboard`, `XISelectEvents`) and injection (`XTestFakeMotionEvent`, `XTestFakeKeyEvent`) fail immediately under native Wayland environments (GNOME Shell on Wayland, KDE Plasma Wayland, Sway, Hyprland).

---

## 2. Modern Standards: `libei` and `xdg-desktop-portal`

To allow accessibility tools, remote desktop clients, and software KVMs to operate safely without compromising system security, the Linux desktop community developed two primary interfaces:

### A. `libei` (Emulated Input)
- **Overview:** Developed by Red Hat / Peter Hutterer, `libei` provides a protocol and library for emulating input devices between a client (`libei`) and an Emulated Input Server (`libeis`).
- **Server Support:** Integrated into GNOME Mutter (45+), KDE KWin (6.0+), and standalone daemons.
- **Mechanisms:**
  - `ei_device`: Represents a virtual pointer, keyboard, touch screen, or absolute pointing device.
  - Device emulation: `ei_device_pointer_motion()`, `ei_device_keyboard_key()`, etc.
  - Device capture: `eis` can negotiate capturing physical events from the compositor when the cursor leaves screen bounds.

### B. `org.freedesktop.portal.RemoteDesktop` (XDG Desktop Portal)
- **Overview:** Standard D-Bus portal mediated by `xdg-desktop-portal` and desktop-specific backends (`xdg-desktop-portal-gnome`, `xdg-desktop-portal-kde`).
- **Permissions:** Prompts the user once per session with an authorization dialog ("Mouser wants to control your mouse and keyboard").
- **D-Bus Workflow:**
  1. `CreateSession()` returns a session handle.
  2. `SelectDevices()` requests `POINTER (1)` and `KEYBOARD (2)`.
  3. `Start()` presents the permission dialog and returns an active token.
  4. Call `NotifyPointerMotion(session, options, dx, dy)` or `NotifyPointerMotionAbsolute(...)`.
  5. Call `NotifyKeyboardKeysym(session, options, keysym, state)` or `NotifyKeyboardKeycode(...)`.

---

## 3. Integration Plan for Mouser

### Phase 1 (Current State): Detection & User Transparency
- Mouser detects native Wayland sessions via `XDG_SESSION_TYPE == "wayland"` or `WAYLAND_DISPLAY != null`.
- `X11InputSink.IsSupported` and `X11InputCapture.IsSupported` safely return `false`.
- The Phone and KVM status banners display an informative hint:
  *"Wayland restricts synthetic input and global capture. Log in to an X11 session or configure RemoteDesktop portal access."*

### Phase 2: D-Bus Portal Input Sink (`PortalInputSink`)
- Implement an `IInputSink` powered by `Tmds.DBus` or custom Unix socket D-Bus messages to `org.freedesktop.portal.Desktop`.
- When Mouser starts on Wayland, request a RemoteDesktop session.
- Once granted, route phone remote control and incoming peer KVM events to the portal session.

### Phase 3: `libei` KVM Input Capture (`EiInputCapture`)
- For outbound KVM sharing (controlling remote peers from a Wayland host):
- Bind `libei.so.1` via P/Invoke.
- Connect to the compositor's EIS socket (either directly via `$LIBEI_SOCKET` or negotiated via the portal).
- Request a capture context to intercept pointer events when moving beyond monitor boundaries.

---

## 4. Summary Matrix

| Platform / Windowing | Inbound Control (Sink) | Outbound Control (Capture) | Status |
| :--- | :--- | :--- | :--- |
| **Windows 10/11** | `SendInput` (`Win32InputSink`) | Low-level Hooks (`Win32InputCapture`) | Supported |
| **macOS** | `CGEventPost` (`MacInputSink`) | Quartz Event Tap (`MacInputCapture`) | Supported |
| **Linux (X11)** | `XTest` (`X11InputSink`) | XInput2 / `XGrabPointer` (`X11InputCapture`) | Supported |
| **Linux (Wayland)** | `RemoteDesktop` Portal (Planned) | `libei` / Portal (Planned) | Design Documented |
