using System;
using Mouser.Core.Clipboard;
using Xunit;

namespace Mouser.Tests;

public class SearchQueryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Parse_EmptyOrWhitespace_ReturnsEmptyTextAndNullFields(string? input)
    {
        var query = SearchQuery.Parse(input);

        Assert.Equal(string.Empty, query.Text);
        Assert.Null(query.Type);
        Assert.Null(query.App);
        Assert.Null(query.Device);
        Assert.Null(query.Tag);
        Assert.Null(query.Before);
        Assert.Null(query.After);
    }

    [Fact]
    public void Parse_PlainTextWithoutOperators_ReturnsText()
    {
        var query = SearchQuery.Parse("hello world from mouser");

        Assert.Equal("hello world from mouser", query.Text);
        Assert.Null(query.Type);
        Assert.Null(query.App);
        Assert.Null(query.Device);
        Assert.Null(query.Tag);
        Assert.Null(query.Before);
        Assert.Null(query.After);
    }

    [Fact]
    public void Parse_QuotedTextPreservation_StripsQuotesAndPreservesSpaces()
    {
        var queryDouble = SearchQuery.Parse("\"invoice 2026\"");
        Assert.Equal("invoice 2026", queryDouble.Text);
        Assert.Null(queryDouble.Type);

        var querySingle = SearchQuery.Parse("'invoice 2026'");
        Assert.Equal("invoice 2026", querySingle.Text);
        Assert.Null(querySingle.Type);

        var queryMultiple = SearchQuery.Parse("search \"invoice 2026\" 'second phrase'");
        Assert.Equal("search invoice 2026 second phrase", queryMultiple.Text);
    }

    [Theory]
    [InlineData("type:image", "image")]
    [InlineData("TYPE:IMAGE", "image")]
    [InlineData("type:text", "text")]
    [InlineData("type:link", "link")]
    [InlineData("type:code", "code")]
    [InlineData("type:file", "file")]
    [InlineData("type:folder", "folder")]
    [InlineData("Type:\"Code\"", "code")]
    public void Parse_TypeOperator_SetsTypeLowercased(string input, string expectedType)
    {
        var query = SearchQuery.Parse(input);

        Assert.Equal(expectedType, query.Type);
        Assert.Equal(string.Empty, query.Text);
    }

    [Theory]
    [InlineData("app:code", "code")]
    [InlineData("APP:chrome", "chrome")]
    [InlineData("app:\"Google Chrome\"", "Google Chrome")]
    [InlineData("app:'Visual Studio'", "Visual Studio")]
    public void Parse_AppOperator_SetsAppPreservingCase(string input, string expectedApp)
    {
        var query = SearchQuery.Parse(input);

        Assert.Equal(expectedApp, query.App);
        Assert.Equal(string.Empty, query.Text);
    }

    [Theory]
    [InlineData("device:desktop", "desktop")]
    [InlineData("device:\"My Laptop\"", "My Laptop")]
    [InlineData("@phone", "phone")]
    [InlineData("@Laptop", "Laptop")]
    [InlineData("@\"my laptop\"", "my laptop")]
    [InlineData("@'my laptop'", "my laptop")]
    public void Parse_DeviceOperator_SetsDevice(string input, string expectedDevice)
    {
        var query = SearchQuery.Parse(input);

        Assert.Equal(expectedDevice, query.Device);
        Assert.Equal(string.Empty, query.Text);
    }

    [Theory]
    [InlineData("#work", "work")]
    [InlineData("#Dev", "Dev")]
    [InlineData("#\"my tag\"", "my tag")]
    [InlineData("#'my tag'", "my tag")]
    public void Parse_TagOperator_SetsTag(string input, string expectedTag)
    {
        var query = SearchQuery.Parse(input);

        Assert.Equal(expectedTag, query.Tag);
        Assert.Equal(string.Empty, query.Text);
    }

    [Fact]
    public void Parse_BeforeAndAfterOperators_SetsDates()
    {
        var queryBefore = SearchQuery.Parse("before:2026-02-01");
        Assert.Equal(new DateTime(2026, 2, 1), queryBefore.Before);
        Assert.Null(queryBefore.After);
        Assert.Equal(string.Empty, queryBefore.Text);

        var queryAfter = SearchQuery.Parse("after:2026-01-01");
        Assert.Equal(new DateTime(2026, 1, 1), queryAfter.After);
        Assert.Null(queryAfter.Before);
        Assert.Equal(string.Empty, queryAfter.Text);

        var queryBoth = SearchQuery.Parse("before:2026-02-01 after:2026-01-01");
        Assert.Equal(new DateTime(2026, 2, 1), queryBoth.Before);
        Assert.Equal(new DateTime(2026, 1, 1), queryBoth.After);
        Assert.Equal(string.Empty, queryBoth.Text);
    }

    [Fact]
    public void Parse_OnOperator_SetsAfterToStartOfDayAndBeforeToNextDay()
    {
        var query = SearchQuery.Parse("on:2026-01-31");

        Assert.Equal(new DateTime(2026, 1, 31), query.After);
        Assert.Equal(new DateTime(2026, 2, 1), query.Before);
        Assert.Equal(string.Empty, query.Text);
    }

    [Fact]
    public void Parse_InvalidDate_KeptInText()
    {
        var queryBefore = SearchQuery.Parse("before:not-a-date");
        Assert.Null(queryBefore.Before);
        Assert.Equal("before:not-a-date", queryBefore.Text);

        var queryAfter = SearchQuery.Parse("after:invalid");
        Assert.Null(queryAfter.After);
        Assert.Equal("after:invalid", queryAfter.Text);

        var queryOn = SearchQuery.Parse("on:bad-date");
        Assert.Null(queryOn.After);
        Assert.Null(queryOn.Before);
        Assert.Equal("on:bad-date", queryOn.Text);
    }

    [Fact]
    public void Parse_UnknownOperator_KeptInText()
    {
        var query = SearchQuery.Parse("foo:bar");

        Assert.Equal("foo:bar", query.Text);
        Assert.Null(query.Type);
        Assert.Null(query.App);
        Assert.Null(query.Device);
        Assert.Null(query.Tag);
        Assert.Null(query.Before);
        Assert.Null(query.After);
    }

    [Fact]
    public void Parse_UnknownType_KeptInText()
    {
        var query = SearchQuery.Parse("type:banana");

        Assert.Equal("type:banana", query.Text);
        Assert.Null(query.Type);
    }

    [Fact]
    public void Parse_CombinedOperatorsWithFreeText_ParsesAllFields()
    {
        var query = SearchQuery.Parse("ssh @server #dev type:code on:2026-03-15");

        Assert.Equal("ssh", query.Text);
        Assert.Equal("server", query.Device);
        Assert.Equal("dev", query.Tag);
        Assert.Equal("code", query.Type);
        Assert.Equal(new DateTime(2026, 3, 15), query.After);
        Assert.Equal(new DateTime(2026, 3, 16), query.Before);
        Assert.Null(query.App);
    }

    [Fact]
    public async Task ViewModel_ReloadAsync_FiltersByType()
    {
        await using var app = new AppFixture();
        var vm = app.Main.ClipboardHistory;
        await vm.InitializeAsync();

        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "regular text", ContentType = ClipboardContentType.Text });
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "const int x = 10;", ContentType = ClipboardContentType.Code });
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "https://mouser.app", ContentType = ClipboardContentType.Link });
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "[image bytes]", ContentType = ClipboardContentType.Image });
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "C:\\docs\\file.txt", ContentType = ClipboardContentType.FileUri });

        vm.SearchQuery = "type:code";
        await vm.ReloadAsync();
        Assert.Equal(ClipboardContentType.Code, Assert.Single(vm.FilteredClips).Kind);

        vm.SearchQuery = "type:image";
        await vm.ReloadAsync();
        Assert.Equal(ClipboardContentType.Image, Assert.Single(vm.FilteredClips).Kind);

        vm.SearchQuery = "type:link";
        await vm.ReloadAsync();
        Assert.Equal(ClipboardContentType.Link, Assert.Single(vm.FilteredClips).Kind);

        vm.SearchQuery = "type:file";
        await vm.ReloadAsync();
        Assert.Equal(ClipboardContentType.FileUri, Assert.Single(vm.FilteredClips).Kind);

        vm.SearchQuery = "type:folder";
        await vm.ReloadAsync();
        Assert.Equal(ClipboardContentType.FileUri, Assert.Single(vm.FilteredClips).Kind);
    }

    [Fact]
    public async Task ViewModel_ReloadAsync_FiltersByDevice()
    {
        await using var app = new AppFixture();
        var vm = app.Main.ClipboardHistory;
        await vm.InitializeAsync();

        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "clip 1", SourceDevice = "WorkLaptop" });
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "clip 2", SourceDevice = "GamingDesktop" });

        vm.SearchQuery = "@WorkLaptop";
        await vm.ReloadAsync();
        var match = Assert.Single(vm.FilteredClips);
        Assert.Equal("WorkLaptop", match.SourceDevice);

        vm.SearchQuery = "device:GamingDesktop";
        await vm.ReloadAsync();
        match = Assert.Single(vm.FilteredClips);
        Assert.Equal("GamingDesktop", match.SourceDevice);
    }

    [Fact]
    public async Task ViewModel_ReloadAsync_FiltersByApp()
    {
        await using var app = new AppFixture();
        var vm = app.Main.ClipboardHistory;
        await vm.InitializeAsync();

        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "tab url", SourceApp = "Google Chrome" });
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "chat msg", SourceApp = "Slack Messenger" });

        vm.SearchQuery = "app:chrome";
        await vm.ReloadAsync();
        var match = Assert.Single(vm.FilteredClips);
        Assert.Equal("Google Chrome", match.SourceApp);

        vm.SearchQuery = "app:slack";
        await vm.ReloadAsync();
        match = Assert.Single(vm.FilteredClips);
        Assert.Equal("Slack Messenger", match.SourceApp);
    }

    [Fact]
    public async Task ViewModel_ReloadAsync_FiltersByTag()
    {
        await using var app = new AppFixture();
        var vm = app.Main.ClipboardHistory;
        await vm.InitializeAsync();

        var clip1 = new ClipboardPayload { TextContent = "project spec" };
        var clip2 = new ClipboardPayload { TextContent = "shopping receipt" };
        await app.Storage.SaveClipAsync(clip1);
        await app.Storage.SaveClipAsync(clip2);

        await app.Storage.AddTagAsync(clip1.Id, "work");
        await app.Storage.AddTagAsync(clip2.Id, "personal");

        vm.SearchQuery = "#work";
        await vm.ReloadAsync();
        var match = Assert.Single(vm.FilteredClips);
        Assert.Equal("project spec", match.Content);

        vm.SearchQuery = "#personal";
        await vm.ReloadAsync();
        match = Assert.Single(vm.FilteredClips);
        Assert.Equal("shopping receipt", match.Content);
    }

    [Fact]
    public async Task ViewModel_ReloadAsync_FiltersByDate()
    {
        await using var app = new AppFixture();
        var vm = app.Main.ClipboardHistory;
        await vm.InitializeAsync();

        var oldTime = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var midTime = new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var newTime = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "old clip", TimestampUtc = oldTime });
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "mid clip", TimestampUtc = midTime });
        await app.Storage.SaveClipAsync(new ClipboardPayload { TextContent = "new clip", TimestampUtc = newTime });

        vm.SearchQuery = "before:2026-02-01";
        await vm.ReloadAsync();
        Assert.Equal("old clip", Assert.Single(vm.FilteredClips).Content);

        vm.SearchQuery = "after:2026-03-01";
        await vm.ReloadAsync();
        Assert.Equal("new clip", Assert.Single(vm.FilteredClips).Content);

        vm.SearchQuery = "on:2026-02-10";
        await vm.ReloadAsync();
        Assert.Equal("mid clip", Assert.Single(vm.FilteredClips).Content);
    }

    [Fact]
    public async Task ViewModel_ReloadAsync_CombinesFreeTextAndOperators()
    {
        await using var app = new AppFixture();
        var vm = app.Main.ClipboardHistory;
        await vm.InitializeAsync();

        var target = new ClipboardPayload
        {
            TextContent = "docker run server",
            SourceDevice = "DevBox",
            SourceApp = "PowerShell"
        };
        var other = new ClipboardPayload
        {
            TextContent = "docker run local",
            SourceDevice = "Laptop",
            SourceApp = "PowerShell"
        };
        await app.Storage.SaveClipAsync(target);
        await app.Storage.SaveClipAsync(other);
        await app.Storage.AddTagAsync(target.Id, "infra");

        vm.SearchQuery = "docker @DevBox #infra app:powershell";
        await vm.ReloadAsync();
        var match = Assert.Single(vm.FilteredClips);
        Assert.Equal("docker run server", match.Content);
    }
}
