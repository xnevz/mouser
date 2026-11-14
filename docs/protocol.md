# Mouser Network Protocol Specification

This document summarizes the wire protocol implemented across Mouser instances and companion apps.

---

## 1. Network Architecture Overview

Mouser uses two distinct communication channels:
1. **UDP Channel (Ports 58500–58502):**
   - **Discovery Broadcasts (58500):** Beacon broadcast advertising device existence, screen bounds, and capabilities.
   - **Real-Time Input Injection (58501–58502):** High-frequency, low-latency mouse moves, clicks, wheel scrolls, and keystrokes.
2. **TCP Channel (Port 58503 default):**
   - **Control & Pairing:** Mutual P-256 ECDH key exchange with Short Authentication String (SAS) confirmation.
   - **Data Transfer:** Clipboard synchronization, multi-gigabyte chunked file transfers, and remote lock commands.

---

## 2. Discovery Beacons (UDP Broadcast)

Mouser instances periodically broadcast a lightweight JSON payload to `255.255.255.255:58500` (and unicast targets configured in Settings):

```json
{
  "DeviceId": "mouser-a1b2c3d4",
  "Hostname": "Studio-Desktop",
  "ScreenX": 0,
  "ScreenY": 0,
  "ScreenWidth": 2560,
  "ScreenHeight": 1440,
  "MotionPort": 58501,
  "TransferPort": 58503,
  "Monitors": [
    { "X": 0, "Y": 0, "Width": 2560, "Height": 1440 }
  ]
}
```

---

## 3. Real-Time Input Injection (UDP)

### Protocol v2: Authenticated AEAD Datagram (51 Bytes)
All modern input traffic is sealed using AES-256-GCM.

```
+---------------+----------------+-------------------+--------------------+-------------------+
| Version (1B)  | DevIdHash (4B) | SessionId (8B)    | PacketCounter (8B) | Cipher+Tag (30B)  |
| 0x02          | FNV-1a (uint)  | uint64            | uint64 monotonic   | 14B plain + 16B tag|
+---------------+----------------+-------------------+--------------------+-------------------+
```

- **DevIdHash (4B):** Fast routing index based on sender's device ID.
- **SessionId (8B):** Unique cryptographic session identifier established during handshake.
- **PacketCounter (8B):** Monotonically increasing counter used as the AES-GCM IV/nonce (`nonce = [0x00 * 4] || counter`).
- **Cipher+Tag (30B):** 14-byte decrypted motion frame + 16-byte cryptographic authentication tag.
- **Sliding Replay Window:** The receiver tracks a 64-slot bitmask of recent counters to immediately reject replayed packets.

### Decrypted Motion Frame (14 Bytes)
```
+------------+---------------+---------------+---------------+--------------+--------------+
| Type (1B)  | Seq (2B)      | DeltaX (2B)   | DeltaY (2B)   | Buttons (1B) | Mods (1B)    | Extra (5B)   |
+------------+---------------+---------------+---------------+--------------+--------------+
```
- **Type:** `0x01` Move (relative), `0x02` Move (absolute), `0x03` Button, `0x04` Key, `0x05` Wheel, `0x06` Char.

---

## 4. Peer Transfer Channel (TCP)

All TCP communication uses length-prefixed, encrypted framing:

```
+------------+-----------+-------------------+--------------------------------+-------------------+
| Magic (4B) | Type (1B) | PayloadLength (4B)| Ciphertext (N bytes)           | Tag (16B)         |
| 'MOUS'     | FrameType | uint32 big-endian | AES-256-GCM encrypted payload | GCM Auth Tag      |
+------------+-----------+-------------------+--------------------------------+-------------------+
```

### Frame Types
- `0x01` — **Clip:** Synced clipboard payload (JSON containing text, HTML, image metadata, source info).
- `0x02` — **FileStart:** Header specifying TransferId, SafeFileName, TotalBytes, and SHA-256 hash.
- `0x03` — **FileChunk:** 64 KB binary stream slice.
- `0x04` — **FileEnd:** Signals end of stream, triggering SHA-256 verification.
- `0x05` — **Lock:** Signals remote screen lock command to paired devices.
- `0x10` — **PairRequest / PairResponse:** Mutual P-256 ECDH exchange.

---

## 5. Security & Verification

- **SHA-256 Verification:** Received files are verified against the announced SHA-256 digest before being finalized. Any interrupted or mismatched transfer triggers immediate deletion of partial files.
- **Safe File Names:** Path traversal sequences (`../`), reserved Windows filenames (`CON`, `PRN`, `AUX`), and NTFS alternate data streams (`file:stream`) are stripped and neutralized.
