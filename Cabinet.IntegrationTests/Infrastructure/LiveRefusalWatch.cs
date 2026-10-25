using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// Counts the requests on the live route that the host answered with 429, from the outermost point of the pipeline. A refused
/// WebSocket handshake is not always visible to the client or to a browser as a response, so the server's own answer is the
/// reliable place to see a refusal. Register it with <see cref="Register"/> in the factory's service set-up.
/// </summary>
public sealed class LiveRefusalWatch : IStartupFilter
{
    private const string LiveRoutePrefix = "/cabinet/live";

    private int _refusals;

    /// <summary>How many requests on the live route were answered 429 so far.</summary>
    public int Refusals => Volatile.Read(ref _refusals);

    /// <summary>Adds the watch to the services so it wraps the host's whole pipeline.</summary>
    /// <param name="services">The service collection to add to.</param>
    public LiveRefusalWatch Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IStartupFilter>(this);

        return this;
    }

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Use(async (context, following) =>
            {
                await following(context);

                if (context.Request.Path.StartsWithSegments(LiveRoutePrefix, StringComparison.OrdinalIgnoreCase)
                    && context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
                {
                    Interlocked.Increment(ref _refusals);
                }
            });

            next(app);
        };
    }
}
