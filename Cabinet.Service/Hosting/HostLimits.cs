using System.Globalization;
using System.Runtime;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Cabinet.Service.Hosting;

/// <summary>The limits Kestrel applies to every listener of the host.</summary>
/// <param name="MaxConcurrentConnections">The most connections one listener holds open at once.</param>
/// <param name="MaxRequestBodyBytes">The largest request body a request may carry unless its endpoint asks for more.</param>
/// <param name="RequestHeadersTimeout">How long a connection may take to finish sending its request headers.</param>
public sealed record HostLimitOptions(int MaxConcurrentConnections, long MaxRequestBodyBytes, TimeSpan RequestHeadersTimeout)
{
    /// <summary>
    /// The connection ceiling used when the setting is absent. It applies to each listener separately, because Kestrel adds
    /// one connection-limit middleware per bound endpoint, so the loopback ops listener keeps its own budget even while the
    /// public listener is full. It also counts Server-Sent Events connections, so it stays generous.
    /// </summary>
    public const int DefaultMaxConcurrentConnections = 512;

    /// <summary>
    /// The request body limit used when the setting is absent. It equals the default of the owner pages and nothing public
    /// reads a body, so no form changes behaviour. The one endpoint allowed more is the owner settings page, which takes the
    /// owner-data file for checking and restoring and may receive 3 MB, because a backup of a large collection is that big.
    /// </summary>
    public const long DefaultMaxRequestBodyBytes = 65536;

    /// <summary>The header timeout, in seconds, used when the setting is absent; it ends slow-request attacks that hold a connection open.</summary>
    public const int DefaultRequestHeadersTimeoutSeconds = 10;
}

/// <summary>
/// Reads the host limits from the HostLimits configuration section. Each value is checked here, so a typo in the server env
/// file stops the app at startup with a message naming the key.
/// </summary>
public static class HostLimitSettings
{
    private const string MaxConcurrentConnectionsKey = "HostLimits:MaxConcurrentConnections";
    private const string MaxRequestBodyBytesKey = "HostLimits:MaxRequestBodyBytes";
    private const string RequestHeadersTimeoutKey = "HostLimits:RequestHeadersTimeoutSeconds";

    private const int SmallestConnections = 16;
    private const int LargestConnections = 100_000;
    private const long SmallestBodyBytes = 1024;
    private const long LargestBodyBytes = 10_485_760;
    private const int SmallestTimeoutSeconds = 2;
    private const int LargestTimeoutSeconds = 120;

    /// <summary>Reads the HostLimits keys. An absent key takes the default; a value that is not a whole number in range throws.</summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <exception cref="InvalidOperationException">A value is invalid; the message names the full key.</exception>
    public static HostLimitOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connections = Read(
            configuration,
            MaxConcurrentConnectionsKey,
            HostLimitOptions.DefaultMaxConcurrentConnections,
            SmallestConnections,
            LargestConnections);
        var bodyBytes = Read(
            configuration,
            MaxRequestBodyBytesKey,
            HostLimitOptions.DefaultMaxRequestBodyBytes,
            SmallestBodyBytes,
            LargestBodyBytes);
        var timeoutSeconds = Read(
            configuration,
            RequestHeadersTimeoutKey,
            HostLimitOptions.DefaultRequestHeadersTimeoutSeconds,
            SmallestTimeoutSeconds,
            LargestTimeoutSeconds);

        return new HostLimitOptions((int)connections, bodyBytes, TimeSpan.FromSeconds(timeoutSeconds));
    }

    private static long Read(IConfiguration configuration, string key, long fallback, long smallest, long largest)
    {
        var text = configuration[key];

        if (text is null)
        {
            return fallback;
        }

        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < smallest
            || value > largest)
        {
            throw new InvalidOperationException($"{key} must be a whole number between {smallest} and {largest}.");
        }

        return value;
    }
}

/// <summary>
/// Registers the Kestrel limits for a host that faces the internet: a connection ceiling, a request body limit and a
/// request-header timeout. The keep-alive timeout is deliberately left at Kestrel's default because it must stay above the
/// reverse proxy's 90-second backend idle timeout; shortening it would let the proxy reuse a connection the app has just closed.
/// </summary>
public static class HostLimits
{
    /// <summary>
    /// Validates the host limit settings now, so a bad value stops the app at startup, applies them to Kestrel and registers the
    /// start-up report of what the runtime sees.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration the HostLimits settings are read from.</param>
    public static IServiceCollection AddCabinetHostLimits(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = HostLimitSettings.FromConfiguration(configuration);

        services.AddSingleton(options);
        services.Configure<KestrelServerOptions>(kestrel =>
        {
            kestrel.Limits.MaxConcurrentConnections = options.MaxConcurrentConnections;
            kestrel.Limits.MaxRequestBodySize = options.MaxRequestBodyBytes;
            kestrel.Limits.RequestHeadersTimeout = options.RequestHeadersTimeout;
        });
        services.AddHostedService<HostLimitsReport>();

        return services;
    }
}

/// <summary>
/// Logs once at start what the .NET runtime sees: the memory limit, the processor count and whether server garbage collection
/// is on, so a check of the container can confirm the runtime honours the unit's memory limit.
/// </summary>
/// <param name="logger">Where the report is written.</param>
public sealed partial class HostLimitsReport(ILogger<HostLimitsReport> logger) : IHostedService
{
    private const long BytesPerMegabyte = 1024 * 1024;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var megabytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / BytesPerMegabyte;

        LogRuntimeView(megabytes, Environment.ProcessorCount, GCSettings.IsServerGC);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The runtime sees {MemoryLimitMegabytes} MB of memory and {ProcessorCount} processors; server GC: {ServerGc}")]
    private partial void LogRuntimeView(long memoryLimitMegabytes, int processorCount, bool serverGc);
}
