using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mouser.Core.Clipboard;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public sealed class ClipboardPrivacyFilterTests
{
    [Theory]
    [InlineData("KeePass", "keepass", true)]
    [InlineData("keepassxc.exe", "KeePassXC", true)]
    [InlineData("1Password.exe", "1password.exe", true)]
    [InlineData("Bitwarden", "bitwarden.exe", true)]
    [InlineData("LastPass", "LASTPASS", true)]
    [InlineData("dashlane", "Dashlane", true)]
    [InlineData("enpass.exe", "enpass", true)]
    [InlineData("notepad", "keepass", false)]
    [InlineData("notepad.exe", "keepassxc", false)]
    [InlineData("", "keepass", false)]
    [InlineData("   ", "keepass", false)]
    [InlineData(null, "keepass", false)]
    public void IsIgnoredApp_MatchesProcessName_CaseInsensitive(string? app, string ignoredItem, bool expected)
    {
        var list = new List<string> { ignoredItem };
        bool result = ClipboardPrivacyFilter.IsIgnoredApp(app, list);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsIgnoredApp_NullOrEmptyIgnoredList_ReturnsFalse()
    {
        Assert.False(ClipboardPrivacyFilter.IsIgnoredApp("keepass", null));
        Assert.False(ClipboardPrivacyFilter.IsIgnoredApp("keepass", Enumerable.Empty<string>()));
        Assert.False(ClipboardPrivacyFilter.IsIgnoredApp("keepass", new List<string> { "", "   " }));
    }

    [Fact]
    public async Task PollClipboardAsync_WhenAppIgnored_SkipsCaptureAndSetsStatus()
    {
        await using var app = new AppFixture();
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        bool shareCalled = false;
        history.ShareCapturedText = _ =>
        {
            shareCalled = true;
            return Task.CompletedTask;
        };

        // App is KeePass, which is in the default IgnoredApps list
        history.AppProbe = () => ("KeePass", "KeePass Password Safe");
        app.Clipboard.Text = "SuperSecretPassword123!";

        bool captured = await history.PollClipboardAsync();

        Assert.False(captured);
        Assert.Empty(history.FilteredClips);
        Assert.False(shareCalled);
        Assert.Equal("Skipped a copy from a password manager.", history.StatusMessage);
    }

    [Fact]
    public async Task PollClipboardAsync_WhenExcludedByOwner_SkipsCaptureAndSetsStatus()
    {
        await using var app = new AppFixture();
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        bool shareCalled = false;
        history.ShareCapturedText = _ =>
        {
            shareCalled = true;
            return Task.CompletedTask;
        };

        // App is not in ignored list, but owner marked content as excluded
        history.AppProbe = () => ("notepad", "Untitled - Notepad");
        history.ExcludedByOwnerProbe = () => true;
        app.Clipboard.Text = "MasterVaultSecretKey";

        bool captured = await history.PollClipboardAsync();

        Assert.False(captured);
        Assert.Empty(history.FilteredClips);
        Assert.False(shareCalled);
        Assert.Equal("Skipped a copy from a password manager.", history.StatusMessage);
    }

    [Fact]
    public async Task PollClipboardAsync_WhenNotIgnoredAndNotExcluded_CapturesAndShares()
    {
        await using var app = new AppFixture();
        var history = app.Main.ClipboardHistory;
        await history.InitializeAsync();

        string? sharedText = null;
        history.ShareCapturedText = t =>
        {
            sharedText = t;
            return Task.CompletedTask;
        };

        history.AppProbe = () => ("code", "Visual Studio Code");
        history.ExcludedByOwnerProbe = () => false;
        app.Clipboard.Text = "public record Point(int X, int Y);";

        bool captured = await history.PollClipboardAsync();

        Assert.True(captured);
        Assert.Single(history.FilteredClips);
        Assert.Equal("public record Point(int X, int Y);", sharedText);
    }

    [Fact]
    public void Settings_IgnoredAppsText_UpdatesListAndPersists()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"mouser_st_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string settingsFile = Path.Combine(tempDir, "settings.json");

        try
        {
            var store = new SettingsStore(settingsFile);
            var vm = new SettingsViewModel(store, new MouserHost(store, new RecordingSink(), null, 0, tempDir));

            // Verify default ignored apps are populated in IgnoredAppsText
            Assert.Contains("keepass", vm.IgnoredAppsText);
            Assert.Contains("1password", vm.IgnoredAppsText);
            Assert.Contains("bitwarden", vm.IgnoredAppsText);

            // Update IgnoredAppsText with custom whitespace, uppercase, duplicates, and .exe extensions
            vm.IgnoredAppsText = "CustomVault\r\nKeePass\n  customvault  \n\n1Password.exe\n";

            // Verify normalized, lowercased, and distinct list in store.Current.IgnoredApps
            Assert.Equal(new[] { "customvault", "keepass", "1password.exe" }, store.Current.IgnoredApps);

            // Reload from disk to verify persistence
            var storeReloaded = new SettingsStore(settingsFile);
            Assert.Equal(new[] { "customvault", "keepass", "1password.exe" }, storeReloaded.Current.IgnoredApps);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void ClipboardFormats_IsExcludedByOwner_SafeExecution()
    {
        // Must never throw an exception, returning bool
        bool result = ClipboardFormats.IsExcludedByOwner();
        // Since we are running in tests (not copying from KeePass), result will be a valid boolean
        Assert.True(result == true || result == false);
    }
}
