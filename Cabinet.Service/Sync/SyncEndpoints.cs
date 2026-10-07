using Cabinet.Domain;
using Cabinet.Domain.Collection;
using Cabinet.Repository.Bgg;
using Cabinet.Repository.Storage;
using Cabinet.Service.Collection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cabinet.Service.Sync;

/// <summary>
/// Registers the sync and maps the endpoint that asks for one. The endpoint reads nothing from the request: the
/// username, the host and the query that go to BGG come only from configuration, so a visitor cannot steer them.
/// </summary>
public static class SyncEndpoints
{
    /// <summary>The route that asks for a sync.</summary>
    public const string SyncRoute = "/cabinet/sync";

    private const int ResponseBufferLimitBytes = 20_000_000;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Validates the BGG and storage settings now, so a bad value stops the app at startup, and registers the BGG client and
    /// its pacer, the snapshot store, the sync machinery and the hosted services that run it.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration the Bgg and Storage settings are read from.</param>
    /// <param name="environment">The hosting environment the app runs in.</param>
    public static IServiceCollection AddCabinetSync(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        const string validationVersion = "0";
        BggSettings.FromConfiguration(configuration, environment, validationVersion);

        var storage = new StorageDirectory(StorageLocation.Resolve(configuration, environment));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(storage);
        services.AddSingleton(provider => BggSettings.FromConfiguration(
            configuration,
            environment,
            provider.GetRequiredService<BuildInfo>().Version));
        services.AddSingleton<ISnapshotStore>(provider => new SnapshotStore(
            provider.GetRequiredService<StorageDirectory>().Path,
            provider.GetRequiredService<ILogger<SnapshotStore>>()));
        services.AddSingleton<IRequestPacer>(provider => new RequestPacer(
            provider.GetRequiredService<BggOptions>().MinRequestGap,
            provider.GetRequiredService<TimeProvider>()));
        services.AddTransient<BggAuthHandler>();

        services
            .AddHttpClient<ICollectionSource, BggClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<BggOptions>();
                client.BaseAddress = options.BaseUri;
                client.Timeout = RequestTimeout;
                client.MaxResponseContentBufferSize = ResponseBufferLimitBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(BggTransport.CreatePrimaryHandler)
            .AddHttpMessageHandler<BggAuthHandler>();

        services.AddSingleton(provider => new SyncRunner(
            provider.GetRequiredService<ICollectionSource>,
            provider.GetRequiredService<ISnapshotStore>(),
            provider.GetRequiredService<CollectionStore>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<SyncRunner>>()));
        services.AddSingleton<SyncCoordinator>();
        services.AddHostedService<SyncStartup>();
        services.AddHostedService<SyncWorker>();

        return services;
    }

    /// <summary>Maps the route that asks for a sync; it answers 202 when one starts and 409 when one is already running.</summary>
    public static IEndpointRouteBuilder MapCabinetSync(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(SyncRoute, Handle);

        return endpoints;
    }

    private static IResult Handle(SyncCoordinator coordinator, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";

        return coordinator.TryRequest(SyncTrigger.Manual) switch
        {
            SyncRequestResult.Started => Results.Json(new { outcome = "started" }, statusCode: StatusCodes.Status202Accepted),
            SyncRequestResult.CoolingDown => Results.Json(new { outcome = "cooldown" }, statusCode: StatusCodes.Status429TooManyRequests),
            _ => Results.Json(new { outcome = "running" }, statusCode: StatusCodes.Status409Conflict),
        };
    }
}
