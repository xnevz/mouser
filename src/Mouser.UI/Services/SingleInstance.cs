using System;
using System.Threading;

namespace Mouser.UI.Services;

/// <summary>First process owns the mutex; later launches ping it to show its window, then exit.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex? _mutex;
    private readonly EventWaitHandle? _show;
    public bool IsFirst { get; }
    public string MutexName { get; }

    public SingleInstance(string name = "Mouser")
    {
        string user = Environment.UserName.Replace("\\", "_");
        string baseName = name == "Mouser" ? $"Mouser-SingleInstance-{user}" : $"{name}-{user}";
        string globalMutexName = $@"Global\{baseName}.Lock";
        string globalShowName = $@"Global\{baseName}.Show";

        bool created = false;
        try
        {
            _mutex = new Mutex(true, globalMutexName, out created);
            _show = new EventWaitHandle(false, EventResetMode.AutoReset, globalShowName);
            MutexName = globalMutexName;
            IsFirst = created;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            try
            {
                string localMutexName = $@"Local\{baseName}.Lock";
                string localShowName = $@"Local\{baseName}.Show";
                _mutex = new Mutex(true, localMutexName, out created);
                _show = new EventWaitHandle(false, EventResetMode.AutoReset, localShowName);
                MutexName = localMutexName;
                IsFirst = created;
            }
            catch
            {
                MutexName = globalMutexName;
                IsFirst = false;
            }
        }
    }

    /// <summary>Second instance calls this and then exits.</summary>
    public void SignalFirst()
    {
        try
        {
            _show?.Set();
        }
        catch { }
    }

    /// <summary>First instance: runs onShow on a background thread each time another launch happens.</summary>
    public void Listen(Action onShow)
    {
        if (_show == null) return;
        new Thread(() =>
        {
            try
            {
                while (_show.WaitOne()) onShow();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (AbandonedMutexException)
            {
            }
        }) { IsBackground = true, Name = "Mouser single-instance" }.Start();
    }

    public void Dispose()
    {
        if (IsFirst && _mutex != null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex?.Dispose();
        _show?.Dispose();
    }
}
