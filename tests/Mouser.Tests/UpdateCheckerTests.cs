using Mouser.Core.Services;
using Mouser.UI.Services;

namespace Mouser.Tests;

public class UpdateCheckerTests
{
    [Fact]
    public void VersionComparer_StandardSemVer_CorrectPrecedence()
    {
        Assert.True(VersionComparer.IsNewer("v1.2.0", "1.1.0"));
        Assert.True(VersionComparer.IsNewer("1.10.0", "1.2.0"));
        Assert.True(VersionComparer.IsNewer("v2.0.0", "v1.99.99"));
        Assert.True(VersionComparer.IsNewer("0.9.1", "0.9.0"));

        Assert.False(VersionComparer.IsNewer("1.2.0", "1.2.0"));
        Assert.False(VersionComparer.IsNewer("1.2.0", "v1.2.0"));
        Assert.False(VersionComparer.IsNewer("1.0.0", "1.2.0"));
        Assert.False(VersionComparer.IsNewer("0.9.0", "1.0.0"));
    }

    [Fact]
    public void VersionComparer_PreReleaseTags_CorrectPrecedence()
    {
        // Stable release takes precedence over pre-release of the same version
        Assert.True(VersionComparer.IsNewer("1.0.0", "1.0.0-beta"));
        Assert.True(VersionComparer.IsNewer("1.0.0", "1.0.0-rc.1"));

        // Comparing pre-releases
        Assert.True(VersionComparer.IsNewer("1.0.0-beta.2", "1.0.0-beta.1"));
        Assert.True(VersionComparer.IsNewer("1.0.0-rc.1", "1.0.0-beta.9"));
        Assert.False(VersionComparer.IsNewer("1.0.0-alpha", "1.0.0-beta"));
    }

    [Fact]
    public async Task UpdateChecker_LocalTestHook_DetectsNewerVersion()
    {
        string tempJson = Path.Combine(Path.GetTempPath(), $"mouser_release_{Guid.NewGuid():N}.json");
        try
        {
            string payload = @"{
                ""tag_name"": ""v1.5.0"",
                ""html_url"": ""https://github.com/xnevz/mouser/releases/tag/v1.5.0"",
                ""body"": ""Feature improvements and bug fixes.""
            }";
            await File.WriteAllTextAsync(tempJson, payload);

            var result = await UpdateChecker.CheckForUpdateAsync(
                currentVersion: "0.9.0",
                endpointUrl: tempJson);

            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("v1.5.0", result.LatestVersion);
            Assert.Equal("https://github.com/xnevz/mouser/releases/tag/v1.5.0", result.ReleaseUrl);
            Assert.Equal("Feature improvements and bug fixes.", result.ReleaseNotes);
            Assert.Null(result.Error);
        }
        finally
        {
            if (File.Exists(tempJson)) File.Delete(tempJson);
        }
    }

    [Fact]
    public async Task UpdateChecker_LocalTestHook_CurrentVersionIsNewerOrEqual()
    {
        string tempJson = Path.Combine(Path.GetTempPath(), $"mouser_release_{Guid.NewGuid():N}.json");
        try
        {
            string payload = @"{
                ""tag_name"": ""v0.9.0"",
                ""html_url"": ""https://github.com/xnevz/mouser/releases/tag/v0.9.0"",
                ""body"": ""Current version.""
            }";
            await File.WriteAllTextAsync(tempJson, payload);

            var result = await UpdateChecker.CheckForUpdateAsync(
                currentVersion: "0.9.0",
                endpointUrl: tempJson);

            Assert.False(result.IsUpdateAvailable);
            Assert.Equal("v0.9.0", result.LatestVersion);
            Assert.Null(result.Error);

            // Newer local version
            var resultNewer = await UpdateChecker.CheckForUpdateAsync(
                currentVersion: "1.0.0",
                endpointUrl: tempJson);

            Assert.False(resultNewer.IsUpdateAvailable);
        }
        finally
        {
            if (File.Exists(tempJson)) File.Delete(tempJson);
        }
    }

    [Fact]
    public void SettingsStore_UpdateSettings_PersistAndReload()
    {
        string tempSettings = Path.Combine(Path.GetTempPath(), $"mouser_settings_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tempSettings);
            Assert.True(store.Current.CheckForUpdates);
            Assert.Null(store.Current.UpdateProxy);

            store.Current.CheckForUpdates = false;
            store.Current.UpdateProxy = "http://127.0.0.1:8888";
            store.Save();

            var reloaded = new SettingsStore(tempSettings);
            Assert.False(reloaded.Current.CheckForUpdates);
            Assert.Equal("http://127.0.0.1:8888", reloaded.Current.UpdateProxy);
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }
    }
}
