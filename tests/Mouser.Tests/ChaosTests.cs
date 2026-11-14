using System.Net;
using System.Security.Cryptography;
using Mouser.Core.Files;
using Mouser.Core.Geometry;
using Mouser.Core.Input;
using Mouser.Core.Kvm;
using Mouser.Core.Networking;
using Mouser.Core.Security;
using Mouser.Core.Services;

namespace Mouser.Tests;

/// <summary>
/// Chaos and network resilience tests (Task Q5).
/// Covers lossy UDP networks (loss, duplication, reordering),
/// mid-file-transfer network crashes, and endpoint/IP switching mid-session.
/// </summary>
public class ChaosTests
{
    private sealed class LossyChannel
    {
        private readonly double _dropRate;
        private readonly double _duplicateRate;
        private readonly int _reorderDepth;
        private readonly Random _rng;

        public LossyChannel(double dropRate = 0.20, double duplicateRate = 0.10, int reorderDepth = 4, int seed = 42)
        {
            _dropRate = dropRate;
            _duplicateRate = duplicateRate;
            _reorderDepth = reorderDepth;
            _rng = new Random(seed);
        }

        public IEnumerable<T> Process<T>(IEnumerable<T> source)
        {
            var buffer = new List<T>(_reorderDepth);
            foreach (var item in source)
            {
                // Drop?
                if (_rng.NextDouble() < _dropRate)
                {
                    continue;
                }

                buffer.Add(item);

                // Duplicate?
                if (_rng.NextDouble() < _duplicateRate)
                {
                    buffer.Add(item);
                }

                if (buffer.Count >= _reorderDepth)
                {
                    // Shuffle buffer partially and yield
                    int idx = _rng.Next(buffer.Count);
                    yield return buffer[idx];
                    buffer.RemoveAt(idx);
                }
            }

            // Flush remaining
            while (buffer.Count > 0)
            {
                int idx = _rng.Next(buffer.Count);
                yield return buffer[idx];
                buffer.RemoveAt(idx);
            }
        }
    }

    [Fact]
    public void LossyChannel_SequenceTracker_And_SecureReplayWindow_FilterDuplicatesAndReversals()
    {
        var channel = new LossyChannel(dropRate: 0.20, duplicateRate: 0.10, reorderDepth: 5, seed: 12345);

        // Test 1: SequenceTracker with 16-bit sequence numbers
        var tracker = new SequenceTracker();
        var ep = new IPEndPoint(IPAddress.Loopback, 5000);
        var inputSequences = Enumerable.Range(1, 1000).Select(i => (ushort)i).ToList();

        var delivered = channel.Process(inputSequences).ToList();
        var accepted = new List<ushort>();

        foreach (var seq in delivered)
        {
            if (tracker.Accept(ep, seq))
            {
                accepted.Add(seq);
            }
        }

        // SequenceTracker invariant: every accepted packet must be strictly greater than the previous
        for (int i = 1; i < accepted.Count; i++)
        {
            short diff = unchecked((short)(accepted[i] - accepted[i - 1]));
            Assert.True(diff > 0, $"Sequence violation: {accepted[i - 1]} followed by {accepted[i]}");
        }

        // Test 2: SessionCrypto sliding 64-bit replay window
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        using var senderCrypto = new SessionCrypto(key, isInitiator: true, sessionId: 12345);
        using var receiverCrypto = new SessionCrypto(key, isInitiator: false, sessionId: 12345);

        var packets = new List<(ulong Counter, byte[] Sealed)>();
        for (int i = 1; i <= 500; i++)
        {
            byte[] plain = BitConverter.GetBytes(i);
            byte[] sealedData = senderCrypto.Seal(plain, out ulong counter);
            packets.Add((counter, sealedData));
        }

        var lossyPackets = channel.Process(packets).ToList();
        var openedCounters = new HashSet<ulong>();

        foreach (var (counter, sealedData) in lossyPackets)
        {
            if (receiverCrypto.TryOpen(counter, sealedData, out byte[] opened))
            {
                int decrypted = BitConverter.ToInt32(opened);
                Assert.Equal((int)counter, decrypted);

                // Anti-replay invariant: No counter must ever be accepted more than once!
                Assert.True(openedCounters.Add(counter), $"Counter {counter} accepted more than once through replay window!");
            }
        }

        Assert.NotEmpty(openedCounters);
    }

    [Fact]
    public async Task MidTransferFailure_SenderThrows_ReceiverDeletesPartialFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"mouser_chaos_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string srcFile = Path.Combine(tempDir, "source.bin");
        string destFile = Path.Combine(tempDir, "dest.bin");

        try
        {
            // Create 512 KB source file
            byte[] payload = new byte[512 * 1024];
            RandomNumberGenerator.Fill(payload);
            await File.WriteAllBytesAsync(srcFile, payload);

            string sha = await FileStreamingService.ComputeSha256Async(srcFile);
            var header = new FileTransferHeader("xfer-chaos-1", "dest.bin", payload.Length, sha);

            // Simulate broken connection after 2 chunks
            int chunksDelivered = 0;
            var ex = await Assert.ThrowsAsync<IOException>(async () =>
            {
                await FileStreamingService.ReceiveFileAsync(destFile, header, async writeChunk =>
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (chunksDelivered >= 2)
                        {
                            throw new IOException("Simulated network disconnection mid-transfer.");
                        }
                        await writeChunk(new byte[64 * 1024]);
                        chunksDelivered++;
                    }
                });
            });

            Assert.Equal("Simulated network disconnection mid-transfer.", ex.Message);
            Assert.False(File.Exists(destFile), "Receiver did not clean up partial file after aborted transfer!");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task PeerEndpointChange_MidSession_ReturnsAndResumesControl()
    {
        await using var rig = await KvmRig.CreateAsync(PeerPlacement.Right);

        // Move to remote peer
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);

        // Simulate peer endpoint change mid-session: peer restarts on a new port
        var newSink = new RecordingSink();
        using var newPeerHost = new InputInjectionService(newSink, new PairingGate(KvmRig.Pin), 0) { AllowOlderDevices = true };
        newPeerHost.Start();

        using var newLink = new PeerLink(new IPEndPoint(IPAddress.Loopback, newPeerHost.BoundPort), allowOlderDevices: true);
        Assert.True(await newLink.PairAsync(KvmRig.Pin));

        // Re-setting peer with the new link must return active control to local first!
        rig.Kvm.SetPeer("peer", "Studio PC", newLink, KvmRig.Remote, PeerPlacement.Right);
        Assert.Null(rig.Kvm.ActivePeerId);

        // Now pushing across the edge resumes control to the new endpoint
        rig.CrossRight();
        Assert.Equal("peer", rig.Kvm.ActivePeerId);
    }
}
