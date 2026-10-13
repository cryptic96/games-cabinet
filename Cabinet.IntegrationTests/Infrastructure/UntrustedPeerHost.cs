using System.Net;
using Cabinet.Service.Hosting;
using Cabinet.Service.Owner;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// A minimal in-memory host that runs the real forwarded-headers setup and the owner client-range wall, with the peer
/// address and local port of every request chosen by the test. The full factory only ever sees loopback peers, and loopback
/// is a trusted proxy there, so a forged forwarding header from an untrusted peer can only be shown here.
/// </summary>
internal sealed class UntrustedPeerHost : IAsyncDisposable
{
    /// <summary>The port the host treats as the owner listener.</summary>
    public const int OwnerPort = 5082;

    /// <summary>A port the host does not treat as the owner listener.</summary>
    public const int PublicPort = 5080;

    private readonly WebApplication _app;
    private readonly TestServer _server;

    private UntrustedPeerHost(WebApplication app, TestServer server)
    {
        _app = app;
        _server = server;
    }

    /// <summary>Starts the host with one known proxy and the given allowed client ranges.</summary>
    /// <param name="knownProxy">The only peer whose forwarded address is believed.</param>
    /// <param name="ranges">The client ranges allowed on the owner port.</param>
    public static async Task<UntrustedPeerHost> StartAsync(string knownProxy, params string[] ranges)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var settings = new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Owner:Url"] = $"http://127.0.0.1:{OwnerPort}",
            ["ReverseProxy:KnownProxies:0"] = knownProxy,
        };
        for (var index = 0; index < ranges.Length; index++)
        {
            settings[$"Owner:AllowedRanges:{index}"] = ranges[index];
        }

        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton(OwnerAccess.FromConfiguration(builder.Configuration));
        builder.Services.Configure<ForwardedHeadersOptions>(
            options => ForwardedHeadersSetup.Configure(options, builder.Configuration));

        var app = builder.Build();
        app.UseConnectionPeer();
        app.UseForwardedHeaders();
        app.UseOwnerClientRange();
        app.MapGet("/probe", context => context.Response.WriteAsync(
            context.Connection.RemoteIpAddress?.ToString() ?? string.Empty));

        await app.StartAsync();

        return new UntrustedPeerHost(app, app.GetTestServer());
    }

    /// <summary>Sends one request as if it came from the given peer to the given local port.</summary>
    /// <param name="peer">The address of the connection's remote end.</param>
    /// <param name="localPort">The port the request arrived on.</param>
    /// <param name="forwardedFor">The forwarding header value to send, or null to send none.</param>
    public async Task<(int Status, string Body)> GetProbeAsync(string peer, int localPort, string? forwardedFor)
    {
        var context = await _server.SendAsync(http =>
        {
            http.Request.Method = HttpMethods.Get;
            http.Request.Path = "/probe";
            http.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            http.Connection.LocalPort = localPort;
            if (forwardedFor is not null)
            {
                http.Request.Headers["X-Forwarded-For"] = forwardedFor;
            }
        });

        using var reader = new StreamReader(context.Response.Body);

        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
