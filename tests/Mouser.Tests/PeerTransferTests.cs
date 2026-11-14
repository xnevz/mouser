using System.Net;
using System.Net.Sockets;
using Mouser.Core.Clipboard;
using Mouser.Core.Networking;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;

namespace Mouser.Tests;

public sealed class PeerTransferTests : IDisposable
{
    private readonly string _downloads = Path.Combine(Path.GetTempPath(), "mouser_dl_" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _pairKey = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
    private readonly byte[] _wrongKey = new byte[32] { 99, 99, 99, 99, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
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

        await _a.SendClipAsync(B, _wrongKey, new ClipboardPayload { TextContent = "intruder" });
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
            var wrongKey = _wrongKey;
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
    [InlineData(@"..\..\secret.txt", "secret.txt")]
    [InlineData("/etc/shadow", "shadow")]
    [InlineData("a.txt:evil", "a.txt")]
    [InlineData("data.pdf:Stream:$DATA", "data.pdf")]
    [InlineData("CON", "_CON")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("nul.tar.gz", "_nul.tar.gz")]
    [InlineData("aux.dat", "_aux.dat")]
    [InlineData("COM1.png", "_COM1.png")]
    [InlineData("lpt1", "_lpt1")]
    [InlineData("report.pdf...   ", "report.pdf")]
    [InlineData("bad*char?test.txt", "bad_char_test.txt")]
    [InlineData("", "received-file")]
    [InlineData("   ", "received-file")]
    [InlineData(":::", "received-file")]
    public void FileNames_CannotEscapeTheDownloadFolder(string sent, string saved)
    {
        Assert.Equal(saved, PeerTransferService.SafeFileName(sent));
    }

    [Fact]
    public void SafeFileName_TruncatesLongFileNames_PreservingExtension()
    {
        string longName = new string('a', 200) + ".pdf";
        string sanitized = PeerTransferService.SafeFileName(longName);
        Assert.True(sanitized.Length <= 150);
        Assert.EndsWith(".pdf", sanitized);
        Assert.Equal(150, sanitized.Length);
    }

    [Fact]
    public void UniquePath_NeverOverwrites_UsesParenthesesOneFirst()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "mouser_unique_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string baseFile = Path.Combine(tempDir, "document.txt");
            Assert.Equal(baseFile, PeerTransferService.UniquePath(baseFile));

            File.WriteAllText(baseFile, "original");
            string copy1 = PeerTransferService.UniquePath(baseFile);
            Assert.Equal(Path.Combine(tempDir, "document (1).txt"), copy1);

            File.WriteAllText(copy1, "copy 1");
            string copy2 = PeerTransferService.UniquePath(baseFile);
            Assert.Equal(Path.Combine(tempDir, "document (2).txt"), copy2);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ReceiveFile_OverSizeLimit_RefusesWithAck2()
    {
        _b.MaxIncomingBytes = 100 * 1024; // 100 KB limit
        string source = Path.Combine(Path.GetTempPath(), $"mouser_oversize_{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(source, new byte[200 * 1024]); // 200 KB
        try
        {
            var ex = await Assert.ThrowsAsync<IOException>(() => _a.SendFileAsync(B, _pairKey, source));
            Assert.Contains("declined", ex.Message);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task ReceiveFile_BlockedExtension_RefusesWithAck2()
    {
        string source = Path.Combine(Path.GetTempPath(), $"mouser_blocked_{Guid.NewGuid():N}.exe");
        await File.WriteAllBytesAsync(source, new byte[1024]);
        try
        {
            var ex = await Assert.ThrowsAsync<IOException>(() => _a.SendFileAsync(B, _pairKey, source));
            Assert.Contains("declined", ex.Message);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task ReceiveFile_UserDeclines_RefusesWithAck2()
    {
        _b.AcceptFile = incoming => Task.FromResult(false);
        string source = Path.Combine(Path.GetTempPath(), $"mouser_decline_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(source, "hello");
        try
        {
            var ex = await Assert.ThrowsAsync<IOException>(() => _a.SendFileAsync(B, _pairKey, source));
            Assert.Contains("declined", ex.Message);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task ReceiveFile_UserAccepts_SavesFileSuccessfully()
    {
        _b.AcceptFile = incoming =>
        {
            Assert.Equal("device-a", incoming.SenderId);
            Assert.Equal("report.txt", incoming.FileName);
            Assert.True(incoming.TotalBytes > 0);
            return Task.FromResult(true);
        };

        string source = Path.Combine(Path.GetTempPath(), "report.txt");
        await File.WriteAllTextAsync(source, "my report content");
        var saved = new TaskCompletionSource<string>();
        _b.FileReceived += (_, path) => saved.TrySetResult(path);
        try
        {
            await _a.SendFileAsync(B, _pairKey, source);
            string savedPath = await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(savedPath));
            Assert.Equal("my report content", await File.ReadAllTextAsync(savedPath));
        }
        finally
        {
            File.Delete(source);
        }
    }

    [WindowsFact]
    public async Task ReceiveFile_ExecutableExtension_WritesZoneIdentifier_OnWindows()
    {
        // Allow .bat for this test to verify Mark-of-the-web
        _b.BlockedExtensions.Remove(".bat");
        _b.AcceptFile = _ => Task.FromResult(true);

        string source = Path.Combine(Path.GetTempPath(), $"script_{Guid.NewGuid():N}.bat");
        await File.WriteAllTextAsync(source, "echo hello");
        var saved = new TaskCompletionSource<string>();
        _b.FileReceived += (_, path) => saved.TrySetResult(path);
        try
        {
            await _a.SendFileAsync(B, _pairKey, source);
            string savedPath = await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(savedPath));

            string adsPath = savedPath + ":Zone.Identifier";
            Assert.True(File.Exists(adsPath));
            string zoneContent = await File.ReadAllTextAsync(adsPath);
            Assert.Contains("ZoneId=3", zoneContent);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task PowerEvent_IsDeliveredToThePairedPeer()
    {
        var received = new TaskCompletionSource<(string, PowerEventKind)>();
        _b.PowerReceived += (sender, kind) => received.TrySetResult((sender, kind));

        await _a.SendPowerAsync(B, _pairKey, PowerEventKind.Lock);

        var (from, kind) = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("device-a", from);
        Assert.Equal(PowerEventKind.Lock, kind);
    }

    [Fact]
    public async Task PowerEvent_WithWrongKey_IsDropped()
    {
        bool received = false;
        _b.PowerReceived += (_, _) => received = true;

        await _a.SendPowerAsync(B, _wrongKey, PowerEventKind.Sleep);
        await Task.Delay(300);

        Assert.False(received);
    }

    [Fact]
    public async Task LockRequest_IsDeliveredToThePairedPeer()
    {
        var received = new TaskCompletionSource<string>();
        _b.LockRequested += sender => received.TrySetResult(sender);

        await _a.SendLockAsync(B, _pairKey);

        var from = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("device-a", from);
    }

    [Fact]
    public async Task LockRequest_WithWrongKey_IsDropped()
    {
        bool received = false;
        _b.LockRequested += _ => received = true;

        await _a.SendLockAsync(B, _wrongKey);
        await Task.Delay(300);

        Assert.False(received);
    }

    [Fact]
    public async Task ReceiveFile_AutoAcceptEnabled_SkipsPromptAndSaves()
    {
        bool promptCalled = false;
        _b.AcceptFile = incoming =>
        {
            promptCalled = true;
            return Task.FromResult(true);
        };

        string source = Path.Combine(Path.GetTempPath(), $"auto_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(source, "auto content");
        var saved = new TaskCompletionSource<string>();
        _b.FileReceived += (_, path) => saved.TrySetResult(path);
        try
        {
            await _a.SendFileAsync(B, _pairKey, source);
            string savedPath = await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(savedPath));
            Assert.True(promptCalled);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task MainViewModel_IncomingFilePrompt_Accept_ReturnsTrue()
    {
        await using var app = new AppFixture();
        Assert.NotNull(app.Host.IncomingFilePromptHandler);

        var promptTask = app.Host.IncomingFilePromptHandler("peer-1", "Laptop-Pro", "report.pdf", 3_200_000);
        await Wait.UntilAsync(() => app.Main.IsIncomingFilePromptVisible);

        Assert.Contains("Laptop-Pro wants to send report.pdf", app.Main.IncomingFilePromptTitle);
        Assert.Contains("MB", app.Main.IncomingFilePromptTitle);

        app.Main.AcceptIncomingFileCommand.Execute(null);
        bool accepted = await promptTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(accepted);
        Assert.False(app.Main.IsIncomingFilePromptVisible);
    }

    [Fact]
    public async Task MainViewModel_IncomingFilePrompt_Decline_ReturnsFalse()
    {
        await using var app = new AppFixture();
        Assert.NotNull(app.Host.IncomingFilePromptHandler);

        var promptTask = app.Host.IncomingFilePromptHandler("peer-1", "Laptop-Pro", "report.pdf", 1024);
        await Wait.UntilAsync(() => app.Main.IsIncomingFilePromptVisible);

        app.Main.DeclineIncomingFileCommand.Execute(null);
        bool accepted = await promptTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(accepted);
        Assert.False(app.Main.IsIncomingFilePromptVisible);
    }

    [Fact]
    public async Task MainViewModel_IncomingFilePrompt_AlwaysAccept_PersistsAutoAcceptFiles()
    {
        await using var app = new AppFixture();
        string peerId = "peer-always";
        app.Settings.Current.Peers[peerId] = new PeerSettings { Name = "Desktop-Friend", AutoAcceptFiles = false };
        app.Settings.Save();

        Assert.NotNull(app.Host.IncomingFilePromptHandler);
        var promptTask = app.Host.IncomingFilePromptHandler!(peerId, "Desktop-Friend", "notes.txt", 2048);
        await Wait.UntilAsync(() => app.Main.IsIncomingFilePromptVisible);

        app.Main.AlwaysAcceptIncomingFileCommand.Execute(null);
        bool accepted = await promptTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(accepted);
        Assert.True(app.Settings.Current.Peers[peerId].AutoAcceptFiles);
    }

    public void Dispose()
    {
        _a.Dispose();
        _b.Dispose();
        try { Directory.Delete(_downloads, true); } catch { }
    }
}
