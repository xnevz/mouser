using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Mouser.Core.Files;

public sealed record FileTransferHeader(
    string TransferId,
    string FileName,
    long TotalBytes,
    string Sha256Checksum);

public sealed record FileTransferProgress(
    string TransferId,
    long BytesTransferred,
    long TotalBytes,
    double PercentComplete);

/// <summary>
/// Handles reliable chunked file streaming across screen boundaries with SHA-256 integrity verification.
/// </summary>
public static class FileStreamingService
{
    public const int DefaultChunkSize = 64 * 1024; // 64 KB

    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        byte[] hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Reads a file in 64KB chunks and passes each chunk to a consumer callback.
    /// </summary>
    public static async Task<FileTransferHeader> StreamFileChunksAsync(
        string filePath,
        Func<int, ReadOnlyMemory<byte>, Task> onChunkReceived,
        IProgress<FileTransferProgress>? progress = null,
        int chunkSize = DefaultChunkSize,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Source file for drag-and-drop transfer not found.", filePath);
        }

        var fileInfo = new FileInfo(filePath);
        string sha256 = await ComputeSha256Async(filePath, ct);
        string transferId = Guid.NewGuid().ToString("N");

        var header = new FileTransferHeader(
            TransferId: transferId,
            FileName: fileInfo.Name,
            TotalBytes: fileInfo.Length,
            Sha256Checksum: sha256);

        await using var fileStream = File.OpenRead(filePath);
        byte[] buffer = new byte[chunkSize];
        long totalBytesRead = 0;
        int chunkIndex = 0;

        int bytesRead;
        while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, chunkSize), ct)) > 0)
        {
            await onChunkReceived(chunkIndex++, buffer.AsMemory(0, bytesRead));
            totalBytesRead += bytesRead;

            double percent = fileInfo.Length > 0 ? (double)totalBytesRead / fileInfo.Length * 100.0 : 100.0;
            progress?.Report(new FileTransferProgress(transferId, totalBytesRead, fileInfo.Length, percent));
        }

        return header;
    }

    /// <summary>
    /// Writes received chunks into a destination file and verifies SHA-256 integrity upon completion.
    /// </summary>
    public static async Task ReceiveFileAsync(
        string destinationFilePath,
        FileTransferHeader header,
        Func<Func<ReadOnlyMemory<byte>, Task>, Task> chunkSource,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        long totalWritten = 0;
        await using (var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await chunkSource(async chunk =>
            {
                await fileStream.WriteAsync(chunk, ct);
                totalWritten += chunk.Length;

                double percent = header.TotalBytes > 0 ? (double)totalWritten / header.TotalBytes * 100.0 : 100.0;
                progress?.Report(new FileTransferProgress(header.TransferId, totalWritten, header.TotalBytes, percent));
            });

            await fileStream.FlushAsync(ct);
        }

        // Verify integrity
        string actualHash = await ComputeSha256Async(destinationFilePath, ct);
        if (!string.Equals(actualHash, header.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destinationFilePath);
            throw new CryptographicException(
                $"File transfer SHA-256 mismatch! Expected {header.Sha256Checksum}, got {actualHash}. File removed for security.");
        }
    }
}
