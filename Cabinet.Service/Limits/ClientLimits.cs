using System.Globalization;

namespace Cabinet.Service.Limits;

/// <summary>The settings of the per-client limits.</summary>
/// <param name="SyncPressesPerWindow">The most presses of the sync button one client address may make in one window.</param>
/// <param name="SyncWindow">The length of the window the presses are counted in.</param>
/// <param name="LiveConnectionsPerClient">The most live connections one client address may hold open at once.</param>
/// <param name="LiveConnectsPerWindow">The most new live connections one client address may ask for in one window.</param>
/// <param name="LiveWindow">The length of the window the new live connections are counted in.</param>
/// <remarks>The defaults are starting values, to be sized later from a measured first visit.</remarks>
public sealed record ClientLimitOptions(
    int SyncPressesPerWindow,
    TimeSpan SyncWindow,
    int LiveConnectionsPerClient,
    int LiveConnectsPerWindow,
    TimeSpan LiveWindow)
{
    /// <summary>The presses allowed per window when the setting is absent.</summary>
    public const int DefaultSyncPressesPerWindow = 5;

    /// <summary>The length of the window in seconds when the setting is absent.</summary>
    public const int DefaultSyncWindowSeconds = 60;

    /// <summary>The live connections one address may hold at once when the setting is absent; enough for a game night on one shared connection.</summary>
    public const int DefaultLiveConnectionsPerClient = 20;

    /// <summary>The new live connections allowed per window when the setting is absent.</summary>
    public const int DefaultLiveConnectsPerWindow = 60;

    /// <summary>The length of the live connect window in seconds when the setting is absent.</summary>
    public const int DefaultLiveWindowSeconds = 60;
}

/// <summary>
/// Reads the per-client limit settings from the ClientLimits configuration section. Every value is checked here, so a
/// typo in the server env file stops the app at startup with a message naming the full key.
/// </summary>
public static class ClientLimitSettings
{
    private const string SyncPressesKey = "ClientLimits:SyncPressesPerWindow";
    private const string SyncWindowKey = "ClientLimits:SyncWindowSeconds";
    private const string LiveConnectionsKey = "ClientLimits:LiveConnectionsPerClient";
    private const string LiveConnectsKey = "ClientLimits:LiveConnectsPerWindow";
    private const string LiveWindowKey = "ClientLimits:LiveWindowSeconds";
    private const int SmallestPresses = 1;
    private const int LargestPresses = 1000;
    private const int SmallestWindowSeconds = 1;
    private const int LargestWindowSeconds = 3600;
    private const int SmallestLiveConnections = 1;
    private const int LargestLiveConnections = 1000;
    private const int SmallestLiveConnects = 1;
    private const int LargestLiveConnects = 10_000;

    /// <summary>Reads the ClientLimits keys. An absent key takes the default; a value that is not a whole number in range throws.</summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <exception cref="InvalidOperationException">A value is invalid; the message names the full key.</exception>
    public static ClientLimitOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var presses = ReadWholeNumber(
            configuration,
            SyncPressesKey,
            ClientLimitOptions.DefaultSyncPressesPerWindow,
            SmallestPresses,
            LargestPresses);
        var windowSeconds = ReadWholeNumber(
            configuration,
            SyncWindowKey,
            ClientLimitOptions.DefaultSyncWindowSeconds,
            SmallestWindowSeconds,
            LargestWindowSeconds);

        var liveConnections = ReadWholeNumber(
            configuration,
            LiveConnectionsKey,
            ClientLimitOptions.DefaultLiveConnectionsPerClient,
            SmallestLiveConnections,
            LargestLiveConnections);
        var liveConnects = ReadWholeNumber(
            configuration,
            LiveConnectsKey,
            ClientLimitOptions.DefaultLiveConnectsPerWindow,
            SmallestLiveConnects,
            LargestLiveConnects);
        var liveWindowSeconds = ReadWholeNumber(
            configuration,
            LiveWindowKey,
            ClientLimitOptions.DefaultLiveWindowSeconds,
            SmallestWindowSeconds,
            LargestWindowSeconds);

        return new ClientLimitOptions(
            presses,
            TimeSpan.FromSeconds(windowSeconds),
            liveConnections,
            liveConnects,
            TimeSpan.FromSeconds(liveWindowSeconds));
    }

    private static int ReadWholeNumber(IConfiguration configuration, string key, int fallback, int smallest, int largest)
    {
        var text = configuration[key];

        if (text is null)
        {
            return fallback;
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < smallest
            || value > largest)
        {
            throw new InvalidOperationException($"{key} must be a whole number between {smallest} and {largest}.");
        }

        return value;
    }
}

/// <summary>
/// Registers and applies the limits the app puts on the routes that cost something. The reverse proxy counts requests per
/// internet address and refuses a flood early; the app adds stricter limits of its own, counted per client address after the
/// forwarded-headers setup has resolved who the client is. The limiter must run after routing so it sees the policy an
/// endpoint asks for.
/// </summary>
public static class ClientLimits
{
    /// <summary>The name of the policy that limits presses of the sync button.</summary>
    public const string SyncPressPolicyName = "sync-press";

    /// <summary>The name of the policy that limits live connections per client address.</summary>
    public const string LiveConnectPolicyName = "live-connect";

    /// <summary>
    /// Validates the limit settings now, so a bad value stops the app at startup, and registers the limiter with the sync
    /// press policy and the live connect policy.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration the ClientLimits settings are read from.</param>
    public static IServiceCollection AddCabinetClientLimits(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = ClientLimitSettings.FromConfiguration(configuration);

        services.AddSingleton(options);
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy<string, SyncPressPolicy>(SyncPressPolicyName);
            limiter.AddPolicy<string, LiveConnectPolicy>(LiveConnectPolicyName);
        });

        return services;
    }

    /// <summary>Adds the limiter to the pipeline. Call it after the forwarded headers and after routing.</summary>
    /// <param name="app">The pipeline to add to.</param>
    public static IApplicationBuilder UseCabinetClientLimits(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseRateLimiter();
    }
}
