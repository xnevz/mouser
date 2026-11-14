using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Mouser.Core.Networking;

/// <summary>
/// Provides Wake-on-LAN magic packet generation, MAC address parsing and formatting,
/// UDP broadcast transmission, and ARP-based MAC address resolution on Windows.
/// </summary>
public static class WakeOnLan
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref uint physicalAddrLen);

    /// <summary>
    /// Constructs a standard 102-byte Wake-on-LAN magic packet composed of 6 bytes of 0xFF
    /// followed by 16 repetitions of the specified 6-byte MAC address.
    /// </summary>
    public static byte[] BuildMagicPacket(byte[] mac)
    {
        if (mac == null || mac.Length != 6)
            throw new ArgumentException("MAC address must be 6 bytes.", nameof(mac));

        byte[] packet = new byte[102];
        Array.Fill(packet, (byte)0xFF, 0, 6);
        for (int i = 0; i < 16; i++)
        {
            Buffer.BlockCopy(mac, 0, packet, 6 + i * 6, 6);
        }
        return packet;
    }

    /// <summary>
    /// Parses a MAC address string in colon, hyphen, or delimiter-less format (case-insensitive).
    /// </summary>
    public static bool TryParseMac(string? text, [NotNullWhen(true)] out byte[]? mac)
    {
        mac = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();
        if (trimmed.Contains(':') || trimmed.Contains('-'))
        {
            string[] parts = trimmed.Split(new[] { ':', '-' }, StringSplitOptions.None);
            if (parts.Length != 6) return false;

            byte[] result = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                if (parts[i].Length != 2 || !char.IsAsciiHexDigit(parts[i][0]) || !char.IsAsciiHexDigit(parts[i][1]))
                    return false;

                result[i] = Convert.ToByte(parts[i], 16);
            }
            mac = result;
            return true;
        }
        else
        {
            if (trimmed.Length != 12) return false;

            byte[] result = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                if (!char.IsAsciiHexDigit(trimmed[i * 2]) || !char.IsAsciiHexDigit(trimmed[i * 2 + 1]))
                    return false;

                result[i] = Convert.ToByte(trimmed.Substring(i * 2, 2), 16);
            }
            mac = result;
            return true;
        }
    }

    /// <summary>
    /// Formats a 6-byte MAC address as lowercase hex separated by colons (e.g. "aa:bb:cc:dd:ee:ff").
    /// </summary>
    public static string FormatMac(byte[] mac)
    {
        if (mac == null || mac.Length != 6)
            throw new ArgumentException("MAC address must be 6 bytes.", nameof(mac));

        return string.Join(":", mac.Select(b => b.ToString("x2")));
    }

    /// <summary>
    /// Sends a Wake-on-LAN magic packet as a UDP broadcast to the specified port (default 9)
    /// across all broadcast addresses returned by <see cref="NetworkInfo.GetBroadcastAddresses"/>.
    /// </summary>
    public static async Task SendAsync(byte[] mac, int port = 9)
    {
        byte[] packet = BuildMagicPacket(mac);
        var addresses = NetworkInfo.GetBroadcastAddresses();
        using var client = new UdpClient();
        client.EnableBroadcast = true;

        foreach (var address in addresses)
        {
            try
            {
                await client.SendAsync(packet, packet.Length, new IPEndPoint(address, port)).ConfigureAwait(false);
            }
            catch
            {
                // Allow other broadcasts to succeed if one interface errors
            }
        }
    }

    /// <summary>
    /// Resolves the MAC address for an IPv4 address using the Windows SendARP API.
    /// Returns false on non-Windows platforms, non-IPv4 addresses, or when resolution fails.
    /// </summary>
    public static bool TryGetMac(IPAddress ip, [NotNullWhen(true)] out byte[]? mac)
    {
        mac = null;
        if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork)
            return false;

        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            uint destIp = BitConverter.ToUInt32(ip.GetAddressBytes(), 0);
            byte[] buffer = new byte[6];
            uint len = (uint)buffer.Length;
            int result = SendARP(destIp, 0, buffer, ref len);
            if (result == 0 && len == 6)
            {
                mac = buffer;
                return true;
            }
        }
        catch
        {
            // Ignore native errors
        }

        return false;
    }
}
