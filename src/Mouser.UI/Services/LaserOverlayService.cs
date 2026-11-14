using System;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Mouser.UI.Views;

namespace Mouser.UI.Services;

public sealed class LaserOverlayService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WinPoint lpPoint);

    private LaserOverlayWindow? _window;
    private DispatcherTimer? _trackingTimer;
    private bool _isActive;

    public bool IsActive => _isActive;

    public void SetLaserActive(bool active)
    {
        if (_isActive == active) return;
        _isActive = active;

        Dispatcher.UIThread.Post(() =>
        {
            if (active)
            {
                if (_window == null)
                {
                    _window = new LaserOverlayWindow();
                }

                UpdateCursorPosition();
                _window.Show();

                if (_trackingTimer == null)
                {
                    _trackingTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(16) // ~60fps
                    };
                    _trackingTimer.Tick += (_, _) => UpdateCursorPosition();
                }
                _trackingTimer.Start();
            }
            else
            {
                _trackingTimer?.Stop();
                _window?.Hide();
            }
        });
    }

    private void UpdateCursorPosition()
    {
        if (_window == null || !_isActive) return;

        if (OperatingSystem.IsWindows())
        {
            if (GetCursorPos(out var pt))
            {
                _window.UpdatePosition(pt.X, pt.Y);
            }
        }
    }

    public void Dispose()
    {
        _trackingTimer?.Stop();
        _trackingTimer = null;
        if (_window != null)
        {
            _window.Hide();
            _window.Close();
            _window = null;
        }
    }
}
