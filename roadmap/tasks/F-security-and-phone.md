# Track F (F1–F10 security rewrite) and Track G (G1–G8 Android phone companion v2)

Read [../AGENT_GUIDE.md](../AGENT_GUIDE.md) first.
Feature lists: [../phase-7-feature-parity-gaps.md](../phase-7-feature-parity-gaps.md) Phases 10 and 11.

**Security cards (F1–F10) change the wire protocol.** Rules for the whole security part:
1. **Do F1 first and get the user's written OK on the design note before any code.** Crypto mistakes are silent.
2. Use only the .NET base library (`ECDiffieHellman`, `HKDF`, `AesGcm`, `RandomNumberGenerator`, `CryptographicOperations`). No new packages. Never invent your own primitives.
3. Compare secrets with `CryptographicOperations.FixedTimeEquals`. Never log keys, PINs, passphrases or codes.
4. Every new parser gets a "garbage in" test (random bytes, truncated, oversized) that must not throw anything except the documented exception.
5. Old and new builds must fail with a **clear message** ("Update Mouser on the other computer"), never silently.

---

## F1: Protocol v2 design note (no code)

- **Size:** S · **Output:** create `docs/protocol-v2.md` (new folder `docs/`). **Stop after writing it and ask the user to approve.**

**Write these sections, using the decisions below as the starting point:**

1. **Goals:** mutual authentication, confidentiality and integrity of *everything* (input, clipboard, files), replay protection, no trust by IP address, no PIN stored.
2. **Pairing (P-256 ECDH + short code).**
   1. A connects over TCP to B's control port and sends `PairRequest{ protocolVersion=2, deviceId, deviceName, ecdhPublicKey(65 bytes, uncompressed) }`.
   2. B replies `PairResponse{ ..., ecdhPublicKey }` **only if** the user enabled "Allow new devices" (a 2-minute window opened from Devices → Add; otherwise reply `PairRefused`).
   3. Both compute `shared = ECDH(own private, peer public)`; `okm = HKDF-SHA256(shared, salt = sorted(deviceIdA, deviceIdB), info = "mouser-pair-v2", 64 bytes)`; `pairKey = okm[0..32]`, `confirmSeed = okm[32..64]`.
   4. **Confirmation code** = `(BinaryPrimitives.ReadUInt32BigEndian(confirmSeed) % 1_000_000)` shown as `123 456` on **both** screens. The users compare them; B presses **Allow**, A presses **Codes match**. A man in the middle gets a 1 in 1,000,000 chance per attempt; limit to 3 attempts per minute per source address.
   5. Each side stores `{ peerDeviceId, name, pairKey }` (key protected with DPAPI on Windows, F6). Nothing else.
3. **Session keys.** At start each side picks `sessionId` = current Unix time in milliseconds (`ulong`). Keys: `k_ab = HKDF(pairKey, salt = sessionId, info = "mouser-a2b")`, `k_ba` likewise (A = the device with the smaller deviceId when sorted ordinally). Two directions → no nonce reuse.
4. **Input datagram (UDP).** `[version=2 : 1][senderIdHash : 4][sessionId : 8][counter : 8][ciphertext : 14][tag : 16]` = 51 bytes. Nonce = 4 zero bytes ‖ counter (AES-GCM, 12 bytes). Plaintext = today's 14-byte `InputMotionPacket`. Receiver: look up the peer by `senderIdHash`, derive the session key, check `counter` against a 64-wide sliding window and `sessionId` ≥ the last seen for that peer (persist it), then decrypt.
5. **TCP frames (clip, files, hello, lock, power).** Keep the `MCH1` framing but encrypt with `k_ab`/`k_ba` and a per-frame counter nonce instead of the PIN-derived key. Frame header gets `version = 2`.
6. **Phones** use the same pairing (the phone is a device with a key pair) and the same datagram format; the phone shows the code, the PC asks to Allow.
7. **Migration:** v2 builds still accept v1 for **one release** behind a setting "Allow older devices" (default off after first run) so users can re-pair; v1 paths are then deleted (F8).
8. **Threat model table:** attacker on LAN (passive, active), stolen laptop, malicious paired device, malware as the same user; what each protects and what it does not (see phase-7a §7 for the "honest limits" style).
9. **Open questions** for the user.

**Done when:** `docs/protocol-v2.md` exists, covers the 9 sections, and the user replied "approved" (record the date in ROADMAP.md).

---

## F2: Pairing and session crypto in Core (pure, tested)

- **Needs:** F1 approved · **Size:** M · **Files:** create `src/Mouser.Core/Security/PairingCrypto.cs`, `src/Mouser.Core/Security/SessionCrypto.cs`, `tests/Mouser.Tests/PairingCryptoTests.cs`

**API**
```csharp
public sealed class PairingHandshake : IDisposable
{
    public static PairingHandshake Start();                 // creates a P-256 key pair
    public byte[] PublicKey { get; }                        // 65 bytes (ExportSubjectPublicKeyInfo is NOT used: raw 0x04||X||Y)
    public PairingResult Complete(byte[] peerPublicKey, string ownDeviceId, string peerDeviceId); // throws CryptographicException on a bad key
}
public sealed record PairingResult(byte[] PairKey, string ConfirmationCode /* "123 456" */);

public sealed class SessionCrypto
{
    public SessionCrypto(byte[] pairKey, bool isInitiator, ulong sessionId);
    public byte[] Seal(ReadOnlySpan<byte> plaintext, out ulong counter);      // for the outgoing direction
    public bool TryOpen(ulong counter, ReadOnlySpan<byte> cipherAndTag, out byte[] plaintext); // replay window inside
}
```
Implementation notes: `ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256)`; import the peer key with `ECDiffieHellman.Create(new ECParameters{ Curve = nistP256, Q = new ECPoint{ X = …, Y = … } })`; `DeriveRawSecretAgreement`; `HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, outputLength, salt, info)`; AES-256-GCM via `AesGcm(key, 16)`.
Replay window: a `ulong` bitmap of 64 + `highest`; reject counter ≤ highest − 64, reject seen.

**Tests (≥ 12):** both sides derive the **same** `PairKey` and code; code format `ddd ddd`; different peers → different codes; tampered ciphertext fails; replayed counter fails; out-of-order within 64 succeeds once; counter 0 rejected? (decide: counters start at 1); two directions use different keys (a packet sealed by A cannot be opened as if sealed by B); wrong public key length throws; random garbage for `TryOpen` never throws.

**Done when:** build; tests pass (+12).

---

## F3: Pairing over TCP + "Allow this device?" prompt

- **Needs:** F2, B9 · **Size:** L · **Files:** `PeerTransferService.cs` (new frame types `PairRequest=8`, `PairResponse=9`, `PairRefused=10` that are **not** encrypted with a pair key: they carry only public data), `MouserHost.cs`, `DevicesViewModel.cs`, `DevicesView.axaml`, `SettingsStore.cs` (`PeerSettings.PairKeyProtected` base64), tests

**Steps**
1. `MouserHost.AllowNewDevices(TimeSpan window)` opens a window (default 2 min, shown as a countdown in the Devices page: "Waiting for a new device… 1:42  [Cancel]"). Outside the window all `PairRequest`s get `PairRefused`.
2. Incoming `PairRequest` inside the window: raise `PairingRequested(deviceName, code, Action allow, Action deny)`; the UI shows a modal card: "**Laptop-Pro** wants to connect. Code **123 456**. [Allow] [Deny]" (same card on the initiator: "Is the code on the other screen **123 456**? [Codes match] [Cancel]").
3. On both confirmations store the key (F6) and mark the peer paired; the existing `PeerSettings.Pin` stays for v1 until F8.
4. Rate limit: 3 requests per minute per source IP; 5 minutes lockout after 3 denials.
5. Tests: two `PeerTransferService` instances on loopback complete a handshake; wrong confirmation → no key stored; request outside the window → `PairRefused`; rate limit.

**Done when:** build; tests pass (+4); manual with two instances.

---

## F4: Encrypted input channel

- **Needs:** F3 · **Size:** XL · **Risk:** HIGH · **Files:** `src/Mouser.Core/Networking/SecureInputPacket.cs`, `UdpInputReceiver.cs`, `PeerLink.cs`, `Services/InputInjectionService.cs`, `PairingGate.cs`, tests (`UdpNetworkingTests`, `InputInjectionServiceTests`, `KvmControllerTests`)

**Steps**
1. `SecureInputPacket`: `static byte[] Write(byte[] senderIdHash, ulong sessionId, ulong counter, byte[] cipherAndTag)` and `TryRead(...)`; size constant 51; version byte 2. Garbage tests.
2. `PeerLink.Send` (used by `KvmController`): when the peer has a pair key, seal the 14-byte packet with `SessionCrypto` and send the secure datagram; otherwise (v1 peer, allowed only if "Allow older devices") send as today.
3. `InputInjectionService`: receive path first checks byte 0: `2` → secure; look up the peer by hash; `TryOpen`; on success hand the 14-byte plaintext to the **existing** handling code. v1 packets: accepted only when `AllowOlderDevices`.
4. **Remove IP trust for v2:** a secure packet from an unknown hash is dropped silently (no reply, no log spam; count in `Stats`).
5. Existing tests that pair with a PIN keep using v1 mode (set `AllowOlderDevices = true` in those fixtures: that is the only allowed change to them).
6. New tests: end-to-end KVM move over secure packets (copy `TwoComputerTests` fixture); spoofed source IP with a bad tag is ignored; replay of a captured datagram is ignored; session restart (new `sessionId`) works; old `sessionId` is rejected.

**Done when:** build; tests pass; manual: `netsh trace`/Wireshark shows no readable key codes.

---

## F5: Android crypto and pairing

- **Needs:** F2, F3 · **Size:** XL · **Files:** `android/app/src/main/java/com/mouser/companion/network/` (new `PairingCrypto.kt`, `SessionCrypto.kt`; edit `MouserConnection.kt`, `Protocol.kt`), `MainActivity.kt` (code dialog), JVM tests

**Steps**
1. Kotlin equivalents using `java.security.KeyPairGenerator("EC")` (`secp256r1`), `javax.crypto.KeyAgreement("ECDH")`, HKDF written with `Mac("HmacSHA256")` (RFC 5869: 20 lines), `Cipher("AES/GCM/NoPadding")` with `GCMParameterSpec(128, nonce)`.
2. **Interop test vectors:** generate fixed vectors from the C# tests (deterministic inputs: fixed private keys, ids, session id, counter, plaintext) and put the expected hex into `PairingCryptoTest.kt`; the Kotlin implementation must match byte for byte. The C# side must have a test that prints/asserts the same vectors (add `PairingCryptoTests.KnownAnswer`).
3. Pair flow: phone opens TCP to the host control port (`CPort` in the beacon: parse it in `BeaconParser` into `DiscoveredHost.controlPort`), shows the code, "Codes match" button; store the key in `EncryptedSharedPreferences` is a package: instead use **Android Keystore** to wrap it (`KeyGenParameterSpec`, AES/GCM) and store the wrapped blob in the existing `"mouser"` prefs.
4. Replace the 14-byte plaintext datagrams by secure datagrams (same layout as F4; the phone is device `A`/`B` by deviceId ordering). Keep v1 behind a hidden toggle until F8.

**Done when:** `.\gradlew.bat testDebugUnitTest assembleDebug` pass; interop test vectors match; manual: phone pairs by code and moves the cursor.

---

## F6: Keys at rest (DPAPI) and no more stored PINs

- **Needs:** F3 · **Size:** M · **Files:** create `src/Mouser.UI/Services/SecretStore.cs`, edit `SettingsStore.cs`, `MouserHost.cs`, tests

**Steps**
1. `SecretStore.Protect(byte[]) → string` / `Unprotect(string) → byte[]?` using `ProtectedData` (`System.Security.Cryptography.ProtectedData`, in the shared framework on Windows; call behind `OperatingSystem.IsWindows()`; on other OSes use a clearly named fallback that stores `base64` and logs a one-time warning: `// shortcut: no OS key store off Windows, upgrade with libsecret/Keychain`).
2. Move `PeerSettings.PairKeyProtected` through it. `Unprotect` failure (different Windows user) → treat as unpaired and show "Pair again".
3. Stop persisting `PairingPin` and `PeerSettings.Pin` once F8 removes v1; until then keep them.
4. Test: round trip on Windows (`[WindowsFact]`), tampered input → null.

**Done when:** build; tests pass.

---

## F7: Incoming-file safety

- **Needs:** B9 · **Size:** M · **Files:** `PeerTransferService.cs` (receive path), `MouserHost.cs`, `SettingsStore.cs` (`PeerSettings.AutoAcceptFiles`, `AppSettings.MaxIncomingFileMB = 2048`, `BlockedExtensions`), Files UI, tests in `PeerTransferTests.cs`

**Rules**
1. Receiver decides **before** accepting data, from the `FileStart` header: size limit, blocked extensions (default `.exe .msi .bat .cmd .ps1 .vbs .js .scr .lnk .jar .com .dll`), and **ask the user** unless the peer has `AutoAcceptFiles` (default off). Add a callback `Func<IncomingFile, Task<bool>> AcceptFile` to the service; a refusal answers the sender with ack byte `2` (sender shows "The other computer declined the file").
2. File names are sanitised: strip directory parts, invalid characters, reserved Windows names (`CON`, `NUL`…), trailing dots/spaces, and limit to 150 characters; never overwrite: `name (1).ext`. **Test path traversal** (`..\..\x`, absolute paths, `C:\x`, alternate data streams `a.txt:evil`).
3. Received files go to `Downloads\Mouser`; after saving, `Zone.Identifier` (mark-of-the-web) is written for `.exe`-like types by creating the ADS `file:Zone.Identifier` with `[ZoneTransfer]\r\nZoneId=3` (Windows, ignore errors).
4. UI: toast with **Accept / Decline** buttons is too complex for the toast window: use a small modal card in the main window ("Laptop-Pro wants to send **report.pdf** (3.2 MB) [Accept] [Decline] [Always accept from this device]"); if the window is hidden, show it first.

**Tests:** sanitiser (≥ 10 cases), size limit, blocked extension → refusal ack, accept path unchanged.
**Done when:** build; tests pass (+14).

---

## F8: Remove v1 trust

- **Needs:** F4, F5, F6 and one release cycle · **Size:** M · **Files:** `PairingGate.cs`, `InputInjectionService.cs`, `UdpInputReceiver.cs`, `InputMotionPacket.cs` (`TypePair*`), `MouserHost.cs`, Android `MouserConnection.kt`, tests

Delete the PIN path (`TypePair`, `TypePairAck`, `PairingGate` IP trust, PBKDF2-from-PIN keys, `PeerSettings.Pin`, `AppSettings.PairingPin`, the "New PIN" UI). Update or delete **only** the tests that exercise the removed code (list them in the commit message). Update README "Known limits".
**Done when:** build; remaining tests pass; grep for `TryPair|PairingPin|DeriveKey(` finds nothing.

---

## F9: Hardening tests and the security doc

- **Needs:** F4 · **Size:** M · **Files:** `tests/Mouser.Tests/FuzzTests.cs`, `SECURITY.md`, `docs/threat-model.md`

1. Fuzz-style tests (deterministic `new Random(1234)`, 2000 iterations each): `InputMotionPacket.TryRead`, `SecureInputPacket.TryRead`, `PeerDiscoveryService.TryParseBeaconPayload`, the TCP frame reader (feed random bytes through a `MemoryStream`), `ClipboardPayload.Deserialize` (must throw only `EndOfStreamException`/`InvalidDataException`/`FormatException`), `SearchQuery.Parse` (never throws). The assertion is "no unexpected exception type and no hang (> 1 s)".
2. Beacon spoofing: a beacon claiming a known `deviceId` from a different IP must not hijack the saved peer's link until the peer authenticates (test in `PeerDiscoveryTests`/`MouserHost`: `EnsureLink` currently follows the new endpoint: require a successful secure datagram before switching).
3. `SECURITY.md`: supported versions, how to report (ask the user for the address; put a placeholder `TODO: contact` and flag it in ROADMAP Blocked if none given), a plain summary of the model and its limits.

**Done when:** build; tests pass.

---

## F10: Network exposure options

- **Needs:** B11 · **Size:** S · **Files:** `SettingsStore.cs`, `PeerDiscoveryService.cs`, `PeerTransferService.cs`, `UdpInputReceiver.cs`, Settings → Sharing → Advanced

Options: **"Only accept connections from my local network"** (default on): drop packets whose source is not private (`10/8`, `172.16/12`, `192.168/16`, `169.254/16`, `100.64/10` for Tailscale, `fd00::/8`, loopback); and **Preferred network adapter** (combo from `NetworkInfo`), **separate UDP and TCP ports** with a **Find free port** button, **Use IPv6** toggle (stretch). Pure function `NetworkInfo.IsPrivate(IPAddress)` with tests.
**Done when:** build; tests pass (+6).

---

# Phone companion v2 (Android)

Common rules: every phone feature that needs the PC to **receive text, clipboard or files** depends on F5 (secure TCP channel) except G1 (it reuses the UDP datagram path with `TypeChar` from E12). Add JVM unit tests for pure logic; UI is checked manually on the emulator (`scripts/run-android-debug.sh` documents the AVD).

## G1: Typing and dictation

- **Needs:** E12 · **Size:** M · **Files:** `android/.../model/InputMotionPacket.kt`, `network/MouserConnection.kt`, `MouserViewModel.kt`, `ui/components/` (new `TypeSurface.kt`), `MainActivity.kt` (new tab `AppNavigationTab.TYPE`), tests `InputMotionPacketTest.kt`
1. Add `TYPE_CHAR = 0x0A` to the Kotlin packet model, mirroring E12 (dx = UTF-16 unit, flag bit 0 = down; send down then up for each unit).
2. `TypeSurface`: a single multi-line `OutlinedTextField` + **Send** button + **Enter**, **Backspace**, arrow keys (use existing key packet with VK codes `0x0D`, `0x08`, `0x25..0x28`). Live mode toggle: every change in the field sends the diff immediately (append → chars, delete → Backspace presses). Voice dictation works through the system keyboard's microphone, nothing extra.
3. Pure function `diffToKeys(old: String, new: String): List<Action>` (common prefix, then Backspaces, then chars) with tests (append, delete, replace middle, emoji surrogate pairs).
**Done when:** `testDebugUnitTest assembleDebug` pass; manual: typed text appears in Notepad on the PC.

## G2: Clipboard bridge (foreground, no accessibility service)

- **Needs:** F5, D4 · **Size:** L · **Files:** Android `network/` (TCP client for `Clip` frames), `ui/components/ConnectionBar.kt` (two buttons), desktop `MouserHost.cs` (accept clips from paired phones)
- v1 scope: two buttons in the app: **Send phone clipboard to PC** (reads `ClipboardManager` while the app is in the foreground, which Android 10+ allows) and **Get PC clipboard** (host answers with the latest clip). The phone appears as a normal device in the Delivery list. **No background reading** (needs an IME or accessibility service; a separate spike, with Play Store policy review).
- Desktop: `PeerTransferService` already handles `Clip` frames from a known sender id: register phones' device ids as paired senders (F3).

## G3: Share-sheet target

- **Needs:** G2 · **Size:** M · **Files:** `AndroidManifest.xml`, new `ShareReceiverActivity.kt`
- Intent filters `ACTION_SEND` for `text/plain`, `image/*`, `*/*`; the activity shows "Send to: [paired computers]" and sends text as a clip, files through the file frames (reuse `FileStart/Chunk/End`). Files over 2 GB refused with a message. Receive-side limits come from F7.

## G4: Macro pad with custom buttons

- **Needs:** none · **Size:** M · **Files:** `ui/components/MediaMacrosView.kt` (split), new `MacroPadView.kt`, `MacroStore.kt` (JSON in prefs), tests
- Model `Macro(label, steps: List<Step>)`, `Step = Key(vk) | Combo(vks) | Text(String) | Delay(ms)`. Grid of buttons, long-press → edit dialog (label, a step list with "add key/combo/text/delay"), import/export as JSON (share intent). Defaults: Copy, Paste, Undo, Redo, Screenshot (`Win+Shift+S`), Task view (`Win+Tab`), Mute.
- Pure `MacroRunner` that turns steps into timed key packets, with tests (combo = down in order, up in reverse).

## G5: Gestures and settings

- **Needs:** none · **Size:** M · **Files:** `TrackpadSurface.kt`, `MouserViewModel.kt`, a new Settings sheet
- Add: pinch → Ctrl+wheel (zoom), three-finger swipe left/right → `Alt+Left/Right`? **No:** configurable: default left/right = switch virtual desktop (`Ctrl+Win+Left/Right`), up = Task view (`Win+Tab`), down = show desktop (`Win+D`); drag lock; tap-and-hold options. Settings sheet (three sliders: pointer speed, scroll speed, air-mouse sensitivity; toggles: reverse scroll, haptics, tap to click). Persist in prefs. Pure gesture classifier tests (feed pointer sequences, expect gestures).

## G6: Presentation tools (laser pointer and slides)

- **Needs:** E7 (overlay window pattern) · **Size:** L · **Files:** desktop `Views/LaserOverlayWindow`, `InputInjectionService.cs`, `InputMotionPacket.cs` (`TypeLaser = 0x0B`), Android `AirMouseSurface.kt`, new `SlideRemoteView.kt`
- `TypeLaser` (flag 1 = on): the desktop shows a click-through red dot window that follows the cursor while on. Air-mouse "HOLD TO POINT" turns it on. Slide remote tab: big **Next/Previous** buttons (Right/Left keys), **Start (F5)**, **End (Esc)**, a timer, optional notes text typed on the phone.
- Tests: packet round trip; desktop VM toggles overlay on/off.

## G7: Connection quality and service

- **Needs:** none · **Size:** M · **Files:** `MouserConnection.kt`, `ConnectionBar.kt`, a new `ConnectionService.kt` (foreground service), `AndroidManifest.xml`
- Ping/pong packets already exist (`TypePing/TypePong`): measure RTT every 2 s; show "12 ms" and a quality dot in the bar; show battery level of the **phone** (`BatteryManager`) in the same bar. Foreground service with a persistent notification (**Disconnect** action) keeps the connection while the screen is off and the app is in the background; auto-reconnect with back-off 1/2/4/8 s after Wi-Fi changes (`ConnectivityManager.NetworkCallback`). Request `ignoreBatteryOptimizations` with an explanatory dialog (optional).
- Pure `Backoff` class + tests.

## G8: Phone polish and release

- **Needs:** A13 · **Size:** M
- Landscape and tablet layouts for the trackpad, content descriptions on every button (TalkBack), localisation scaffolding (`strings.xml` for all visible text; English first, then French and Arabic with RTL check), release signing config reading from `local.properties` (never commit keystores; add to `.gitignore`), Play Store checklist doc `docs/play-store.md`, an instrumented UI smoke test on the emulator. Keep `iOS` in the backlog file.
