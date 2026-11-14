using System;
using System.IO;
using Mouser.Core.Networking;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public sealed class PowerSyncPolicyTests
{
    [Theory]
    [InlineData(PowerEventKind.Lock, true, false, false, true)]
    [InlineData(PowerEventKind.Lock, false, true, true, false)]
    [InlineData(PowerEventKind.Sleep, false, true, false, true)]
    [InlineData(PowerEventKind.Sleep, true, false, true, false)]
    [InlineData(PowerEventKind.DisplayOff, false, false, true, true)]
    [InlineData(PowerEventKind.DisplayOff, true, true, false, false)]
    public void ShouldApply_RespectsKindSpecificToggle(
        PowerEventKind kind,
        bool syncLock,
        bool syncSleep,
        bool syncDisplayOff,
        bool expected)
    {
        var now = DateTime.UtcNow;
        bool result = PowerSyncPolicy.ShouldApply(kind, syncLock, syncSleep, syncDisplayOff, null, now);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldApply_UnknownKind_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        bool result = PowerSyncPolicy.ShouldApply((PowerEventKind)99, true, true, true, null, now);
        Assert.False(result);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(3.0)]
    [InlineData(4.9)]
    [InlineData(5.0)]
    public void ShouldApply_WithinPingPongGuardInterval_RefusesEvent(double secondsAgo)
    {
        var now = DateTime.UtcNow;
        var lastEvent = now.AddSeconds(-secondsAgo);

        bool result = PowerSyncPolicy.ShouldApply(
            PowerEventKind.Lock,
            syncLock: true,
            syncSleep: true,
            syncDisplayOff: true,
            lastEventUtc: lastEvent,
            nowUtc: now);

        Assert.False(result);
    }

    [Theory]
    [InlineData(5.01)]
    [InlineData(6.0)]
    [InlineData(60.0)]
    public void ShouldApply_AfterPingPongGuardInterval_AllowsEvent(double secondsAgo)
    {
        var now = DateTime.UtcNow;
        var lastEvent = now.AddSeconds(-secondsAgo);

        bool result = PowerSyncPolicy.ShouldApply(
            PowerEventKind.Lock,
            syncLock: true,
            syncSleep: true,
            syncDisplayOff: true,
            lastEventUtc: lastEvent,
            nowUtc: now);

        Assert.True(result);
    }

    [Fact]
    public void PeerSettings_SyncProperties_DefaultToFalse()
    {
        var settings = new PeerSettings();
        Assert.False(settings.SyncLock);
        Assert.False(settings.SyncSleep);
        Assert.False(settings.SyncDisplayOff);
    }

    [Fact]
    public void DeviceDetailViewModel_TogglingSyncProperties_SavesToSettings()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_powersync_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            var vm = new DeviceDetailViewModel("peer-test", DeviceKind.Computer, store);

            Assert.False(vm.SyncLock);
            Assert.False(vm.SyncSleep);
            Assert.False(vm.SyncDisplayOff);

            vm.SyncLock = true;
            vm.SyncSleep = true;
            vm.SyncDisplayOff = true;

            Assert.True(store.Current.Peers["peer-test"].SyncLock);
            Assert.True(store.Current.Peers["peer-test"].SyncSleep);
            Assert.True(store.Current.Peers["peer-test"].SyncDisplayOff);

            var reloaded = new SettingsStore(tmp);
            Assert.True(reloaded.Current.Peers["peer-test"].SyncLock);
            Assert.True(reloaded.Current.Peers["peer-test"].SyncSleep);
            Assert.True(reloaded.Current.Peers["peer-test"].SyncDisplayOff);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void WindowsPower_Hooks_AreInvoked()
    {
        bool lockCalled = false;
        bool displayOffCalled = false;
        bool sleepCalled = false;

        WindowsPower.LockHook = () => lockCalled = true;
        WindowsPower.DisplayOffHook = () => displayOffCalled = true;
        WindowsPower.SleepHook = () => sleepCalled = true;

        try
        {
            WindowsPower.Lock();
            WindowsPower.TurnOffDisplay();
            WindowsPower.Sleep();

            Assert.True(lockCalled);
            Assert.True(displayOffCalled);
            Assert.True(sleepCalled);
        }
        finally
        {
            WindowsPower.LockHook = null;
            WindowsPower.DisplayOffHook = null;
            WindowsPower.SleepHook = null;
        }
    }

    [Fact]
    public async Task MouserHost_BroadcastPowerEventAsync_UpdatesLastPowerEventUtc()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_host_power_{Guid.NewGuid():N}");
        try
        {
            var store = new SettingsStore(Path.Combine(tmp, "settings.json"));
            using var host = new MouserHost(store, new RecordingSink(), null, discoveryPort: 0, downloadDirectory: tmp);
            Assert.Null(host.LastPowerEventUtc);

            await host.BroadcastPowerEventAsync(PowerEventKind.Lock);
            Assert.NotNull(host.LastPowerEventUtc);
            Assert.True(DateTime.UtcNow - host.LastPowerEventUtc.Value < TimeSpan.FromSeconds(2));
        }
        finally
        {
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
        }
    }
}
