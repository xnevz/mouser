using System;
using System.IO;
using System.Linq;
using Mouser.Core.Security;
using Xunit;

namespace Mouser.Tests;

public sealed class AppLockTests : IDisposable
{
    private readonly string _dir;
    private readonly string _lockFile;

    public AppLockTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "MouserLockTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _lockFile = Path.Combine(_dir, "lock.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }

    [Fact]
    public void Setup_Restart_Locked_RightPassphraseUnlocks_MasterKeyMatches()
    {
        var service = new AppLockService(_lockFile);
        Assert.False(service.IsConfigured);
        Assert.False(service.IsLocked);
        Assert.Null(service.MasterKey);

        string recoveryKey = service.Setup("CorrectHorseBatteryStaple123!");
        Assert.NotEmpty(recoveryKey);
        Assert.True(service.IsConfigured);
        Assert.False(service.IsLocked);
        Assert.NotNull(service.MasterKey);
        Assert.Equal(32, service.MasterKey.Length);
        byte[] originalMasterKey = service.MasterKey.ToArray();

        // Restart (simulate by instantiating a new service pointing to the same file)
        var restarted = new AppLockService(_lockFile);
        Assert.True(restarted.IsConfigured);
        Assert.True(restarted.IsLocked);
        Assert.Null(restarted.MasterKey);

        bool unlockedFired = false;
        restarted.Unlocked += () => unlockedFired = true;

        bool unlockResult = restarted.TryUnlock("CorrectHorseBatteryStaple123!");
        Assert.True(unlockResult);
        Assert.False(restarted.IsLocked);
        Assert.NotNull(restarted.MasterKey);
        Assert.Equal(32, restarted.MasterKey.Length);
        Assert.Equal(originalMasterKey, restarted.MasterKey);
        Assert.True(unlockedFired);
    }

    [Fact]
    public void WrongPassphrase_ReturnsFalse_Backoff_UnlocksAfterClockAdvance()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var service = new AppLockService(_lockFile, () => now);

        service.Setup("SuperSecretPassphrase123");
        service.Lock();
        Assert.True(service.IsLocked);

        // Failures 1 to 3: no backoff delay
        for (int i = 1; i <= 3; i++)
        {
            Assert.False(service.TryUnlock($"wrong_pass_{i}"));
            Assert.Equal(TimeSpan.Zero, service.BackoffRemaining);
        }

        // 4th failure: 5-second backoff delay
        Assert.False(service.TryUnlock("wrong_pass_4"));
        Assert.True(service.BackoffRemaining > TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromSeconds(5), service.BackoffRemaining);

        // Correct passphrase during backoff returns false immediately without unlocking
        Assert.False(service.TryUnlock("SuperSecretPassphrase123"));
        Assert.True(service.IsLocked);

        // Advance clock past backoff
        now = now.AddSeconds(6);
        Assert.Equal(TimeSpan.Zero, service.BackoffRemaining);

        // Now correct passphrase unlocks
        Assert.True(service.TryUnlock("SuperSecretPassphrase123"));
        Assert.False(service.IsLocked);
        Assert.Equal(TimeSpan.Zero, service.BackoffRemaining);
    }

    [Fact]
    public void RecoveryKey_Unlocks_WithOrWithoutDashes_AndLowercase()
    {
        var service = new AppLockService(_lockFile);
        string recoveryKey = service.Setup("Password12345");
        Assert.NotEmpty(recoveryKey);
        byte[] originalMasterKey = service.MasterKey!.ToArray();

        // Unlock with dashes
        service.Lock();
        Assert.True(service.IsLocked);
        Assert.True(service.TryUnlockWithRecoveryKey(recoveryKey));
        Assert.False(service.IsLocked);
        Assert.Equal(originalMasterKey, service.MasterKey);

        // Unlock without dashes
        service.Lock();
        Assert.True(service.IsLocked);
        string noDashes = recoveryKey.Replace("-", "");
        Assert.True(service.TryUnlockWithRecoveryKey(noDashes));
        Assert.False(service.IsLocked);
        Assert.Equal(originalMasterKey, service.MasterKey);

        // Unlock with lowercase
        service.Lock();
        Assert.True(service.IsLocked);
        string lowercase = recoveryKey.ToLowerInvariant();
        Assert.True(service.TryUnlockWithRecoveryKey(lowercase));
        Assert.False(service.IsLocked);
        Assert.Equal(originalMasterKey, service.MasterKey);

        // ChangePassphrase makes old fail, new succeed, master key unchanged
        Assert.True(service.ChangePassphrase("Password12345", "BrandNewPassword999!"));
        service.Lock();
        Assert.False(service.TryUnlock("Password12345"));
        Assert.True(service.TryUnlock("BrandNewPassword999!"));
        Assert.Equal(originalMasterKey, service.MasterKey);
    }

    [Fact]
    public void Lock_ZeroesMasterKey_AndRaisesEventOnce()
    {
        var service = new AppLockService(_lockFile);
        service.Setup("Password12345");
        byte[] masterKeyRef = service.MasterKey!;
        Assert.NotNull(masterKeyRef);
        Assert.Contains(masterKeyRef, b => b != 0);

        int lockedEvents = 0;
        service.Locked += () => lockedEvents++;

        service.Lock();
        Assert.True(service.IsLocked);
        Assert.Null(service.MasterKey);
        Assert.All(masterKeyRef, b => Assert.Equal(0, b));
        Assert.Equal(1, lockedEvents);

        // Calling Lock() again while already locked should not raise event again
        service.Lock();
        Assert.Equal(1, lockedEvents);
    }

    [Fact]
    public void Passphrase_ShorterThan8Chars_ThrowsArgumentException()
    {
        var service = new AppLockService(_lockFile);
        Assert.Throws<ArgumentException>(() => service.Setup("short"));
        Assert.Throws<ArgumentException>(() => service.Setup("1234567"));

        service.Setup("ValidPass123");
        Assert.Throws<ArgumentException>(() => service.ChangePassphrase("ValidPass123", "short"));
        Assert.Throws<ArgumentException>(() => service.ChangePassphrase("short", "ValidNewPass456"));
    }

    [Fact]
    public void CorruptLockFile_ThrowsInvalidDataException_AndCreatesCorruptBackup()
    {
        File.WriteAllText(_lockFile, "{ \"Corrupt\": true, \"NotValidLockData\": 123 }");

        var ex = Assert.Throws<InvalidDataException>(() => new AppLockService(_lockFile));
        Assert.NotNull(ex);

        string corruptPath = _lockFile + ".corrupt";
        Assert.True(File.Exists(corruptPath));
        Assert.Equal("{ \"Corrupt\": true, \"NotValidLockData\": 123 }", File.ReadAllText(corruptPath));
    }

    [Fact]
    public void Remove_WithCorrectPassphrase_DeletesFileAndUnconfigures_WrongPassphraseThrows()
    {
        var service = new AppLockService(_lockFile);
        // Remove on unconfigured does nothing
        service.Remove("anything");
        Assert.False(service.IsConfigured);

        service.Setup("MasterPassphrase123");
        Assert.True(File.Exists(_lockFile));
        Assert.True(service.IsConfigured);

        // Wrong passphrase throws UnauthorizedAccessException
        Assert.Throws<UnauthorizedAccessException>(() => service.Remove("WrongPassphrase!"));
        Assert.True(File.Exists(_lockFile));
        Assert.True(service.IsConfigured);

        // Correct passphrase removes file and unconfigures
        service.Remove("MasterPassphrase123");
        Assert.False(File.Exists(_lockFile));
        Assert.False(service.IsConfigured);
        Assert.False(service.IsLocked);
        Assert.Null(service.MasterKey);
    }

    [Fact]
    public void Backoff_EscalatesWithConsecutiveFailures()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var service = new AppLockService(_lockFile, () => now);
        service.Setup("TargetPassphrase123");
        service.Lock();

        // 1-3: 0s
        for (int i = 0; i < 3; i++)
        {
            service.TryUnlock("wrong");
            Assert.Equal(TimeSpan.Zero, service.BackoffRemaining);
        }

        // 4th: 5s
        service.TryUnlock("wrong");
        Assert.Equal(TimeSpan.FromSeconds(5), service.BackoffRemaining);

        // 5th: 15s
        now = now.AddSeconds(5);
        service.TryUnlock("wrong");
        Assert.Equal(TimeSpan.FromSeconds(15), service.BackoffRemaining);

        // 6th: 60s
        now = now.AddSeconds(15);
        service.TryUnlock("wrong");
        Assert.Equal(TimeSpan.FromSeconds(60), service.BackoffRemaining);

        // 7th: 300s
        now = now.AddSeconds(60);
        service.TryUnlock("wrong");
        Assert.Equal(TimeSpan.FromSeconds(300), service.BackoffRemaining);

        // Advance 300s and unlock succeeds, resetting backoff
        now = now.AddSeconds(300);
        Assert.Equal(TimeSpan.Zero, service.BackoffRemaining);
        Assert.True(service.TryUnlock("TargetPassphrase123"));
    }
}
