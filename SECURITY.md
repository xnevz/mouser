# Security Policy

## Supported Versions

| Version | Supported          | Protocol Version | Notes                                        |
| ------- | ------------------ | ---------------- | -------------------------------------------- |
| 0.2.x   | :white_check_mark: | Protocol v2      | Current release; full ECDH + AES-256-GCM     |
| 0.1.x   | :warning:          | Protocol v1      | Deprecated; optional `AllowOlderDevices` flag |

---

## Reporting a Vulnerability

We take the security and privacy of Mouser users seriously. If you discover a vulnerability, please report it responsibly rather than opening a public GitHub issue.

- **Security Contact:** <!-- TODO: contact: security@mouser.local (or specify security team contact address) --> `security@mouser.local`
- **Response Timeline:**
  - **Acknowledgment:** Within 48 hours of report receipt.
  - **Triage & Assessment:** Within 5 business days.
  - **Remediation & Advisory:** Fix released within 30 days depending on severity.

Please provide a detailed description of the vulnerability, including:
1. Steps to reproduce or proof-of-concept exploit.
2. Affected version(s) and operating system(s).
3. Assessment of impact (e.g. denial of service, unauthorized input, data leakage).

---

## Security Model Overview

Mouser is designed for seamless, multi-device mouse, keyboard, clipboard, and file sharing across local networks. Its security architecture is built on the following pillars:

1. **Authenticated Encryption (AEAD):**
   - All network traffic (UDP input datagrams and TCP clipboard/file streams) is encrypted using **AES-256-GCM**.
   - Authenticated tags verify payload integrity. Modified, tampered, or corrupted datagrams fail decryption and are dropped immediately.

2. **Zero-Trust Device Pairing:**
   - Devices pair using ephemeral Elliptic-Curve Diffie-Hellman (**ECDH**) over NIST curve **P-256**.
   - A 6-digit Short Authentication String (**SAS**) confirmation code derived from the shared secret and public keys must be confirmed out-of-band by the user on both screens.
   - Passive sniffers and active Man-in-the-Middle (MITM) adversaries cannot forge or tamper with key exchange without detection.
   - Pairing is restricted to an explicit 2-minute pairing window initiated by the user, with rate-limiting and lockout protections.

3. **Replay & Session Protection:**
   - Outbound datagrams and frames utilize directional session keys (`k_ab`, `k_ba`) and monotonically increasing 64-bit sequence counters.
   - Incoming sequence trackers reject replayed or significantly out-of-order packets.

4. **Keys at Rest Protection:**
   - On Windows, paired device secret keys are protected at rest using the Windows **Data Protection API (DPAPI)** (`CryptProtectData` / `CryptUnprotectData`) scoped to `CurrentUser`.
   - Keys are decrypted only in memory when the user is logged in.

5. **Filesystem & Traversal Hardening:**
   - Incoming file names are strictly sanitized: path separators, directory traversal sequences (`..`), alternate data streams (`:evil`), Windows reserved device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`), and trailing dots or spaces are stripped or safely prefixed.
   - Names are clamped to 150 characters and unique numbering is applied to prevent file overwrites.
   - On Windows, received executable file types receive the Mark-of-the-Web (`Zone.Identifier` ADS with `ZoneId=3`).
   - Transfer size caps and blocked extensions are enforced before data is streamed to disk.

6. **Spoofing & Fuzz Resiliency:**
   - Unauthenticated discovery beacons cannot hijack links to paired peers; link endpoints only switch after cryptographic authentication.
   - Deserialization logic and network packet readers are bounded by maximum frame sizes and fuzz-tested against random and adversarial payloads to prevent crashes or infinite hangs.

---

## Honest Security Limits

Mouser runs as a user-space application and adheres to realistic boundaries:
- **Same-User Process Boundary:** Malware running under the same operating system user account has access to user memory, process handles, and synthetic OS input APIs. Mouser cannot protect against compromised local accounts beyond its AppLock UI screen.
- **Root / Administrator Adversary:** Mouser does not defend against local root/SYSTEM users or kernel rootkits, which have unrestricted access to hardware drivers, memory, and DPAPI master keys.
- **Traffic Analysis:** While packet contents are fully encrypted, a passive network observer can observe packet timing and bandwidth, revealing whether mouse movement or high-volume file transfers are taking place.
