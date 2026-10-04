using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>Boots the cabinet host on real Kestrel sockets so the public and ops listeners are genuinely separate.</summary>
public class CabinetWebApplicationFactory : WebApplicationFactory<Program>
{
    private IHost? _realHost;

    /// <summary>Creates the factory and picks two free loopback ports so clients can be built before the host starts.</summary>
    public CabinetWebApplicationFactory()
    {
        PublicPort = GetFreeLoopbackPort();
        OpsPort = GetFreeLoopbackPort();

        EnsureHostStarted();
    }

    /// <summary>The loopback port the public listener uses for this instance.</summary>
    public int PublicPort { get; }

    /// <summary>The loopback port the ops listener uses for this instance.</summary>
    public int OpsPort { get; }

    /// <summary>An HttpClient bound to the public listener.</summary>
    public HttpClient CreatePublicClient() => new() { BaseAddress = new Uri($"http://127.0.0.1:{PublicPort}") };

    /// <summary>An HttpClient bound to the ops listener.</summary>
    public HttpClient CreateOpsClient() => new() { BaseAddress = new Uri($"http://127.0.0.1:{OpsPort}") };

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kestrel:Endpoints:Web:Url"] = $"http://127.0.0.1:{PublicPort}",
                ["Kestrel:Endpoints:Ops:Url"] = $"http://127.0.0.1:{OpsPort}",
            });
        });
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(webHostBuilder => webHostBuilder.UseKestrel());

        var realHost = builder.Build();
        realHost.Start();
        _realHost = realHost;

        var addresses = realHost.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();

        testHost.Start();
        var testHostAddresses = testHost.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses;
        testHostAddresses.Clear();
        foreach (var address in addresses!.Addresses)
        {
            testHostAddresses.Add(address);
        }

        return testHost;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _realHost?.StopAsync().GetAwaiter().GetResult();
            _realHost?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (_realHost is not null)
        {
            await _realHost.StopAsync();
            _realHost.Dispose();
        }

        await base.DisposeAsync();
    }

    /// <summary>Touches Server, the only member that makes the factory build and start the host.</summary>
    private void EnsureHostStarted()
    {
        _ = Server;
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
