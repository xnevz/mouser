using System.Net;

namespace Mouser.Core.Networking;

/// <summary>
/// Per-sender 16-bit sequence filter: drops duplicated or reordered UDP datagrams and handles wrap-around.
/// A jump far into the past is treated as the sender restarting, not as a stale packet.
/// </summary>
public sealed class SequenceTracker
{
    public const int RestartWindow = 1024;

    private readonly Dictionary<IPEndPoint, ushort> _last = new();

    public bool Accept(IPEndPoint sender, ushort sequence)
    {
        lock (_last)
        {
            if (_last.TryGetValue(sender, out ushort last))
            {
                short diff = unchecked((short)(sequence - last));
                if (diff <= 0 && diff >= -RestartWindow) return false;
            }

            _last[sender] = sequence;
            return true;
        }
    }

    public void Reset(IPEndPoint sender)
    {
        lock (_last) _last.Remove(sender);
    }
}
