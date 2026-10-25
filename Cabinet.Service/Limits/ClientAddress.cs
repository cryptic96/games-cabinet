using System.Net;
using System.Net.Sockets;

namespace Cabinet.Service.Limits;

/// <summary>
/// Works out which client a request counts against. It reads only the connection's remote address, which the
/// forwarded-headers setup has already replaced with the forwarded client address when, and only when, the peer is the known
/// proxy. It never reads the forwarding header itself, so a header written by anyone else buys no separate budget.
/// </summary>
public static class ClientAddress
{
    private const string Unknown = "unknown";
    private const int IPv6PrefixBytes = 8;

    /// <summary>
    /// The key requests are counted under: an IPv4 address as itself, an IPv4-mapped IPv6 address as its IPv4 address, and
    /// every other IPv6 address as its /64 prefix, so one client cannot dodge a limit by cycling addresses inside its prefix.
    /// </summary>
    /// <param name="context">The request.</param>
    public static string PartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var address = context.Connection.RemoteIpAddress;

        if (address is null)
        {
            return Unknown;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, IPv6PrefixBytes, bytes.Length - IPv6PrefixBytes);

        return $"{new IPAddress(bytes)}/64";
    }
}
