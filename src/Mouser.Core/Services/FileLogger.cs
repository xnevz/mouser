using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Mouser.Core.Services;

/// <summary>
/// Thread-safe file logger that persists log lines to daily files with automatic rotation.
/// Keeps at most 7 log files and rolls over to .1.log, .2.log, etc. when a file reaches 2 MB.
/// </summary>
public sealed class FileLogger
{
    public const long DefaultMaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB
    public const int DefaultMaxFiles = 7;

    private readonly object _lock = new();

    public static string DefaultLogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mouser", "logs");

    private static FileLogger? _default;
    public static FileLogger Default
    {
        get => _default ??= new FileLogger();
        set => _default = value;
    }

    public string LogDirectory { get; }
    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;
    public int MaxFiles { get; set; } = DefaultMaxFiles;

    public FileLogger(string? logDirectory = null)
    {
        LogDirectory = string.IsNullOrWhiteSpace(logDirectory) ? DefaultLogDirectory : logDirectory;
        try
        {
            Directory.CreateDirectory(LogDirectory);
            CleanOldLogs();
        }
        catch
        {
            // Directory creation or initial cleanup failures must not crash the app
        }
    }

    public string CurrentLogFilePath
    {
        get
        {
            lock (_lock)
            {
                return GetTargetFilePath(DateTime.UtcNow);
            }
        }
    }

    public void Log(ActivityEvent entry)
    {
        string line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Category}] {entry.Message}";
        WriteLine(line, entry.Timestamp);
    }

    public void Log(ActivityCategory category, string message)
    {
        var now = DateTime.UtcNow;
        string line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{category}] {message}";
        WriteLine(line, now);
    }

    public void Log(string message)
    {
        var now = DateTime.UtcNow;
        string line = $"{now:yyyy-MM-dd HH:mm:ss.fff} {message}";
        WriteLine(line, now);
    }

    public void WriteLine(string line, DateTime? timestamp = null)
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var dt = timestamp ?? DateTime.UtcNow;
                string filePath = GetTargetFilePath(dt);
                File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
                CleanOldLogs();
            }
            catch
            {
                // Never crash the application on file logging failure
            }
        }
    }

    private string GetTargetFilePath(DateTime timestamp)
    {
        string dateStr = timestamp.ToString("yyyyMMdd");
        string baseFile = Path.Combine(LogDirectory, $"mouser-{dateStr}.log");

        if (!File.Exists(baseFile))
            return baseFile;

        var fi = new FileInfo(baseFile);
        if (fi.Length < MaxFileSizeBytes)
            return baseFile;

        int index = 1;
        while (true)
        {
            string rolled = Path.Combine(LogDirectory, $"mouser-{dateStr}.{index}.log");
            if (!File.Exists(rolled))
                return rolled;

            var rfi = new FileInfo(rolled);
            if (rfi.Length < MaxFileSizeBytes)
                return rolled;

            index++;
        }
    }

    public void CleanOldLogs()
    {
        lock (_lock)
        {
            try
            {
                if (!Directory.Exists(LogDirectory)) return;

                var files = Directory.GetFiles(LogDirectory, "mouser-*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => GetFileSortKey(f).Date)
                    .ThenByDescending(f => GetFileSortKey(f).Index)
                    .ThenByDescending(f => f.LastWriteTimeUtc)
                    .ToList();

                if (files.Count > MaxFiles)
                {
                    foreach (var oldFile in files.Skip(MaxFiles))
                    {
                        try
                        {
                            oldFile.Delete();
                        }
                        catch
                        {
                            // Ignore locked or in-use old log files
                        }
                    }
                }
            }
            catch
            {
                // Cleanup failure must not throw
            }
        }
    }

    private static (string Date, int Index, DateTime LastWrite) GetFileSortKey(FileInfo f)
    {
        string name = Path.GetFileNameWithoutExtension(f.Name);
        string datePart = "";
        int index = 0;
        if (name.StartsWith("mouser-", StringComparison.OrdinalIgnoreCase))
        {
            string rest = name[7..];
            int dot = rest.IndexOf('.');
            if (dot >= 0)
            {
                datePart = rest[..dot];
                int.TryParse(rest[(dot + 1)..], out index);
            }
            else
            {
                datePart = rest;
            }
        }
        return (datePart, index, f.LastWriteTimeUtc);
    }

    public void Rotate() => CleanOldLogs();
}
