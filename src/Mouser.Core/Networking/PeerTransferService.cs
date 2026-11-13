using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Mouser.Core.Clipboard;
using Mouser.Core.Files;

namespace Mouser.Core.Networking;

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

    private enum FrameType : byte { Clip = 1, FileStart = 2, FileChunk = 3, FileEnd = 4, Hello = 5 }

    private static readonly ConcurrentDictionary<(int, string), byte[]> KeyCache = new();

    private readonly string _deviceId;
    private readonly Func<string, IPAddress, IReadOnlyList<byte[]>> _receiveKeys;
    private readonly string _downloadDirectory;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();

    public int BoundPort => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>A clip arrived from a paired computer (sender device id, clip).</summary>
    public event Action<string, ClipboardPayload>? ClipReceived;

    /// <summary>
    /// A computer proved it holds a pairing key (sender device id, key used). Sent right after pairing so the
    /// paired-to side learns the pairer's device id, which lets it sync back even if the pairer's IP differs.
    /// </summary>
    public event Action<string, byte[]>? HelloReceived;

    /// <summary>A file arrived and passed its SHA-256 check (sender device id, saved path).</summary>
    public event Action<string, string>? FileReceived;

    /// <param name="receiveKeys">Keys that may have encrypted traffic from this sender (empty = not paired, reject).</param>
    public PeerTransferService(string deviceId, Func<string, IPAddress, IReadOnlyList<byte[]>> receiveKeys,
        string downloadDirectory, int port = 0)
    {
        _deviceId = deviceId;
        _receiveKeys = receiveKeys;
        _downloadDirectory = downloadDirectory;
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    /// <summary>Key shared by a pairing: derived from the PIN of the computer that was paired to, salted with its id.</summary>
    public static byte[] DeriveKey(int pin, string pinOwnerDeviceId) =>
        KeyCache.GetOrAdd((pin, pinOwnerDeviceId), k => Rfc2898DeriveBytes.Pbkdf2(
            k.Item1.ToString("000000"), Encoding.UTF8.GetBytes("mouser:" + k.Item2), KdfIterations,
            HashAlgorithmName.SHA256, ClipboardCryptoEngine.KeySizeBytes));

    public async Task SendClipAsync(IPEndPoint target, byte[] key, ClipboardPayload clip, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(target, ct);
        await using var stream = client.GetStream();
        await WriteFrameAsync(stream, FrameType.Clip, key, clip.Serialize(), ct);
    }

    public async Task SendHelloAsync(IPEndPoint target, byte[] key, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(target, ct);
        await using var stream = client.GetStream();
        await WriteFrameAsync(stream, FrameType.Hello, key, Array.Empty<byte>(), ct);
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
        await FileStreamingService.StreamFileChunksAsync(path,
            (_, chunk) => WriteFrameAsync(stream, FrameType.FileChunk, key, chunk.ToArray(), ct), progress, ct: ct);
        await WriteFrameAsync(stream, FrameType.FileEnd, key, Array.Empty<byte>(), ct);

        // The receiver answers 1 only after the SHA-256 check passed and the file is saved.
        var ack = new byte[1];
        if (await stream.ReadAsync(ack, ct) != 1 || ack[0] != 1)
            throw new IOException("The other computer didn't accept the file (wrong pairing or corrupted transfer).");
    }

    private async Task WriteFrameAsync(Stream stream, FrameType type, byte[] key, byte[] plaintext, CancellationToken ct)
    {
        byte[] sender = Encoding.UTF8.GetBytes(_deviceId);
        byte[] sealedBody = ClipboardCryptoEngine.Encrypt(plaintext, key);
        var head = new byte[Magic.Length + 1 + 1 + sender.Length + 4];
        Magic.CopyTo(head, 0);
        head[4] = (byte)type;
        head[5] = (byte)sender.Length;
        sender.CopyTo(head, 6);
        BinaryPrimitives.WriteInt32BigEndian(head.AsSpan(6 + sender.Length), sealedBody.Length);
        await stream.WriteAsync(head, ct);
        await stream.WriteAsync(sealedBody, ct);
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
                else if (type == FrameType.Hello)
                {
                    HelloReceived?.Invoke(sender, key);
                }
                else if (type == FrameType.FileStart)
                {
                    await ReceiveFileAsync(stream, remote, sender, key, DecodeHeader(body), ct);
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
        if (header.TotalBytes is < 0 or > MaxFileBytes) throw new InvalidDataException("File too large.");

        Directory.CreateDirectory(_downloadDirectory);
        string path = UniquePath(Path.Combine(_downloadDirectory, SafeFileName(header.FileName)));
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
        FileReceived?.Invoke(sender, path);
    }

    /// <summary>Reads one frame and decrypts it. A known key (mid-transfer) must match; otherwise the sender's keys are tried.</summary>
    private async Task<(FrameType Type, string Sender, byte[] Key, byte[] Body)?> ReadFrameAsync(
        NetworkStream stream, IPAddress remote, byte[]? knownKey, CancellationToken ct)
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

    /// <summary>Strips directories and invalid characters so a sender can't write outside the download folder.</summary>
    public static string SafeFileName(string name)
    {
        string file = Path.GetFileName(name.Replace('\\', '/').Split('/').Last());
        foreach (char c in Path.GetInvalidFileNameChars()) file = file.Replace(c, '_');
        file = file.Trim().TrimStart('.');
        return string.IsNullOrEmpty(file) ? "received-file" : file;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path)!, name = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
    }
}
