using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public sealed class ClipboardImageViewModelTests : IDisposable
{
    private readonly string _tempDir;

    public ClipboardImageViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mouser_img_vm_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void ClipboardItemViewModel_DimensionsText_FormatsCorrectly()
    {
        // Both width and height present
        var payload1 = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Image,
            Width = 512,
            Height = 251
        };
        var vm1 = new ClipboardItemViewModel(payload1);
        Assert.Equal("512 × 251", vm1.DimensionsText);

        // Only width
        var payload2 = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Image,
            Width = 512,
            Height = null
        };
        var vm2 = new ClipboardItemViewModel(payload2);
        Assert.Equal(string.Empty, vm2.DimensionsText);

        // Only height
        var payload3 = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Image,
            Width = null,
            Height = 251
        };
        var vm3 = new ClipboardItemViewModel(payload3);
        Assert.Equal(string.Empty, vm3.DimensionsText);

        // Neither
        var payload4 = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Image
        };
        var vm4 = new ClipboardItemViewModel(payload4);
        Assert.Equal(string.Empty, vm4.DimensionsText);
    }

    [Fact]
    public void ClipboardItemViewModel_FormattedSizeText_FormatsUnits()
    {
        // Null or non-positive
        var vmNull = new ClipboardItemViewModel(new ClipboardPayload { MediaBytes = null });
        Assert.Equal(string.Empty, vmNull.FormattedSizeText);

        var vmZero = new ClipboardItemViewModel(new ClipboardPayload { MediaBytes = 0 });
        Assert.Equal(string.Empty, vmZero.FormattedSizeText);

        // Bytes (< 1024)
        var vmBytes = new ClipboardItemViewModel(new ClipboardPayload { MediaBytes = 500 });
        Assert.Equal("500 B", vmBytes.FormattedSizeText);

        // KB (exact 1 KB)
        var vm1Kb = new ClipboardItemViewModel(new ClipboardPayload { MediaBytes = 1024 });
        Assert.Equal("1 KB", vm1Kb.FormattedSizeText);

        // KB (fractional 12.5 KB = 12800 bytes)
        var vm12_5Kb = new ClipboardItemViewModel(new ClipboardPayload { MediaBytes = 12800 });
        Assert.Equal("12.5 KB", vm12_5Kb.FormattedSizeText);

        // MB (fractional ~1.2 MB = 1258291 bytes)
        var vmMb = new ClipboardItemViewModel(new ClipboardPayload { MediaBytes = 1258291 });
        Assert.Equal("1.2 MB", vmMb.FormattedSizeText);

        // GB (~2.5 GB)
        var vmGb = new ClipboardItemViewModel(new ClipboardPayload { MediaBytes = 2684354560L });
        Assert.Equal("2.5 GB", vmGb.FormattedSizeText);
    }

    [Fact]
    public void ClipboardItemViewModel_ImageFormatText_FormatsWithOrWithoutSize()
    {
        // Without size
        var vmNoSize = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.Image,
            MediaBytes = null
        });
        Assert.Equal("PNG", vmNoSize.ImageFormatText);

        // With size (12.5 KB)
        var vmWithSize = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.Image,
            MediaBytes = 12800
        });
        Assert.Equal("PNG · 12.5 KB", vmWithSize.ImageFormatText);
    }

    [Fact]
    public void ClipboardItemViewModel_ThumbnailAndPreview_HandleHeadlessOrMissingGracefully()
    {
        // Non-image
        var textPayload = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Text,
            TextContent = "hello",
            MediaPath = Path.Combine(_tempDir, "missing.png")
        };
        using var textVm = new ClipboardItemViewModel(textPayload);
        Assert.Null(textVm.Thumbnail);
        Assert.Null(textVm.PreviewImage);

        // Missing file
        var missingImgPayload = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Image,
            MediaPath = Path.Combine(_tempDir, "missing.png")
        };
        using var missingImgVm = new ClipboardItemViewModel(missingImgPayload);
        Assert.Null(missingImgVm.Thumbnail);
        Assert.Null(missingImgVm.PreviewImage);

        // Existing dummy file in headless test (where Avalonia rendering engine is not initialized)
        var dummyPath = Path.Combine(_tempDir, "dummy.png");
        File.WriteAllBytes(dummyPath, new byte[] { 1, 2, 3, 4 });
        var dummyImgPayload = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Image,
            MediaPath = dummyPath
        };
        using var dummyImgVm = new ClipboardItemViewModel(dummyImgPayload);
        // Should catch exception and return null safely without throwing
        Assert.Null(dummyImgVm.Thumbnail);
        Assert.Null(dummyImgVm.PreviewImage);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_CopyImageAsync_CopiesPngAndUpdatesLastSeenImageHash()
    {
        var dbPath = Path.Combine(_tempDir, "copy_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);
        var clipboard = new FakeClipboard();
        vm.AttachClipboard(clipboard);

        var imgPath = Path.Combine(_tempDir, "test_clip.png");
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 10, 20, 30 };
        await File.WriteAllBytesAsync(imgPath, pngBytes);

        var payload = new ClipboardPayload
        {
            Id = "clip-img-1",
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 100×100",
            MediaPath = imgPath,
            MediaHash = "hash123456",
            Width = 100,
            Height = 100,
            MediaBytes = pngBytes.Length
        };
        await storage.SaveClipAsync(payload);
        await vm.ReloadAsync();

        var clipVm = Assert.Single(vm.FilteredClips);

        // Explicit clip passed to CopyImageAsync
        await vm.CopyImageCommand.ExecuteAsync(clipVm);

        Assert.Equal(pngBytes, clipboard.ImagePng);
        Assert.Equal("hash123456", vm.LastSeenImageHash);
        Assert.Equal("Image copied to clipboard.", vm.StatusMessage);

        // Clear clipboard and test fallback to SelectedClip
        clipboard.ImagePng = null;
        vm.SelectedClip = clipVm;
        await vm.CopyImageCommand.ExecuteAsync(null);

        Assert.Equal(pngBytes, clipboard.ImagePng);
        Assert.Equal("hash123456", vm.LastSeenImageHash);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_SaveImageAsAsync_CallsPickSavePathAndCopiesFile()
    {
        var dbPath = Path.Combine(_tempDir, "save_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);

        var sourceImgPath = Path.Combine(_tempDir, "source_img.png");
        var imgBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(sourceImgPath, imgBytes);

        var payload = new ClipboardPayload
        {
            Id = "clip-save-1",
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 200×150",
            MediaPath = sourceImgPath,
            MediaHash = "savehash999",
            Width = 200,
            Height = 150,
            MediaBytes = imgBytes.Length
        };
        await storage.SaveClipAsync(payload);
        await vm.ReloadAsync();
        var clipVm = Assert.Single(vm.FilteredClips);

        var targetPath = Path.Combine(_tempDir, "saved_output.png");
        Assert.False(File.Exists(targetPath));

        vm.PickSavePath = () => Task.FromResult<string?>(targetPath);

        await vm.SaveImageAsCommand.ExecuteAsync(clipVm);

        Assert.True(File.Exists(targetPath));
        var savedBytes = await File.ReadAllBytesAsync(targetPath);
        Assert.Equal(imgBytes, savedBytes);
        Assert.Equal($"Image saved to {Path.GetFileName(targetPath)}.", vm.StatusMessage);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_SaveImageAsAsync_Cancelled_DoesNotCopy()
    {
        var dbPath = Path.Combine(_tempDir, "save_cancel_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);

        var sourceImgPath = Path.Combine(_tempDir, "source_cancel.png");
        await File.WriteAllBytesAsync(sourceImgPath, new byte[] { 1, 2, 3 });

        var payload = new ClipboardPayload
        {
            Id = "clip-cancel",
            ContentType = ClipboardContentType.Image,
            MediaPath = sourceImgPath
        };
        var clipVm = new ClipboardItemViewModel(payload);

        // User cancels file dialog
        vm.PickSavePath = () => Task.FromResult<string?>(null);
        await vm.SaveImageAsCommand.ExecuteAsync(clipVm);

        // No status change
        Assert.Empty(vm.StatusMessage);
    }

    [Fact]
    public void ClipboardHistoryViewModel_OpenImage_InvokesOpener()
    {
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var storage = new ClipboardStorageService(Path.Combine(_tempDir, "open_test.db"));
        var vm = new ClipboardHistoryViewModel(storage, settings);

        var imgPath = Path.Combine(_tempDir, "open_target.png");
        File.WriteAllBytes(imgPath, new byte[] { 1, 2, 3 });

        var payload = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Image,
            MediaPath = imgPath
        };
        var clipVm = new ClipboardItemViewModel(payload);

        string? opened = null;
        vm.ImageOpener = path => opened = path;

        vm.OpenImageCommand.Execute(clipVm);

        Assert.Equal(imgPath, opened);
    }

    [Fact]
    public void ClipboardHistoryViewModel_ZoomCommands_ToggleFitAndStretch()
    {
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var storage = new ClipboardStorageService(Path.Combine(_tempDir, "zoom_test.db"));
        var vm = new ClipboardHistoryViewModel(storage, settings);

        // Default: fit to window
        Assert.True(vm.IsImageFitToWindow);
        Assert.Equal(Stretch.Uniform, vm.ImageStretch);

        // Toggle
        vm.ToggleImageZoomCommand.Execute(null);
        Assert.False(vm.IsImageFitToWindow);
        Assert.Equal(Stretch.None, vm.ImageStretch);

        // Toggle back
        vm.ToggleImageZoomCommand.Execute(null);
        Assert.True(vm.IsImageFitToWindow);
        Assert.Equal(Stretch.Uniform, vm.ImageStretch);

        // Actual size (100%)
        vm.SetImageActualSizeCommand.Execute(null);
        Assert.False(vm.IsImageFitToWindow);
        Assert.Equal(Stretch.None, vm.ImageStretch);

        // Fit
        vm.SetImageFitCommand.Execute(null);
        Assert.True(vm.IsImageFitToWindow);
        Assert.Equal(Stretch.Uniform, vm.ImageStretch);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_FilteringByImage_ShowsOnlyImageClips()
    {
        var dbPath = Path.Combine(_tempDir, "filter_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);

        // Add text clip
        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "clip-text",
            ContentType = ClipboardContentType.Text,
            TextContent = "Hello plain text"
        });

        // Add code clip
        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "clip-code",
            ContentType = ClipboardContentType.Code,
            TextContent = "function test() { return 1; }"
        });

        // Add link clip
        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "clip-link",
            ContentType = ClipboardContentType.Link,
            TextContent = "https://example.com"
        });

        // Add image clip
        var imgPath = Path.Combine(_tempDir, "img_clip.png");
        await File.WriteAllBytesAsync(imgPath, new byte[] { 1, 2, 3 });
        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "clip-img",
            ContentType = ClipboardContentType.Image,
            TextContent = "Image 640×480",
            MediaPath = imgPath,
            MediaHash = "hash640",
            Width = 640,
            Height = 480,
            MediaBytes = 3
        });

        // Initial reload: all types
        vm.SelectedFilter = "All types";
        await vm.ReloadAsync();
        Assert.Equal(4, vm.FilteredClips.Count);

        // Filter by Image
        vm.SelectedFilter = "Image";
        await vm.ReloadAsync();
        Assert.Single(vm.FilteredClips);
        var imageClip = vm.FilteredClips[0];
        Assert.True(imageClip.IsImage);
        Assert.Equal("clip-img", imageClip.Id);
        Assert.Equal(ClipboardContentType.Image, imageClip.Kind);

        // Filter by Text
        vm.SelectedFilter = "Text";
        await vm.ReloadAsync();
        Assert.Single(vm.FilteredClips);
        Assert.False(vm.FilteredClips[0].IsImage);
        Assert.Equal("clip-text", vm.FilteredClips[0].Id);
    }
}
