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
}
