using System.Net;
using System.Text.RegularExpressions;
using Mouser.Core.Networking;

namespace Mouser.Tests;

public sealed class TcpPairingTests : IDisposable
{
    private readonly string _downloadsA = Path.Combine(Path.GetTempPath(), "mouser_pair_a_" + Guid.NewGuid().ToString("N"));
    private readonly string _downloadsB = Path.Combine(Path.GetTempPath(), "mouser_pair_b_" + Guid.NewGuid().ToString("N"));
    private readonly PeerTransferService _serviceA;
    private readonly PeerTransferService _serviceB;

    public TcpPairingTests()
    {
        _serviceA = new PeerTransferService("device-a", (_, _) => Array.Empty<byte[]>(), _downloadsA, port: 0, deviceName: "Computer-A");
        _serviceB = new PeerTransferService("device-b", (_, _) => Array.Empty<byte[]>(), _downloadsB, port: 0, deviceName: "Computer-B");
    }

    private IPEndPoint EndPointB => new(IPAddress.Loopback, _serviceB.BoundPort);

    [Fact]
    public async Task Handshake_Loopback_BothConfirm_StoresKeyAndMatchesCode()
    {
        _serviceB.AllowNewDevices(TimeSpan.FromMinutes(2));

        string? responderCode = null;
        _serviceB.PairingRequested += args =>
        {
            responderCode = args.ConfirmationCode;
            args.Allow();
        };

        var responderCompleted = new TaskCompletionSource<(string Id, string Name, byte[] Key)>();
        _serviceB.PairingCompleted += (id, name, key) => responderCompleted.TrySetResult((id, name, key));

        string? initiatorCode = null;
        var result = await _serviceA.RequestPairingAsync(EndPointB, (name, code) =>
        {
            initiatorCode = code;
            return Task.FromResult(true);
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PairKey);
        Assert.Equal(32, result.PairKey.Length);
        Assert.NotNull(result.ConfirmationCode);
        Assert.Matches(@"^\d{3} \d{3}$", result.ConfirmationCode);

        // Codes shown on both screens must be identical
        Assert.Equal(result.ConfirmationCode, responderCode);
        Assert.Equal(result.ConfirmationCode, initiatorCode);

        // Responder completed event
        var completed = await responderCompleted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("device-a", completed.Id);
        Assert.Equal("Computer-A", completed.Name);
        Assert.Equal(result.PairKey, completed.Key);
    }

    [Fact]
    public async Task Handshake_WrongConfirmation_ResponderDenies_NoKeyStored()
    {
        _serviceB.AllowNewDevices(TimeSpan.FromMinutes(2));

        _serviceB.PairingRequested += args =>
        {
            args.Deny();
        };

        bool responderCompletedFired = false;
        _serviceB.PairingCompleted += (_, _, _) => responderCompletedFired = true;

        var result = await _serviceA.RequestPairingAsync(EndPointB, (name, code) => Task.FromResult(true));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsRefused);
        Assert.Equal("DeniedByUser", result.Reason);
        Assert.Null(result.PairKey);
        Assert.False(responderCompletedFired);
    }

    [Fact]
    public async Task Handshake_WrongConfirmation_InitiatorCancels_NoKeyStored()
    {
        _serviceB.AllowNewDevices(TimeSpan.FromMinutes(2));

        _serviceB.PairingRequested += args =>
        {
            args.Allow();
        };

        bool responderCompletedFired = false;
        _serviceB.PairingCompleted += (_, _, _) => responderCompletedFired = true;

        var result = await _serviceA.RequestPairingAsync(EndPointB, (name, code) => Task.FromResult(false));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsRefused);
        Assert.Equal("CancelledByUser", result.Reason);
        Assert.Null(result.PairKey);
        Assert.False(responderCompletedFired);
    }

    [Fact]
    public async Task Handshake_OutsideWindow_RefusedWithPairingDisabled()
    {
        // Notice: _serviceB has NOT called AllowNewDevices, so window is closed
        Assert.False(_serviceB.IsPairingWindowOpen());

        var result = await _serviceA.RequestPairingAsync(EndPointB, (name, code) => Task.FromResult(true));

        Assert.False(result.IsSuccess);
        Assert.True(result.IsRefused);
        Assert.Equal("PairingDisabled", result.Reason);
        Assert.Null(result.PairKey);
    }

    [Fact]
    public void RateLimiter_EnforcesThreeRequestsPerMinute()
    {
        var limiter = new PairingRateLimiter();
        var ip = IPAddress.Parse("192.168.1.50");
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(limiter.CanAttempt(ip, now, out var reason1));
        Assert.Null(reason1);
        limiter.RecordAttempt(ip, now);

        Assert.True(limiter.CanAttempt(ip, now.AddSeconds(10), out var reason2));
        limiter.RecordAttempt(ip, now.AddSeconds(10));

        Assert.True(limiter.CanAttempt(ip, now.AddSeconds(20), out var reason3));
        limiter.RecordAttempt(ip, now.AddSeconds(20));

        // 4th attempt in same minute is blocked
        Assert.False(limiter.CanAttempt(ip, now.AddSeconds(30), out var reason4));
        Assert.Equal("RateLimited", reason4);

        // After 61 seconds, old attempts fall out of window
        Assert.True(limiter.CanAttempt(ip, now.AddSeconds(65), out var reason5));
        Assert.Null(reason5);
    }

    [Fact]
    public void RateLimiter_EnforcesLockout_AfterThreeDenials()
    {
        var limiter = new PairingRateLimiter();
        var ip = IPAddress.Parse("10.0.0.99");
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

        limiter.RecordDenial(ip, now);
        Assert.True(limiter.CanAttempt(ip, now, out _));

        limiter.RecordDenial(ip, now.AddSeconds(5));
        Assert.True(limiter.CanAttempt(ip, now, out _));

        limiter.RecordDenial(ip, now.AddSeconds(10));

        // 3 denials: locked out for 5 minutes
        Assert.False(limiter.CanAttempt(ip, now.AddSeconds(15), out var reason));
        Assert.Equal("LockedOut", reason);

        // 4 minutes later: still locked out
        Assert.False(limiter.CanAttempt(ip, now.AddMinutes(4), out _));

        // 5 minutes after 3rd denial: lockout cleared
        Assert.True(limiter.CanAttempt(ip, now.AddSeconds(10).AddMinutes(5).AddSeconds(1), out var reasonAfter));
        Assert.Null(reasonAfter);
    }

    [Fact]
    public void Payloads_SerializeAndDeserialize_RoundTrip()
    {
        var pubKey = new byte[65];
        pubKey[0] = 0x04;
        new Random(42).NextBytes(pubKey.AsSpan(1));

        var req = new PairRequestPayload("dev-123", "Laptop-Pro", pubKey);
        var reqBytes = req.Serialize();
        var reqRestored = PairRequestPayload.Deserialize(reqBytes);

        Assert.Equal(req.DeviceId, reqRestored.DeviceId);
        Assert.Equal(req.DeviceName, reqRestored.DeviceName);
        Assert.Equal(req.PublicKey, reqRestored.PublicKey);

        var resp = new PairResponsePayload("dev-456", "Desktop-Work", pubKey);
        var respBytes = resp.Serialize();
        var respRestored = PairResponsePayload.Deserialize(respBytes);

        Assert.Equal(resp.DeviceId, respRestored.DeviceId);
        Assert.Equal(resp.DeviceName, respRestored.DeviceName);
        Assert.Equal(resp.PublicKey, respRestored.PublicKey);

        var refused = new PairRefusedPayload("PairingDisabled");
        var refusedBytes = refused.Serialize();
        var refusedRestored = PairRefusedPayload.Deserialize(refusedBytes);
        Assert.Equal(refused.Reason, refusedRestored.Reason);
    }

    public void Dispose()
    {
        _serviceA.Dispose();
        _serviceB.Dispose();
        try { if (Directory.Exists(_downloadsA)) Directory.Delete(_downloadsA, true); } catch { }
        try { if (Directory.Exists(_downloadsB)) Directory.Delete(_downloadsB, true); } catch { }
    }
}
