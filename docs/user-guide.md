# Mouser User Guide

Welcome to **Mouser** — the modern, lightweight, end-to-end encrypted mouse, keyboard, and clipboard sharing solution for your local network.

---

## 1. System Requirements & Installation

- **Operating System:** Windows 10 (1809+) or Windows 11 (64-bit).
- **Runtime:** .NET 8.0 Desktop Runtime.
- **Network:** Local Area Network (Wi-Fi or Ethernet). Computers must be able to communicate via UDP (ports 58500–58502) and TCP (port 58503).
- **Installation:** Extract the portable distribution zip or run `Mouser.exe`.

---

## 2. First-Run Setup & Onboarding

When launching Mouser for the first time:
1. **Name your computer:** Choose a friendly name (e.g., "Work Laptop" or "Studio PC") so your other devices can easily identify it.
2. **Configure feature defaults:** Enable or disable Desktop KVM sharing, Clipboard History & Sync, and Start at Login.
3. **Review your pairing PIN:** A 6-digit numeric PIN is assigned to your device for initial peer authorization.

---

## 3. Pairing Devices

### Pairing Two Computers
1. Ensure Mouser is running on both machines connected to the same Wi-Fi or wired network.
2. Navigate to the **Devices** tab in the sidebar.
3. Available computers will be automatically discovered via local broadcast beacons.
4. Click **Pair** next to the target computer and enter the 6-digit PIN displayed on that computer's screen.
5. Once paired, you can position the computer relative to your primary display (Left, Right, Above, Below).

### Pairing Across Different Subnets or VPNs
If your machines are on different subnets, VLANs, or a VPN (such as Tailscale):
1. In the **Devices** tab, click **Add device by IP**.
2. Enter the target machine's IP address and port (e.g. `192.168.1.50:58500` or `100.64.0.5:58500`).
3. Mouser will establish a direct unicast handshake and add the device to your list.

### Pairing an Android Phone Companion
1. Open the Mouser Android companion app on your phone.
2. The phone will auto-discover your desktop.
3. Enter the desktop PIN on your phone, or scan the pairing QR code.

---

## 4. Seamless Mouse & Keyboard Sharing (KVM)

### Crossing Between Screens
- Push your mouse cursor smoothly against the screen boundary bordering your target computer (for example, off the right edge of your monitor).
- The cursor seamlessly transitions onto the neighboring computer's desktop.
- Keystrokes, mouse buttons, and scroll wheel events are instantly routed to the active computer with sub-millisecond latency.

### Returning Control
- Move the mouse cursor back through the shared boundary edge onto your primary screen.
- **Emergency Return Hotkey:** Press `Ctrl + Alt + Esc` (or `Scroll Lock`) at any time to instantly snap control back to your local computer.
- **Pin Cursor:** Press `Win + Scroll Lock` while on your local computer to lock the cursor to your screen and prevent unintended edge crossings (ideal for full-screen gaming).

---

## 5. Smart Clipboard History & Cross-Device Sync

### Clipboard History
- Press `Win + Alt + V` anywhere in Windows to open the fast, floating **Quick Paste** palette.
- Navigate to the **Clipboard** tab to browse your full searchable history.
- Filter by data type (Text, Code, Links, Images, Files), device origin, or time range.
- Star favorite items or organize them with custom `#tags`.

### Automatic Cross-Device Clipboard Sync
- When enabled in Settings, text and image copies are instantly and securely synced to all paired computers.
- Synced payloads are authenticated and encrypted end-to-end using AES-256-GCM.

### Sensitive Data Redaction
Mouser automatically detects and suppresses sensitive data from being synced across the network or stored in plaintext:
- Private SSH / PGP keys
- API tokens (GitHub PATs, AWS Access Keys, OpenAI keys, Stripe API keys)
- Credit card numbers (validated via Luhn algorithm)
- Passwords copied from popular password managers (Bitwarden, 1Password, KeePass, etc.)

---

## 6. App Lock & Security

You can protect your clipboard history and prevent unauthorized network access by configuring an **App Lock**:
- Navigate to **Settings → Security** and click **Set up Master Passphrase**.
- A 32-character Base32 **Recovery Key** will be generated and shown once. Print or store this key securely.
- When locked (`Ctrl + L` or via system tray), Mouser:
  - Shuts down network sockets and stops KVM sharing.
  - Clears all decrypted clipboard data from memory.
  - Locks the SQLite database with SQLCipher encryption.
  - Rejects quick-paste and window access until unlocked.

---

## 7. System Tray & Background Operation

- **Minimize to Tray:** Closing the main window or minimizing sends Mouser to the notification area.
- **Tray Menu Actions:**
  - Double-click or select *Open Mouser* to restore the main window.
  - *Pause Sharing:* Temporarily suspends KVM mouse and keyboard redirection.
  - *Pause Clipboard:* Temporarily disables clipboard capture and sync.
  - *Lock Now:* Instantly locks Mouser if App Lock is configured.
  - *Quit:* Exits the application cleanly.

---

## 8. Troubleshooting & FAQ

### 1. The cursor does not type or click inside Task Manager or Command Prompt
**Cause:** Windows User Interface Privilege Isolation (UIPI) prohibits standard applications from sending synthetic input events to elevated processes (running as Administrator).  
**Solution:** Right-click `Mouser.exe` and select **Run as administrator**, or enable the elevated startup option in Mouser settings.

### 2. Computers cannot discover each other
**Check Firewall:** Ensure Windows Defender Firewall allows incoming and outgoing traffic for `Mouser.exe` on UDP ports 58500–58502 and TCP port 58503.  
**Check Guest Wi-Fi Isolation:** Some Wi-Fi routers enable "AP Isolation" or "Client Isolation" on guest networks, preventing devices from talking to each other. Connect both devices to the main Wi-Fi or wired network.

### 3. Cursor crossing is too sensitive or accidentally triggers in games
**Solution:** In **Settings → Sharing**, select the **Careful** edge resistance preset (requiring a deliberate push against the boundary to cross), or press `Win + Scroll Lock` to pin your cursor to the current screen.
