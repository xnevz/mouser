using System;
using System.Threading;

namespace Mouser.UI.Services;

/// <summary>First process owns the mutex; later launches ping it to show its window, then exit.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show;
    public bool IsFirst { get; }

    public SingleInstance(string name = "Mouser")
    {
        _mutex = new Mutex(true, $@"Local\{name}.Lock", out bool created);
        _show = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Show");
        IsFirst = created;
    }

    /// <summary>Second instance calls this and then exits.</summary>
    public void SignalFirst() => _show.Set();

    /// <summary>First instance: runs onShow on a background thread each time another launch happens.</summary>
    public void Listen(Action onShow) =>
        new Thread(() =>
        {
            try
            {
                while (_show.WaitOne()) onShow();
            }
            catch (ObjectDisposedException)
            {
            }
        }) { IsBackground = true, Name = "Mouser single-instance" }.Start();

    public void Dispose()
    {
        if (IsFirst)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex.Dispose();
        _show.Dispose();
    }
}
