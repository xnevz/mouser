using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public sealed class FakeDimOverlayWindow : IDimOverlayWindow
{
    public PixelPoint Position { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string? PeerName { get; private set; }
    public double Opacity { get; private set; }
    public bool IsShown { get; private set; }
    public bool IsClosed { get; private set; }

    public void SetPeerName(string name) => PeerName = name;
    public void SetDimOpacity(double opacity) => Opacity = Math.Clamp(opacity, 0.0, 0.7);
    public void Show() => IsShown = true;
    public void Close() => IsClosed = true;
}

public class DimOverlayTests
{
    [Fact]
    public void DimOverlayService_Lifecycle_ActivePeerShowsOverlays_AndNullPeerHidesOverlays()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_dim_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            var screens = new List<OverlayScreen>
            {
                new(new PixelRect(0, 0, 1920, 1080), 1.0),
                new(new PixelRect(1920, 0, 2560, 1440), 1.25)
            };

            var created = new List<FakeDimOverlayWindow>();
            using var service = new DimOverlayService(
                store,
                screenProvider: () => screens,
                windowFactory: () =>
                {
                    var w = new FakeDimOverlayWindow();
                    created.Add(w);
                    return w;
                },
                onWindowShown: _ => { });

            // Active peer -> overlays created and shown for all screens
            service.Update("RemoteDesktop");
            Assert.Equal(2, service.ActiveOverlays.Count);
            Assert.Equal(2, created.Count);

            var w1 = created[0];
            Assert.Equal(new PixelPoint(0, 0), w1.Position);
            Assert.Equal(1920, w1.Width);
            Assert.Equal(1080, w1.Height);
            Assert.Equal("RemoteDesktop", w1.PeerName);
            Assert.Equal(0.4, w1.Opacity);
            Assert.True(w1.IsShown);
            Assert.False(w1.IsClosed);

            var w2 = created[1];
            Assert.Equal(new PixelPoint(1920, 0), w2.Position);
            Assert.Equal(2560 / 1.25, w2.Width);
            Assert.Equal(1440 / 1.25, w2.Height);
            Assert.Equal("RemoteDesktop", w2.PeerName);
            Assert.Equal(0.4, w2.Opacity);
            Assert.True(w2.IsShown);
            Assert.False(w2.IsClosed);

            // Null peer -> returns to local desktop, all overlays closed
            service.Update(null);
            Assert.Empty(service.ActiveOverlays);
            Assert.True(w1.IsClosed);
            Assert.True(w2.IsClosed);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void DimOverlayService_DisabledSetting_SuppressesOverlays()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_dim_disabled_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            store.Current.DimScreensOnControl = false;

            var screens = new List<OverlayScreen>
            {
                new(new PixelRect(0, 0, 1920, 1080), 1.0)
            };

            using var service = new DimOverlayService(
                store,
                screenProvider: () => screens,
                windowFactory: () => new FakeDimOverlayWindow(),
                onWindowShown: _ => { });

            // Setting disabled -> no overlay shown
            service.Update("RemoteDesktop");
            Assert.Empty(service.ActiveOverlays);

            // Re-enabling setting -> shows overlay
            store.Current.DimScreensOnControl = true;
            service.Update("RemoteDesktop");
            Assert.Single(service.ActiveOverlays);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void DimOverlayService_LivePeerChange_UpdatesExistingOverlays()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_dim_peer_change_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            var screens = new List<OverlayScreen>
            {
                new(new PixelRect(0, 0, 1920, 1080), 1.0)
            };

            int factoryCalls = 0;
            using var service = new DimOverlayService(
                store,
                screenProvider: () => screens,
                windowFactory: () =>
                {
                    factoryCalls++;
                    return new FakeDimOverlayWindow();
                },
                onWindowShown: _ => { });

            service.Update("Peer1");
            Assert.Equal(1, factoryCalls);
            var overlay = (FakeDimOverlayWindow)service.ActiveOverlays[0];
            Assert.Equal("Peer1", overlay.PeerName);

            // Switching to another peer reuses existing overlay instances
            service.Update("Peer2");
            Assert.Equal(1, factoryCalls);
            Assert.Equal("Peer2", overlay.PeerName);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Theory]
    [InlineData(-0.2, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.35, 0.35)]
    [InlineData(0.7, 0.7)]
    [InlineData(0.95, 0.7)]
    public void DimOverlayService_AppliesClampedOpacity_ToOverlays(double configuredOpacity, double expectedOverlayOpacity)
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_dim_clamp_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            store.Current.DimScreensOpacity = configuredOpacity;

            var screens = new List<OverlayScreen>
            {
                new(new PixelRect(0, 0, 1920, 1080), 1.0)
            };

            using var service = new DimOverlayService(
                store,
                screenProvider: () => screens,
                windowFactory: () => new FakeDimOverlayWindow(),
                onWindowShown: _ => { });

            service.Update("Peer");
            Assert.Single(service.ActiveOverlays);
            var overlay = (FakeDimOverlayWindow)service.ActiveOverlays[0];
            Assert.Equal(expectedOverlayOpacity, overlay.Opacity);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void SettingsStore_DimScreensOpacity_ClampingAndDefaults()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_dim_settings_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            // Default settings
            Assert.True(store.Current.DimScreensOnControl);
            Assert.Equal(0.4, store.Current.DimScreensOpacity);

            // Clamping opacity above 0.7
            store.Current.DimScreensOpacity = 0.95;
            store.Save();
            var reloadedAbove = new SettingsStore(tmp);
            Assert.Equal(0.7, reloadedAbove.Current.DimScreensOpacity);

            // Clamping opacity below 0.0
            reloadedAbove.Current.DimScreensOpacity = -0.3;
            reloadedAbove.Save();
            var reloadedBelow = new SettingsStore(tmp);
            Assert.Equal(0.0, reloadedBelow.Current.DimScreensOpacity);

            // Preserving in-range opacity
            reloadedBelow.Current.DimScreensOpacity = 0.55;
            reloadedBelow.Save();
            var reloadedValid = new SettingsStore(tmp);
            Assert.Equal(0.55, reloadedValid.Current.DimScreensOpacity);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void DimOverlay_WindowOpacity_Clamped()
    {
        var overlay = new FakeDimOverlayWindow();
        overlay.SetPeerName("Laptop");
        Assert.Equal("Laptop", overlay.PeerName);

        overlay.SetDimOpacity(1.5);
        Assert.Equal(0.7, overlay.Opacity);

        overlay.SetDimOpacity(-0.3);
        Assert.Equal(0.0, overlay.Opacity);

        overlay.SetDimOpacity(0.45);
        Assert.Equal(0.45, overlay.Opacity);
    }

    [Fact]
    public void ClickThrough_Apply_HandlesNullOrZeroHandleSafely()
    {
        ClickThrough.Apply((Window?)null);
        ClickThrough.Apply(IntPtr.Zero);
    }

    [Fact]
    public async Task SettingsViewModel_DimSettings_UpdatesSettingsAndFiresEvent()
    {
        await using var fixture = new AppFixture();
        var vm = fixture.Main.Settings;

        Assert.True(vm.DimScreensOnControl);
        Assert.Equal(0.4, vm.DimScreensOpacity);
        Assert.Equal("40%", vm.DimScreensOpacityPercentText);

        bool eventFired = false;
        vm.DimSettingsChanged += () => eventFired = true;

        vm.DimScreensOnControl = false;
        Assert.True(eventFired);
        Assert.False(fixture.Settings.Current.DimScreensOnControl);

        eventFired = false;
        vm.DimScreensOpacity = 0.65;
        Assert.True(eventFired);
        Assert.Equal(0.65, fixture.Settings.Current.DimScreensOpacity);
        Assert.Equal("65%", vm.DimScreensOpacityPercentText);

        // Clamping on viewmodel
        vm.DimScreensOpacity = 0.85;
        Assert.Equal(0.7, fixture.Settings.Current.DimScreensOpacity);
        Assert.Equal("70%", vm.DimScreensOpacityPercentText);
    }
}
