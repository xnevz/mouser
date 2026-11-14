using System.Runtime.Versioning;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;

namespace Mouser.Tests;

public class QuickPasteTests
{
    private static async Task<(AppFixture App, QuickPasteViewModel Paste)> SetupAsync()
    {
        var app = new AppFixture();
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();
        await history.CaptureAsync("ssh deploy@build-server");
        await Task.Delay(15);
        await history.CaptureAsync("Meeting moved to Thursday 3pm");
        await Task.Delay(15);
        await history.CaptureAsync("https://example.com/invoice/42");
        var paste = new QuickPasteViewModel(app.Storage);
        await paste.LoadAsync();
        return (app, paste);
    }

    [Fact]
    public async Task ShowsRecentClips_NewestFirst_WithTheFirstSelected()
    {
        var (app, paste) = await SetupAsync();
        await using var _ = app;

        Assert.Equal(3, paste.Items.Count);
        Assert.StartsWith("https://example.com", paste.Items[0].Content);
        Assert.Same(paste.Items[0], paste.Selected);
    }

    [Fact]
    public async Task StarredClips_ComeFirst()
    {
        var (app, paste) = await SetupAsync();
        await using var _ = app;
        var ssh = app.Main.ClipboardHistory.FilteredClips.Single(c => c.Content.StartsWith("ssh"));
        await app.Main.ClipboardHistory.ToggleStarCommand.ExecuteAsync(ssh);

        await paste.LoadAsync();

        Assert.StartsWith("ssh", paste.Items[0].Content);
        Assert.Equal(3, paste.Items.Count); // no duplicates
    }

    [Fact]
    public async Task Typing_FiltersWithFullTextSearch()
    {
        var (app, paste) = await SetupAsync();
        await using var _ = app;

        paste.Query = "thursday";
        await paste.LoadAsync();

        Assert.Contains("Thursday", Assert.Single(paste.Items).Content);
    }

    [Fact]
    public async Task ArrowKeysMoveTheSelection_AndEnterPastesIt()
    {
        var (app, paste) = await SetupAsync();
        await using var _ = app;
        string? pasted = null;
        bool? rich = null;
        paste.PasteRequested += (text, r) =>
        {
            pasted = text;
            rich = r;
        };

        paste.MoveSelection(1);
        paste.MoveSelection(5); // clamps at the last item
        paste.MoveSelection(-1);
        paste.PasteCommand.Execute(null);

        Assert.Equal("Meeting moved to Thursday 3pm", pasted);
        Assert.False(rich);
    }

    [Fact]
    public async Task QuickPaste_PasteSelected_PlainAndRich_RaisesEvent()
    {
        var (app, paste) = await SetupAsync();
        await using var _ = app;

        string? receivedText = null;
        bool? receivedRich = null;
        paste.PasteRequested += (text, rich) =>
        {
            receivedText = text;
            receivedRich = rich;
        };

        // Default / plain: PasteSelected(false)
        paste.PasteSelected(false);
        Assert.Equal(paste.Selected?.Content, receivedText);
        Assert.False(receivedRich);

        // Rich: PasteSelected(true)
        paste.PasteSelected(true);
        Assert.Equal(paste.Selected?.Content, receivedText);
        Assert.True(receivedRich);
    }

    [Fact]
    public async Task ClipboardHistory_Poll_CapturesHtmlContent_WhenPresent()
    {
        var app = new AppFixture();
        await using var _ = app;
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        // FakeClipboard with text and html
        await app.Clipboard.SetSnapshotAsync(new ClipboardSnapshot("Hello World", "<b>Hello World</b>", null, null));

        bool captured = await history.PollClipboardAsync();
        Assert.True(captured);

        var clip = Assert.Single(history.FilteredClips);
        Assert.Equal("Hello World", clip.Content);
        Assert.Equal("<b>Hello World</b>", clip.HtmlContent);
        Assert.True(clip.HasHtml);
    }

    [Fact]
    public async Task ClipboardHistory_CopyClip_CopiesPlainTextOnlyByDefault()
    {
        var app = new AppFixture();
        await using var _ = app;
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        // Capture a clip with HTML
        await history.CaptureAsync("Formatted snippet", html: "<p>Formatted snippet</p>");
        var clip = Assert.Single(history.FilteredClips);
        Assert.True(clip.HasHtml);

        // Clear clipboard first
        await app.Clipboard.SetSnapshotAsync(new ClipboardSnapshot(null, null, null, null));
        var initial = await app.Clipboard.GetSnapshotAsync();
        Assert.True(initial.IsEmpty);

        // Copy clip from history
        await history.CopyClipCommand.ExecuteAsync(clip);

        var snapshot = await app.Clipboard.GetSnapshotAsync();
        Assert.Equal("Formatted snippet", snapshot.Text);
        Assert.Null(snapshot.Html);
    }

    [Fact]
    public async Task CopyingFromHistory_IsNotCapturedOrSyncedAgain()
    {
        var (app, _) = await SetupAsync();
        await using var __ = app;
        int shared = 0;
        app.Main.ClipboardHistory.ShareCapturedText = _ => { shared++; return Task.FromResult<IReadOnlyList<ClipDelivery>>(Array.Empty<ClipDelivery>()); };

        await app.Main.ClipboardHistory.CopyTextAsync("ssh deploy@build-server");

        Assert.Equal("ssh deploy@build-server", app.Clipboard.Text);
        Assert.False(await app.Main.ClipboardHistory.PollClipboardAsync());
        Assert.Equal(0, shared);
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void Hotkey_IsExclusive_AndReleasedOnDispose()
    {
        var first = new QuickPasteHotkey();
        if (!first.IsRegistered)
        {
            first.Dispose();
            return; // a running Mouser (or another app) owns Win+Alt+V on this machine
        }

        using (var second = new QuickPasteHotkey()) Assert.False(second.IsRegistered);
        first.Dispose();
        using var third = new QuickPasteHotkey();
        Assert.True(third.IsRegistered);
    }
}
