using System.Linq;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class ViewModelTests
{
    [Fact]
    public void MainViewModel_Navigation_SwitchesPagesCorrectly()
    {
        var vm = new MainViewModel();

        Assert.Equal(NavigationTab.DisplayMatrix, vm.CurrentTab);
        Assert.IsType<MonitorLayoutViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("SmartClipboard");
        Assert.Equal(NavigationTab.SmartClipboard, vm.CurrentTab);
        Assert.IsType<ClipboardHistoryViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("AndroidRemote");
        Assert.Equal(NavigationTab.AndroidRemote, vm.CurrentTab);
        Assert.IsType<AndroidRemoteViewModel>(vm.CurrentPage);

        vm.NavigateCommand.Execute("Settings");
        Assert.Equal(NavigationTab.Settings, vm.CurrentTab);
        Assert.IsType<SettingsViewModel>(vm.CurrentPage);
    }

    [Fact]
    public void MonitorLayoutViewModel_AutoArrange_PositionsScreensHorizontally()
    {
        var vm = new MonitorLayoutViewModel();
        Assert.True(vm.Screens.Count >= 2);

        vm.AutoArrangeScreensCommand.Execute(null);

        // Screens should be arranged in non-overlapping order along X axis
        for (int i = 0; i < vm.Screens.Count - 1; i++)
        {
            var left = vm.Screens[i];
            var right = vm.Screens[i + 1];
            Assert.True(right.PositionX > left.PositionX, $"Screen {right.Hostname} should be to the right of {left.Hostname}");
        }
    }

    [Fact]
    public void MonitorLayoutViewModel_ToggleSharing_UpdatesState()
    {
        var vm = new MonitorLayoutViewModel();
        Assert.True(vm.IsSharingActive);

        vm.ToggleSharingCommand.Execute(null);
        Assert.False(vm.IsSharingActive);

        vm.ToggleSharingCommand.Execute(null);
        Assert.True(vm.IsSharingActive);
    }

    [Fact]
    public void MonitorLayoutViewModel_SelectScreen_MarksSelection()
    {
        var vm = new MonitorLayoutViewModel();
        var secondScreen = vm.Screens[1];

        vm.SelectScreenCommand.Execute(secondScreen);

        Assert.Equal(secondScreen, vm.SelectedScreen);
        Assert.True(secondScreen.IsSelected);
        Assert.False(vm.Screens[0].IsSelected);
    }

    [Fact]
    public void ClipboardHistoryViewModel_FilteringAndSearch_WorksAccurately()
    {
        var vm = new ClipboardHistoryViewModel();
        Assert.NotEmpty(vm.FilteredClips);

        // Filter by Code
        vm.SetFilterCommand.Execute("Code");
        Assert.All(vm.FilteredClips, clip => Assert.Equal("Code", clip.ContentType));

        // Filter by Link
        vm.SetFilterCommand.Execute("Link");
        Assert.All(vm.FilteredClips, clip => Assert.Equal("Link", clip.ContentType));

        // Search text
        vm.SetFilterCommand.Execute("All");
        vm.SearchQuery = "Meeting";
        Assert.Single(vm.FilteredClips);
        Assert.Contains("Meeting", vm.FilteredClips[0].PreviewTitle);
    }

    [Fact]
    public void ClipboardHistoryViewModel_DeleteAndClear_ModifiesList()
    {
        var vm = new ClipboardHistoryViewModel();
        int initialCount = vm.FilteredClips.Count;
        var first = vm.FilteredClips.First();

        vm.DeleteClipCommand.Execute(first);
        Assert.Equal(initialCount - 1, vm.FilteredClips.Count);

        vm.ClearAllCommand.Execute(null);
        Assert.Empty(vm.FilteredClips);
    }

    [Fact]
    public void SettingsViewModel_RegeneratePin_CreatesValidFormat()
    {
        var vm = new SettingsViewModel();
        string initialPin = vm.PairingPin;

        vm.RegeneratePinCommand.Execute(null);

        Assert.NotEqual(initialPin, vm.PairingPin);
        Assert.Matches(@"^\d{3}-\d{3}$", vm.PairingPin);
    }

    [Fact]
    public void AndroidRemoteViewModel_ToggleGyro_SwitchesActiveState()
    {
        var vm = new AndroidRemoteViewModel();
        Assert.False(vm.IsGyroPointerActive);

        vm.ToggleGyroPointerCommand.Execute(null);
        Assert.True(vm.IsGyroPointerActive);

        vm.ToggleGyroPointerCommand.Execute(null);
        Assert.False(vm.IsGyroPointerActive);
    }

    [Fact]
    public void MonitorLayoutViewModel_ScreensHaveIdentifierLetters()
    {
        var vm = new MonitorLayoutViewModel();
        Assert.Equal("A", vm.Screens[0].Letter);
        Assert.Equal("B", vm.Screens[1].Letter);
        Assert.Equal("C", vm.Screens[2].Letter);

        Assert.True(vm.ShowScreenLetters);
        vm.ToggleScreenLettersCommand.Execute(null);
        Assert.False(vm.ShowScreenLetters);
        vm.ToggleScreenLettersCommand.Execute(null);
        Assert.True(vm.ShowScreenLetters);
    }

    [Fact]
    public void MonitorLayoutViewModel_UpdateScreenPosition_DragsAndClampsCorrectly()
    {
        var vm = new MonitorLayoutViewModel();
        var screen = vm.Screens[0];

        // Drag to free position without snapping
        vm.UpdateScreenPosition(screen, 200, 250, enableSnapping: false);
        Assert.Equal(200, screen.PositionX);
        Assert.Equal(250, screen.PositionY);

        // Clamping to canvas bounds (canvas 1000 x 600)
        vm.UpdateScreenPosition(screen, -100, 1500, enableSnapping: false);
        Assert.Equal(0, screen.PositionX);
        Assert.Equal(600 - screen.DisplayHeight, screen.PositionY);
    }

    [Fact]
    public void MonitorLayoutViewModel_UpdateScreenPosition_SnapsToAdjacentScreen()
    {
        var vm = new MonitorLayoutViewModel();
        var screenA = vm.Screens[0]; // width: 260
        screenA.PositionX = 100;
        screenA.PositionY = 100;

        var screenB = vm.Screens[1];
        // Position screenB close to screenA's right edge (100 + 260 = 360, close at 365)
        vm.UpdateScreenPosition(screenB, 365, 105, enableSnapping: true);

        // Should snap X to 360 and Y to 100 (aligned top edge)
        Assert.Equal(360, screenB.PositionX);
        Assert.Equal(100, screenB.PositionY);
    }

    [Fact]
    public void ClipboardHistoryViewModel_UniClipboard_FeaturesWork()
    {
        var vm = new ClipboardHistoryViewModel();
        Assert.True(vm.AllCount > 0);
        Assert.True(vm.StarredCount > 0);

        // Test Star toggling
        var clip = vm.FilteredClips.First(c => !c.IsPinned);
        int initialStarred = vm.StarredCount;
        vm.ToggleStarCommand.Execute(clip);
        Assert.True(clip.IsPinned);
        Assert.Equal(initialStarred + 1, vm.StarredCount);

        // Test Library filter: Starred
        vm.SetLibraryCommand.Execute("Starred");
        Assert.All(vm.FilteredClips, c => Assert.True(c.IsPinned));

        // Test Device filter
        vm.SetLibraryCommand.Execute("All");
        vm.SetDeviceFilterCommand.Execute("Android");
        Assert.All(vm.FilteredClips, c => Assert.Contains("Android", c.SourceDevice));

        // Test Send to Device command
        vm.SendToDeviceCommand.Execute("Windows Workstation");
        Assert.Contains("Dispatched", vm.StatusMessage);

        // Test metadata properties
        Assert.NotEmpty(clip.Sha256Checksum);
        Assert.True(clip.ByteCount > 0);
        Assert.NotEmpty(clip.FormattedSize);
    }
}
