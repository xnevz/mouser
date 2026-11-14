using Mouser.Core.Kvm;

namespace Mouser.Tests;

public class DragDropPolicyTests
{
    [Fact]
    public void ShouldStartDrag_HappyPath_ReturnsTrue()
    {
        bool shouldStart = DragDropPolicy.ShouldStartDrag(
            isLeftButtonDown: true,
            isSourceExplorer: true,
            totalSizeBytes: 10 * 1024 * 1024); // 10 MB

        Assert.True(shouldStart);
    }

    [Fact]
    public void ShouldStartDrag_WhenButtonNotDown_ReturnsFalse()
    {
        bool shouldStart = DragDropPolicy.ShouldStartDrag(
            isLeftButtonDown: false,
            isSourceExplorer: true,
            totalSizeBytes: 10 * 1024 * 1024);

        Assert.False(shouldStart);
    }

    [Fact]
    public void ShouldStartDrag_WhenSourceNotExplorer_ReturnsFalse()
    {
        bool shouldStart = DragDropPolicy.ShouldStartDrag(
            isLeftButtonDown: true,
            isSourceExplorer: false,
            totalSizeBytes: 10 * 1024 * 1024);

        Assert.False(shouldStart);
    }

    [Fact]
    public void ShouldStartDrag_WhenSizeExceedsBudget_ReturnsFalse()
    {
        long oversized = DragDropPolicy.DefaultMaxDragSizeBytes + 1;

        bool shouldStart = DragDropPolicy.ShouldStartDrag(
            isLeftButtonDown: true,
            isSourceExplorer: true,
            totalSizeBytes: oversized);

        Assert.False(shouldStart);
        Assert.True(DragDropPolicy.ExceedsSizeBudget(oversized));
    }

    [Fact]
    public void ShouldStartDrag_WhenSizeZeroOrNegative_ReturnsFalse()
    {
        Assert.False(DragDropPolicy.ShouldStartDrag(true, true, 0));
        Assert.False(DragDropPolicy.ShouldStartDrag(true, true, -100));
    }

    [Theory]
    [InlineData("explorer", "CabinetWClass", true)]
    [InlineData("explorer.exe", "ExploreWClass", true)]
    [InlineData("explorer", "WorkerW", true)]
    [InlineData("explorer", "Progman", true)]
    [InlineData("explorer", null, true)]
    [InlineData("chrome.exe", "Chrome_WidgetWin_1", false)]
    [InlineData("notepad.exe", "Notepad", false)]
    [InlineData(null, null, false)]
    public void IsExplorerWindow_DetectsShellProcessesCorrectly(string? processName, string? windowClass, bool expected)
    {
        bool result = DragDropPolicy.IsExplorerWindow(processName, windowClass);
        Assert.Equal(expected, result);
    }
}
