using System;
using System.IO;
using System.Threading.Tasks;
using Mouser.Core.Geometry;
using Mouser.Core.Kvm;
using Mouser.UI.Services;
using Xunit;

namespace Mouser.Tests;

public class SwitchPolicyTests
{
    private static readonly ScreenBounds Screen1080p = new()
    {
        X = 0,
        Y = 0,
        Width = 1920,
        Height = 1080
    };

    private static SwitchContext CreateContext(
        bool fullscreen = false,
        bool maximized = false,
        double cursorX = 1918,
        double cursorY = 540,
        bool ctrl = false,
        bool alt = false,
        bool shift = false,
        DateTime? now = null)
    {
        return new SwitchContext(
            ForegroundFullscreen: fullscreen,
            ForegroundMaximized: maximized,
            CursorX: cursorX,
            CursorY: cursorY,
            Local: Screen1080p,
            CtrlDown: ctrl,
            AltDown: alt,
            ShiftDown: shift,
            Now: now ?? DateTime.UtcNow);
    }

    [Fact]
    public void Allows_DefaultOptions_AllowsCrossing()
    {
        var options = new KvmOptions();
        var policy = new SwitchPolicy(options);

        var ctx = CreateContext();
        Assert.True(policy.Allows(ctx));
    }

    [Fact]
    public void Allows_BlockInFullscreen_DeniesWhenFullscreen_AllowsWhenNotFullscreen()
    {
        var blockingOptions = new KvmOptions { BlockInFullscreen = true };
        var policy = new SwitchPolicy(blockingOptions);

        Assert.False(policy.Allows(CreateContext(fullscreen: true)));
        Assert.True(policy.Allows(CreateContext(fullscreen: false)));

        var nonBlockingOptions = new KvmOptions { BlockInFullscreen = false };
        var nonBlockingPolicy = new SwitchPolicy(nonBlockingOptions);
        Assert.True(nonBlockingPolicy.Allows(CreateContext(fullscreen: true)));
    }

    [Fact]
    public void Allows_BlockInMaximized_DeniesWhenMaximized_AllowsWhenNot()
    {
        var blockingOptions = new KvmOptions { BlockInMaximized = true };
        var policy = new SwitchPolicy(blockingOptions);

        Assert.False(policy.Allows(CreateContext(maximized: true)));
        Assert.True(policy.Allows(CreateContext(maximized: false)));

        var nonBlockingOptions = new KvmOptions { BlockInMaximized = false };
        var nonBlockingPolicy = new SwitchPolicy(nonBlockingOptions);
        Assert.True(nonBlockingPolicy.Allows(CreateContext(maximized: true)));
    }

    [Fact]
    public void Allows_BlockInCorners_DeniesWithin80PxOfCornersOn1080p()
    {
        var options = new KvmOptions { BlockInCorners = true };
        var policy = new SwitchPolicy(options);

        // Top-left corner
        Assert.False(policy.Allows(CreateContext(cursorX: 10, cursorY: 10)));
        // Top-right corner
        Assert.False(policy.Allows(CreateContext(cursorX: 1915, cursorY: 20)));
        // Bottom-left corner
        Assert.False(policy.Allows(CreateContext(cursorX: 50, cursorY: 1050)));
        // Bottom-right corner
        Assert.False(policy.Allows(CreateContext(cursorX: 1918, cursorY: 1075)));

        // Non-corners along edges
        Assert.True(policy.Allows(CreateContext(cursorX: 1918, cursorY: 540)));
        Assert.True(policy.Allows(CreateContext(cursorX: 960, cursorY: 2)));

        // When corner blocking is disabled, corner crossing is allowed
        var nonCornerPolicy = new SwitchPolicy(new KvmOptions { BlockInCorners = false });
        Assert.True(nonCornerPolicy.Allows(CreateContext(cursorX: 10, cursorY: 10)));
    }

    [Fact]
    public void Allows_RequireModifier_Ctrl_RequiresCtrlDown()
    {
        var options = new KvmOptions { RequireModifier = "Ctrl" };
        var policy = new SwitchPolicy(options);

        Assert.False(policy.Allows(CreateContext(ctrl: false)));
        Assert.True(policy.Allows(CreateContext(ctrl: true)));
    }

    [Fact]
    public void Allows_RequireModifier_Alt_RequiresAltDown()
    {
        var options = new KvmOptions { RequireModifier = "Alt" };
        var policy = new SwitchPolicy(options);

        Assert.False(policy.Allows(CreateContext(alt: false)));
        Assert.True(policy.Allows(CreateContext(alt: true)));
    }

    [Fact]
    public void Allows_RequireModifier_Shift_RequiresShiftDown()
    {
        var options = new KvmOptions { RequireModifier = "Shift" };
        var policy = new SwitchPolicy(options);

        Assert.False(policy.Allows(CreateContext(shift: false)));
        Assert.True(policy.Allows(CreateContext(shift: true)));
    }

    [Fact]
    public void Allows_RequireModifier_None_AllowsWithoutModifiers()
    {
        var options = new KvmOptions { RequireModifier = "None" };
        var policy = new SwitchPolicy(options);

        Assert.True(policy.Allows(CreateContext(ctrl: false, alt: false, shift: false)));
    }

    [Fact]
    public void Allows_MultipleRulesCombined()
    {
        var options = new KvmOptions
        {
            BlockInFullscreen = true,
            BlockInMaximized = true,
            BlockInCorners = true,
            RequireModifier = "Ctrl"
        };
        var policy = new SwitchPolicy(options);

        // Denied if fullscreen even with Ctrl held
        Assert.False(policy.Allows(CreateContext(fullscreen: true, ctrl: true)));

        // Denied if maximized even with Ctrl held
        Assert.False(policy.Allows(CreateContext(maximized: true, ctrl: true)));

        // Denied if in corner even with Ctrl held
        Assert.False(policy.Allows(CreateContext(cursorX: 10, cursorY: 10, ctrl: true)));

        // Denied if Ctrl not held even when not fullscreen/maximized/corner
        Assert.False(policy.Allows(CreateContext(cursorX: 1918, cursorY: 540, ctrl: false)));

        // Allowed when all rules satisfied
        Assert.True(policy.Allows(CreateContext(cursorX: 1918, cursorY: 540, ctrl: true)));
    }

    [Fact]
    public void DoubleTapOverride_Inside400ms_AllowsCrossing()
    {
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new KvmOptions { BlockInFullscreen = true, DoubleTapOverride = true };
        var policy = new SwitchPolicy(options);

        // Tap 1 at t0 -> denied
        Assert.False(policy.Allows(CreateContext(fullscreen: true, now: t0)));

        // Tap 2 at t0 + 100ms -> allowed via double-tap override!
        Assert.True(policy.Allows(CreateContext(fullscreen: true, now: t0.AddMilliseconds(100))));
    }

    [Fact]
    public void DoubleTapOverride_Outside400ms_DeniesCrossing()
    {
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new KvmOptions { BlockInFullscreen = true, DoubleTapOverride = true };
        var policy = new SwitchPolicy(options);

        // Tap 1 at t0 -> denied
        Assert.False(policy.Allows(CreateContext(fullscreen: true, now: t0)));

        // Tap 2 at t0 + 500ms (> 400ms) -> denied
        Assert.False(policy.Allows(CreateContext(fullscreen: true, now: t0.AddMilliseconds(500))));
    }

    [Fact]
    public void DoubleTapOverride_ContinuousHold_DoesNotTriggerOverride()
    {
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new KvmOptions { BlockInFullscreen = true, DoubleTapOverride = true };
        var policy = new SwitchPolicy(options);

        // Initial bump -> denied
        Assert.False(policy.Allows(CreateContext(fullscreen: true, now: t0)));

        // Immediate continuation of the same bump (e.g. 10ms later without gap) -> still denied
        Assert.False(policy.Allows(CreateContext(fullscreen: true, now: t0.AddMilliseconds(10))));
    }

    [Fact]
    public void DoubleTapOverride_Disabled_AlwaysDenies()
    {
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new KvmOptions { BlockInFullscreen = true, DoubleTapOverride = false };
        var policy = new SwitchPolicy(options);

        // Tap 1 at t0 -> denied
        Assert.False(policy.Allows(CreateContext(fullscreen: true, now: t0)));

        // Tap 2 at t0 + 100ms -> still denied because override is off
        Assert.False(policy.Allows(CreateContext(fullscreen: true, now: t0.AddMilliseconds(100))));
    }

    [Fact]
    public void DoubleTapOverride_CursorLeavesEdgeZone_TriggersOverride()
    {
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var options = new KvmOptions { BlockInFullscreen = true, DoubleTapOverride = true };
        var policy = new SwitchPolicy(options);

        // Tap 1 at t0 -> denied
        Assert.False(policy.Allows(CreateContext(fullscreen: true, now: t0)));

        // Cursor pulls back away from edge
        policy.ResetEdgeZone();

        // Tap 2 at t0 + 80ms -> allowed!
        Assert.True(policy.Allows(CreateContext(fullscreen: true, now: t0.AddMilliseconds(80))));
    }

    [Fact]
    public void SettingsStore_SwitchOptions_RoundTripAndValidate()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mouser_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(tmp);
            store.Current.BlockInFullscreen = false;
            store.Current.BlockInMaximized = true;
            store.Current.BlockInCorners = true;
            store.Current.RequireModifier = "Alt";
            store.Current.DoubleTapOverride = false;
            store.Save();

            var loaded = new SettingsStore(tmp);
            Assert.False(loaded.Current.BlockInFullscreen);
            Assert.True(loaded.Current.BlockInMaximized);
            Assert.True(loaded.Current.BlockInCorners);
            Assert.Equal("Alt", loaded.Current.RequireModifier);
            Assert.False(loaded.Current.DoubleTapOverride);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public async Task KvmController_WithFullscreenProbe_BlocksSwitching()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.Kvm.ForegroundProbe = () => (Fullscreen: true, Maximized: false);

        rig.CrossRight(540);

        Assert.Null(rig.Kvm.ActivePeerId);
        Assert.Empty(rig.PeerSink.Snapshot());
    }

    [Fact]
    public async Task KvmController_WithDoubleTap_CrossesOnSecondTap()
    {
        await using var rig = await KvmRig.CreateAsync();
        rig.Kvm.ForegroundProbe = () => (Fullscreen: true, Maximized: false);

        // First tap -> blocked
        rig.CrossRight(540);
        Assert.Null(rig.Kvm.ActivePeerId);

        // Wait short gap so double-tap timing is met (between 30ms and 400ms)
        await Task.Delay(80);

        // Second tap -> double-tap override triggers crossing!
        rig.CrossRight(540);
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
    }
}
