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

    public event Action<ActivityEvent>? EntryAdded;

    public void Record(ActivityCategory category, string message)
    {
        var entry = new ActivityEvent(DateTime.UtcNow, category, message);
        lock (_lock)
        {
            if (_events.Count >= Capacity)
                _events.Dequeue();
            _events.Enqueue(entry);
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
