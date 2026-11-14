namespace Mouser.Core.Kvm;

public sealed class MoveCoalescer
{
    private readonly Action<short, short> _send;
    private readonly int _minIntervalMs;
    private readonly Func<long> _clock;
    private long _lastSentTime = -100000;
    private short? _pendingX;
    private short? _pendingY;
    private bool _hasPending;

    public MoveCoalescer(Action<short, short> send, int minIntervalMs = 2, Func<long>? clock = null)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _minIntervalMs = minIntervalMs;
        _clock = clock ?? (() => Environment.TickCount64);
    }

    public void Post(short x, short y)
    {
        long now = _clock();
        if (now - _lastSentTime >= _minIntervalMs)
        {
            _lastSentTime = now;
            _hasPending = false;
            _send(x, y);
        }
        else
        {
            _pendingX = x;
            _pendingY = y;
            _hasPending = true;
        }
    }

    public void Flush()
    {
        if (_hasPending && _pendingX.HasValue && _pendingY.HasValue)
        {
            _hasPending = false;
            _lastSentTime = _clock();
            _send(_pendingX.Value, _pendingY.Value);
        }
    }
}
