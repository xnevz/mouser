using System.Net;
using System.Security.Cryptography;

namespace Mouser.Core.Networking;

/// <summary>
/// Decides which remote addresses may inject input. A sender becomes trusted by sending the host's
/// 6-digit PIN; repeated wrong guesses lock that address out for a while.
/// shortcut: trust is keyed by source IP and the PIN travels in clear text, which stops casual LAN
/// injection but not an attacker who can sniff or spoof packets; upgrade to a key-exchange + HMAC
/// handshake before advertising this as secure.
/// </summary>
public sealed class PairingGate
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(60);

    private readonly object _lock = new();
    private readonly HashSet<IPAddress> _trusted = new();
    private readonly Dictionary<IPAddress, (int Failures, DateTime LockedUntil)> _failures = new();
    private readonly Func<DateTime> _clock;
    private int _pin;

    public PairingGate(int pin, Func<DateTime>? clock = null)
    {
        _clock = clock ?? (() => DateTime.UtcNow);
        Pin = pin;
    }

    public event Action<IPAddress>? Paired;

    public int Pin
    {
        get { lock (_lock) return _pin; }
        set
        {
            if (value is < 0 or > 999_999) throw new ArgumentOutOfRangeException(nameof(value), "PIN must have 6 digits.");
            lock (_lock)
            {
                if (_pin == value) return;
                _pin = value;
                // A new PIN invalidates everyone who paired with the old one.
                _trusted.Clear();
                _failures.Clear();
            }
        }
    }

    public static int GeneratePin() => RandomNumberGenerator.GetInt32(0, 1_000_000);

    public static string FormatPin(int pin) => pin.ToString("000000").Insert(3, " ");

    public bool IsTrusted(IPAddress address)
    {
        lock (_lock) return _trusted.Contains(Normalize(address));
    }

    public IReadOnlyList<IPAddress> TrustedAddresses
    {
        get { lock (_lock) return _trusted.ToList(); }
    }

    public bool TryPair(IPAddress address, int pin)
    {
        address = Normalize(address);
        bool accepted;
        lock (_lock)
        {
            var now = _clock();
            if (_failures.TryGetValue(address, out var f) && f.LockedUntil > now) return false;

            accepted = pin == _pin;
            if (accepted)
            {
                _trusted.Add(address);
                _failures.Remove(address);
            }
            else
            {
                int failures = f.LockedUntil > DateTime.MinValue && f.LockedUntil <= now ? 1 : f.Failures + 1;
                _failures[address] = failures >= MaxFailedAttempts
                    ? (0, now + LockoutDuration)
                    : (failures, DateTime.MinValue);
            }
        }

        if (accepted) Paired?.Invoke(address);
        return accepted;
    }

    public void Revoke(IPAddress address)
    {
        lock (_lock) _trusted.Remove(Normalize(address));
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
