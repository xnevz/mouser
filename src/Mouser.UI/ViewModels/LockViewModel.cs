using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mouser.Core.Security;

namespace Mouser.UI.ViewModels;

public partial class LockViewModel : ViewModelBase
{
    private readonly AppLockService _appLock;

    [ObservableProperty] private string _passphrase = "";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBackingOff;
    [ObservableProperty] private bool _showRecoveryBox;
    [ObservableProperty] private string _recoveryKey = "";

    public LockViewModel(AppLockService appLock)
    {
        _appLock = appLock ?? throw new ArgumentNullException(nameof(appLock));
    }

    [RelayCommand]
    private void ToggleRecoveryBox()
    {
        ShowRecoveryBox = !ShowRecoveryBox;
    }

    [RelayCommand]
    private void Unlock()
    {
        if (_appLock.BackoffRemaining > TimeSpan.Zero)
        {
            IsBackingOff = true;
            int seconds = (int)Math.Ceiling(_appLock.BackoffRemaining.TotalSeconds);
            if (seconds < 1) seconds = 1;
            Error = $"Too many tries. Wait {seconds} seconds.";
            Passphrase = "";
            return;
        }

        bool ok = _appLock.TryUnlock(Passphrase);
        Passphrase = "";

        if (!ok)
        {
            if (_appLock.BackoffRemaining > TimeSpan.Zero)
            {
                IsBackingOff = true;
                int seconds = (int)Math.Ceiling(_appLock.BackoffRemaining.TotalSeconds);
                if (seconds < 1) seconds = 1;
                Error = $"Too many tries. Wait {seconds} seconds.";
            }
            else
            {
                IsBackingOff = false;
                Error = "That passphrase is wrong.";
            }
        }
        else
        {
            Error = null;
            IsBackingOff = false;
            ShowRecoveryBox = false;
        }
    }

    [RelayCommand]
    private void UseRecoveryKey()
    {
        if (_appLock.BackoffRemaining > TimeSpan.Zero)
        {
            IsBackingOff = true;
            int seconds = (int)Math.Ceiling(_appLock.BackoffRemaining.TotalSeconds);
            if (seconds < 1) seconds = 1;
            Error = $"Too many tries. Wait {seconds} seconds.";
            return;
        }

        bool ok = _appLock.TryUnlockWithRecoveryKey(RecoveryKey);
        if (!ok)
        {
            if (_appLock.BackoffRemaining > TimeSpan.Zero)
            {
                IsBackingOff = true;
                int seconds = (int)Math.Ceiling(_appLock.BackoffRemaining.TotalSeconds);
                if (seconds < 1) seconds = 1;
                Error = $"Too many tries. Wait {seconds} seconds.";
            }
            else
            {
                IsBackingOff = false;
                Error = "That recovery key is wrong.";
            }
        }
        else
        {
            Error = null;
            IsBackingOff = false;
            ShowRecoveryBox = false;
            RecoveryKey = "";
        }
    }
}
