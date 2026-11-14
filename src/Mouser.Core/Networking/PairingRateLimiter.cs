using System.Collections.Concurrent;
using System.Net;

namespace Mouser.Core.Networking;

/// <summary>
/// Rate limiter and lockout gate for device pairing requests.
/// Enforces:
/// 1. Maximum 3 requests per minute per source IP.
/// 2. 5 minutes lockout for a source IP after 3 denials.
/// </summary>
public sealed class PairingRateLimiter
{
    private readonly object _lock = new();
    private readonly Dictionary<IPAddress, List<DateTime>> _attempts = new();
    private readonly Dictionary<IPAddress, List<DateTime>> _denials = new();
    private readonly Dictionary<IPAddress, DateTime> _lockouts = new();

    public int MaxAttemptsPerMinute { get; init; } = 3;
    public int MaxDenialsBeforeLockout { get; init; } = 3;
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan AttemptWindow { get; init; } = TimeSpan.FromMinutes(1);

    public bool CanAttempt(IPAddress ip, DateTime utcNow, out string? refusalReason)
    {
        lock (_lock)
        {
            // Normalize IP (e.g. IPv4 mapped to IPv6)
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

            if (_lockouts.TryGetValue(ip, out var lockoutUntil))
            {
                if (utcNow < lockoutUntil)
                {
                    refusalReason = "LockedOut";
                    return false;
                }
                _lockouts.Remove(ip);
            }

            if (_attempts.TryGetValue(ip, out var list))
            {
                list.RemoveAll(t => utcNow - t > AttemptWindow);
                if (list.Count >= MaxAttemptsPerMinute)
                {
                    refusalReason = "RateLimited";
                    return false;
                }
            }

            refusalReason = null;
            return true;
        }
    }

    public void RecordAttempt(IPAddress ip, DateTime utcNow)
    {
        lock (_lock)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

            if (!_attempts.TryGetValue(ip, out var list))
            {
                list = new List<DateTime>();
                _attempts[ip] = list;
            }
            list.Add(utcNow);
        }
    }

    public void RecordDenial(IPAddress ip, DateTime utcNow)
    {
        lock (_lock)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

            if (!_denials.TryGetValue(ip, out var list))
            {
                list = new List<DateTime>();
                _denials[ip] = list;
            }
            // Keep denials within lockout duration window
            list.RemoveAll(t => utcNow - t > LockoutDuration);
            list.Add(utcNow);

            if (list.Count >= MaxDenialsBeforeLockout)
            {
                _lockouts[ip] = utcNow + LockoutDuration;
                list.Clear();
            }
        }
    }

    public void Reset(IPAddress ip)
    {
        lock (_lock)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            _attempts.Remove(ip);
            _denials.Remove(ip);
            _lockouts.Remove(ip);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _attempts.Clear();
            _denials.Clear();
            _lockouts.Clear();
        }
    }
}
