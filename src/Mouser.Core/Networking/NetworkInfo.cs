using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mouser.Core.Networking;

public static class NetworkInfo
{
    /// <summary>
    /// IPv4 addresses users can type into a phone. Adapters with a default gateway (the real LAN) are preferred,
    /// which hides virtual adapters from Hyper-V, WSL, VMware and VPNs; falls back to every active adapter.
    /// </summary>
    public static IReadOnlyList<IPAddress> GetLocalIPv4Addresses()
    {
        var all = GetIPv4Unicast(requireGateway: false).Select(u => u.Address).Distinct().ToList();
        var routed = GetIPv4Unicast(requireGateway: true).Select(u => u.Address).Distinct().ToList();
        return routed.Count > 0 ? routed : all;
    }

    /// <summary>Limited broadcast plus the directed broadcast address of every active IPv4 subnet.</summary>
    public static IReadOnlyList<IPAddress> GetBroadcastAddresses()
    {
        var result = new List<IPAddress> { IPAddress.Broadcast };
        foreach (var unicast in GetIPv4Unicast(requireGateway: false))
        {
            var broadcast = GetDirectedBroadcast(unicast.Address, unicast.IPv4Mask);
            if (broadcast != null && !result.Contains(broadcast)) result.Add(broadcast);
        }
        return result;
    }

    public static IPAddress? GetDirectedBroadcast(IPAddress address, IPAddress? mask)
    {
        if (mask == null || address.AddressFamily != AddressFamily.InterNetwork) return null;
        byte[] ip = address.GetAddressBytes();
        byte[] m = mask.GetAddressBytes();
        if (m.All(b => b == 0)) return null;
        for (int i = 0; i < 4; i++) ip[i] = (byte)(ip[i] | ~m[i]);
        return new IPAddress(ip);
    }

    private static IEnumerable<UnicastIPAddressInformation> GetIPv4Unicast(bool requireGateway)
    {
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            yield break;
        }

        foreach (var ni in interfaces)
        {
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var props = ni.GetIPProperties();
            if (requireGateway && !props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))
                continue;

            foreach (var unicast in props.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(unicast.Address))
                    yield return unicast;
            }
        }
    }
}
