using System.Collections.Concurrent;
using Microsoft.Extensions.Http;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// Keeps a test host off the real network. Every HttpClient the host builds whose last handler would open a real
/// connection refuses any address that is not loopback, before a connection is made, and remembers the refusal so the
/// test fails even when the code under test swallows the error, as the sync does with a failed source call.
/// Clients a test gave a scripted handler never touch the network and are left alone.
/// </summary>
public sealed class RealNetworkGuard
{
    private readonly ConcurrentQueue<string> _refused = new();

    /// <summary>The requests this guard refused so far, as method and address.</summary>
    public IReadOnlyCollection<string> Refused => _refused.ToArray();

    /// <summary>The filter to register in a host, so every client the host's factory builds goes through this guard.</summary>
    public IHttpMessageHandlerBuilderFilter Filter() => new GuardFilter(this);

    /// <summary>Throws when any request was refused, naming each one, and forgets them so a second dispose stays quiet.</summary>
    public void ThrowIfAnyRefused()
    {
        var refused = new List<string>();
        while (_refused.TryDequeue(out var request))
        {
            refused.Add(request);
        }

        if (refused.Count > 0)
        {
            throw new InvalidOperationException(
                "A test host tried to reach the real network. Give the client a scripted handler in the test host. Refused: "
                + string.Join(", ", refused));
        }
    }

    /// <summary>Whether a handler opens real connections rather than answering from a script.</summary>
    /// <param name="handler">The primary handler a client was built with.</param>
    internal static bool OpensRealConnections(HttpMessageHandler handler) =>
        handler is SocketsHttpHandler or HttpClientHandler;

    private void Record(HttpRequestMessage request) =>
        _refused.Enqueue($"{request.Method} {request.RequestUri}");

    /// <summary>Adds the refusing handler next to the primary handler of every client that would open real connections.</summary>
    private sealed class GuardFilter(RealNetworkGuard guard) : IHttpMessageHandlerBuilderFilter
    {
        /// <inheritdoc />
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) =>
            builder =>
            {
                next(builder);

                if (OpensRealConnections(builder.PrimaryHandler))
                {
                    builder.AdditionalHandlers.Add(new RefusingHandler(guard));
                }
            };
    }

    /// <summary>Lets loopback requests through and refuses every other address without opening a connection.</summary>
    private sealed class RefusingHandler(RealNetworkGuard guard) : DelegatingHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is { IsLoopback: true })
            {
                return base.SendAsync(request, cancellationToken);
            }

            guard.Record(request);

            throw new InvalidOperationException(
                $"A test host may not reach the real network; {request.Method} {request.RequestUri} was refused.");
        }
    }
}
