# Mouser Protocol v2 Design Specification

**Document Status:** Approved (Task F1)  
**Authors:** Mouser Engineering Team  
**Scope:** Core cryptography, mutual device pairing, secure input datagrams, authenticated streaming channels, and companion phone integration.

---

## 1. Goals & Security Principles

Mouser Protocol v2 replaces legacy PIN-based symmetric authentication with modern, public-key-based mutual authentication and authenticated encryption with associated data (AEAD) across all transport channels.

### Core Objectives
1. **Mutual Authentication:** Both endpoints mathematically prove possession of established long-term private keys. No communication occurs without mutual cryptographic verification.
2. **End-to-End Confidentiality & Integrity:** All traffic—including high-frequency mouse/keyboard input, clipboard payloads, multi-gigabyte file transfers, and power/lock commands—is encrypted and tamper-evident using AES-256-GCM.
3. **Robust Replay & Reordering Protection:** Every datagram and frame carries a monotonically increasing sequence counter. UDP input packets use a 64-slot sliding window; TCP streams enforce strict monotonic nonces.
4. **Zero Trust in Network Identifiers:** IP addresses and MAC addresses are treated strictly as routing hints. Identity and authorization are anchored solely in public keys and unique cryptographic device identifiers.
5. **Safe Key Hygiene & Secrets at Rest:** No shared PINs, plaintext keys, or reversible secrets are persisted. Keys at rest are protected by platform-native hardware security (Windows DPAPI and Android Keystore).
6. **Low Latency & High Throughput:** Input packets are compact (51 bytes total) to fit standard Ethernet MTU without fragmentation, incurring < 0.1 ms cryptographic overhead per event on 1000 Hz polling rates.

---

## 2. Pairing: P-256 ECDH + Short Authentication String (SAS)

Pairing establishes a mutual trust root between two devices over an unauthenticated network using ephemeral Elliptic-Curve Diffie-Hellman (ECDH) over the NIST P-256 curve, verified out-of-band using a 6-digit Short Authentication String (SAS).

```
   Device A (Initiator)                                Device B (Responder)
          |                                                    |
          |  1. TCP Connect (Control Port)                     |
          |--------------------------------------------------->|
          |  2. PairRequest(v=2, DevIdA, NameA, PubKeyA)       |
          |--------------------------------------------------->|
          |                                                    | [Check: "Allow new devices"
          |                                                    |  pairing window active?]
          |  3. PairResponse(v=2, DevIdB, NameB, PubKeyB)      |
          |<---------------------------------------------------|
          |                                                    |
 [Compute shared secret:                               [Compute shared secret:
  ECDH(PrivA, PubKeyB)]                                 ECDH(PrivB, PubKeyA)]
 [KDF -> PairKey, ConfirmSeed]                         [KDF -> PairKey, ConfirmSeed]
 [Code: ConfirmSeed % 1,000,000]                       [Code: ConfirmSeed % 1,000,000]
          |                                                    |
          |  "123 456"                                         |  "123 456"
          |  User compares numbers on both physical displays   |
          |                                                    |
   [Click "Codes Match"]                                 [Click "Allow"]
          |                                                    |
          |  4. ConfirmCommit(HMAC(PairKey, "A"))              |
          |--------------------------------------------------->|
          |  5. ConfirmAcknowledge(HMAC(PairKey, "B"))         |
          |<---------------------------------------------------|
          |                                                    |
 [Save PairKey in DPAPI]                               [Save PairKey in DPAPI]
```

### Protocol Steps
1. **Initiation:** The initiator connects to the responder's control port and transmits `PairRequest`:
   - `ProtocolVersion`: `byte = 2`
   - `DeviceId`: UTF-8 string (UUID format, max 64 bytes)
   - `DeviceName`: UTF-8 human-readable label (max 64 bytes)
   - `EphemeralPublicKey`: 65 bytes uncompressed ANSI X9.62 point (`0x04 || X || Y`) on NIST P-256.
2. **Access Gatekeeping:**
   - Responder checks whether a pairing window is open (activated by user via "Add Computer" or "Allow new devices" for 2 minutes).
   - If closed: returns `PairRefused { Reason = "PairingDisabled" }` and terminates TCP connection.
   - If open: returns `PairResponse` containing Responder's `DeviceId`, `DeviceName`, and uncompressed 65-byte `EphemeralPublicKey`.
3. **Key Derivation (HKDF):**
   - Both compute ECDH shared secret: `Z = ECDH(PrivKey_local, PubKey_remote)`.
   - Compute salt by sorting device IDs ordinally:
     `salt = UTF8(Min(DeviceIdA, DeviceIdB)) || ":" || UTF8(Max(DeviceIdA, DeviceIdB))`.
   - Derive 64 bytes using HKDF-SHA256:
     `OKM = HKDF-SHA256(ikm = Z, salt = salt, info = "mouser-pair-v2", length = 64)`.
   - Partition:
     - `PairKey = OKM[0..31]` (32-byte long-term shared authentication key).
     - `ConfirmSeed = OKM[32..63]` (32 bytes used for out-of-band verification).
4. **Short Authentication String (SAS) Verification:**
   - Visual Confirmation Code:
     `Code = BinaryPrimitives.ReadUInt32BigEndian(ConfirmSeed[0..4]) % 1_000_000`.
   - Rendered on both screens as `XXX YYY` (e.g. `482 910`).
   - User verifies that the numbers displayed on both monitors are identical.
   - Responder clicks **Allow**; Initiator clicks **Codes match**.
   - An active MITM attacker unable to forge P-256 ECDH points has exactly a 1 in 1,000,000 chance of producing matching codes.
   - Rate limiting: Max 3 failed attempts per source IP per minute; exponential backoff locks pairing gate for 10 minutes on repeated failure.
5. **Secure Storage at Rest:**
   - Windows: `PairKey` is encrypted using DPAPI (`ProtectedData.Protect(pairKey, entropy, DataProtectionScope.CurrentUser)`) and saved in `peers.v2.json`.
   - Android: `PairKey` is stored in encrypted SharedPreferences backed by Android Keystore Master Key.
   - Zero PINs or plaintext keys exist on disk.

---

## 3. Directional Session Keys

To prevent key wear-out and eliminate any possibility of nonce reuse across full-duplex communications, endpoints derive distinct directional session keys upon connection establishment.

### Session Initialization
1. Each endpoint records `SessionId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` (64-bit unsigned integer).
2. Direction assignment:
   - Identify peer order: Let `Device_Low` be `Min(IdA, IdB)` and `Device_High` be `Max(IdA, IdB)`.
3. Directional Key Derivation (HKDF-SHA256):
   - `k_low_to_high = HKDF-SHA256(ikm = PairKey, salt = BitConverter.GetBytes(SessionId), info = "mouser-v2-low-to-high", length = 32)`
   - `k_high_to_low = HKDF-SHA256(ikm = PairKey, salt = BitConverter.GetBytes(SessionId), info = "mouser-v2-high-to-low", length = 32)`
4. Every transmission from `Device_Low` uses `k_low_to_high`; every transmission from `Device_High` uses `k_high_to_low`.

---

## 4. Input Datagram: Ultra-Low Latency UDP (51 Bytes)

High-performance mouse and keyboard sharing requires sending motion deltas up to 1000 times per second with negligible packet serialization and parsing overhead.

### Packet Wire Layout
```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
| Version (0x02)|             SenderIdHash (4 bytes)            |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                       SessionId (8 bytes)                     |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                      PacketCounter (8 bytes)                  |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                                                               |
|                 Encrypted Payload (14 bytes)                  |
|               (AES-256-GCM of InputMotionPacket)              |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                                                               |
|                    AEAD Tag (16 bytes)                        |
|                                                               |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
Total Size: Exactly 51 bytes (Version=1 + Hash=4 + Session=8 + Counter=8 + Payload=14 + Tag=16)
```

### Encryption Parameters
- **Cipher:** AES-256-GCM.
- **Key:** Outgoing directional session key (`k_ab` or `k_ba`).
- **Nonce (12 bytes):**
  - Bytes 0..3: `0x00, 0x00, 0x00, 0x00`
  - Bytes 4..11: 64-bit big-endian `PacketCounter` (increments by 1 per datagram).
- **Associated Authenticated Data (AAD):**
  - First 21 bytes of packet header: `Version || SenderIdHash || SessionId || PacketCounter`.
- **Plaintext (14 bytes):**
  - Standard 14-byte `InputMotionPacket` (Type, Dx, Dy, Buttons, Key, Flags).

### Receiver Processing & Sliding Window Verification
When a UDP datagram arrives:
1. Verify `Version == 2` and `Length == 51`.
2. Look up peer by `SenderIdHash` (FNV-1a 32-bit hash of `DeviceId`).
3. Check `SessionId`:
   - If `SessionId < LastSeenSessionId`, drop immediately (stale session replay).
   - If `SessionId > LastSeenSessionId`, reset sliding window and accept new session.
4. Check `PacketCounter` against 64-bit sliding window bitmap:
   - If `counter <= maxCounter`:
     - If `counter <= maxCounter - 64`: drop (too old).
     - If `bit (maxCounter - counter)` is already set: drop (duplicate/replay).
5. Decrypt and authenticate ciphertext with AES-256-GCM using directional session key and AAD.
6. If authentication passes:
   - Update sliding window bitmask and `maxCounter`.
   - Dispatch `InputMotionPacket` directly to OS injection sink.

---

## 5. TCP Frames (Clipboard, Files, Power, and Control)

Bulk data transfers (clipboard text/HTML/images, multi-gigabyte file chunks, device lock, and power sync) use persistent or on-demand TLS-grade TCP sessions with `MCH2` framing.

### Framing Layout
```
[Magic: 4 bytes "MCH2"]
[Version: 1 byte = 0x02]
[FrameType: 1 byte]
[SenderIdLength: 1 byte]
[SenderId: N bytes UTF-8]
[FrameCounter: 8 bytes big-endian]
[PayloadLength: 4 bytes big-endian]
[Ciphertext: PayloadLength - 16 bytes]
[AEAD Tag: 16 bytes]
```

### Frame Types
- `0x01` - `Clip`: Clipboard payload (JSON metadata + text + optional HTML/image).
- `0x02` - `FileStart`: Initiates file transfer (Header: ID, name, size, SHA-256).
- `0x03` - `FileChunk`: 64 KB streamed file segment.
- `0x04` - `FileEnd`: Finalizes transfer; receiver verifies SHA-256 hash before committing.
- `0x05` - `Hello`: Session presence ping and synchronization acknowledgment.
- `0x06` - `LockRequest`: Remote lock signal triggered by "Lock all devices".
- `0x07` - `PowerEvent`: Workstation lock, sleep, or display-off synchronization.

### Security Attributes
- Strict monotonic `FrameCounter` on each TCP stream. If an unexpected sequence number is encountered, connection is severed.
- Nonce = `0x00, 0x00, 0x00, 0x00 || FrameCounter (8 bytes)`.
- Associated Data includes frame header `Magic .. PayloadLength`.

---

## 6. Mobile Phone Companion (Android Integration)

The Android companion application operates under identical security guarantees without down-grading to insecure protocols:

1. **Native Key Storage:** Android generates a P-256 EC key pair inside Android Keystore (`KeyGenParameterSpec.Builder(..., PURPOSE_SIGN | PURPOSE_AGREE_KEY)`). Private keys never leave hardware-backed secure enclaves.
2. **Identical Pairing Experience:**
   - Phone displays 6-digit confirmation code.
   - Desktop displays identical code.
   - User approves on both devices.
3. **UDP Remote Control:**
   - Phone sends standard 51-byte AES-GCM datagrams for trackpad motions, taps, and keyboard typing (`TypeChar = 0x0A` from E12).
4. **Foreground Clipboard & File Sharing:**
   - "Send phone clipboard to PC" connects over TCP `MCH2` using directional session keys.
   - Android Share Sheet integration routes files through `FileStart/FileChunk/FileEnd` authenticated frames.

---

## 7. Migration & Backward Compatibility Strategy

Upgrading an active multi-device installation from Protocol v1 (PIN-based) to v2 requires an orderly migration:

| Release Phase | v1 Protocol Support | v2 Protocol Support | User Experience |
|:---|:---|:---|:---|
| **Release N (v2 Introduction)** | Enabled (Opt-in toggle: "Allow legacy v1 devices", default OFF for new installs) | Enabled (Default) | Legacy paired devices continue functioning with a warning badge: "Legacy Security (v1) - Re-pair to upgrade". |
| **Release N+1 (v1 Deprecation)** | Disabled by default; deprecation toast upon connection. | Enabled | User is guided by 1-click wizard to upgrade to v2. |
| **Release N+2 (v1 Removal - Task F8)** | Completely removed from codebase. | Exclusive | Wire parser rejects any non-`MCH2` frames. PIN storage removed. |

---

## 8. Comprehensive Threat Model & Honest Security Limits

Following Mouser's engineering transparency standards, the matrix below details realistic attack vectors, the protections provided by Protocol v2, and the physical limits of user-space software.

| Threat / Attacker Profile | What Protocol v2 Protects | What It Does NOT Protect (Honest Limits) |
|:---|:---|:---|
| **Passive LAN Eavesdropper** (Wi-Fi sniffer, hub, tap, monitor mode) | Complete protection. Attacker sees only opaque AES-256-GCM ciphertext. Keystrokes, clipboard text, passwords, and files are undecipherable. | Traffic analysis (timing and packet volume reveals active mouse motion or high-bandwidth file transfer). |
| **Active Man-in-the-Middle (MITM)** (ARP spoofing, DNS poisoning, rogue AP) | Full protection. ECDH with SAS confirmation code prevents key tampering. Modified datagrams or replayed packets fail AEAD tag or sequence checks and are instantly dropped. | Attacker can cause Denial of Service by dropping, delaying, or corrupting UDP/TCP packets. |
| **Rogue Device on Local Network** (Unknown computer running Mouser) | Full protection. Cannot inject input, cannot receive clipboard, cannot wake or lock. Discovered beacons from unpaired devices are ignored unless user explicitly pairs them. | Unpaired devices can observe discovery UDP beacons broadcast on the local subnet. |
| **Stolen / Lost Laptop** (Device at rest) | Protection via OS credential. Keys stored in DPAPI / Android Keystore cannot be read without local user logon or disk unlock passphrase. | If the OS user account has no password or auto-logs in without AppLock configured, attacker gains user session access. |
| **Malicious / Compromised Paired Computer** | Limits blast radius: Malicious peer can only send input and clipboard text. Path traversal in file transfers is strictly blocked (`UniquePath` + directory sanitization). | If a peer is fully compromised, the attacker can move the mouse or copy clipboard text onto the target computer. |
| **Local Malware Running as Same User** | Mouser AppLock (C1) locks clipboard history and prevents UI access without passphrase. | Malware running under the same OS user account can attach debuggers, read process memory, or inject input using synthetic OS APIs directly. |
| **Root / Administrator Adversary** | Mouser runs in user-space; cannot defend against local SYSTEM or kernel rootkits. | An elevated administrator has full control over Windows memory, drivers, and DPAPI master keys. |

---

## 9. Open Questions & Architectural Decisions

1. **Ephemeral vs Static ECDH for Pairing:**
   - *Decision:* Use ephemeral ECDH during the pairing handshake to ensure forward secrecy for the handshake itself. Once confirmed, the resulting `PairKey` is persisted to avoid requiring interactive SAS code entry upon every restart.
2. **Session Key Lifespan:**
   - *Decision:* Re-derive directional session keys (`k_ab`, `k_ba`) whenever Mouser restarts or after 24 hours of continuous connection, incrementing `SessionId`.
3. **UDP Packet Loss Handling:**
   - *Decision:* Input packets are stateless deltas or absolute coordinates. Dropped UDP packets are not retransmitted (mouse updates arrive at 1000 Hz, making stale retransmission undesirable). The 64-bit sliding window effortlessly accommodates out-of-order arrivals.
4. **Offline Pairing:**
   - *Decision:* Both devices must be reachable over the local network via TCP during the initial 2-minute pairing handshake. Once paired, connection is maintained automatically.
