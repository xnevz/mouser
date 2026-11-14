using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Mouser.Core.Networking;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class ElevationTests
{
    [Fact]
    public void ElevationInfo_IsCurrentProcessElevated_ReturnsBoolWithoutThrowing()
    {
        bool isElevated = ElevationInfo.IsCurrentProcessElevated();
        // Just verify it returns a boolean without throwing on any platform
        Assert.True(isElevated || !isElevated);
    }

    [Fact]
    public void ForegroundWindowInfo_IsElevated_DoesNotThrow()
    {
        bool isElevated = ForegroundWindowInfo.IsElevated();
        Assert.True(isElevated || !isElevated);
    }

    [Fact]
    public async Task MainViewModel_ForegroundIsElevated_UpdatesBasedOnElevatedProbe_WhenInputIsActive()
    {
        await using var app = new AppFixture();
        app.Main.RemoteInputActiveProbe = () => true;
        app.Main.ElevatedProbe = () => true;
        app.Settings.Current.WarnElevatedWindows = true;

        app.Main.Refresh();

        Assert.True(app.Main.ForegroundIsElevated);
        Assert.True(app.Main.DevicesPage.ForegroundIsElevated);

        // When probe returns false, warning clears
        app.Main.ElevatedProbe = () => false;
        app.Main.Refresh();

        Assert.False(app.Main.ForegroundIsElevated);
        Assert.False(app.Main.DevicesPage.ForegroundIsElevated);
    }

    [Fact]
    public async Task MainViewModel_ForegroundIsElevated_FalseWhenRemoteInputInactive()
    {
        await using var app = new AppFixture();
        app.Main.RemoteInputActiveProbe = () => false;
        app.Main.ElevatedProbe = () => true;
        app.Settings.Current.WarnElevatedWindows = true;

        app.Main.Refresh();

        Assert.False(app.Main.ForegroundIsElevated);
        Assert.False(app.Main.DevicesPage.ForegroundIsElevated);
    }

    [Fact]
    public async Task MainViewModel_WarnElevatedWindows_SettingTogglesWarning()
    {
        await using var app = new AppFixture();
        app.Main.RemoteInputActiveProbe = () => true;
        app.Main.ElevatedProbe = () => true;

        // Enabled by default -> warning active
        app.Settings.Current.WarnElevatedWindows = true;
        app.Main.Refresh();
        Assert.True(app.Main.ForegroundIsElevated);

        // Setting turned off -> warning disabled
        app.Settings.Current.WarnElevatedWindows = false;
        app.Main.Refresh();
        Assert.False(app.Main.ForegroundIsElevated);

        // Setting turned back on -> warning active
        app.Settings.Current.WarnElevatedWindows = true;
        app.Main.Refresh();
        Assert.True(app.Main.ForegroundIsElevated);
    }

    [Fact]
    public async Task SettingsViewModel_WarnElevatedWindows_TogglesAndPersists()
    {
        await using var app = new AppFixture();
        var vm = app.Main.Settings;

        // Default is true
        Assert.True(vm.WarnElevatedWindows);
        Assert.True(app.Settings.Current.WarnElevatedWindows);

        // Change to false
        vm.WarnElevatedWindows = false;
        Assert.False(app.Settings.Current.WarnElevatedWindows);

        // Reload from disk to verify persistence
        var reloaded = new SettingsStore(app.Settings.FilePath);
        Assert.False(reloaded.Current.WarnElevatedWindows);

        // Change back to true
        vm.WarnElevatedWindows = true;
        Assert.True(app.Settings.Current.WarnElevatedWindows);

        var reloaded2 = new SettingsStore(app.Settings.FilePath);
        Assert.True(reloaded2.Current.WarnElevatedWindows);
    }

    [Fact]
    public async Task MainViewModel_ActiveClient_TriggersElevatedWarning()
    {
        await using var app = new AppFixture();
        app.Main.ElevatedProbe = () => true;
        app.Settings.Current.WarnElevatedWindows = true;

        // No clients -> inactive
        app.Main.Refresh();
        Assert.False(app.Main.ForegroundIsElevated);

        // When a real phone connects and sends input
        using var phone = new FakePhone(app.Host.Input!.BoundPort);
        Assert.True(await phone.PairAsync(app.Settings.Current.PairingPin));
        phone.Send(InputMotionPacket.TypeMotion, 3, 4);
        await Wait.UntilAsync(() => app.Sink.Snapshot().Length == 1);

        app.Main.Refresh();
        Assert.True(app.Main.ForegroundIsElevated);
        Assert.True(app.Main.DevicesPage.ForegroundIsElevated);
    }
}
