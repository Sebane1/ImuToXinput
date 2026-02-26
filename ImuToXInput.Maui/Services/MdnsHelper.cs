using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace ImuToXInput.Maui.Services;

/// <summary>
/// Resolves hostnames to IPv4 addresses, including mDNS (.local) via Zeroconf when system DNS fails.
/// </summary>
public static class MdnsHelper
{
    /// <summary>mDNS service type for Joypad dongles. Dongle should advertise this (e.g. _joypad._tcp.local.).</summary>
    public const string JoypadServiceType = "_joypad._tcp.local.";

    /// <summary>
    /// Resolve a host name or address to an IPv4 string, or null if resolution fails.
    /// Tries: (1) parse as IP, (2) Dns.GetHostAddresses, (3) if host contains ".local", Zeroconf resolve for Joypad service.
    /// </summary>
    public static async Task<string?> ResolveHostToIpAsync(string host, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        host = host.Trim();

        if (IPAddress.TryParse(host, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
            return parsed.ToString();

        try
        {
            var addrs = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            var ipv4 = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4 != null)
                return ipv4.ToString();
        }
        catch
        {
            // Fall through to mDNS
        }

        if (!host.Contains(".local", StringComparison.OrdinalIgnoreCase))
            return null;

        var discovered = await DiscoverJoypadDonglesAsync(3000, cancellationToken).ConfigureAwait(false);
        var namePart = host.Replace(".local", "").TrimEnd('.');
        var match = discovered.FirstOrDefault(d =>
            string.Equals(d.DisplayName, namePart, StringComparison.OrdinalIgnoreCase) ||
            (d.DisplayName?.StartsWith(namePart, StringComparison.OrdinalIgnoreCase) == true));
        if (match.DisplayName != null)
            return match.IpAddress;
        return discovered.Count > 0 ? discovered[0].IpAddress : null;
    }

    /// <summary>
    /// Discover Joypad dongles on the local network via mDNS. Returns list of (display name, IP address, port).
    /// </summary>
    public static async Task<IReadOnlyList<(string DisplayName, string IpAddress, int Port)>> DiscoverJoypadDonglesAsync(
        int scanTimeMs = 5000,
        CancellationToken cancellationToken = default)
    {
        var result = new List<(string, string, int)>();
        try
        {
            var scanTime = TimeSpan.FromMilliseconds(Math.Max(1000, scanTimeMs));
            var hosts = await Zeroconf.ZeroconfResolver.ResolveAsync(JoypadServiceType, scanTime, 2, 1000, null).ConfigureAwait(false);
            if (hosts == null) return result;
            foreach (var host in hosts)
            {
                string? ipv4 = null;
                foreach (var s in host.IPAddresses ?? Array.Empty<string>())
                {
                    if (IPAddress.TryParse(s, out var a) && a.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipv4 = a.ToString();
                        break;
                    }
                }
                if (string.IsNullOrEmpty(ipv4)) continue;
                var port = 30100;
                var svc = host.Services?.Values.FirstOrDefault();
                if (svc?.Port != null && svc.Port > 0)
                    port = svc.Port;
                result.Add((host.DisplayName ?? host.Id ?? ipv4, ipv4, port));
            }
        }
        catch
        {
            // Return empty
        }
        return result;
    }
}
