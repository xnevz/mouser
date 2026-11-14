namespace Mouser.Core.Networking;

public static class PowerSyncPolicy
{
    public static readonly TimeSpan PingPongGuardInterval = TimeSpan.FromSeconds(5);

    public static bool ShouldApply(
        PowerEventKind kind,
        bool syncLock,
        bool syncSleep,
        bool syncDisplayOff,
        DateTime? lastEventUtc,
        DateTime nowUtc)
    {
        // 5-second guard against ping-pong:
        if (lastEventUtc.HasValue && (nowUtc - lastEventUtc.Value) <= PingPongGuardInterval)
        {
            return false;
        }

        return kind switch
        {
            PowerEventKind.Lock => syncLock,
            PowerEventKind.Sleep => syncSleep,
            PowerEventKind.DisplayOff => syncDisplayOff,
            _ => false
        };
    }
}
