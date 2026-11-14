using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mouser.Core.Networking;

public sealed record NetworkAdapterInfo(string Id, string Name, string Description, IPAddress? IPv4Address)
{
    public override string ToString() => IPv4Address != null ? $"{Name} ({IPv4Address})" : Name;
}

public static class NetworkInfo
{
    /// <summary>
    /// Pure function determining whether an IP address belongs to a local, private, or Tailscale network.
    /// Checks 10/8, 172.16/12, 192.168/16, 169.254/16 (link-local), 100.64/10 (CGNAT/Tailscale),
    /// fc00::/7 (ULA including fd00::/8), fe80::/10 (link-local IPv6), and loopback.
    /// </summary>
    public static bool IsPrivate(IPAddress? address)
    {
        if (address == null) return false;

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address)) return true;

        byte[] b = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            if (b.Length != 4) return false;
            // 10.0.0.0/8
            if (b[0] == 10) return true;
            // 172.16.0.0/12
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            // 192.168.0.0/16
            if (b[0] == 192 && b[1] == 168) return true;
            // 169.254.0.0/16 (link-local)
            if (b[0] == 169 && b[1] == 254) return true;
            // 100.64.0.0/10 (CGNAT / Tailscale)
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;
            // 127.0.0.0/8 (loopback)
            if (b[0] == 127) return true;

            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (b.Length != 16) return false;
            // fc00::/7 (Unique Local Address, includes fd00::/8)
            if ((b[0] & 0xfe) == 0xfc) return true;
            // fe80::/10 (link-local)
            if (b[0] == 0xfe && (b[1] & 0xc0) == 0x80) return true;

            return false;
        }

        return false;
    }

    /// <summary>Finds an available UDP port by binding to port 0.</summary>
    public static int FindFreeUdpPort()
    {
        using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udp.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udp.LocalEndPoint!).Port;
    }

    /// <summary>Finds an available TCP port by starting a listener on port 0.</summary>
    public static int FindFreeTcpPort()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        int port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    /// <summary>Returns active network adapters with their IP addresses.</summary>
    public static IReadOnlyList<NetworkAdapterInfo> GetAdapters()
    {
        var list = new List<NetworkAdapterInfo>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var ip = ni.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address))
                    ?.Address;

                list.Add(new NetworkAdapterInfo(ni.Id, ni.Name, ni.Description, ip));
            }
        }
        catch (NetworkInformationException)
        {
        }
        return list;
    }

    /// <summary>Finds the IPv4 address associated with a network adapter ID.</summary>
    public static IPAddress? GetAdapterAddress(string? adapterId)
    {
        if (string.IsNullOrWhiteSpace(adapterId)) return null;
        try
        {
            var ni = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == adapterId);
            if (ni == null) return null;
            return ni.GetIPProperties().UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address))
                ?.Address;
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }
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
