using System.Globalization;

namespace Cabinet.Service.Limits;

/// <summary>The settings of the per-client limits.</summary>
/// <param name="SyncPressesPerWindow">The most presses of the sync button one client address may make in one window.</param>
/// <param name="SyncWindow">The length of the window the presses are counted in.</param>
/// <remarks>The defaults are starting values, to be sized later from a measured first visit.</remarks>
public sealed record ClientLimitOptions(int SyncPressesPerWindow, TimeSpan SyncWindow)
{
    /// <summary>The presses allowed per window when the setting is absent.</summary>
    public const int DefaultSyncPressesPerWindow = 5;

    /// <summary>The length of the window in seconds when the setting is absent.</summary>
    public const int DefaultSyncWindowSeconds = 60;
}

/// <summary>
/// Reads the per-client limit settings from the ClientLimits configuration section. Every value is checked here, so a
/// typo in the server env file stops the app at startup with a message naming the full key.
/// </summary>
public static class ClientLimitSettings
{
    private const string SyncPressesKey = "ClientLimits:SyncPressesPerWindow";
    private const string SyncWindowKey = "ClientLimits:SyncWindowSeconds";
    private const int SmallestPresses = 1;
    private const int LargestPresses = 1000;
    private const int SmallestWindowSeconds = 1;
    private const int LargestWindowSeconds = 3600;

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

        return new ClientLimitOptions(presses, TimeSpan.FromSeconds(windowSeconds));
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

    /// <summary>
    /// Validates the limit settings now, so a bad value stops the app at startup, and registers the limiter with the sync
    /// press policy.
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
