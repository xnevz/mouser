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

public sealed class ClipboardDeliveryTests : IAsyncDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mouser_delivery_tests_" + Guid.NewGuid().ToString("N"));
    private readonly ClipboardStorageService _storage;
    private readonly SettingsStore _settings;

    public ClipboardDeliveryTests()
    {
        Directory.CreateDirectory(_dir);
        _storage = new ClipboardStorageService(Path.Combine(_dir, "clipboard.db"));
        _settings = new SettingsStore(Path.Combine(_dir, "settings.json"));
    }

    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public async Task Storage_SaveDeliveries_And_GetDeliveries_RoundTrips()
    {
        await _storage.InitializeAsync();
        var clip = new ClipboardPayload { TextContent = "Sample text for delivery" };
        await _storage.SaveClipAsync(clip);

        var initialDeliveries = new List<ClipDelivery>
        {
            new("dev-1", "Laptop-Pro", "Sent", null),
            new("dev-2", "MacBook-Air", "Failed", "Connection refused")
        };

        await _storage.SaveDeliveriesAsync(clip.Id, initialDeliveries);

        var fetched = await _storage.GetDeliveriesAsync(clip.Id);
        Assert.Equal(2, fetched.Count);

        var d1 = Assert.Single(fetched, d => d.DeviceId == "dev-1");
        Assert.Equal("Laptop-Pro", d1.DeviceName);
        Assert.Equal("Sent", d1.Status);
        Assert.Null(d1.Error);

        var d2 = Assert.Single(fetched, d => d.DeviceId == "dev-2");
        Assert.Equal("MacBook-Air", d2.DeviceName);
        Assert.Equal("Failed", d2.Status);
        Assert.Equal("Connection refused", d2.Error);

        // Upsert update: dev-2 succeeds on retry
        var updatedDeliveries = new List<ClipDelivery>
        {
            new("dev-2", "MacBook-Air", "Delivered", null)
        };
        await _storage.SaveDeliveriesAsync(clip.Id, updatedDeliveries);

        var afterUpdate = await _storage.GetDeliveriesAsync(clip.Id);
        Assert.Equal(2, afterUpdate.Count);

        var d2Updated = Assert.Single(afterUpdate, d => d.DeviceId == "dev-2");
        Assert.Equal("Delivered", d2Updated.Status);
        Assert.Null(d2Updated.Error);
    }

    [Fact]
    public async Task Storage_DeleteClip_DeletesAssociatedDeliveries()
    {
        await _storage.InitializeAsync();
        var clip = new ClipboardPayload { TextContent = "To be deleted" };
        await _storage.SaveClipAsync(clip);

        await _storage.SaveDeliveriesAsync(clip.Id, new[]
        {
            new ClipDelivery("d1", "PC-1", "Sent"),
            new ClipDelivery("d2", "PC-2", "Failed", "Error")
        });

        var deliveriesBefore = await _storage.GetDeliveriesAsync(clip.Id);
        Assert.Equal(2, deliveriesBefore.Count);

        await _storage.DeleteClipAsync(clip.Id);

        var deliveriesAfter = await _storage.GetDeliveriesAsync(clip.Id);
        Assert.Empty(deliveriesAfter);
    }

    [Fact]
    public async Task Storage_ClearAsync_And_Purge_DeletesAssociatedDeliveries()
    {
        await _storage.InitializeAsync();
        var oldClip = new ClipboardPayload
        {
            TextContent = "Old clip",
            TimestampUtc = DateTimeOffset.UtcNow.AddDays(-10).ToUnixTimeMilliseconds(),
            IsFavorite = false
        };
        await _storage.SaveClipAsync(oldClip);
        await _storage.SaveDeliveriesAsync(oldClip.Id, new[] { new ClipDelivery("d1", "PC-1", "Sent") });

        // Purge older than 5 days
        int purged = await _storage.PurgeClipsOlderThanAsync(5);
        Assert.Equal(1, purged);

        var deliveries = await _storage.GetDeliveriesAsync(oldClip.Id);
        Assert.Empty(deliveries);

        // ClearAsync test
        var anotherClip = new ClipboardPayload { TextContent = "Another clip" };
        await _storage.SaveClipAsync(anotherClip);
        await _storage.SaveDeliveriesAsync(anotherClip.Id, new[] { new ClipDelivery("d2", "PC-2", "Sent") });

        await _storage.ClearAsync(includeFavorites: true);
        var clearedDeliveries = await _storage.GetDeliveriesAsync(anotherClip.Id);
        Assert.Empty(clearedDeliveries);
    }

    [Fact]
    public async Task Host_ShareClipboardAsync_ReturnsSkipped_WhenPeerOffline()
    {
        await using var app = new AppFixture();
        app.Settings.Current.Peers["offline-peer-1"] = new PeerSettings
        {
            Name = "Remote Office PC",
            PairKeyProtected = SecretStore.Protect(new byte[32])
        };

        var deliveries = await app.Host.ShareClipboardAsync("Testing offline peer clipboard sync");

        Assert.NotEmpty(deliveries);
        var offlineDelivery = Assert.Single(deliveries, d => d.DeviceId == "offline-peer-1");
        Assert.Equal("Remote Office PC", offlineDelivery.DeviceName);
        Assert.Equal("Skipped", offlineDelivery.Status);
        Assert.NotNull(offlineDelivery.Error);
        Assert.Contains("Not on the network", offlineDelivery.Error);
    }

    [Fact]
    public async Task ClipboardHistory_LoadsDeliveries_WhenSelectedClipChanges()
    {
        await _storage.InitializeAsync();
        var vm = new ClipboardHistoryViewModel(_storage, _settings);
        await vm.InitializeAsync();

        var clip1 = new ClipboardPayload { TextContent = "First clip" };
        var clip2 = new ClipboardPayload { TextContent = "Second clip" };
        await _storage.SaveClipAsync(clip1);
        await _storage.SaveClipAsync(clip2);

        await _storage.SaveDeliveriesAsync(clip1.Id, new[]
        {
            new ClipDelivery("dev-1", "Studio Mac", "Sent", null),
            new ClipDelivery("dev-2", "Windows Laptop", "Delivered", null),
            new ClipDelivery("dev-3", "Linux Box", "Failed", "Target machine actively refused it")
        });

        await vm.ReloadAsync();
        Assert.Equal(2, vm.FilteredClips.Count);

        // Select clip1
        vm.SelectedClip = vm.FilteredClips.First(c => c.Id == clip1.Id);
        await vm.LoadDeliveriesForSelectedClipAsync();

        Assert.True(vm.HasDeliveries);
        Assert.Equal(3, vm.SelectedClipDeliveries.Count);
        Assert.Equal("Delivery · 2 of 3 devices", vm.DeliverySummaryHeader);

        var item1 = vm.SelectedClipDeliveries[0];
        Assert.Equal("Studio Mac", item1.DeviceName);
        Assert.Equal("Sent", item1.StatusDisplay);
        Assert.False(item1.HasError);
        Assert.Null(item1.Error);
        Assert.True(item1.IsDeliveredOrSent);

        var item2 = vm.SelectedClipDeliveries[1];
        Assert.Equal("Windows Laptop", item2.DeviceName);
        Assert.Equal("Sent", item2.StatusDisplay); // "Delivered" displays as "Sent"
        Assert.False(item2.HasError);
        Assert.True(item2.IsDeliveredOrSent);

        var item3 = vm.SelectedClipDeliveries[2];
        Assert.Equal("Linux Box", item3.DeviceName);
        Assert.Equal("Failed", item3.StatusDisplay);
        Assert.True(item3.HasError);
        Assert.Equal("Target machine actively refused it", item3.Error);
        Assert.False(item3.IsDeliveredOrSent);

        // Switch to clip2 (which has no deliveries)
        vm.SelectedClip = vm.FilteredClips.First(c => c.Id == clip2.Id);
        await vm.LoadDeliveriesForSelectedClipAsync();

        Assert.False(vm.HasDeliveries);
        Assert.Empty(vm.SelectedClipDeliveries);
        Assert.Equal("Delivery", vm.DeliverySummaryHeader);
    }

    [Fact]
    public async Task ClipboardHistory_PollClipboardAsync_SavesDeliveries_WhenShared()
    {
        await _storage.InitializeAsync();
        var fakeClipboard = new FakeClipboard();
        var vm = new ClipboardHistoryViewModel(_storage, _settings);
        vm.AttachClipboard(fakeClipboard);
        await vm.InitializeAsync();

        vm.ShareCapturedText = text =>
        {
            IReadOnlyList<ClipDelivery> list = new List<ClipDelivery>
            {
                new("dev-mock", "Mock PC", "Sent")
            };
            return Task.FromResult(list);
        };

        fakeClipboard.Text = "Hello from polled copy";
        bool polled = await vm.PollClipboardAsync();
        Assert.True(polled);

        Assert.NotNull(vm.SelectedClip);
        var deliveries = await _storage.GetDeliveriesAsync(vm.SelectedClip!.Id);
        Assert.Single(deliveries);
        Assert.Equal("dev-mock", deliveries[0].DeviceId);
        Assert.Equal("Mock PC", deliveries[0].DeviceName);
        Assert.Equal("Sent", deliveries[0].Status);

        Assert.True(vm.HasDeliveries);
        Assert.Single(vm.SelectedClipDeliveries);
        Assert.Equal("Delivery · 1 of 1 devices", vm.DeliverySummaryHeader);
    }

    [Theory]
    [InlineData("Sent", null, "Sent", true, false)]
    [InlineData("Delivered", null, "Sent", true, false)]
    [InlineData("Failed", "Connection reset", "Failed", false, true)]
    [InlineData("Skipped", "Not on the network", "Skipped", false, true)]
    [InlineData("Queued", null, "Queued", false, false)]
    public void ClipDeliveryViewModel_Properties_MatchExpectations(string status, string? error, string expectedDisplay, bool expectedDeliveredOrSent, bool expectedHasError)
    {
        var delivery = new ClipDelivery("dev-x", "Machine-X", status, error);
        var vm = new ClipDeliveryViewModel(delivery);

        Assert.Equal("dev-x", vm.DeviceId);
        Assert.Equal("Machine-X", vm.DeviceName);
        Assert.Equal(status, vm.Status);
        Assert.Equal(error, vm.Error);
        Assert.Equal(expectedDisplay, vm.StatusDisplay);
        Assert.Equal(expectedDeliveredOrSent, vm.IsDeliveredOrSent);
        Assert.Equal(expectedHasError, vm.HasError);
        Assert.NotNull(vm.StatusBrush);
    }
}
