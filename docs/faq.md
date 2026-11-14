# Frequently Asked Questions (FAQ)

### 1. How is Mouser different from Synergy, Barrier, or Mouse without Borders?
- **Zero Configuration & Instant Discovery:** Mouser automatically finds paired computers and phones over local UDP broadcast without requiring dedicated server/client setup or fixed IP addresses.
- **Modern Encryption:** Traffic is authenticated and encrypted end-to-end using AES-256-GCM. Mouse motion packets use a 51-byte compact authenticated datagram with sliding-window anti-replay protection.
- **Smart Clipboard:** Mouser includes an SQLite/SQLCipher full-text search clipboard manager, image and rich-text sync, snippet expansion, and automatic secret filtering.
- **Native Avalonia UI:** Mouser features a clean, responsive interface supporting both Light and Dark modes.

---

### 2. Why does Mouser use UDP instead of TCP for mouse movement?
Mouse movement requires real-time, low-latency responsiveness. High-polling-rate mice generate up to 1,000 movement packets per second. If a single packet is delayed on a TCP stream, Head-of-Line (HoL) blocking would stall all subsequent packets, causing visible stutter and lag. UDP delivers sub-millisecond motion updates without buffering delays.

---

### 3. Why does synthetic input not work in Task Manager, Registry Editor, or Administrator Command Prompts?
Windows enforces **User Interface Privilege Isolation (UIPI)**. Non-elevated applications cannot inject window messages or synthetic input events into windows belonging to elevated (Administrator) processes.
To control elevated windows across computers:
1. Run Mouser as Administrator, OR
2. Install the upcoming `uiAccess` signed package.

---

### 4. What happens if I forget my App Lock passphrase?
There is **no backdoor**. If you forget your master passphrase, you can unlock Mouser using the 32-character Base32 **Recovery Key** generated when you first configured the lock. If both the passphrase and the recovery key are lost, the encrypted database (`clipboard.db`) cannot be decrypted. You can reset Mouser by deleting the configuration file in `%APPDATA%\Mouser\settings.json`.

---

### 5. How does Mouser prevent leaking passwords or API keys?
Mouser inspects copied text before persisting or syncing it. If text matches known secret patterns (e.g. AWS access keys, GitHub tokens, OpenAI keys, private keys, credit cards) or originates from known password manager processes (Bitwarden, 1Password, KeePassXC, etc.), it is marked as sensitive:
- Sensitive items are never synced across the network.
- They are omitted from persistent history or masked according to your preferences in Settings.

---

### 6. Can someone on my network eavesdrop on my keystrokes or clipboard?
No. All peer-to-peer communication requires mutual pairing. After pairing:
- Keystrokes and mouse events are encrypted using AES-256-GCM with per-session derived keys.
- Clipboard transfers and file streaming are sent over authenticated TCP frames with SHA-256 checksum verification.
- Replayed packets are discarded using a 64-slot sliding replay window.

---

### 7. Does Mouser send any telemetry or data to the internet?
No. Mouser is 100% offline and peer-to-peer. It operates strictly within your local area network (LAN) or private virtual network (such as Tailscale/WireGuard). No telemetry, analytics, or clipboard data is ever sent to external cloud servers.
