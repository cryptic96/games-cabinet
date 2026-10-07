using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>A listener on a free loopback port that answers every request through one delegate and counts what reached it.</summary>
public sealed class LoopbackListener : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly Counter _counter;

    private LoopbackListener(WebApplication app, Counter counter)
    {
        _app = app;
        _counter = counter;
        BaseAddress = new Uri(app.Urls.Single() + "/");
    }

    /// <summary>The address the listener answers on; it ends with a slash.</summary>
    public Uri BaseAddress { get; }

    /// <summary>How many requests have arrived.</summary>
    public int RequestCount => Volatile.Read(ref _counter.Value);

    /// <summary>Starts a listener that answers every request through <paramref name="answer"/>.</summary>
    /// <param name="answer">Fills in the response of each request.</param>
    public static async Task<LoopbackListener> Start(Func<HttpContext, Task> answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls($"http://{IPAddress.Loopback}:0");
        var app = builder.Build();
        var counter = new Counter();

        app.Run(context =>
        {
            Interlocked.Increment(ref counter.Value);

            return answer(context);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);

        return new LoopbackListener(app, counter);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync(TestContext.Current.CancellationToken);
        await _app.DisposeAsync();
    }

    private sealed class Counter
    {
        public int Value;
    }
}
