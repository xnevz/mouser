using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Mouser.Core.Clipboard;
using Mouser.Core.Files;
using Mouser.Core.Security;

namespace Mouser.Core.Networking;

public sealed record IncomingFile(string SenderId, string FileName, long TotalBytes, string Sha256Checksum);

/// <summary>
/// Encrypted TCP channel between paired Mouser computers for clipboard sync and file transfer.
/// Every frame is AES-256-GCM encrypted with a key derived from the PIN of the computer that was paired to,
/// so both sides of one pairing can talk in either direction and nobody else can.
/// shortcut: the key comes from a 6-digit PIN, so a captured frame can be brute-forced offline (1M guesses,
/// slowed by PBKDF2); upgrade to an ECDH key exchange during pairing for real confidentiality.
/// </summary>
public sealed class PeerTransferService : IDisposable
{
    public const int MaxFrameBytes = 4 * 1024 * 1024;
    public const long MaxFileBytes = 4L * 1024 * 1024 * 1024;
    private const int KdfIterations = 200_000;
    private static readonly byte[] Magic = "MCH1"u8.ToArray();

    public Func<IncomingFile, Task<bool>>? AcceptFile { get; set; }
    public long MaxIncomingBytes { get; set; } = 2048L * 1024 * 1024;
    public HashSet<string> BlockedExtensions { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".scr", ".lnk", ".jar", ".com", ".dll"
    };

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".scr", ".lnk", ".jar", ".com", ".dll", ".appx", ".msix"
    };

    public static bool IsExecutableExtension(string ext) => ExecutableExtensions.Contains(ext);

    public enum FrameType : byte
    {
        Clip = 1,
        FileStart = 2,
        FileChunk = 3,
        FileEnd = 4,
        Hello = 5,
        LockRequest = 6,
        PowerEvent = 7,
        PairRequest = 8,
        PairResponse = 9,
        PairRefused = 10,
        PairConfirm = 11,
        ClipRequest = 12
    }

    private static readonly ConcurrentDictionary<(int, string), byte[]> KeyCache = new();

    private readonly string _deviceId;
    private readonly string _deviceName;
    private readonly Func<string, IPAddress, IReadOnlyList<byte[]>> _receiveKeys;
    private readonly string _downloadDirectory;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly PairingRateLimiter _rateLimiter = new();
    private DateTime _pairingWindowEndUtc = DateTime.MinValue;

    public int BoundPort => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public PairingRateLimiter RateLimiter => _rateLimiter;
    public Func<ClipboardPayload?>? GetLatestClip { get; set; }

    /// <summary>A clip arrived from a paired computer (sender device id, clip).</summary>
    public event Action<string, ClipboardPayload>? ClipReceived;

    /// <summary>
    /// A computer proved it holds a pairing key (sender device id, key used). Sent right after pairing so the
    /// paired-to side learns the pairer's device id, which lets it sync back even if the pairer's IP differs.
    /// </summary>
    public event Action<string, byte[]>? HelloReceived;

    /// <summary>A file arrived and passed its SHA-256 check (sender device id, saved path).</summary>
    public event Action<string, string>? FileReceived;

    /// <summary>A power event arrived from a paired computer (sender device id, power event kind).</summary>
    public event Action<string, PowerEventKind>? PowerReceived;

    /// <summary>A lock request arrived from a paired computer (sender device id).</summary>
    public event Action<string>? LockRequested;

    /// <summary>An incoming pairing request arrived within the allowed window.</summary>
    public event Action<PairingRequestArgs>? PairingRequested;

    /// <summary>Pairing handshake completed successfully with mutual confirmation (peerDeviceId, peerDeviceName, pairKey).</summary>
    public event Action<string, string, byte[]>? PairingCompleted;

    public bool LocalNetworkOnly { get; set; } = true;

    /// <param name="receiveKeys">Keys that may have encrypted traffic from this sender (empty = not paired, reject).</param>
    public PeerTransferService(string deviceId, Func<string, IPAddress, IReadOnlyList<byte[]>> receiveKeys,
        string downloadDirectory, int port = 0, string? deviceName = null, IPAddress? bindAddress = null)
    {
        _deviceId = deviceId;
        _deviceName = string.IsNullOrWhiteSpace(deviceName) ? Environment.MachineName : deviceName;
        _receiveKeys = receiveKeys;
        _downloadDirectory = downloadDirectory;
        _listener = new TcpListener(bindAddress ?? IPAddress.Any, port);
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public bool IsPairingWindowOpen(DateTime? now = null) =>
        (now ?? DateTime.UtcNow) < _pairingWindowEndUtc;

    public TimeSpan PairingWindowRemaining(DateTime? now = null)
    {
        var current = now ?? DateTime.UtcNow;
        return _pairingWindowEndUtc > current ? _pairingWindowEndUtc - current : TimeSpan.Zero;
    }

    public void AllowNewDevices(TimeSpan window)
    {
        _pairingWindowEndUtc = DateTime.UtcNow + window;
    }

    public void CancelPairingWindow()
    {
        _pairingWindowEndUtc = DateTime.MinValue;
    }

    public async Task SendClipAsync(IPEndPoint target, byte[] key, ClipboardPayload clip, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(target, ct);
        await using var stream = client.GetStream();
        await WriteFrameAsync(stream, FrameType.Clip, key, clip.Serialize(), ct);
    }

    public async Task<ClipboardPayload?> RequestClipAsync(IPEndPoint target, byte[] key, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(target, ct);
        await using var stream = client.GetStream();
        await WriteFrameAsync(stream, FrameType.ClipRequest, key, Array.Empty<byte>(), ct);
        var reply = await ReadFrameAsync(stream, target.Address, key, ct);
        if (reply is { Type: FrameType.Clip } clipFrame)
        {
            return ClipboardPayload.Deserialize(clipFrame.Body);
        }
        return null;
    }

    public async Task SendHelloAsync(IPEndPoint target, byte[] key, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(target, ct);
        await using var stream = client.GetStream();
        await WriteFrameAsync(stream, FrameType.Hello, key, Array.Empty<byte>(), ct);
    }

    public async Task SendPowerAsync(IPEndPoint target, byte[] key, PowerEventKind kind, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(target, ct);
        await using var stream = client.GetStream();
        await WriteFrameAsync(stream, FrameType.PowerEvent, key, new[] { (byte)kind }, ct);
    }

    public async Task SendLockAsync(IPEndPoint target, byte[] key, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(target, ct);
        await using var stream = client.GetStream();
        await WriteFrameAsync(stream, FrameType.LockRequest, key, Array.Empty<byte>(), ct);
    }

    public async Task SendFileAsync(IPEndPoint target, byte[] key, string path, IProgress<FileTransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxFileBytes) throw new IOException($"{info.Name} is larger than 4 GB.");

        string sha = await FileStreamingService.ComputeSha256Async(path, ct);
        using var client = new TcpClient();
        await client.ConnectAsync(target, ct);
        await using var stream = client.GetStream();

        var header = new FileTransferHeader(Guid.NewGuid().ToString("N"), info.Name, info.Length, sha);
        await WriteFrameAsync(stream, FrameType.FileStart, key, EncodeHeader(header), ct);

        var ackTask = Task.Run(async () =>
        {
            var ack = new byte[1];
            int n = await stream.ReadAsync(ack, ct);
            return n == 1 ? ack[0] : (byte)0;
        }, ct);

        try
        {
            await FileStreamingService.StreamFileChunksAsync(path, async (_, chunk) =>
            {
                if (ackTask.IsCompleted) return;
                await WriteFrameAsync(stream, FrameType.FileChunk, key, chunk.ToArray(), ct);
            }, progress, ct: ct);
        }
        catch (Exception) when (ackTask.IsCompleted)
        {
        }

        if (!ackTask.IsCompleted)
        {
            try
            {
                await WriteFrameAsync(stream, FrameType.FileEnd, key, Array.Empty<byte>(), ct);
            }
            catch (Exception) when (ackTask.IsCompleted) { }
        }

        byte ackByte = await ackTask;
        if (ackByte == 2)
            throw new IOException("The other computer declined the file.");
        if (ackByte != 1)
            throw new IOException("The other computer didn't accept the file (wrong pairing or corrupted transfer).");
    }

    public static async Task WritePlainFrameAsync(Stream stream, string deviceId, FrameType type, byte[] plaintext, CancellationToken ct)
    {
        byte[] sender = Encoding.UTF8.GetBytes(deviceId);
        var head = new byte[Magic.Length + 1 + 1 + sender.Length + 4];
        Magic.CopyTo(head, 0);
        head[4] = (byte)type;
        head[5] = (byte)sender.Length;
        sender.CopyTo(head, 6);
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(6 + sender.Length), plaintext.Length);
        await stream.WriteAsync(head, ct);
        if (plaintext.Length > 0)
        {
            await stream.WriteAsync(plaintext, ct);
        }
    }

    private async Task WriteFrameAsync(Stream stream, FrameType type, byte[] key, byte[] plaintext, CancellationToken ct)
    {
        byte[] sealedBody = ClipboardCryptoEngine.Encrypt(plaintext, key);
        await WritePlainFrameAsync(stream, _deviceId, type, sealedBody, ct);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (Exception) when (_cts.IsCancellationRequested) { return; }
            catch (SocketException) { continue; }
            _ = HandleConnectionAsync(client);
        }
    }

    private async Task HandleConnectionAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(30)); // a silent connection is dropped
                var ct = timeout.Token;
                var stream = client.GetStream();
                var remote = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;
                if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
                if (LocalNetworkOnly && !NetworkInfo.IsPrivate(remote)) return;

                var first = await ReadFrameAsync(stream, remote, null, ct);
                if (first == null) return;
                var (type, sender, key, body) = first.Value;
                timeout.CancelAfter(Timeout.Infinite); // authenticated: large files may take a while

                if (type == FrameType.Clip)
                {
                    var clip = ClipboardPayload.Deserialize(body);
                    if (clip.TextContent.Length > ClipboardTextLimit) return;
                    ClipReceived?.Invoke(sender, clip);
                }
                else if (type == FrameType.ClipRequest)
                {
                    var latest = GetLatestClip?.Invoke();
                    if (latest != null)
                    {
                        await WriteFrameAsync(stream, FrameType.Clip, key, latest.Serialize(), ct);
                    }
                }
                else if (type == FrameType.Hello)
                {
                    HelloReceived?.Invoke(sender, key);
                }
                else if (type == FrameType.LockRequest)
                {
                    LockRequested?.Invoke(sender);
                }
                else if (type == FrameType.PowerEvent)
                {
                    if (body.Length >= 1)
                    {
                        PowerReceived?.Invoke(sender, (PowerEventKind)body[0]);
                    }
                }
                else if (type == FrameType.FileStart)
                {
                    await ReceiveFileAsync(stream, remote, sender, key, DecodeHeader(body), ct);
                }
                else if (type == FrameType.PairRequest)
                {
                    await HandlePairRequestAsync(stream, remote, body, ct);
                }
            }
            catch (Exception e) when (e is IOException or SocketException or CryptographicException or
                                          InvalidDataException or EndOfStreamException or OperationCanceledException)
            {
                // Bad or interrupted transfer: drop the connection; partial files are removed by FileStreamingService.
            }
        }
    }

    private const int ClipboardTextLimit = 1_000_000;

    private async Task ReceiveFileAsync(NetworkStream stream, IPAddress remote, string sender, byte[] key,
        FileTransferHeader header, CancellationToken ct)
    {
        if (header.TotalBytes is < 0 or > MaxFileBytes || header.TotalBytes > MaxIncomingBytes)
        {
            await stream.WriteAsync(new byte[] { 2 }, ct);
            return;
        }

        string safeName = SafeFileName(header.FileName);
        string ext = Path.GetExtension(safeName);
        if (BlockedExtensions.Contains(ext))
        {
            await stream.WriteAsync(new byte[] { 2 }, ct);
            return;
        }

        if (AcceptFile != null)
        {
            var incoming = new IncomingFile(sender, safeName, header.TotalBytes, header.Sha256Checksum);
            bool accepted = await AcceptFile(incoming);
            if (!accepted)
            {
                await stream.WriteAsync(new byte[] { 2 }, ct);
                return;
            }
        }

        Directory.CreateDirectory(_downloadDirectory);
        string path = UniquePath(Path.Combine(_downloadDirectory, safeName));
        long received = 0;

        await FileStreamingService.ReceiveFileAsync(path, header, async writeChunk =>
        {
            while (true)
            {
                var frame = await ReadFrameAsync(stream, remote, key, ct)
                            ?? throw new InvalidDataException("Transfer interrupted.");
                if (frame.Type == FrameType.FileEnd) return;
                if (frame.Type != FrameType.FileChunk || frame.Sender != sender) throw new InvalidDataException("Unexpected frame.");
                received += frame.Body.Length;
                if (received > header.TotalBytes) throw new InvalidDataException("More data than announced.");
                await writeChunk(frame.Body);
            }
        }, ct: ct);

        await stream.WriteAsync(new byte[] { 1 }, ct);

        if (OperatingSystem.IsWindows() && IsExecutableExtension(ext))
        {
            try
            {
                File.WriteAllText(path + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
            }
            catch
            {
                // Windows, ignore errors (e.g. non-NTFS filesystem)
            }
        }

        FileReceived?.Invoke(sender, path);
    }

    /// <summary>Reads one frame and decrypts it. A known key (mid-transfer) must match; otherwise the sender's keys are tried.</summary>
    internal async Task<(FrameType Type, string Sender, byte[] Key, byte[] Body)?> ReadFrameAsync(
        Stream stream, IPAddress remote, byte[]? knownKey, CancellationToken ct)
    {
        var head = new byte[6];
        if (!await ReadExactAsync(stream, head, ct)) return null;
        if (!head.AsSpan(0, 4).SequenceEqual(Magic)) throw new InvalidDataException("Not a Mouser frame.");
        var type = (FrameType)head[4];

        var senderBytes = new byte[head[5]];
        var lengthBytes = new byte[4];
        if (!await ReadExactAsync(stream, senderBytes, ct) || !await ReadExactAsync(stream, lengthBytes, ct))
            throw new EndOfStreamException();
        int length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
        if (length is < 0 or > MaxFrameBytes) throw new InvalidDataException("Frame too large.");

        var sealedBody = new byte[length];
        if (!await ReadExactAsync(stream, sealedBody, ct)) throw new EndOfStreamException();

        string sender = Encoding.UTF8.GetString(senderBytes);
        if (type is FrameType.PairRequest or FrameType.PairResponse or FrameType.PairRefused or FrameType.PairConfirm)
        {
            return (type, sender, Array.Empty<byte>(), sealedBody);
        }

        var keys = knownKey != null ? new[] { knownKey } : _receiveKeys(sender, remote);
        foreach (var key in keys)
        {
            try
            {
                return (type, sender, key, ClipboardCryptoEngine.Decrypt(sealedBody, key));
            }
            catch (CryptographicException)
            {
                // try the next candidate key
            }
        }
        throw new CryptographicException("Frame from an unpaired or mismatched computer.");
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0)
            {
                if (read == 0) return false; // clean end of stream between frames
                throw new EndOfStreamException();
            }
            read += n;
        }
        return true;
    }

    private static byte[] EncodeHeader(FileTransferHeader h) =>
        Encoding.UTF8.GetBytes(string.Join('\n', h.TransferId, h.FileName, h.TotalBytes, h.Sha256Checksum));

    private static FileTransferHeader DecodeHeader(byte[] body)
    {
        var parts = Encoding.UTF8.GetString(body).Split('\n');
        if (parts.Length != 4 || !long.TryParse(parts[2], out long size)) throw new InvalidDataException("Bad file header.");
        return new FileTransferHeader(parts[0], parts[1], size, parts[3]);
    }

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>Strips directories, invalid characters, alternate data streams, and reserved device names so a sender can't write outside the download folder.</summary>
    public static string SafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "received-file";

        // Strip directory separators (both \ and /) and take only the last segment
        string file = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;

        // Remove Alternate Data Stream specifier: e.g. "foo.txt:evil" -> "foo.txt"
        int colon = file.IndexOf(':');
        if (colon >= 0) file = file.Substring(0, colon);

        // Strip invalid file characters
        foreach (char c in Path.GetInvalidFileNameChars()) file = file.Replace(c, '_');

        // Strip trailing dots and spaces, and leading dots
        file = file.Trim().TrimStart('.').TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(file)) return "received-file";

        // Reserved Windows names check on root base name (e.g. CON, NUL.txt, nul.tar.gz)
        string rootBase = file.Split('.')[0];
        if (ReservedDeviceNames.Contains(rootBase))
        {
            file = "_" + file;
        }

        // Limit to 150 characters while preserving extension
        if (file.Length > 150)
        {
            string ext = Path.GetExtension(file);
            string basePart = Path.GetFileNameWithoutExtension(file);
            if (ext.Length >= 150)
            {
                file = file.Substring(0, 150);
            }
            else
            {
                file = basePart.Substring(0, 150 - ext.Length) + ext;
            }
        }

        return string.IsNullOrWhiteSpace(file) ? "received-file" : file;
    }

    public static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        for (int i = 1; ; i++)
        {
            string candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private async Task HandlePairRequestAsync(NetworkStream stream, IPAddress remote, byte[] body, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (!_rateLimiter.CanAttempt(remote, now, out var refusalReason))
        {
            await WritePlainFrameAsync(stream, _deviceId, FrameType.PairRefused,
                new PairRefusedPayload(refusalReason ?? "RateLimited").Serialize(), ct);
            return;
        }
        _rateLimiter.RecordAttempt(remote, now);

        if (!IsPairingWindowOpen(now))
        {
            await WritePlainFrameAsync(stream, _deviceId, FrameType.PairRefused,
                new PairRefusedPayload("PairingDisabled").Serialize(), ct);
            return;
        }

        PairRequestPayload request;
        try
        {
            request = PairRequestPayload.Deserialize(body);
        }
        catch
        {
            await WritePlainFrameAsync(stream, _deviceId, FrameType.PairRefused,
                new PairRefusedPayload("InvalidPayload").Serialize(), ct);
            return;
        }

        using var handshake = PairingHandshake.Start();
        PairingResult result;
        try
        {
            result = handshake.Complete(request.PublicKey, _deviceId, request.DeviceId);
        }
        catch
        {
            await WritePlainFrameAsync(stream, _deviceId, FrameType.PairRefused,
                new PairRefusedPayload("InvalidPublicKey").Serialize(), ct);
            return;
        }

        // Send PairResponse with our public key
        var response = new PairResponsePayload(_deviceId, _deviceName, handshake.PublicKey);
        await WritePlainFrameAsync(stream, _deviceId, FrameType.PairResponse, response.Serialize(), ct);

        var localDecisionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnAllow() => localDecisionTcs.TrySetResult(true);
        void OnDeny() => localDecisionTcs.TrySetResult(false);

        var args = new PairingRequestArgs(request.DeviceName, request.DeviceId, result.ConfirmationCode, OnAllow, OnDeny);
        PairingRequested?.Invoke(args);

        // Read remote frame concurrently
        var remoteConfirmTask = ReadFrameAsync(stream, remote, null, ct);

        // Await local user decision or remote disconnect/refusal
        var first = await Task.WhenAny(localDecisionTcs.Task, remoteConfirmTask);
        if (first == remoteConfirmTask)
        {
            var rf = await remoteConfirmTask;
            if (rf == null || rf.Value.Type == FrameType.PairRefused)
            {
                return;
            }
        }

        bool localDecision;
        try
        {
            localDecision = await localDecisionTcs.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
        }
        catch (TimeoutException)
        {
            localDecision = false;
        }

        if (!localDecision)
        {
            _rateLimiter.RecordDenial(remote, DateTime.UtcNow);
            await WritePlainFrameAsync(stream, _deviceId, FrameType.PairRefused,
                new PairRefusedPayload("DeniedByUser").Serialize(), ct);
            return;
        }

        // Local user clicked Allow: send PairConfirm
        await WritePlainFrameAsync(stream, _deviceId, FrameType.PairConfirm, Array.Empty<byte>(), ct);

        // Await remote confirmation
        var finalRemoteFrame = await remoteConfirmTask;
        if (finalRemoteFrame != null && finalRemoteFrame.Value.Type == FrameType.PairConfirm)
        {
            PairingCompleted?.Invoke(request.DeviceId, request.DeviceName, result.PairKey);
        }
    }

    public async Task<PairingInitiatorResult> RequestPairingAsync(
        IPEndPoint target,
        Func<string, string, Task<bool>> confirmationPrompt,
        CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(target, ct);
        await using var stream = client.GetStream();

        using var handshake = PairingHandshake.Start();
        var req = new PairRequestPayload(_deviceId, _deviceName, handshake.PublicKey);
        await WritePlainFrameAsync(stream, _deviceId, FrameType.PairRequest, req.Serialize(), ct);

        var firstFrame = await ReadFrameAsync(stream, target.Address, null, ct);
        if (firstFrame == null) return PairingInitiatorResult.Failed("ConnectionClosed");

        if (firstFrame.Value.Type == FrameType.PairRefused)
        {
            var refused = PairRefusedPayload.Deserialize(firstFrame.Value.Body);
            return PairingInitiatorResult.Refused(refused.Reason);
        }

        if (firstFrame.Value.Type != FrameType.PairResponse)
        {
            return PairingInitiatorResult.Failed("UnexpectedFrame");
        }

        var resp = PairResponsePayload.Deserialize(firstFrame.Value.Body);
        PairingResult result = handshake.Complete(resp.PublicKey, _deviceId, resp.DeviceId);

        // Read remote frame concurrently
        var remoteConfirmTask = ReadFrameAsync(stream, target.Address, null, ct);

        // Prompt local user
        bool confirmed = await confirmationPrompt(resp.DeviceName, result.ConfirmationCode);
        if (!confirmed)
        {
            await WritePlainFrameAsync(stream, _deviceId, FrameType.PairRefused,
                new PairRefusedPayload("CancelledByUser").Serialize(), ct);
            return PairingInitiatorResult.Refused("CancelledByUser");
        }

        // Local user confirmed: send PairConfirm
        await WritePlainFrameAsync(stream, _deviceId, FrameType.PairConfirm, Array.Empty<byte>(), ct);

        var finalFrame = await remoteConfirmTask;
        if (finalFrame == null)
        {
            return PairingInitiatorResult.Failed("ConnectionClosed");
        }
        if (finalFrame.Value.Type == FrameType.PairRefused)
        {
            var refused = PairRefusedPayload.Deserialize(finalFrame.Value.Body);
            return PairingInitiatorResult.Refused(refused.Reason);
        }
        if (finalFrame.Value.Type == FrameType.PairConfirm)
        {
            return PairingInitiatorResult.Success(resp.DeviceId, resp.DeviceName, result.PairKey, result.ConfirmationCode);
        }

        return PairingInitiatorResult.Failed("UnexpectedConfirmationFrame");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
    }
}
