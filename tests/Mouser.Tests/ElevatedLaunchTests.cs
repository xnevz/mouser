using System;
using System.IO;
using System.Threading.Tasks;
using Mouser.UI.Services;
using Mouser.UI.ViewModels;
using Xunit;

namespace Mouser.Tests;

public class ElevatedLaunchTests
{
    [Fact]
    public void GetCreateTaskCommand_WithSpacesAndDefaultArgs_ProducesExpectedCommand()
    {
        string exe = @"C:\Program Files\Mouser\Mouser.exe";
        string cmd = ElevatedLaunch.GetCreateTaskCommand(exe);
        Assert.Equal(@"/Create /F /TN ""Mouser"" /TR ""\""C:\Program Files\Mouser\Mouser.exe\"" --minimized"" /SC ONLOGON /RL HIGHEST /IT", cmd);
    }

    [Fact]
    public void GetCreateTaskCommand_WithCustomArgs_ProducesExpectedCommand()
    {
        string exe = @"C:\Mouser\Mouser.exe";
        string cmd = ElevatedLaunch.GetCreateTaskCommand(exe, "--test-arg");
        Assert.Equal(@"/Create /F /TN ""Mouser"" /TR ""\""C:\Mouser\Mouser.exe\"" --test-arg"" /SC ONLOGON /RL HIGHEST /IT", cmd);
    }

    [Fact]
    public void GetDeleteTaskCommand_ProducesExpectedCommand()
    {
        Assert.Equal(@"/Delete /F /TN ""Mouser""", ElevatedLaunch.GetDeleteTaskCommand());
    }

    [Fact]
    public void GetQueryTaskCommand_ProducesExpectedCommand()
    {
        Assert.Equal(@"/Query /TN ""Mouser""", ElevatedLaunch.GetQueryTaskCommand());
    }

    [Fact]
    public void SingleInstance_UsesGlobalPrefixAndExposesMutexName()
    {
        using var si = new SingleInstance();
        Assert.StartsWith(@"Global\Mouser-SingleInstance-", si.MutexName);
        Assert.EndsWith(".Lock", si.MutexName);
    }

    [Fact]
    public void SingleInstance_CustomName_IncludesPrefix()
    {
        string customName = "MouserCustom_" + Guid.NewGuid().ToString("N");
        using var si = new SingleInstance(customName);
        Assert.StartsWith(@"Global\" + customName, si.MutexName);
        Assert.EndsWith(".Lock", si.MutexName);
    }

    [Fact]
    public void StartupRegistration_ChooseMethod_AllFourCases()
    {
        Assert.Equal(StartupMethod.None, StartupRegistration.ChooseMethod(runAsAdmin: false, enabled: false));
        Assert.Equal(StartupMethod.None, StartupRegistration.ChooseMethod(runAsAdmin: true, enabled: false));
        Assert.Equal(StartupMethod.RunKey, StartupRegistration.ChooseMethod(runAsAdmin: false, enabled: true));
        Assert.Equal(StartupMethod.Task, StartupRegistration.ChooseMethod(runAsAdmin: true, enabled: true));
    }

    [Fact]
    public void AppSettings_RunAsAdministrator_DefaultsToFalseAndPersists()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "settings_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store1 = new SettingsStore(tempFile);
            Assert.False(store1.Current.RunAsAdministrator);

            store1.Current.RunAsAdministrator = true;
            store1.Save();

            var store2 = new SettingsStore(tempFile);
            Assert.True(store2.Current.RunAsAdministrator);

            store2.Current.RunAsAdministrator = false;
            store2.Save();

            var store3 = new SettingsStore(tempFile);
            Assert.False(store3.Current.RunAsAdministrator);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SettingsViewModel_RunAsAdminToggle_ShowsRestartPromptWhenNotElevated()
    {
        var app = new AppFixture();
        await using var _ = app;
        var vm = app.Main.Settings;
        vm.ElevatedProbe = () => false; // simulate not elevated

        Assert.False(vm.ShowRestartElevatedPrompt);
        vm.RunAsAdministrator = true;
        Assert.True(vm.ShowRestartElevatedPrompt);
        Assert.NotNull(vm.RestartElevatedMessage);

        vm.DismissRestartPromptCommand.Execute(null);
        Assert.False(vm.ShowRestartElevatedPrompt);
    }

    [Fact]
    public async Task MainViewModel_Elevation_UpdatesTitleAndProperty()
    {
        var app = new AppFixture();
        await using var _ = app;
        var main = app.Main;

        Assert.NotNull(main.Title);
        Assert.NotNull(main.WindowTitle);
        if (main.IsElevated)
        {
            Assert.Equal("Mouser (Administrator)", main.Title);
            Assert.Equal("Mouser (Administrator)", main.WindowTitle);
        }
        else
        {
            Assert.Equal("Mouser", main.Title);
            Assert.Equal("Mouser", main.WindowTitle);
        }
    }
}
