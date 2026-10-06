using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
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
    private const int MaxBindAttempts = 5;

    private readonly IReadOnlyDictionary<string, string?> _settings;
    private readonly Func<(int Public, int Ops)> _pickPorts;
    private IHost? _realHost;

    /// <summary>Creates the factory and picks two free loopback ports so clients can be built before the host starts.</summary>
    public CabinetWebApplicationFactory()
        : this(new Dictionary<string, string?>())
    {
    }

    /// <summary>
    /// Creates the factory with extra configuration values applied before the host is built, so a test can turn the
    /// prototype off or set an invalid value.
    /// </summary>
    /// <param name="settings">
    /// Configuration keys and values added on top of the committed settings. They are applied as host settings because
    /// the program reads some of them while it builds its services, before later configuration sources are added.
    /// </param>
    public CabinetWebApplicationFactory(IReadOnlyDictionary<string, string?> settings)
        : this(settings, PickFreeLoopbackPorts)
    {
    }

    /// <summary>Creates the factory with its own port picker, so a test can hand it a port that is already taken.</summary>
    /// <param name="settings">Configuration keys and values added on top of the committed settings.</param>
    /// <param name="pickPorts">Returns the public and ops ports to try; called again whenever a chosen port turns out to be taken.</param>
    internal CabinetWebApplicationFactory(IReadOnlyDictionary<string, string?> settings, Func<(int Public, int Ops)> pickPorts)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(pickPorts);

        _settings = settings;
        _pickPorts = pickPorts;
        (PublicPort, OpsPort) = pickPorts();

        StartOnFreePorts();
    }

    /// <summary>The loopback port the public listener uses for this instance.</summary>
    public int PublicPort { get; private set; }

    /// <summary>The loopback port the ops listener uses for this instance.</summary>
    public int OpsPort { get; private set; }

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

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(webHostBuilder => webHostBuilder.UseKestrel());

        var realHost = builder.Build();
        try
        {
            realHost.Start();
        }
        catch
        {
            realHost.Dispose();
            testHost.StopAsync().GetAwaiter().GetResult();
            testHost.Dispose();
            throw;
        }

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

    /// <summary>
    /// Builds and starts the hosts, moving both listeners to fresh ports when one of the chosen ports was taken between
    /// being picked and being bound, which parallel tests and outgoing connections can both do. Each attempt goes back
    /// through Server: a failed start leaves that attempt's host builder unable to start again, while Server builds a
    /// new one as long as no host has started.
    /// </summary>
    private void StartOnFreePorts()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _ = Server;
                return;
            }
            catch (IOException exception) when (exception.InnerException is AddressInUseException && attempt < MaxBindAttempts)
            {
                (PublicPort, OpsPort) = _pickPorts();
            }
        }
    }

    /// <summary>Asks the system for two free loopback ports, holding the first while taking the second so they differ.</summary>
    internal static (int Public, int Ops) PickFreeLoopbackPorts()
    {
        using var publicListener = new TcpListener(IPAddress.Loopback, 0);
        using var opsListener = new TcpListener(IPAddress.Loopback, 0);
        publicListener.Start();
        opsListener.Start();
        return (((IPEndPoint)publicListener.LocalEndpoint).Port, ((IPEndPoint)opsListener.LocalEndpoint).Port);
    }
}
