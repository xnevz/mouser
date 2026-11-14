using System;
using System.Collections.Generic;
using System.Linq;

namespace Mouser.Core.Services;

public enum ActivityCategory
{
    Discovery,
    Input,
    Pairing,
    Clipboard,
    File,
    Error,
    Network,
    Security
}

public record ActivityEvent(DateTime Timestamp, ActivityCategory Category, string Message);

public sealed class ActivityLog
{
    public const int Capacity = 200;
    private readonly Queue<ActivityEvent> _events = new();
    private readonly object _lock = new();

    public FileLogger? FileLogger { get; set; }

    public event Action<ActivityEvent>? EntryAdded;

    public ActivityLog(FileLogger? fileLogger = null)
    {
        FileLogger = fileLogger;
    }

    public void Record(ActivityCategory category, string message)
    {
        var entry = new ActivityEvent(DateTime.UtcNow, category, message);
        lock (_lock)
        {
            if (_events.Count >= Capacity)
                _events.Dequeue();
            _events.Enqueue(entry);
        }
        try
        {
            FileLogger?.Log(entry);
        }
        catch
        {
            // Persistence failure must never crash the application
        }
        EntryAdded?.Invoke(entry);
    }

    public IReadOnlyList<ActivityEvent> Snapshot()
    {
        lock (_lock)
        {
            return _events.Reverse().ToList(); // newest first
        }
    }
}
