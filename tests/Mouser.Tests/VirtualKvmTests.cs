using System;
using System.Runtime.InteropServices;
using Mouser.Core.Geometry;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Xunit;

namespace Mouser.Tests;

public sealed class VirtualKvmTests
{
    [Fact]
    public void CoordinateTransformation_RightToLeft_NormalizesVerticalRatio()
    {
        var sourceScreen = new ScreenBounds
        {
            HostId = "mac",
            X = 0,
            Y = 0,
            Width = 1920,
            Height = 1080
        };

        var targetScreen = new ScreenBounds
        {
            HostId = "win",
            X = 1920,
            Y = 0,
            Width = 3840,
            Height = 2160
        };

        // Exit at exactly half height (540px) on the right edge
        var (entryX, entryY) = CoordinateTransformationEngine.CalculateEntryPosition(
            sourceExitX: 1920,
            sourceExitY: 540,
            source: sourceScreen,
            target: targetScreen,
            exitEdge: ScreenEdge.Right);

        // Entry X should be target X (1920)
        Assert.Equal(1920, entryX);
        // Entry Y should be half of 2160 (1080)
        Assert.Equal(1080, entryY);
    }

    [Fact]
    public void CoordinateTransformation_LeftToRight_EntersAtRightmostCoordinate()
    {
        var sourceScreen = new ScreenBounds
        {
            HostId = "win",
            X = 1920,
            Y = 0,
            Width = 1920,
            Height = 1080
        };

        var targetScreen = new ScreenBounds
        {
            HostId = "mac",
            X = 0,
            Y = 0,
            Width = 1920,
            Height = 1080
        };

        // Exit at 25% height (270px) on left edge
        var (entryX, entryY) = CoordinateTransformationEngine.CalculateEntryPosition(
            sourceExitX: 1920,
            sourceExitY: 270,
            source: sourceScreen,
            target: targetScreen,
            exitEdge: ScreenEdge.Left);

        // Enters at right edge of target (Right - 1 = 1919)
        Assert.Equal(1919, entryX);
        Assert.Equal(270, entryY);
    }

    [Fact]
    public void CoordinateTransformation_BottomToTop_NormalizesHorizontalRatio()
    {
        var sourceScreen = new ScreenBounds
        {
            X = 0,
            Y = 0,
            Width = 1000,
            Height = 1000
        };

        var targetScreen = new ScreenBounds
        {
            X = 0,
            Y = 1000,
            Width = 2000,
            Height = 1000
        };

        // Exit at 80% width (800px) on bottom edge
        var (entryX, entryY) = CoordinateTransformationEngine.CalculateEntryPosition(
            sourceExitX: 800,
            sourceExitY: 1000,
            source: sourceScreen,
            target: targetScreen,
            exitEdge: ScreenEdge.Bottom);

        Assert.Equal(1600, entryX); // 80% of 2000
        Assert.Equal(1000, entryY);
    }

    [Fact]
    public void CoordinateTransformation_NormalizeDelta_ScalesAccordingToDpi()
    {
        // 2.0x Retina Mac to 1.0x standard display
        var (adjDx, adjDy) = CoordinateTransformationEngine.NormalizeDelta(10, 20, 2.0, 1.0);
        Assert.Equal(5, adjDx);
        Assert.Equal(10, adjDy);

        // 1.0x to 2.0x display
        var (adjDx2, adjDy2) = CoordinateTransformationEngine.NormalizeDelta(10, 20, 1.0, 2.0);
        Assert.Equal(20, adjDx2);
        Assert.Equal(40, adjDy2);
    }

    [Fact]
    public void KeyTranslation_TranslatesModifiersProperly()
    {
        var translator = new KeyTranslationService();

        // Mac Command (Bit 0) -> Windows Control (Bit 2)
        byte winMod1 = translator.TranslateModifiersMacToWindows(0x01);
        Assert.Equal(0x04, winMod1);

        // Mac Command + Shift (Bit 0 + Bit 1) -> Windows Ctrl + Shift (Bit 2 + Bit 1)
        byte winMod2 = translator.TranslateModifiersMacToWindows(0x03);
        Assert.Equal(0x06, winMod2);

        // Mac Option (Bit 3) -> Windows Alt (Bit 3)
        byte winMod3 = translator.TranslateModifiersMacToWindows(0x08);
        Assert.Equal(0x08, winMod3);
    }

    [Fact]
    public void KeyTranslation_TranslatesKeyCodes()
    {
        var translator = new KeyTranslationService();

        // Mac Command (0x37) -> Windows Control (0x11)
        uint winCtrl = translator.TranslateMacToWindows(KeyTranslationService.MacKey_Command);
        Assert.Equal(KeyTranslationService.WinVk_Control, winCtrl);

        // Mac Option (0x3A) -> Windows Alt (0x12)
        uint winAlt = translator.TranslateMacToWindows(KeyTranslationService.MacKey_Option);
        Assert.Equal(KeyTranslationService.WinVk_Menu, winAlt);

        // Mac Return (0x24) -> Windows Return (0x0D)
        uint winReturn = translator.TranslateMacToWindows(KeyTranslationService.MacKey_Return);
        Assert.Equal(KeyTranslationService.WinVk_Return, winReturn);
    }

    [Fact]
    public void KeyTranslation_TracksKeyStatesAndDrainsStuckKeys()
    {
        var translator = new KeyTranslationService();

        translator.RecordKeyState(KeyTranslationService.MacKey_Command, true);
        translator.RecordKeyState(0x00, true); // Key 'A'
        Assert.Equal(2, translator.HeldKeyCount);

        // Release 'A'
        translator.RecordKeyState(0x00, false);
        Assert.Equal(1, translator.HeldKeyCount);

        // Drain remaining held keys (e.g. before screen transition)
        var stuckKeys = translator.DrainStuckKeys();
        Assert.Single(stuckKeys);
        Assert.Contains(KeyTranslationService.MacKey_Command, stuckKeys);
        Assert.Equal(0, translator.HeldKeyCount);
    }

    [Fact]
    public void VirtualKvmManager_ScreenLock_PreventsBoundaryTransition()
    {
        var local = new ScreenBounds { HostId = "local", X = 0, Y = 0, Width = 1920, Height = 1080 };
        var remote = new ScreenBounds { HostId = "remote", X = 1920, Y = 0, Width = 1920, Height = 1080 };

        var kvm = new VirtualKvmManager(local);
        kvm.AddRemoteScreen(remote);

        // Lock screen
        kvm.IsScreenLocked = true;

        // Push at right border
        bool transitioned = kvm.ProcessLocalMotion(1920, 500, 100, 0);

        Assert.False(transitioned);
        Assert.False(kvm.IsRedirected);
        Assert.Equal("local", kvm.CurrentFocusedHostId);
    }

    [Fact]
    public void VirtualKvmManager_EdgeResistance_RequiresThresholdPushBeforeTransition()
    {
        var local = new ScreenBounds { HostId = "local", X = 0, Y = 0, Width = 1920, Height = 1080 };
        var remote = new ScreenBounds { HostId = "remote", X = 1920, Y = 0, Width = 1920, Height = 1080 };

        var kvm = new VirtualKvmManager(local)
        {
            EdgeResistanceMs = 500, // Large time resistance
            EdgePushDistanceThreshold = 30.0 // Requires 30px push
        };
        kvm.AddRemoteScreen(remote);

        bool eventFired = false;
        kvm.CursorTransitionedToRemote += (_, e) =>
        {
            eventFired = true;
            Assert.Equal("remote", e.TargetHostId);
        };

        // Small push (10px) - should NOT cross
        bool step1 = kvm.ProcessLocalMotion(1920, 500, 10, 0);
        Assert.False(step1);
        Assert.False(eventFired);

        // Additional push bringing accumulated total to 35px - should cross!
        bool step2 = kvm.ProcessLocalMotion(1920, 500, 25, 0);
        Assert.True(step2);
        Assert.True(eventFired);
        Assert.True(kvm.IsRedirected);
        Assert.Equal("remote", kvm.CurrentFocusedHostId);

        // Switch back to local
        kvm.SwitchBackToLocal(500, 500);
        Assert.False(kvm.IsRedirected);
        Assert.Equal("local", kvm.CurrentFocusedHostId);
    }

    [Fact]
    public void InputKeyEventPacket_ValidatesBinaryHeaderAndPacking()
    {
        var packet = InputKeyEventPacket.Create(101, 0x11, 0x04, true);

        Assert.True(packet.IsValid());
        Assert.Equal(InputKeyEventPacket.ExpectedMagic, packet.Magic);
        Assert.Equal(InputKeyEventPacket.ExpectedVersion, packet.Version);
        Assert.Equal(InputKeyEventPacket.ExpectedPacketType, packet.PacketType);
        Assert.Equal(101, packet.SequenceId);
        Assert.Equal(0x11u, packet.KeyCode);
        Assert.Equal(0x04, packet.Modifiers);
        Assert.Equal(1, packet.IsDown);

        // Check exact struct size in memory (16 bytes packed)
        int size = Marshal.SizeOf<InputKeyEventPacket>();
        Assert.Equal(16, size);
    }
}
