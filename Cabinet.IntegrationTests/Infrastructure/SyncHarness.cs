using System.Net;
using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg.Testing;
using Cabinet.Repository.Bgg;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>What a page learns from the status route, read back into plain values for assertions.</summary>
/// <param name="Body">The raw JSON text.</param>
/// <param name="Json">The parsed root element, cloned so it outlives the reader.</param>
public sealed record StatusReading(string Body, JsonElement Json)
{
    /// <summary>Whether a sync is in progress.</summary>
    public bool Running => Json.GetProperty("running").GetBoolean();

    /// <summary>The summary of the last run, or null before any run ended.</summary>
    public string? LastResult => Json.GetProperty("lastResult").GetString();

    /// <summary>The end of the shared window, or null when it is over.</summary>
    public DateTimeOffset? CooldownEndsUtc => Json.GetProperty("cooldownEndsUtc").ValueKind == JsonValueKind.Null
        ? null
        : Json.GetProperty("cooldownEndsUtc").GetDateTimeOffset();
}

/// <summary>Shared set-up for the tests that drive the sync through a booted host.</summary>
public static class SyncHarness
{
    /// <summary>The invented username every host is configured with.</summary>
    public const string Username = "sentinel-user-name";

    /// <summary>The invented token every host is configured with.</summary>
    public const string Token = "sentinel-token-value";

    /// <summary>The route that asks for a sync.</summary>
    public const string SyncRoute = "/cabinet/sync";

    /// <summary>The route that reports the sync state.</summary>
    public const string StatusRoute = "/cabinet/status";

    /// <summary>A fixed, invented moment the fake clock starts at.</summary>
    public static DateTimeOffset StartTime { get; } = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a fake clock at the fixed start time.</summary>
    public static FakeTimeProvider NewClock() => new(StartTime);

    /// <summary>The BGG settings every host uses: the invented credentials and the real host name, so the pinned request rules apply.</summary>
    public static BggOptions Options() =>
        new(BggOptions.DefaultBaseUri, Username, Token, null, BggOptions.MinimumRequestGap, false, "0.0.0-test");

    /// <summary>Boots a host whose BGG calls go to the scripted handler, whose pacer never waits and whose clock is the given one.</summary>
    /// <param name="handler">Answers the BGG calls.</param>
    /// <param name="clock">The clock the sync measures its windows on.</param>
    /// <param name="settings">Extra configuration values; null for none.</param>
    public static CabinetWebApplicationFactory CreateFactory(
        HttpMessageHandler handler,
        TimeProvider clock,
        IReadOnlyDictionary<string, string?>? settings = null) =>
        new(
            settings ?? new Dictionary<string, string?>(),
            services =>
            {
                services.AddSingleton(Options());
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
                services.AddSingleton(clock);
                services.AddHttpClient<ICollectionSource, BggClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
            });

    /// <summary>Reads the status route.</summary>
    /// <param name="client">A client on the public listener.</param>
    public static async Task<StatusReading> ReadStatus(HttpClient client)
    {
        using var response = await client.GetAsync(StatusRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);

        return new StatusReading(body, document.RootElement.Clone());
    }

    /// <summary>Waits until the status says a run has ended and no run is in progress.</summary>
    /// <param name="client">A client on the public listener.</param>
    public static Task WaitForRunToEnd(HttpClient client) =>
        WaitUntil(async () =>
        {
            var status = await ReadStatus(client);

            return !status.Running && status.LastResult is not null;
        });

    /// <summary>Polls a condition in real time, because the sync runs on its own thread.</summary>
    /// <param name="condition">Returns true once the wanted state is reached.</param>
    public static async Task WaitUntil(Func<Task<bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not hold within ten seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A scripted handler that answers every call with an authorisation refusal.</summary>
    public static ScriptedBggHandler Refusing() =>
        new(_ => ScriptedResponse.Empty(HttpStatusCode.Unauthorized));
}
