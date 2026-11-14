using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;
using Xunit;

namespace Mouser.Tests;

public class ClipboardSnapshotTests
{
    private readonly ITestOutputHelper _output;

    public ClipboardSnapshotTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ClipboardSnapshot_IsEmpty_WhenAllFieldsNullOrEmpty()
    {
        var nullSnapshot = new ClipboardSnapshot(null, null, null, null);
        Assert.True(nullSnapshot.IsEmpty);

        var emptySnapshot = new ClipboardSnapshot("", "", Array.Empty<byte>(), Array.Empty<string>());
        Assert.True(emptySnapshot.IsEmpty);
    }

    [Theory]
    [InlineData("hello", null, null, false)]
    [InlineData(null, "<b>html</b>", null, false)]
    [InlineData(null, null, new byte[] { 1, 2, 3 }, false)]
    public void ClipboardSnapshot_IsNotEmpty_WhenAnyFieldPresent(string? text, string? html, byte[]? png, bool isFiles)
    {
        var files = isFiles ? new[] { "C:\\test.txt" } : null;
        var snapshot = new ClipboardSnapshot(text, html, png, files);
        Assert.False(snapshot.IsEmpty);
    }

    [Fact]
    public void ClipboardSnapshot_IsNotEmpty_WhenFilesPresent()
    {
        var snapshot = new ClipboardSnapshot(null, null, null, new[] { "C:\\test.txt" });
        Assert.False(snapshot.IsEmpty);
    }

    [Fact]
    public async Task FakeClipboard_ImplementsGetAndSetSnapshot_PreservingText()
    {
        var fake = new FakeClipboard();
        var initial = await fake.GetSnapshotAsync();
        Assert.True(initial.IsEmpty);

        await fake.SetTextAsync("test string");
        Assert.Equal("test string", fake.Text);
        Assert.Equal("test string", await fake.GetTextAsync());

        var snapshotAfterText = await fake.GetSnapshotAsync();
        Assert.Equal("test string", snapshotAfterText.Text);
        Assert.Null(snapshotAfterText.Html);
        Assert.Null(snapshotAfterText.ImagePng);
        Assert.Null(snapshotAfterText.Files);

        byte[] fakePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        string[] fakeFiles = ["C:\\path1.txt", "C:\\path2.txt"];
        var fullSnapshot = new ClipboardSnapshot("plain", "<b>rich</b>", fakePng, fakeFiles);

        await fake.SetSnapshotAsync(fullSnapshot);
        Assert.Equal("plain", fake.Text);
        Assert.Equal("<b>rich</b>", fake.Html);
        Assert.Equal(fakePng, fake.ImagePng);
        Assert.Equal(fakeFiles, fake.Files);

        var roundTripped = await fake.GetSnapshotAsync();
        Assert.Equal("plain", roundTripped.Text);
        Assert.Equal("<b>rich</b>", roundTripped.Html);
        Assert.Equal(fakePng, roundTripped.ImagePng);
        Assert.Equal(fakeFiles, roundTripped.Files);
    }

    [Fact]
    public void Win32Clipboard_FormatHtmlForClipboard_GeneratesValidCfHtmlHeader()
    {
        string rawHtml = "<p>Test <strong>HTML</strong> fragment</p>";
        string formatted = Win32Clipboard.FormatHtmlForClipboard(rawHtml);

        Assert.StartsWith("Version:0.9", formatted);
        Assert.Contains("StartHTML:", formatted);
        Assert.Contains("EndHTML:", formatted);
        Assert.Contains("StartFragment:", formatted);
        Assert.Contains("EndFragment:", formatted);
        Assert.Contains("<!--StartFragment-->" + rawHtml + "<!--EndFragment-->", formatted);

        // Idempotent: If already formatted, returns as-is
        string reFormatted = Win32Clipboard.FormatHtmlForClipboard(formatted);
        Assert.Equal(formatted, reFormatted);
    }

    [WindowsFact]
    public void Win32Clipboard_RoundTrip_TextAndHtml()
    {
        string text = "Mouser Win32 Clipboard Test " + Guid.NewGuid();
        string html = "<b>" + text + "</b>";

        var original = Win32Clipboard.GetSnapshot();
        try
        {
            var testSnapshot = new ClipboardSnapshot(text, html, null, null);
            bool setSuccess = Win32Clipboard.SetSnapshot(testSnapshot);
            Assert.True(setSuccess);

            var read = Win32Clipboard.GetSnapshot();
            Assert.Equal(text, read.Text);
            Assert.NotNull(read.Html);
            Assert.Contains(text, read.Html);
        }
        finally
        {
            // Restore whatever was on the clipboard
            if (!original.IsEmpty)
            {
                Win32Clipboard.SetSnapshot(original);
            }
        }
    }

    [WindowsFact(Skip = "Manual harness for Snipping Tool image capture")]
    public void ManualHarness_SnippingTool_ImageCapture()
    {
        bool hasPng = Win32Clipboard.HasPng();
        bool hasDib = Win32Clipboard.HasDib();
        bool hasHtml = Win32Clipboard.HasHtml();
        bool hasFiles = Win32Clipboard.HasFiles();

        _output.WriteLine($"Formats detected: HasPng={hasPng}, HasDib={hasDib}, HasHtml={hasHtml}, HasFiles={hasFiles}");

        var snapshot = Win32Clipboard.GetSnapshot();
        _output.WriteLine($"Snapshot text: {snapshot.Text}");
        _output.WriteLine($"Snapshot html length: {snapshot.Html?.Length ?? 0}");
        _output.WriteLine($"Snapshot png bytes: {snapshot.ImagePng?.Length ?? 0}");
        _output.WriteLine($"Snapshot files: {string.Join(", ", snapshot.Files ?? Array.Empty<string>())}");

        if (snapshot.ImagePng is { Length: > 0 } png)
        {
            Assert.True(png.Length >= 8);
            // Check PNG magic bytes: 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A
            Assert.Equal(0x89, png[0]);
            Assert.Equal((byte)'P', png[1]);
            Assert.Equal((byte)'N', png[2]);
            Assert.Equal((byte)'G', png[3]);
        }
    }
}
