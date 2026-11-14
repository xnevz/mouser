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
            return Task.FromResult<IReadOnlyList<ClipDelivery>>(Array.Empty<ClipDelivery>());
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
            return Task.FromResult<IReadOnlyList<ClipDelivery>>(Array.Empty<ClipDelivery>());
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
            return Task.FromResult<IReadOnlyList<ClipDelivery>>(Array.Empty<ClipDelivery>());
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

    #region D17 New Secret Detectors Tests

    [Fact]
    public void IsSensitive_DetectsJwtTokens()
    {
        string validJwt = "ey" + "JhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
                          "ey" + "JzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIn0." +
                          "SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        Assert.True(ClipboardPrivacyFilter.IsSensitive(validJwt));
        Assert.True(ClipboardPrivacyFilter.IsSensitive("Bearer " + validJwt));
    }

    [Fact]
    public void IsSensitive_RejectsNonJwtStrings()
    {
        string twoPart = "Token was ey" + "JhbGciOiJIUzI1NiJ9.onlytwoparts";
        Assert.False(ClipboardPrivacyFilter.IsSensitive(twoPart));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("notajwt.something.payload"));
    }

    [Fact]
    public void IsSensitive_DetectsSlackTokens()
    {
        string slackBot = "xo" + "xb-123456789012-1234567890123-" + "456789abcdef";
        string slackUser = "xo" + "xp-123456789012-1234567890123-" + "456789abcdef";
        string slackApp = "xo" + "xa-123456789012-1234567890123-" + "456789abcdef";

        Assert.True(ClipboardPrivacyFilter.IsSensitive(slackBot));
        Assert.True(ClipboardPrivacyFilter.IsSensitive(slackUser));
        Assert.True(ClipboardPrivacyFilter.IsSensitive(slackApp));
    }

    [Fact]
    public void IsSensitive_RejectsNonSlackTokens()
    {
        Assert.False(ClipboardPrivacyFilter.IsSensitive("xoxo-gossip-girl-chat"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("xo" + "xb-short"));
    }

    [Fact]
    public void IsSensitive_DetectsGoogleApiKeys()
    {
        string googleKey = "AI" + "zaSy" + new string('b', 33);
        Assert.True(ClipboardPrivacyFilter.IsSensitive(googleKey));
        Assert.True(ClipboardPrivacyFilter.IsSensitive("https://maps.googleapis.com/maps/api/geocode/json?key=" + googleKey));
    }

    [Fact]
    public void IsSensitive_RejectsNonGoogleApiKeys()
    {
        Assert.False(ClipboardPrivacyFilter.IsSensitive("AI" + "za" + "Short123"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("Key value was AI" + "za" + "Short123456789"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("Searching for AIza in code"));
    }

    [Fact]
    public void IsSensitive_DetectsAzureStorageKeys()
    {
        string azureKey = "Account" + "Key=" + "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6A1B2C3D4E5F6==";
        Assert.True(ClipboardPrivacyFilter.IsSensitive(azureKey));
        Assert.True(ClipboardPrivacyFilter.IsSensitive("DefaultEndpointsProtocol=https;AccountName=test;" + azureKey + ";EndpointSuffix=core.windows.net"));
    }

    [Fact]
    public void IsSensitive_RejectsNonAzureStorageKeys()
    {
        Assert.False(ClipboardPrivacyFilter.IsSensitive("Account" + "Key=" + "shortKey"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("AccountName=mystorageaccount"));
    }

    [Fact]
    public void IsSensitive_DetectsDotEnvSecretLines()
    {
        string envApiKey = "MY_" + "API_" + "KEY" + "=" + "super_secret_value_123";
        string envDbPass = "DATABASE_" + "PASSWORD" + " = \"" + "mySecretPasswd456" + "\"";
        string envAuthToken = "AUTH_" + "TOKEN" + " = 'jwt_tok_123456789'";
        string envSecret = "CLIENT_" + "SECRET" + " = " + "9876543210abcdef";

        Assert.True(ClipboardPrivacyFilter.IsSensitive(envApiKey));
        Assert.True(ClipboardPrivacyFilter.IsSensitive(envDbPass));
        Assert.True(ClipboardPrivacyFilter.IsSensitive(envAuthToken));
        Assert.True(ClipboardPrivacyFilter.IsSensitive(envSecret));
    }

    [Fact]
    public void IsSensitive_RejectsNonDotEnvSecrets()
    {
        Assert.False(ClipboardPrivacyFilter.IsSensitive("API_" + "KEY=" + "short"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("SERVER_PORT=8080"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("DATABASE_HOST=localhost"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("echo MY_SECRET=12345678"));
    }

    [Fact]
    public void IsSensitive_DetectsValidIban()
    {
        string deIban = "DE" + "89" + "370400440532013000";
        string deIbanFormatted = "DE" + "89 3704 0044 0532 0130 00";
        string gbIban = "GB" + "82" + "WEST12345698765432";

        Assert.True(ClipboardPrivacyFilter.IsSensitive(deIban));
        Assert.True(ClipboardPrivacyFilter.IsSensitive(deIbanFormatted));
        Assert.True(ClipboardPrivacyFilter.IsSensitive(gbIban));
        Assert.True(ClipboardPrivacyFilter.IsSensitive("Please transfer funds to " + deIbanFormatted));
    }

    [Fact]
    public void IsSensitive_RejectsInvalidIban()
    {
        // Altered checksum digit in German IBAN
        string invalidCheck = "DE" + "88" + "370400440532013000";
        Assert.False(ClipboardPrivacyFilter.IsSensitive(invalidCheck));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("DE" + "89"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("12" + "34" + "370400440532013000"));
    }

    [Fact]
    public void IsSensitive_DetectsShannonEntropyTokens()
    {
        string highEntropyToken = "aB" + "3dE5gH7jK9mN1pQ3sT5vW7yZ9bD1fH";
        string highEntropyPassword = "P@" + "ssw0rd!SuperSecretToken#2026";

        Assert.True(ClipboardPrivacyFilter.IsSensitive(highEntropyToken));
        Assert.True(ClipboardPrivacyFilter.IsSensitive(highEntropyPassword));
    }

    [Fact]
    public void IsSensitive_EntropyRejectsUrlsUuidsAndSentences()
    {
        // Standard URLs must stay non-sensitive
        Assert.False(ClipboardPrivacyFilter.IsSensitive("https://example.com/api/v1/users?id=123"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("http://localhost:5000/dashboard/overview"));

        // UUIDs must stay non-sensitive
        Assert.False(ClipboardPrivacyFilter.IsSensitive("123e4567-e89b-12d3-a456-426614174000"));

        // Normal sentences with spaces must stay non-sensitive
        Assert.False(ClipboardPrivacyFilter.IsSensitive("The quick brown fox jumps over the lazy dog."));

        // Code snippets with spaces must stay non-sensitive
        Assert.False(ClipboardPrivacyFilter.IsSensitive("public void ProcessData() { int x = 10; }"));

        // Monoclass or low diversity tokens must stay non-sensitive
        Assert.False(ClipboardPrivacyFilter.IsSensitive("abcdefghijklmnopqrstuvwxyz"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("123456789012345678901234"));
        Assert.False(ClipboardPrivacyFilter.IsSensitive("NormalEnglishSentenceWithoutSpaces"));
    }

    [Fact]
    public void ClipboardItemViewModel_SensitiveClip_MaskedByDefault()
    {
        string secret = "AI" + "zaSy" + new string('c', 33);
        var payload = new ClipboardPayload
        {
            Id = "clip-s1",
            ContentType = ClipboardContentType.Text,
            TextContent = secret,
            SourceDevice = "Local"
        };

        using var item = new ClipboardItemViewModel(payload);

        Assert.True(item.IsSensitive);
        Assert.False(item.IsRevealed);
        Assert.Equal("Reveal", item.RevealButtonText);
        Assert.Equal(ClipboardPrivacyFilter.Mask(secret), item.DisplayText);
        Assert.Equal(ClipboardPrivacyFilter.Mask(secret), item.PreviewTitle);
        Assert.Equal(secret, item.Content); // Raw content remains intact
    }

    [Fact]
    public void ClipboardItemViewModel_NonSensitiveClip_NotMasked()
    {
        string text = "https://example.com/api/v1/users?id=123";
        var payload = new ClipboardPayload
        {
            Id = "clip-ns1",
            ContentType = ClipboardContentType.Link,
            TextContent = text,
            SourceDevice = "Local"
        };

        using var item = new ClipboardItemViewModel(payload);

        Assert.False(item.IsSensitive);
        Assert.Equal(text, item.DisplayText);
        Assert.Equal(text, item.PreviewTitle);
    }

    [Fact]
    public void ClipboardItemViewModel_ToggleReveal_SwitchesMasking()
    {
        string secret = "Account" + "Key=" + "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6A1B2C3D4E5F6==";
        var payload = new ClipboardPayload
        {
            Id = "clip-s2",
            ContentType = ClipboardContentType.Text,
            TextContent = secret,
            SourceDevice = "Local"
        };

        using var item = new ClipboardItemViewModel(payload);

        Assert.True(item.IsSensitive);
        Assert.False(item.IsRevealed);
        Assert.Equal(ClipboardPrivacyFilter.Mask(secret), item.DisplayText);

        // Toggle to reveal
        item.ToggleReveal();
        Assert.True(item.IsRevealed);
        Assert.Equal("Hide", item.RevealButtonText);
        Assert.Equal(secret, item.DisplayText);
        Assert.Equal(secret, item.PreviewTitle);

        // Toggle back to hide
        item.ToggleReveal();
        Assert.False(item.IsRevealed);
        Assert.Equal("Reveal", item.RevealButtonText);
        Assert.Equal(ClipboardPrivacyFilter.Mask(secret), item.DisplayText);
        Assert.Equal(ClipboardPrivacyFilter.Mask(secret), item.PreviewTitle);
    }

    [Fact]
    public async Task ClipboardHistoryViewModel_ToggleRevealCommand_TogglesSelectedClip()
    {
        await using var app = new AppFixture();
        var vm = app.Main.ClipboardHistory;
        string secret = "xo" + "xb-123456789012-1234567890123-" + "456789abcdef";
        var payload = new ClipboardPayload
        {
            Id = "clip-s3",
            ContentType = ClipboardContentType.Text,
            TextContent = secret,
            SourceDevice = "Local"
        };

        using var item = new ClipboardItemViewModel(payload);
        vm.SelectedClip = item;

        Assert.False(item.IsRevealed);
        vm.ToggleRevealCommand.Execute(null);
        Assert.True(item.IsRevealed);

        vm.ToggleRevealCommand.Execute(null);
        Assert.False(item.IsRevealed);
    }

    #endregion
}
