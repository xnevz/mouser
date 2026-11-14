using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Mouser.Core;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public sealed class ClipboardTagsTests : IDisposable
{
    private readonly string _tempDir;

    public ClipboardTagsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mouser_tags_test_" + Guid.NewGuid().ToString("N"));
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
    public async Task Storage_AddTag_And_GetClipTags_ReturnsAddedTag()
    {
        var dbPath = Path.Combine(_tempDir, "add_tag.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var payload = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Text,
            TextContent = "Hello World"
        };
        await storage.SaveClipAsync(payload);

        // Add custom tag
        var added = await storage.AddTagAsync(payload.Id, "project");
        Assert.NotNull(added);
        Assert.Equal("project", added.Name);
        Assert.Equal(1, added.Count);
        Assert.False(added.IsSystem);
        Assert.Contains(added.Color, ClipboardStorageService.TagPalette);

        // Add tag with leading #
        var addedHash = await storage.AddTagAsync(payload.Id, "#work");
        Assert.Equal("work", addedHash.Name);

        // Retrieve tags for this clip
        var clipTags = await storage.GetClipTagsAsync(payload.Id);
        Assert.Equal(2, clipTags.Count);
        Assert.Contains(clipTags, t => t.Name == "project" && !t.IsSystem);
        Assert.Contains(clipTags, t => t.Name == "work" && !t.IsSystem);
    }

    [Fact]
    public async Task Storage_RemoveTag_RemovesLink()
    {
        var dbPath = Path.Combine(_tempDir, "remove_tag.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var clip1 = new ClipboardPayload { ContentType = ClipboardContentType.Text, TextContent = "Clip 1" };
        var clip2 = new ClipboardPayload { ContentType = ClipboardContentType.Text, TextContent = "Clip 2" };
        await storage.SaveClipAsync(clip1);
        await storage.SaveClipAsync(clip2);

        await storage.AddTagAsync(clip1.Id, "urgent");
        await storage.AddTagAsync(clip2.Id, "urgent");

        var tags1Before = await storage.GetClipTagsAsync(clip1.Id);
        Assert.Contains(tags1Before, t => t.Name == "urgent");

        // Remove tag from clip1 using name
        await storage.RemoveTagAsync(clip1.Id, "urgent");

        var tags1After = await storage.GetClipTagsAsync(clip1.Id);
        Assert.DoesNotContain(tags1After, t => t.Name == "urgent");

        // clip2 should still have the tag
        var tags2 = await storage.GetClipTagsAsync(clip2.Id);
        Assert.Contains(tags2, t => t.Name == "urgent");
    }

    [Fact]
    public async Task Storage_GetTags_IncludesDerivedSystemTags()
    {
        var dbPath = Path.Combine(_tempDir, "sys_tags.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        // 2 Links
        await storage.SaveClipAsync(new ClipboardPayload { ContentType = ClipboardContentType.Link, TextContent = "https://example.com/1" });
        await storage.SaveClipAsync(new ClipboardPayload { ContentType = ClipboardContentType.Link, TextContent = "https://example.com/2" });

        // 1 Code
        await storage.SaveClipAsync(new ClipboardPayload { ContentType = ClipboardContentType.Code, TextContent = "int x = 42;" });

        // 0 Images

        // 3 Files
        await storage.SaveClipAsync(new ClipboardPayload { ContentType = ClipboardContentType.FileUri, TextContent = "C:\\file1.txt" });
        await storage.SaveClipAsync(new ClipboardPayload { ContentType = ClipboardContentType.FileUri, TextContent = "C:\\file2.txt" });
        await storage.SaveClipAsync(new ClipboardPayload { ContentType = ClipboardContentType.FileUri, TextContent = "C:\\file3.txt" });

        var tags = await storage.GetTagsAsync();

        var linkTag = tags.First(t => t.Name == "Link");
        Assert.Equal(2, linkTag.Count);
        Assert.True(linkTag.IsSystem);

        var codeTag = tags.First(t => t.Name == "Code");
        Assert.Equal(1, codeTag.Count);
        Assert.True(codeTag.IsSystem);

        var imageTag = tags.First(t => t.Name == "Image");
        Assert.Equal(0, imageTag.Count);
        Assert.True(imageTag.IsSystem);

        var fileTag = tags.First(t => t.Name == "File");
        Assert.Equal(3, fileTag.Count);
        Assert.True(fileTag.IsSystem);
    }

    [Fact]
    public async Task Storage_DeleteClip_CascadesAndCleansClipTags()
    {
        var dbPath = Path.Combine(_tempDir, "delete_clip_tags.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var payload = new ClipboardPayload
        {
            ContentType = ClipboardContentType.Text,
            TextContent = "Will be deleted"
        };
        await storage.SaveClipAsync(payload);
        await storage.AddTagAsync(payload.Id, "temporary");

        var tagsBefore = await storage.GetClipTagsAsync(payload.Id);
        Assert.Single(tagsBefore);

        // Delete clip
        await storage.DeleteClipAsync(payload.Id);

        // Check GetClipTagsAsync returns empty
        var tagsAfter = await storage.GetClipTagsAsync(payload.Id);
        Assert.Empty(tagsAfter);

        // Directly query SQLite to verify no orphaned clip_tags row
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM clip_tags WHERE clip_id = @id;";
        cmd.Parameters.AddWithValue("@id", payload.Id);
        var remainingCount = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, remainingCount);
    }

    [Fact]
    public async Task FilterByTag_ShowsOnlyClipsWithTag()
    {
        var dbPath = Path.Combine(_tempDir, "filter_vm.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);

        var clip1 = new ClipboardPayload { ContentType = ClipboardContentType.Text, TextContent = "Clip one" };
        var clip2 = new ClipboardPayload { ContentType = ClipboardContentType.Text, TextContent = "Clip two" };
        var clip3 = new ClipboardPayload { ContentType = ClipboardContentType.Link, TextContent = "https://github.com" };

        await storage.SaveClipAsync(clip1);
        await storage.SaveClipAsync(clip2);
        await storage.SaveClipAsync(clip3);

        await storage.AddTagAsync(clip1.Id, "alpha");
        await storage.AddTagAsync(clip2.Id, "beta");

        await vm.ReloadAsync();
        Assert.Equal(3, vm.FilteredClips.Count);

        // Filter by custom tag "alpha"
        vm.SetTag("alpha");
        await vm.ReloadAsync();
        Assert.Single(vm.FilteredClips);
        Assert.Equal("Clip one", vm.FilteredClips[0].Content);

        // Filter by custom tag "beta"
        vm.SetTag("beta");
        await vm.ReloadAsync();
        Assert.Single(vm.FilteredClips);
        Assert.Equal("Clip two", vm.FilteredClips[0].Content);

        // Filter by system tag "Link"
        vm.SetTag("Link");
        await vm.ReloadAsync();
        Assert.Single(vm.FilteredClips);
        Assert.Equal("https://github.com", vm.FilteredClips[0].Content);

        // Clear tag filter
        vm.ClearTag();
        await vm.ReloadAsync();
        Assert.Equal(3, vm.FilteredClips.Count);
    }

    [Fact]
    public async Task SearchByHashTag_FiltersClips()
    {
        var dbPath = Path.Combine(_tempDir, "search_hashtag.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var vm = new ClipboardHistoryViewModel(storage, settings);

        var clipA = new ClipboardPayload { ContentType = ClipboardContentType.Text, TextContent = "Task A notes" };
        var clipB = new ClipboardPayload { ContentType = ClipboardContentType.Text, TextContent = "Task B notes" };
        await storage.SaveClipAsync(clipA);
        await storage.SaveClipAsync(clipB);

        await storage.AddTagAsync(clipA.Id, "work");
        await storage.AddTagAsync(clipB.Id, "home");

        await vm.ReloadAsync();
        Assert.Equal(2, vm.FilteredClips.Count);

        // Search with #work
        vm.SearchQuery = "#work";
        await vm.ReloadAsync();
        Assert.Single(vm.FilteredClips);
        Assert.Equal("Task A notes", vm.FilteredClips[0].Content);

        // Search with #home
        vm.SearchQuery = "#home";
        await vm.ReloadAsync();
        Assert.Single(vm.FilteredClips);
        Assert.Equal("Task B notes", vm.FilteredClips[0].Content);

        // Empty search query
        vm.SearchQuery = string.Empty;
        await vm.ReloadAsync();
        Assert.Equal(2, vm.FilteredClips.Count);
    }

    [Fact]
    public async Task Storage_AddTag_EmptyOrWhitespace_ThrowsArgumentException()
    {
        var dbPath = Path.Combine(_tempDir, "invalid_tag.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => storage.AddTagAsync("clip1", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.AddTagAsync("clip1", "   "));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.AddTagAsync("clip1", "#"));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.AddTagAsync("clip1", "  ###  "));
    }

    [Fact]
    public async Task Storage_DeleteTagAsync_RemovesTagAndLinks()
    {
        var dbPath = Path.Combine(_tempDir, "delete_tag.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var clip = new ClipboardPayload { ContentType = ClipboardContentType.Text, TextContent = "Test" };
        await storage.SaveClipAsync(clip);
        await storage.AddTagAsync(clip.Id, "deprecated");

        var tagsBefore = await storage.GetClipTagsAsync(clip.Id);
        Assert.Contains(tagsBefore, t => t.Name == "deprecated");

        await storage.DeleteTagAsync("deprecated");

        var tagsAfter = await storage.GetClipTagsAsync(clip.Id);
        Assert.DoesNotContain(tagsAfter, t => t.Name == "deprecated");
    }

    [Fact]
    public void TagPalette_GetTagColor_IsDeterministic()
    {
        var color1 = ClipboardStorageService.GetTagColor("Work");
        var color2 = ClipboardStorageService.GetTagColor("work");
        var color3 = ClipboardStorageService.GetTagColor("  #work  ");

        Assert.Equal(color1, color2);
        Assert.Equal(color1, color3);
        Assert.Contains(color1, ClipboardStorageService.TagPalette);
    }

    [Fact]
    public async Task MainViewModel_TagsIntegration_RefreshesAndFilters()
    {
        var dbPath = Path.Combine(_tempDir, "mainvm_tags.db");
        await using var storage = new ClipboardStorageService(dbPath);
        await storage.InitializeAsync();

        var settings = new SettingsStore(Path.Combine(_tempDir, "settings.json"));
        var sink = new RecordingSink();
        using var host = new MouserHost(settings, sink, discoveryPort: Random.Shared.Next(40_000, 50_000), downloadDirectory: _tempDir);
        var mainVm = new MainViewModel(host, settings, storage);

        var clip = new ClipboardPayload { ContentType = ClipboardContentType.Link, TextContent = "https://github.com/mouser" };
        await storage.SaveClipAsync(clip);
        await storage.AddTagAsync(clip.Id, "devtools");

        await mainVm.RefreshTagsAsync();

        // Should include system tag "Link" (count 1) and custom tag "devtools" (count 1)
        Assert.Contains(mainVm.Tags, t => t.Name == "Link" && t.Count == 1 && t.IsSystem);
        Assert.Contains(mainVm.Tags, t => t.Name == "devtools" && t.Count == 1 && !t.IsSystem);

        // FilterByTag command
        var devtoolsTag = mainVm.Tags.First(t => t.Name == "devtools");
        mainVm.FilterByTagCommand.Execute(devtoolsTag);

        Assert.Equal("devtools", mainVm.ClipboardHistory.SelectedTag);
        Assert.Equal(NavigationTab.SmartClipboard, mainVm.CurrentTab);

        // ShowLibrary clears active tag
        mainVm.ShowLibraryCommand.Execute("All");
        Assert.Null(mainVm.ClipboardHistory.SelectedTag);
    }
}
