using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Mouser.Core.Files;
using Xunit;

namespace Mouser.Tests;

public sealed class FileStreamingTests
{
    [Fact]
    public async Task FileStreamingService_TransfersChunksAndVerifiesSha256()
    {
        string sourceFile = Path.Combine(Path.GetTempPath(), $"source_{Guid.NewGuid():N}.bin");
        string destFile = Path.Combine(Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.bin");

        try
        {
            // Generate a 150 KB test payload spanning multiple 64KB chunks
            byte[] testData = new byte[150 * 1024];
            Random.Shared.NextBytes(testData);
            await File.WriteAllBytesAsync(sourceFile, testData);

            var chunkQueue = new List<byte[]>();
            var progressReports = new List<FileTransferProgress>();
            var progress = new Progress<FileTransferProgress>(p => progressReports.Add(p));

            var header = await FileStreamingService.StreamFileChunksAsync(
                sourceFile,
                (index, chunk) =>
                {
                    chunkQueue.Add(chunk.ToArray());
                    return Task.CompletedTask;
                },
                progress,
                chunkSize: 64 * 1024);

            Assert.Equal(3, chunkQueue.Count); // 64K + 64K + 22K = 3 chunks
            Assert.Equal(testData.Length, header.TotalBytes);

            // Reconstruct on target side
            await FileStreamingService.ReceiveFileAsync(
                destFile,
                header,
                async onChunk =>
                {
                    foreach (var chunk in chunkQueue)
                    {
                        await onChunk(chunk);
                    }
                });

            Assert.True(File.Exists(destFile));
            byte[] receivedBytes = await File.ReadAllBytesAsync(destFile);
            Assert.Equal(testData, receivedBytes);
        }
        finally
        {
            if (File.Exists(sourceFile)) File.Delete(sourceFile);
            if (File.Exists(destFile)) File.Delete(destFile);
        }
    }

    [Fact]
    public async Task FileStreamingService_RejectsCorruptedFileTransfer()
    {
        string sourceFile = Path.Combine(Path.GetTempPath(), $"source_corrupt_{Guid.NewGuid():N}.bin");
        string destFile = Path.Combine(Path.GetTempPath(), $"dest_corrupt_{Guid.NewGuid():N}.bin");

        try
        {
            byte[] testData = "Test payload for checksum verification"u8.ToArray();
            await File.WriteAllBytesAsync(sourceFile, testData);

            var header = await FileStreamingService.StreamFileChunksAsync(
                sourceFile,
                (_, _) => Task.CompletedTask);

            // Simulate corrupted header hash
            var corruptHeader = header with { Sha256Checksum = "0000000000000000000000000000000000000000000000000000000000000000" };

            await Assert.ThrowsAsync<CryptographicException>(async () =>
            {
                await FileStreamingService.ReceiveFileAsync(
                    destFile,
                    corruptHeader,
                    async onChunk =>
                    {
                        await onChunk(testData);
                    });
            });

            // Ensure destination was wiped
            Assert.False(File.Exists(destFile));
        }
        finally
        {
            if (File.Exists(sourceFile)) File.Delete(sourceFile);
            if (File.Exists(destFile)) File.Delete(destFile);
        }
    }
}
