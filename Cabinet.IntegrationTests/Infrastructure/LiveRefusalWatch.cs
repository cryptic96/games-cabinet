using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// Counts the requests on the live route that the host answered with 429, and the messages posted to it, from the outermost
/// point of the pipeline. A refused WebSocket handshake is not always visible to the client or to a browser as a response, so the server's own answer is the
/// reliable place to see a refusal. Register it with <see cref="Register"/> in the factory's service set-up.
/// </summary>
public sealed class LiveRefusalWatch : IStartupFilter
{
    private const string LiveRoutePrefix = "/cabinet/live";

    private const string ConnectionIdKey = "id";

    private int _refusals;
    private int _messages;

    /// <summary>How many requests on the live route were answered 429 so far.</summary>
    public int Refusals => Volatile.Read(ref _refusals);

    /// <summary>How many messages were posted to the live route so far, which is what a Server-Sent Events page sends to stay connected.</summary>
    public int Messages => Volatile.Read(ref _messages);

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

                if (!context.Request.Path.StartsWithSegments(LiveRoutePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
                {
                    Interlocked.Increment(ref _refusals);
                }
                else if (HttpMethods.IsPost(context.Request.Method) && context.Request.Query.ContainsKey(ConnectionIdKey))
                {
                    Interlocked.Increment(ref _messages);
                }
            });

            next(app);
        };
    }
}
