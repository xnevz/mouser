using System.Net;
using System.Net.Sockets;
using Mouser.Core.Clipboard;
using Mouser.Core.Networking;

namespace Mouser.Tests;

public sealed class PeerTransferTests : IDisposable
{
    private const int PinOfB = 246_810;
    private readonly string _downloads = Path.Combine(Path.GetTempPath(), "mouser_dl_" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _pairKey = PeerTransferService.DeriveKey(PinOfB, "device-b");
    private readonly PeerTransferService _b;
    private readonly PeerTransferService _a;

    public PeerTransferTests()
    {
        // B accepts only frames sealed with the key from its own PIN (A paired with B).
        _b = new PeerTransferService("device-b", (sender, _) => sender == "device-a" ? new[] { _pairKey } : Array.Empty<byte[]>(), _downloads);
        _a = new PeerTransferService("device-a", (_, _) => new[] { _pairKey }, _downloads);
    }

    private IPEndPoint B => new(IPAddress.Loopback, _b.BoundPort);

    [Fact]
    public void KeyDerivation_IsStableAndDependsOnPinAndOwner()
    {
        Assert.Equal(_pairKey, PeerTransferService.DeriveKey(PinOfB, "device-b"));
        Assert.NotEqual(_pairKey, PeerTransferService.DeriveKey(PinOfB + 1, "device-b"));
        Assert.NotEqual(_pairKey, PeerTransferService.DeriveKey(PinOfB, "device-c"));
        Assert.Equal(32, _pairKey.Length);
    }

    [Fact]
    public async Task Clip_IsDeliveredToThePairedPeer()
    {
        var received = new TaskCompletionSource<(string, ClipboardPayload)>();
        _b.ClipReceived += (sender, clip) => received.TrySetResult((sender, clip));

        await _a.SendClipAsync(B, _pairKey, new ClipboardPayload { TextContent = "copied on A", SourceDevice = "A-PC" });

        var (from, clip) = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("device-a", from);
        Assert.Equal("copied on A", clip.TextContent);
        Assert.Equal("A-PC", clip.SourceDevice);
    }

    [Fact]
    public async Task Clip_WithTheWrongKey_IsDropped()
    {
        bool received = false;
        _b.ClipReceived += (_, _) => received = true;

        await _a.SendClipAsync(B, PeerTransferService.DeriveKey(111_111, "device-b"), new ClipboardPayload { TextContent = "intruder" });
        await Task.Delay(300);

        Assert.False(received);
    }

    [Fact]
    public async Task File_ArrivesIntact_InTheDownloadFolder()
    {
        string source = Path.Combine(Path.GetTempPath(), $"mouser_src_{Guid.NewGuid():N}.bin");
        var data = new byte[300 * 1024]; // several 64 KB chunks
        Random.Shared.NextBytes(data);
        await File.WriteAllBytesAsync(source, data);
        var saved = new TaskCompletionSource<string>();
        _b.FileReceived += (_, path) => saved.TrySetResult(path);
        try
        {
            await _a.SendFileAsync(B, _pairKey, source);

            string path = await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(_downloads, Path.GetDirectoryName(path));
            Assert.Equal(Path.GetFileName(source), Path.GetFileName(path));
            Assert.Equal(data, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task File_FromAnUnpairedComputer_IsRefused()
    {
        string source = Path.Combine(Path.GetTempPath(), $"mouser_src_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(source, "hello");
        try
        {
            var wrongKey = PeerTransferService.DeriveKey(111_111, "device-b");
            await Assert.ThrowsAnyAsync<Exception>(() => _a.SendFileAsync(B, wrongKey, source));
            Assert.False(Directory.Exists(_downloads) && Directory.EnumerateFiles(_downloads).Any());
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task OversizedFrames_AreRejectedWithoutReadingThem()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(B);
        var stream = client.GetStream();
        byte[] frame = { (byte)'M', (byte)'C', (byte)'H', (byte)'1', 1, 1, (byte)'x', 0x7F, 0xFF, 0xFF, 0xFF };
        await stream.WriteAsync(frame);

        // The server closes the connection instead of allocating 2 GB.
        var buffer = new byte[1];
        int read = await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, read);
    }

    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("../../Windows/System32/evil.dll", "evil.dll")]
    [InlineData(@"C:\Users\x\AppData\run.bat", "run.bat")]
    [InlineData("..", "received-file")]
    [InlineData(".bashrc", "bashrc")]
    public void FileNames_CannotEscapeTheDownloadFolder(string sent, string saved)
    {
        Assert.Equal(saved, PeerTransferService.SafeFileName(sent));
    }

    public void Dispose()
    {
        _a.Dispose();
        _b.Dispose();
        try { Directory.Delete(_downloads, true); } catch { }
    }
}
