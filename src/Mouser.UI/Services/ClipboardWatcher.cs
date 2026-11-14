using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Mouser.UI.Services;

public sealed class ClipboardWatcher : IDisposable
{
    private readonly Func<uint> _sequence;
    private readonly CancellationTokenSource _cts = new();
    public event Action? Changed;

    public ClipboardWatcher(Func<uint>? sequence = null, int intervalMs = 150)
    {
        _sequence = sequence ?? DefaultSequence;
        uint last = _sequence();
        new Thread(() =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    Thread.Sleep(intervalMs);
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }

                if (_cts.IsCancellationRequested) break;

                uint now = _sequence();
                if (now != last)
                {
                    last = now;
                    Changed?.Invoke();
                }
            }
        })
        {
            IsBackground = true,
            Name = "Mouser clipboard watcher"
        }.Start();
    }

    private static uint DefaultSequence() => OperatingSystem.IsWindows() ? GetClipboardSequenceNumber() : 0;

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    public void Dispose() => _cts.Cancel();
}
