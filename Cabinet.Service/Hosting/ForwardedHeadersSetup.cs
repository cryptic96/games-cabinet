using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Cabinet.Service.Hosting;

/// <summary>
/// The one forwarded-headers configuration, shared by the program and by the test host that plays an untrusted peer. A
/// forwarded address is believed only when it comes from a configured known proxy, so anyone else who writes the header
/// is simply the client they are.
/// </summary>
public static class ForwardedHeadersSetup
{
    /// <summary>Applies the forwarded headers the site reads, one hop only, and the known proxies from configuration.</summary>
    /// <param name="options">The options to fill in.</param>
    /// <param name="configuration">The application configuration holding <c>ReverseProxy:KnownProxies</c>.</param>
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        var knownProxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        foreach (var proxy in knownProxies)
        {
            if (!IPAddress.TryParse(proxy, out var address))
            {
                throw new InvalidOperationException(
                    "ReverseProxy:KnownProxies contains an entry that is not an IP address.");
            }

            options.KnownProxies.Add(address);
        }
    }
}
