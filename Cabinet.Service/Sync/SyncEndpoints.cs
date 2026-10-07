using System.Globalization;
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

    /// <summary>The route that tells a page the state of the sync.</summary>
    public const string StatusRoute = "/cabinet/status";

    private const int ResponseBufferLimitBytes = 20_000_000;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Validates the BGG and storage settings now, so a bad value stops the app at startup, and registers the BGG client and
    /// its pacer, the snapshot store, the sync machinery and the hosted services that run it.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration the Bgg, Sync and Storage settings are read from.</param>
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
        SyncSettings.FromConfiguration(configuration);

        var storage = new StorageDirectory(StorageLocation.Resolve(configuration, environment));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(storage);
        services.AddSingleton(provider => BggSettings.FromConfiguration(
            configuration,
            environment,
            provider.GetRequiredService<BuildInfo>().Version));
        services.AddSingleton(_ => SyncSettings.FromConfiguration(configuration));
        services.AddSingleton<ISyncStateStore>(provider => new SyncStateStore(
            provider.GetRequiredService<StorageDirectory>().Path,
            provider.GetRequiredService<ILogger<SyncStateStore>>()));
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
        services.AddSingleton<SyncStatusService>();
        services.AddHostedService<SyncStartup>();
        services.AddHostedService<SyncWorker>();
        services.AddHostedService<SyncScheduler>();

        return services;
    }

    /// <summary>
    /// Maps the route that asks for a sync, which answers 202 when one starts, 409 when one is already running and 429 with
    /// a Retry-After header inside the shared window, and the route that reports the state of the sync.
    /// </summary>
    public static IEndpointRouteBuilder MapCabinetSync(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(SyncRoute, Handle);
        endpoints.MapGet(StatusRoute, ReadStatus);

        return endpoints;
    }

    private static IResult ReadStatus(SyncStatusService statusService, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";

        return Results.Json(statusService.Current());
    }

    private static IResult Handle(SyncCoordinator coordinator, SyncStatusService statusService, TimeProvider time, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";

        switch (coordinator.TryRequest(SyncTrigger.Manual))
        {
            case SyncRequestResult.Started:
                return Results.Json(
                    new { outcome = "started", status = statusService.Current() },
                    statusCode: StatusCodes.Status202Accepted);

            case SyncRequestResult.CoolingDown cooling:
                context.Response.Headers.RetryAfter = WholeSecondsUntil(cooling.Until, time.GetUtcNow())
                    .ToString(CultureInfo.InvariantCulture);

                return Results.Json(
                    new { outcome = "cooldown", status = statusService.Current() },
                    statusCode: StatusCodes.Status429TooManyRequests);

            default:
                return Results.Json(
                    new { outcome = "running", status = statusService.Current() },
                    statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static int WholeSecondsUntil(DateTimeOffset until, DateTimeOffset now) =>
        Math.Max(1, (int)Math.Ceiling((until - now).TotalSeconds));
}
