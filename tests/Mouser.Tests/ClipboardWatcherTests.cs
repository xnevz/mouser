using System.Threading;
using System.Threading.Tasks;
using Mouser.UI.Services;
using Xunit;

namespace Mouser.Tests;

public class ClipboardWatcherTests
{
    [Fact]
    public async Task DoesNotFire_WhileSequenceIsStable()
    {
        uint val = 1;
        int firedCount = 0;
        using var watcher = new ClipboardWatcher(() => val, intervalMs: 20);
        watcher.Changed += () => Interlocked.Increment(ref firedCount);

        await Task.Delay(100);

        Assert.Equal(0, Volatile.Read(ref firedCount));
    }

    [Fact]
    public async Task FiresChanged_WhenSequenceChanges()
    {
        uint val = 1;
        int firedCount = 0;
        using var watcher = new ClipboardWatcher(() => val, intervalMs: 20);
        watcher.Changed += () => Interlocked.Increment(ref firedCount);

        await Task.Delay(50);
        Assert.Equal(0, Volatile.Read(ref firedCount));

        val++;
        await Wait.UntilAsync(() => Volatile.Read(ref firedCount) == 1, timeoutMs: 1000);

        Assert.Equal(1, Volatile.Read(ref firedCount));
    }

    [Fact]
    public async Task DisposedWatcher_StopsFiring()
    {
        uint val = 1;
        int firedCount = 0;
        var watcher = new ClipboardWatcher(() => val, intervalMs: 20);
        watcher.Changed += () => Interlocked.Increment(ref firedCount);

        val++;
        await Wait.UntilAsync(() => Volatile.Read(ref firedCount) == 1, timeoutMs: 1000);

        watcher.Dispose();
        await Task.Delay(60);

        val++;
        await Task.Delay(100);

        Assert.Equal(1, Volatile.Read(ref firedCount));
    }

    [WindowsFact]
    public void DefaultWatcher_ConstructsAndDisposesCleanly()
    {
        using var watcher = new ClipboardWatcher();
        Assert.NotNull(watcher);
    }
}
