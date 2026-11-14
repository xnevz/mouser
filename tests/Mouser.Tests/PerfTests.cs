using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Mouser.Core.Clipboard;
using Mouser.Core.Geometry;
using Mouser.Core.Input;
using Mouser.Core.Kvm;

namespace Mouser.Tests;

/// <summary>
/// Performance budget tests and benchmarks (Task Q4).
/// All tests carry [Trait("Category", "Perf")] so they can be run on demand
/// or excluded from normal fast test runs.
/// 
/// Idle budget:
/// Mouser running minimized/hidden in the Windows tray should consume less than 1% CPU
/// and less than 150 MB working set memory.
/// </summary>
[Trait("Category", "Perf")]
public class PerfTests
{
    [Fact]
    public async Task KvmController_Handle_10kMoves_Under50ms()
    {
        await using var rig = await KvmRig.CreateAsync();

        // Warm up JIT
        for (int i = 0; i < 50; i++)
        {
            rig.Capture.Move(500 + i % 10, 500, 1, 0);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
        {
            rig.Capture.Move(500 + (i % 200), 500, 1, 0);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds <= 50,
            $"KvmController.Handle took {sw.ElapsedMilliseconds} ms for 10k moves (budget <= 50 ms)");
    }

    [Fact]
    public async Task ClipboardStorageService_SaveClip_100Clips_Under2s()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"mouser_perf_save_{Guid.NewGuid():N}.db");
        try
        {
            await using var storage = new ClipboardStorageService(dbPath);
            await storage.InitializeAsync();

            var clips = new List<ClipboardPayload>(100);
            long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < 100; i++)
            {
                clips.Add(new ClipboardPayload
                {
                    Id = $"clip-save-{i}",
                    TimestampUtc = baseTime + i,
                    TextContent = $"Performance clip number {i} with some sample text to index into FTS."
                });
            }

            var sw = Stopwatch.StartNew();
            foreach (var clip in clips)
            {
                await storage.SaveClipAsync(clip);
            }
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds <= 2000,
                $"Saving 100 clips took {sw.ElapsedMilliseconds} ms (budget <= 2000 ms)");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDeleteFile(dbPath);
        }
    }

    [Fact]
    public async Task ClipboardStorageService_FtsQuery_10kClips_Under50ms()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"mouser_perf_fts_{Guid.NewGuid():N}.db");
        try
        {
            await using var storage = new ClipboardStorageService(dbPath);
            await storage.InitializeAsync();

            // Seed 10,000 clips in a single transaction for rapid setup
            var conn = storage.Connection!;
            using (var tx = conn.BeginTransaction())
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO clips (id, timestamp, content_type, text_content, source_device, is_favorite, source_app)
                    VALUES (@id, @ts, 0, @txt, 'Local', 0, '');
                    INSERT INTO clips_fts (id, text_content, source_device)
                    VALUES (@id, @txt, 'Local');";

                var pId = cmd.Parameters.Add("@id", SqliteType.Text);
                var pTs = cmd.Parameters.Add("@ts", SqliteType.Integer);
                var pTxt = cmd.Parameters.Add("@txt", SqliteType.Text);

                long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                for (int i = 0; i < 10_000; i++)
                {
                    string id = $"seed-{i}";
                    string txt = (i == 4321)
                        ? "UniqueTargetIdentifierForFtsBenchmark in this specific entry"
                        : $"Routine item {i} discussing software engineering, mouser clipboard sync and networks";

                    pId.Value = id;
                    pTs.Value = baseTime + i;
                    pTxt.Value = txt;

                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }

            // Warm up
            _ = await storage.SearchClipsAsync("Routine", limit: 50);

            // Benchmark target search
            var sw = Stopwatch.StartNew();
            var results = await storage.SearchClipsAsync("UniqueTargetIdentifierForFtsBenchmark", limit: 50);
            sw.Stop();

            Assert.Single(results);
            Assert.True(sw.ElapsedMilliseconds <= 50,
                $"FTS search across 10k clips took {sw.ElapsedMilliseconds} ms (budget <= 50 ms)");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDeleteFile(dbPath);
        }
    }

    [Fact]
    public void MoveCoalescer_100kPosts_Under100ms()
    {
        var sent = new List<(short X, short Y)>();
        long fakeClock = 0;
        var coalescer = new MoveCoalescer((x, y) => sent.Add((x, y)), minIntervalMs: 2, clock: () => fakeClock);

        // Warm up JIT
        for (int i = 0; i < 100; i++)
        {
            coalescer.Post((short)(i % 100), (short)(i % 100));
        }
        sent.Clear();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100_000; i++)
        {
            // Advance clock every 50 posts to simulate burst coalescing
            fakeClock = i / 50;
            coalescer.Post((short)(i % 1000), (short)(i % 1000));
        }
        coalescer.Flush();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds <= 100,
            $"MoveCoalescer.Post 100k posts took {sw.ElapsedMilliseconds} ms (budget <= 100 ms, <= 1us/post)");
    }

    [Fact]
    public void SearchQuery_Parse_10kParses_Under200ms()
    {
        const string queryText = "ssh @WorkServer #dev type:code before:2026-12-01 app:terminal";

        // Warm up JIT
        for (int i = 0; i < 50; i++)
        {
            _ = SearchQuery.Parse(queryText);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
        {
            _ = SearchQuery.Parse(queryText);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds <= 200,
            $"SearchQuery.Parse took {sw.ElapsedMilliseconds} ms for 10k parses (budget <= 200 ms)");
    }

    [Fact]
    public async Task Memory_1000ClipsAndRefreshes_GrowthUnder20MB()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"mouser_perf_mem_{Guid.NewGuid():N}.db");
        try
        {
            await using var storage = new ClipboardStorageService(dbPath);
            await storage.InitializeAsync();

            // Settle GC before baseline measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long initialMemory = GC.GetTotalMemory(true);

            long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < 1_000; i++)
            {
                await storage.SaveClipAsync(new ClipboardPayload
                {
                    Id = $"mem-clip-{i}",
                    TimestampUtc = baseTime + i,
                    TextContent = $"Memory test payload {i} containing moderate length text to evaluate GC overhead."
                });

                if (i % 200 == 0)
                {
                    _ = await storage.GetRecentClipsAsync(limit: 50);
                }
            }

            // Settle GC after operations
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long finalMemory = GC.GetTotalMemory(true);

            long growthBytes = finalMemory - initialMemory;
            long maxAllowedGrowth = 20 * 1024 * 1024; // 20 MB

            Assert.True(growthBytes <= maxAllowedGrowth,
                $"Memory growth was {growthBytes / (1024.0 * 1024.0):F2} MB (budget <= 20 MB)");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDeleteFile(dbPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}
