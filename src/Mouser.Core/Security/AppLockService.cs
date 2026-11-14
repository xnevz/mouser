using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mouser.Core.Clipboard;

namespace Mouser.Core.Security;

public sealed class AppLockService
{
    private const int SaltSizeBytes = 16;
    private const int MasterKeySizeBytes = 32;
    private const int DefaultIterations = 600_000;
    private const string RecoveryAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Func<DateTime> _clock;
    private readonly int _iterations;
    private readonly object _sync = new();

    private LockData? _lockData;

    public bool IsConfigured { get; private set; }
    public bool IsLocked { get; private set; }
    public byte[]? MasterKey { get; private set; }

    public event Action? Locked;
    public event Action? Unlocked;

    public AppLockService(string filePath, Func<DateTime>? clock = null)
        : this(filePath, clock, DefaultIterations)
    {
    }

    internal AppLockService(string filePath, Func<DateTime>? clock, int iterations)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        _filePath = filePath;
        _clock = clock ?? (() => DateTime.UtcNow);
        _iterations = iterations > 0 ? iterations : DefaultIterations;

        if (!File.Exists(_filePath))
        {
            IsConfigured = false;
            IsLocked = false;
            MasterKey = null;
            return;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<LockData>(json, JsonOptions);
            if (data == null ||
                string.IsNullOrEmpty(data.Salt) ||
                string.IsNullOrEmpty(data.WrappedByPassphrase) ||
                string.IsNullOrEmpty(data.RecoverySalt) ||
                string.IsNullOrEmpty(data.WrappedByRecovery))
            {
                throw new InvalidDataException("Lock data contains missing or invalid fields.");
            }

            byte[] salt = Convert.FromBase64String(data.Salt);
            byte[] recoverySalt = Convert.FromBase64String(data.RecoverySalt);
            if (salt.Length != SaltSizeBytes || recoverySalt.Length != SaltSizeBytes)
            {
                throw new InvalidDataException("Invalid salt length.");
            }

            byte[] wrappedPassphrase = Convert.FromBase64String(data.WrappedByPassphrase);
            byte[] wrappedRecovery = Convert.FromBase64String(data.WrappedByRecovery);
            int minContainerSize = ClipboardCryptoEngine.NonceSizeBytes + ClipboardCryptoEngine.TagSizeBytes + MasterKeySizeBytes;
            if (wrappedPassphrase.Length < minContainerSize || wrappedRecovery.Length < minContainerSize)
            {
                throw new InvalidDataException("Encrypted container is truncated.");
            }

            _lockData = data;
            IsConfigured = true;
            IsLocked = true;
            MasterKey = null;
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            TryBackupCorruptFile();
            throw new InvalidDataException("Failed to read lock file.", ex);
        }
        catch (InvalidDataException)
        {
            TryBackupCorruptFile();
            throw;
        }
    }

    public TimeSpan BackoffRemaining
    {
        get
        {
            lock (_sync)
            {
                if (_lockData?.NextAttemptUtc is not { } nextAttempt)
                {
                    return TimeSpan.Zero;
                }

                DateTime now = _clock();
                DateTime nextUtc = nextAttempt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(nextAttempt, DateTimeKind.Utc)
                    : nextAttempt.ToUniversalTime();
                DateTime nowUtc = now.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(now, DateTimeKind.Utc)
                    : now.ToUniversalTime();

                if (nextUtc > nowUtc)
                {
                    return nextUtc - nowUtc;
                }

                return TimeSpan.Zero;
            }
        }
    }

    /// <summary>First-time setup. Returns the recovery key (show once, never stored).</summary>
    public string Setup(string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase) || passphrase.Length < 8)
        {
            throw new ArgumentException("Passphrase must be at least 8 characters.", nameof(passphrase));
        }

        lock (_sync)
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException("App lock is already configured.");
            }

            byte[] masterKey = ClipboardCryptoEngine.GenerateKey();
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] recoverySalt = RandomNumberGenerator.GetBytes(SaltSizeBytes);

            byte[] kek = DeriveKek(passphrase, salt, _iterations);
            byte[] wrappedByPassphrase = ClipboardCryptoEngine.Encrypt(masterKey, kek);
            CryptographicOperations.ZeroMemory(kek);

            string recoveryKey = GenerateRecoveryKey();
            string normalizedRecoveryKey = NormalizeRecoveryKey(recoveryKey);
            byte[] recoveryKek = DeriveKek(normalizedRecoveryKey, recoverySalt, _iterations);
            byte[] wrappedByRecovery = ClipboardCryptoEngine.Encrypt(masterKey, recoveryKek);
            CryptographicOperations.ZeroMemory(recoveryKek);

            _lockData = new LockData
            {
                Version = 1,
                Salt = Convert.ToBase64String(salt),
                Iterations = _iterations,
                WrappedByPassphrase = Convert.ToBase64String(wrappedByPassphrase),
                RecoverySalt = Convert.ToBase64String(recoverySalt),
                WrappedByRecovery = Convert.ToBase64String(wrappedByRecovery),
                Failures = 0,
                NextAttemptUtc = null
            };

            SaveLockData();

            IsConfigured = true;
            IsLocked = false;
            MasterKey = masterKey;

            return recoveryKey;
        }
    }

    public bool TryUnlock(string passphrase)
    {
        Action? raiseUnlocked = null;
        lock (_sync)
        {
            if (BackoffRemaining > TimeSpan.Zero)
            {
                return false;
            }

            if (!IsConfigured || _lockData == null || string.IsNullOrEmpty(passphrase))
            {
                if (IsConfigured && _lockData != null)
                {
                    RecordFailure();
                }
                return false;
            }

            byte[] salt = Convert.FromBase64String(_lockData.Salt);
            byte[] kek = DeriveKek(passphrase, salt, _lockData.Iterations);
            byte[] wrapped = Convert.FromBase64String(_lockData.WrappedByPassphrase);

            byte[] plain;
            try
            {
                plain = ClipboardCryptoEngine.Decrypt(wrapped, kek);
            }
            catch (CryptographicException)
            {
                RecordFailure();
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(kek);
            }

            if (plain.Length != MasterKeySizeBytes)
            {
                CryptographicOperations.ZeroMemory(plain);
                RecordFailure();
                return false;
            }

            raiseUnlocked = RecordSuccess(plain);
        }

        raiseUnlocked?.Invoke();
        return true;
    }

    public bool TryUnlockWithRecoveryKey(string recoveryKey)
    {
        Action? raiseUnlocked = null;
        lock (_sync)
        {
            if (BackoffRemaining > TimeSpan.Zero)
            {
                return false;
            }

            if (!IsConfigured || _lockData == null || string.IsNullOrWhiteSpace(recoveryKey))
            {
                if (IsConfigured && _lockData != null)
                {
                    RecordFailure();
                }
                return false;
            }

            string normalized = NormalizeRecoveryKey(recoveryKey);
            if (string.IsNullOrEmpty(normalized))
            {
                RecordFailure();
                return false;
            }

            byte[] recoverySalt = Convert.FromBase64String(_lockData.RecoverySalt);
            byte[] recoveryKek = DeriveKek(normalized, recoverySalt, _lockData.Iterations);
            byte[] wrapped = Convert.FromBase64String(_lockData.WrappedByRecovery);

            byte[] plain;
            try
            {
                plain = ClipboardCryptoEngine.Decrypt(wrapped, recoveryKek);
            }
            catch (CryptographicException)
            {
                RecordFailure();
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(recoveryKek);
            }

            if (plain.Length != MasterKeySizeBytes)
            {
                CryptographicOperations.ZeroMemory(plain);
                RecordFailure();
                return false;
            }

            raiseUnlocked = RecordSuccess(plain);
        }

        raiseUnlocked?.Invoke();
        return true;
    }

    public void Lock()
    {
        Action? raiseLocked = null;
        lock (_sync)
        {
            if (!IsConfigured)
            {
                return;
            }

            if (MasterKey != null)
            {
                CryptographicOperations.ZeroMemory(MasterKey);
                MasterKey = null;
            }

            if (!IsLocked)
            {
                IsLocked = true;
                raiseLocked = Locked;
            }
        }

        raiseLocked?.Invoke();
    }

    public bool ChangePassphrase(string oldPassphrase, string newPassphrase)
    {
        if (string.IsNullOrEmpty(oldPassphrase) || oldPassphrase.Length < 8)
        {
            throw new ArgumentException("Passphrase must be at least 8 characters.", nameof(oldPassphrase));
        }

        if (string.IsNullOrEmpty(newPassphrase) || newPassphrase.Length < 8)
        {
            throw new ArgumentException("Passphrase must be at least 8 characters.", nameof(newPassphrase));
        }

        lock (_sync)
        {
            if (!IsConfigured || _lockData == null)
            {
                return false;
            }

            if (BackoffRemaining > TimeSpan.Zero)
            {
                return false;
            }

            byte[] oldSalt = Convert.FromBase64String(_lockData.Salt);
            byte[] oldKek = DeriveKek(oldPassphrase, oldSalt, _lockData.Iterations);
            byte[] wrapped = Convert.FromBase64String(_lockData.WrappedByPassphrase);

            byte[] currentMasterKey;
            try
            {
                currentMasterKey = ClipboardCryptoEngine.Decrypt(wrapped, oldKek);
            }
            catch (CryptographicException)
            {
                RecordFailure();
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(oldKek);
            }

            if (currentMasterKey.Length != MasterKeySizeBytes)
            {
                CryptographicOperations.ZeroMemory(currentMasterKey);
                RecordFailure();
                return false;
            }

            byte[] newSalt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] newKek = DeriveKek(newPassphrase, newSalt, _lockData.Iterations);
            byte[] newWrapped = ClipboardCryptoEngine.Encrypt(currentMasterKey, newKek);
            CryptographicOperations.ZeroMemory(newKek);

            _lockData.Salt = Convert.ToBase64String(newSalt);
            _lockData.WrappedByPassphrase = Convert.ToBase64String(newWrapped);
            _lockData.Failures = 0;
            _lockData.NextAttemptUtc = null;
            SaveLockData();

            if (IsLocked)
            {
                CryptographicOperations.ZeroMemory(currentMasterKey);
            }
            else
            {
                if (MasterKey == null)
                {
                    MasterKey = currentMasterKey;
                }
                else
                {
                    CryptographicOperations.ZeroMemory(currentMasterKey);
                }
            }

            return true;
        }
    }

    public void Remove(string passphrase)
    {
        lock (_sync)
        {
            if (!IsConfigured || _lockData == null)
            {
                return;
            }

            if (BackoffRemaining > TimeSpan.Zero)
            {
                throw new UnauthorizedAccessException("Unlocking is temporarily locked out.");
            }

            byte[] salt = Convert.FromBase64String(_lockData.Salt);
            byte[] kek = DeriveKek(passphrase, salt, _lockData.Iterations);
            byte[] wrapped = Convert.FromBase64String(_lockData.WrappedByPassphrase);

            try
            {
                byte[] plain = ClipboardCryptoEngine.Decrypt(wrapped, kek);
                CryptographicOperations.ZeroMemory(plain);
            }
            catch (CryptographicException)
            {
                RecordFailure();
                throw new UnauthorizedAccessException("Incorrect passphrase.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(kek);
            }

            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            string tmp = _filePath + ".tmp";
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); } catch { }
            }

            if (MasterKey != null)
            {
                CryptographicOperations.ZeroMemory(MasterKey);
                MasterKey = null;
            }

            _lockData = null;
            IsConfigured = false;
            IsLocked = false;
        }
    }

    private void RecordFailure()
    {
        _lockData!.Failures++;
        TimeSpan delay = _lockData.Failures switch
        {
            <= 3 => TimeSpan.Zero,
            4 => TimeSpan.FromSeconds(5),
            5 => TimeSpan.FromSeconds(15),
            6 => TimeSpan.FromSeconds(60),
            _ => TimeSpan.FromSeconds(300)
        };

        DateTime now = _clock();
        _lockData.NextAttemptUtc = delay > TimeSpan.Zero ? now + delay : null;
        SaveLockData();
    }

    private Action? RecordSuccess(byte[] decryptedMasterKey)
    {
        _lockData!.Failures = 0;
        _lockData.NextAttemptUtc = null;
        SaveLockData();

        if (MasterKey != null && !ReferenceEquals(MasterKey, decryptedMasterKey))
        {
            CryptographicOperations.ZeroMemory(MasterKey);
        }
        MasterKey = decryptedMasterKey;

        bool wasLocked = IsLocked;
        IsLocked = false;

        return wasLocked ? Unlocked : null;
    }

    private void SaveLockData()
    {
        if (_lockData == null) return;
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_lockData, JsonOptions));
        File.Move(tmp, _filePath, overwrite: true);
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Copy(_filePath, _filePath + ".corrupt", overwrite: true);
            }
        }
        catch
        {
            // Ignore backup copy failure
        }
    }

    private static byte[] DeriveKek(string secret, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, MasterKeySizeBytes);
    }

    private static string GenerateRecoveryKey()
    {
        var chars = new char[40];
        for (int i = 0; i < 40; i++)
        {
            chars[i] = RecoveryAlphabet[RandomNumberGenerator.GetInt32(RecoveryAlphabet.Length)];
        }

        var parts = new string[8];
        for (int i = 0; i < 8; i++)
        {
            parts[i] = new string(chars, i * 5, 5);
        }

        return string.Join("-", parts);
    }

    private static string NormalizeRecoveryKey(string recoveryKey)
    {
        if (string.IsNullOrWhiteSpace(recoveryKey)) return string.Empty;
        var sb = new StringBuilder();
        foreach (char c in recoveryKey)
        {
            if (c is not '-' and not ' ' and not '\t' and not '\r' and not '\n')
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }
        return sb.ToString();
    }

    private sealed class LockData
    {
        public int Version { get; set; } = 1;
        public string Salt { get; set; } = string.Empty;
        public int Iterations { get; set; } = DefaultIterations;
        public string WrappedByPassphrase { get; set; } = string.Empty;
        public string RecoverySalt { get; set; } = string.Empty;
        public string WrappedByRecovery { get; set; } = string.Empty;
        public int Failures { get; set; }
        public DateTime? NextAttemptUtc { get; set; }
    }
}
