using System.Globalization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Cabinet.Service.Live;

/// <summary>The settings of the live channel.</summary>
/// <param name="MaxConnections">The most live connections held open at once, for the whole site.</param>
public sealed record LiveOptions(int MaxConnections)
{
    /// <summary>The cap used when the setting is absent.</summary>
    public const int DefaultMaxConnections = 100;
}

/// <summary>
/// Reads the live settings from the Live configuration section. The value is checked here, so a typo in the server env file
/// stops the app at startup with a message naming the key.
/// </summary>
public static class LiveSettings
{
    private const string MaxConnectionsKey = "Live:MaxConnections";
    private const int Smallest = 1;
    private const int Largest = 10_000;

    /// <summary>Reads the Live keys. An absent key takes the default; a value that is not a whole number in range throws.</summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <exception cref="InvalidOperationException">A value is invalid; the message names the full key.</exception>
    public static LiveOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var text = configuration[MaxConnectionsKey];

        if (text is null)
        {
            return new LiveOptions(LiveOptions.DefaultMaxConnections);
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < Smallest
            || value > Largest)
        {
            throw new InvalidOperationException($"{MaxConnectionsKey} must be a whole number between {Smallest} and {Largest}.");
        }

        return new LiveOptions(value);
    }
}

/// <summary>
/// Registers and maps the live channel: a hub that only broadcasts, over WebSockets or Server-Sent Events, with a cap on
/// open connections and small message and buffer sizes for a low-power host that faces the internet.
/// </summary>
public static class LiveEndpoints
{
    /// <summary>The route pages connect to.</summary>
    public const string Route = "/cabinet/live";

    private const int MaxMessageBytes = 1024;
    private const int ApplicationBufferBytes = 4096;
    private const int TransportBufferBytes = 8192;

    /// <summary>
    /// Validates the live settings now, so a bad value stops the app at startup, and registers the hub, its limits, the
    /// connection cap and the notifier the sync uses.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration the Live settings are read from.</param>
    public static IServiceCollection AddCabinetLive(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = LiveSettings.FromConfiguration(configuration);

        services.AddSingleton(options);
        services.AddSingleton<LiveConnectionLimiter>();
        services.AddSingleton<ILiveNotifier, HubLiveNotifier>();
        services.AddSignalR(hub =>
        {
            hub.EnableDetailedErrors = false;
            hub.MaximumReceiveMessageSize = MaxMessageBytes;
            hub.KeepAliveInterval = TimeSpan.FromSeconds(15);
            hub.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            hub.HandshakeTimeout = TimeSpan.FromSeconds(10);
        });
        services.Configure<KestrelServerOptions>(kestrel => kestrel.Limits.MaxConcurrentUpgradedConnections = options.MaxConnections);

        return services;
    }

    /// <summary>Maps the hub on WebSockets and Server-Sent Events only; long polling is not offered.</summary>
    public static IEndpointRouteBuilder MapCabinetLive(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHub<CabinetHub>(Route, connection =>
        {
            connection.Transports = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents;
            connection.ApplicationMaxBufferSize = ApplicationBufferBytes;
            connection.TransportMaxBufferSize = TransportBufferBytes;
        });

        return endpoints;
    }
}
