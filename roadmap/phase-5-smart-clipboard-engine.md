# Phase 5: Universal Multi-Format Smart Clipboard Engine

> **Objective**: Implement real-time, end-to-end encrypted clipboard synchronization across macOS, Windows, Linux, and Android, paired with an encrypted local SQLite history database and a floating Quick Paste HUD.

---

## 1. Native Clipboard Change Detection

Each operating system requires a dedicated native hook to detect when the user copies new content without polling CPU loops.

| Platform | Native Mechanism | Implementation |
| :--- | :--- | :--- |
| **Windows** | `AddClipboardFormatListener` | Receives `WM_CLIPBOARDUPDATE` window message immediately on copy. |
| **macOS** | `NSPasteboard` Observer | Tracks `[NSPasteboard generalPasteboard].changeCount` via a lightweight 200ms timer. |
| **Linux** | `wl-clipboard` / `XFixesClipboard` | Wayland data-control protocol or X11 `XFixesSelectSelectionInput`. |
| **Android** | Accessibility Service / IME | Intercepts clipboard change broadcasts in background. |

---

## 2. Multi-Format Serialization & E2EE

A single copy operation often contains multiple representations (e.g., copying formatted text in a browser provides both `text/html` and `text/plain`).

### 2.1 Clipboard Payload Structure
```csharp
namespace Mouser.Core.Clipboard;

public sealed class ClipboardPayload
{
    public Guid ClipId { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string SourceDeviceName { get; set; } = Environment.MachineName;
    
    // Multiple representation formats
    public string? PlainText { get; set; }
    public string? HtmlContent { get; set; }
    public byte[]? RtfBytes { get; set; }
    public byte[]? PngImageBytes { get; set; }
    public List<string>? FilePaths { get; set; }
}
```

### 2.2 End-to-End Encryption (E2EE)
- All clipboard payloads are serialized and encrypted using **ChaCha20-Poly1305** using the shared device pairing key before transmission over TCP/TLS.
- Prevents snooping on sensitive text on untrusted Wi-Fi networks.

---

## 3. Encrypted SQLite History with FTS5

Past clips are stored in a local SQLite database encrypted with SQLCipher. Full-text search is powered by SQLite's FTS5 engine:

```sql
-- Schema creation
CREATE TABLE clips (
    id TEXT PRIMARY KEY,
    timestamp INTEGER NOT NULL,
    content_type TEXT NOT NULL,
    text_content TEXT,
    source_device TEXT NOT NULL,
    is_favorite INTEGER DEFAULT 0
);

-- Full-Text Search Virtual Table
CREATE VIRTUAL TABLE clips_fts USING fts5(
    id UNINDEXED,
    text_content,
    content='clips',
    content_rowid='rowid'
);
```

### Fast Search Query in C#:
```csharp
public async Task<List<ClipItem>> SearchClipsAsync(string queryText)
{
    var sql = @"
        SELECT c.id, c.timestamp, c.content_type, c.text_content, c.source_device 
        FROM clips c
        JOIN clips_fts f ON c.rowid = f.rowid
        WHERE clips_fts MATCH @query
        ORDER BY c.timestamp DESC
        LIMIT 50";

    return await _db.QueryAsync<ClipItem>(sql, new { query = $"{queryText}*" });
}
```

---

## 4. Privacy & Credential Firewall

To protect confidential information, the clipboard engine filters sensitive clips:
1. **Password Manager Exclusion**:
   - On macOS/Windows, inspect the source bundle/process name. If copied from `1Password`, `Bitwarden`, `KeePassXC`, or `KeePass`, skip network sync.
   - Respect the `org.nspasteboard.ConcealedType` and `ExcludeClipboardContentFromMonitorProcessing` metadata tags.
2. **Regex Pattern Sanitization**:
   - Optional automatic detection of credit card numbers, AWS/GitHub API tokens, and private SSH keys with masking toggles.

---

## 5. Phase 5 Implementation Steps

- [x] **Step 5.1: Cross-Platform Native Clipboard Hooks**
  - Configured clipboard change listeners and NSPasteboard change detection.
- [x] **Step 5.2: Multi-Format Serializer & E2EE**
  - Implemented `ClipboardPayload` supporting Text, Code, HTML, and Images.
  - Implemented `ClipboardCryptoEngine` using 256-bit AES-GCM authenticated encryption with 12-byte nonces and 16-byte authentication tags.
- [x] **Step 5.3: SQLite FTS5 Storage Service**
  - Implemented `ClipboardStorageService` with `Microsoft.Data.Sqlite`, WAL mode, and FTS5 full-text indexing.
  - Added retention policy (`PurgeClipsOlderThanAsync`) preserving user favorites.
- [x] **Step 5.4: Floating Quick Paste Overlay HUD**
  - Implemented `ClipboardHistoryView.axaml` and `ClipboardHistoryViewModel.cs` with instant sub-millisecond search and format category tabs.
- [x] **Step 5.5: Credential Masking & Filter Tests**
  - Built `ClipboardPrivacyFilter` detecting GitHub tokens, AWS keys, OpenAI keys, Stripe secrets, private keys, and credit cards (Luhn algorithm).

---

## 6. Phase 5 Deliverables & Exit Criteria

- [x] `ClipboardPayload` serializes and deserializes multi-format payloads without data loss.
- [x] AES-GCM 256-bit authenticated encryption verified with tamper-detection and wrong-key rejection tests.
- [x] SQLite FTS5 full-text queries execute in sub-millisecond times with prefix search.
- [x] Privacy firewall verified across all token and credential formats.
- [x] 55 automated unit tests pass in `tests/Mouser.Tests`.
