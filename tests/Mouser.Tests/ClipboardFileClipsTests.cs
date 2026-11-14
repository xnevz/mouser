using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public sealed class ClipboardFileClipsTests : IDisposable
{
    private readonly string _tempDir;

    public ClipboardFileClipsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mouser_file_clips_test_" + Guid.NewGuid().ToString("N"));
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
    public void ClipboardItemViewModel_IsFileClip_And_FilePaths_Parsing()
    {
        var file1 = Path.Combine(_tempDir, "doc.txt");
        var file2 = Path.Combine(_tempDir, "sheet.xlsx");
        var content = $"{file1}\r\n{file2}";

        var payload = new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = content
        };
        var vm = new ClipboardItemViewModel(payload);

        Assert.True(vm.IsFileClip);
        Assert.True(vm.HasFiles);
        Assert.Equal(2, vm.FilePaths.Count);
        Assert.Equal(file1, vm.FilePaths[0]);
        Assert.Equal(file2, vm.FilePaths[1]);

        // Non-file clip
        var textPayload = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Text,
            TextContent = content
        };
        var textVm = new ClipboardItemViewModel(textPayload);
        Assert.False(textVm.IsFileClip);
        Assert.False(textVm.HasFiles);
        Assert.Empty(textVm.FilePaths);

        // Empty file clip
        var emptyPayload = new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = ""
        };
        var emptyVm = new ClipboardItemViewModel(emptyPayload);
        Assert.True(emptyVm.IsFileClip);
        Assert.False(emptyVm.HasFiles);
        Assert.Empty(emptyVm.FilePaths);
    }

    [Fact]
    public void ClipboardItemViewModel_FileTitle_And_DisplayTitle_Formatting()
    {
        var file1 = Path.Combine(_tempDir, "report.pdf");
        var file2 = Path.Combine(_tempDir, "image.png");
        var file3 = Path.Combine(_tempDir, "data.csv");

        // Single file
        var vm1 = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = file1
        });
        Assert.Equal("report.pdf", vm1.FileTitle);
        Assert.Equal("report.pdf", vm1.DisplayTitle);
        Assert.Equal("report.pdf", vm1.PreviewTitle);

        // Two files
        var vm2 = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = $"{file1}\n{file2}"
        });
        Assert.Equal("report.pdf and 1 more", vm2.FileTitle);
        Assert.Equal("report.pdf and 1 more", vm2.DisplayTitle);
        Assert.Equal("report.pdf and 1 more", vm2.PreviewTitle);

        // Three files
        var vm3 = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = $"{file1}\n{file2}\n{file3}"
        });
        Assert.Equal("report.pdf and 2 more", vm3.FileTitle);
        Assert.Equal("report.pdf and 2 more", vm3.DisplayTitle);

        // Root path
        var root = OperatingSystem.IsWindows() ? @"C:\" : "/";
        var vmRoot = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = root
        });
        Assert.Equal(root, vmRoot.FileTitle);
    }

    [Fact]
    public void ClipboardItemViewModel_IsDirectoryOnly_And_BadgeText()
    {
        var subDir = Path.Combine(_tempDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        var tempFile = Path.Combine(_tempDir, "file.txt");
        File.WriteAllText(tempFile, "hello");

        // Folder only
        var folderVm = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = subDir
        });
        Assert.True(folderVm.IsDirectoryOnly);
        Assert.Equal("FOLDER", folderVm.FileBadgeText);
        Assert.Equal("FOLDER", folderVm.BadgeText);
        Assert.Equal("FOLDER", folderVm.TypeIcon);

        // File only
        var fileVm = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = tempFile
        });
        Assert.False(fileVm.IsDirectoryOnly);
        Assert.Equal("FILE", fileVm.FileBadgeText);
        Assert.Equal("FILE", fileVm.BadgeText);
        Assert.Equal("FILE", fileVm.TypeIcon);

        // Mixed folder and file
        var mixedVm = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = $"{subDir}\n{tempFile}"
        });
        Assert.False(mixedVm.IsDirectoryOnly);
        Assert.Equal("FILE", mixedVm.FileBadgeText);
        Assert.Equal("FILE", mixedVm.BadgeText);
    }

    [Fact]
    public void ClipboardItemViewModel_FileClipItems_MapsCorrectly()
    {
        var subDir = Path.Combine(_tempDir, "TestDir");
        Directory.CreateDirectory(subDir);

        var testFile = Path.Combine(_tempDir, "test.bin");
        // 12.5 KB = 12 * 1024 + 512 = 12800 bytes
        var bytes = new byte[12800];
        File.WriteAllBytes(testFile, bytes);

        var missingPath = Path.Combine(_tempDir, "non_existent.dat");

        var vm = new ClipboardItemViewModel(new ClipboardPayload
        {
            ContentType = ClipboardContentType.FileUri,
            TextContent = $"{subDir}\n{testFile}\n{missingPath}"
        });

        var items = vm.FileClipItems;
        Assert.Equal(3, items.Count);

        // Directory
        Assert.Equal(subDir, items[0].Path);
        Assert.Equal("TestDir", items[0].Name);
        Assert.Equal("Folder", items[0].SizeOrType);
        Assert.True(items[0].Exists);
        Assert.True(items[0].IsDirectory);
        Assert.Equal("📁", items[0].Icon);

        // File with size
        Assert.Equal(testFile, items[1].Path);
        Assert.Equal("test.bin", items[1].Name);
        Assert.Equal("12.5 KB", items[1].SizeOrType);
        Assert.True(items[1].Exists);
        Assert.False(items[1].IsDirectory);
        Assert.Equal("📄", items[1].Icon);

        // Missing file
        Assert.Equal(missingPath, items[2].Path);
        Assert.Equal("non_existent.dat", items[2].Name);
        Assert.Equal("Missing", items[2].SizeOrType);
        Assert.False(items[2].Exists);
        Assert.False(items[2].IsDirectory);
        Assert.Equal("⚠️", items[2].Icon);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_PollClipboardAsync_CapturesFilesSnapshot_And_AvoidsDuplicates()
    {
        var dbPath = Path.Combine(_tempDir, "poll_files_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);
        var clipboard = new FakeClipboard();
        vm.AttachClipboard(clipboard);

        var file1 = Path.Combine(_tempDir, "a.txt");
        var file2 = Path.Combine(_tempDir, "b.txt");
        await File.WriteAllTextAsync(file1, "A");
        await File.WriteAllTextAsync(file2, "B");

        clipboard.Files = new[] { file1, file2 };

        // First poll: should capture
        bool captured = await vm.PollClipboardAsync();
        Assert.True(captured);
        Assert.Single(vm.FilteredClips);

        var clip = vm.FilteredClips[0];
        Assert.True(clip.IsFileClip);
        Assert.Equal(ClipboardContentType.FileUri, clip.Kind);
        Assert.Equal(2, clip.FilePaths.Count);
        Assert.Equal($"{file1}\n{file2}", vm.LastSeenFilesText);

        // Second poll without changes: should return false (deduplicated)
        bool capturedAgain = await vm.PollClipboardAsync();
        Assert.False(capturedAgain);
        Assert.Single(vm.FilteredClips);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_PollClipboardAsync_RespectsPrivacyFilter()
    {
        var dbPath = Path.Combine(_tempDir, "privacy_files_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings)
        {
            AppProbe = () => ("keepass", "KeePass Password Safe")
        };
        var clipboard = new FakeClipboard();
        vm.AttachClipboard(clipboard);

        var file = Path.Combine(_tempDir, "secret.kdbx");
        await File.WriteAllTextAsync(file, "secret");
        clipboard.Files = new[] { file };

        bool captured = await vm.PollClipboardAsync();
        Assert.False(captured);
        Assert.Contains("password manager", vm.StatusMessage);
        Assert.Empty(vm.FilteredClips);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_CopyFilesAsync_SetsSnapshotOnClipboard()
    {
        var dbPath = Path.Combine(_tempDir, "copy_files_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);
        var clipboard = new FakeClipboard();
        vm.AttachClipboard(clipboard);

        var file1 = Path.Combine(_tempDir, "one.txt");
        var file2 = Path.Combine(_tempDir, "two.txt");
        await File.WriteAllTextAsync(file1, "1");
        await File.WriteAllTextAsync(file2, "2");

        var payload = new ClipboardPayload
        {
            Id = "clip-files-1",
            ContentType = ClipboardContentType.FileUri,
            TextContent = $"{file1}\n{file2}"
        };
        await storage.SaveClipAsync(payload);
        await vm.ReloadAsync();

        var clipVm = Assert.Single(vm.FilteredClips);

        // Test CopyFilesAsync directly
        await vm.CopyFilesCommand.ExecuteAsync(clipVm);

        Assert.NotNull(clipboard.Files);
        Assert.Equal(2, clipboard.Files.Count);
        Assert.Equal(file1, clipboard.Files[0]);
        Assert.Equal(file2, clipboard.Files[1]);
        Assert.Equal(clipVm.Content, vm.LastSeenFilesText);
        Assert.Equal("2 files copied to clipboard.", vm.StatusMessage);

        // Test CopyClipAsync delegates to CopyFilesAsync
        clipboard.Files = null;
        await vm.CopyClipCommand.ExecuteAsync(clipVm);
        Assert.NotNull(clipboard.Files);
        Assert.Equal(2, clipboard.Files.Count);

        // Test single file copy status message
        var singleFile = Path.Combine(_tempDir, "single.txt");
        await File.WriteAllTextAsync(singleFile, "single");
        var singlePayload = new ClipboardPayload
        {
            Id = "clip-single-1",
            ContentType = ClipboardContentType.FileUri,
            TextContent = singleFile
        };
        await storage.SaveClipAsync(singlePayload);
        await vm.ReloadAsync();

        var singleVm = vm.FilteredClips.First(c => c.Id == "clip-single-1");
        await vm.CopyFilesCommand.ExecuteAsync(singleVm);
        Assert.Equal("File copied to clipboard.", vm.StatusMessage);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_SendFileToPeerAsync_CallsSendFileToPeerAction()
    {
        var dbPath = Path.Combine(_tempDir, "send_files_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);

        var file1 = Path.Combine(_tempDir, "package.zip");
        var file2 = Path.Combine(_tempDir, "readme.txt");
        await File.WriteAllTextAsync(file1, "zip");
        await File.WriteAllTextAsync(file2, "read");

        var payload = new ClipboardPayload
        {
            Id = "clip-send-1",
            ContentType = ClipboardContentType.FileUri,
            TextContent = $"{file1}\n{file2}"
        };
        await storage.SaveClipAsync(payload);
        await vm.ReloadAsync();

        var clipVm = Assert.Single(vm.FilteredClips);

        string? receivedPath = null;
        vm.SendFileToPeerAction = path =>
        {
            receivedPath = path;
            return Task.CompletedTask;
        };

        await vm.SendFileToPeerCommand.ExecuteAsync(clipVm);

        Assert.Equal(file1, receivedPath);
        Assert.Equal("Sending package.zip to peer…", vm.StatusMessage);

        // Test when SendFileToPeerAction is null
        vm.SendFileToPeerAction = null;
        await vm.SendFileToPeerCommand.ExecuteAsync(clipVm);
        Assert.Equal("No connected computer found to receive files.", vm.StatusMessage);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_SelectedFilter_Files_FiltersCorrectly()
    {
        var dbPath = Path.Combine(_tempDir, "filter_files_test.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();
        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);

        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "txt-1",
            ContentType = ClipboardContentType.Text,
            TextContent = "Hello World"
        });
        await storage.SaveClipAsync(new ClipboardPayload
        {
            Id = "file-1",
            ContentType = ClipboardContentType.FileUri,
            TextContent = Path.Combine(_tempDir, "a.txt")
        });

        await vm.ReloadAsync();
        Assert.Equal(2, vm.FilteredClips.Count);

        vm.SelectedFilter = "Files";
        await vm.ReloadAsync();
        Assert.Single(vm.FilteredClips);
        Assert.Equal("file-1", vm.FilteredClips[0].Id);
    }

    [Fact]
    public async Task MainViewModel_Wires_SendFileToPeerAction()
    {
        await using var fixture = new AppFixture();
        var mainVm = new MainViewModel(fixture.Host, fixture.Settings, fixture.Storage);
        Assert.NotNull(mainVm.ClipboardHistory.SendFileToPeerAction);
    }
}
