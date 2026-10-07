using System.Globalization;
using System.Net;
using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>What the layout route served at one moment: its entity tag and every title it placed.</summary>
/// <param name="ETag">The entity tag of the layout.</param>
/// <param name="Titles">The title of every placement.</param>
public sealed record LayoutReading(string ETag, IReadOnlyList<string> Titles);

/// <summary>Drives a booted host through presses of the sync button, one finished run at a time, on a fake clock.</summary>
public static class SyncRounds
{
    /// <summary>The length of the shared window the host is configured with.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    private const string LayoutPath = "/cabinet/layout?profile=desktop";

    /// <summary>
    /// Moves the clock past the shared window, presses the button, and waits until that run has ended. The first press of a
    /// test needs no move, because the host starts with no window open.
    /// </summary>
    /// <param name="client">A client on the public listener.</param>
    /// <param name="clock">The clock the host measures its windows on.</param>
    /// <param name="advance">Whether to move the clock past the window before pressing.</param>
    /// <param name="moveClockWhileWaiting">
    /// Whether to keep moving the clock forward while the run is in progress, so a wait inside the run (such as the pause
    /// before a retry) passes without a real wait.
    /// </param>
    /// <param name="timeout">How long to wait for the run to end; null for the harness default.</param>
    public static async Task PressAndWait(
        HttpClient client,
        FakeTimeProvider clock,
        bool advance = true,
        bool moveClockWhileWaiting = false,
        TimeSpan? timeout = null)
    {
        if (advance)
        {
            clock.Advance(Cooldown);
        }

        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        press.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var pressedAt = clock.GetUtcNow();

        await SyncHarness.WaitUntil(async () =>
        {
            var status = await SyncHarness.ReadStatus(client);
            var ended = !status.Running
                && status.Json.GetProperty("lastResultAtUtc").ValueKind != JsonValueKind.Null
                && status.Json.GetProperty("lastResultAtUtc").GetDateTimeOffset() >= pressedAt;

            if (!ended && moveClockWhileWaiting)
            {
                clock.Advance(TimeSpan.FromSeconds(10));
            }

            return ended;
        }, timeout);
    }

    /// <summary>Reads what the desktop layout route serves now.</summary>
    /// <param name="client">A client on the public listener.</param>
    public static async Task<LayoutReading> ReadLayout(HttpClient client)
    {
        using var response = await client.GetAsync(LayoutPath, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var titles = document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray())
            .Select(placement => placement.GetProperty("title").GetString()!)
            .ToList();

        return new LayoutReading(response.Headers.ETag!.Tag, titles);
    }

    /// <summary>
    /// Answers collection calls with the given entries, whatever the filters ask for, and details calls with the details of
    /// the games they name.
    /// </summary>
    /// <param name="request">The request being answered.</param>
    /// <param name="items">The invented entries.</param>
    public static ScriptedResponse Healthy(HttpRequestMessage request, IReadOnlyList<FakeBggItem> items)
    {
        var address = request.RequestUri!;

        if (!address.AbsolutePath.EndsWith("/thing", StringComparison.Ordinal))
        {
            return ScriptedResponse.Xml(BggXml.Collection(items, CollectionQuery.Parse(address.Query)));
        }

        var query = QueryHelpers.ParseQuery(address.Query);
        var ids = query["id"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => int.Parse(id, CultureInfo.InvariantCulture));

        return ScriptedResponse.Xml(BggXml.Things(ids, items, query["stats"].ToString() == "1"));
    }

    /// <summary>
    /// A handler whose answers a test can switch: it answers like a healthy BGG for the current entries until a failure is
    /// set, and then answers every call with that failure.
    /// </summary>
    public sealed class SwitchableBgg
    {
        private readonly object _gate = new();
        private IReadOnlyList<FakeBggItem> _items;
        private ScriptedResponse? _failure;

        /// <summary>Creates the source with the entries it first serves.</summary>
        /// <param name="items">The invented entries.</param>
        public SwitchableBgg(IReadOnlyList<FakeBggItem> items)
        {
            _items = items;
            Handler = new ScriptedBggHandler(Answer);
        }

        /// <summary>The handler the host sends its BGG calls to.</summary>
        public ScriptedBggHandler Handler { get; }

        /// <summary>Serves these entries from now on, and no failure.</summary>
        /// <param name="items">The invented entries.</param>
        public void Serve(IReadOnlyList<FakeBggItem> items)
        {
            lock (_gate)
            {
                _items = items;
                _failure = null;
            }
        }

        /// <summary>Answers every call with this response from now on.</summary>
        /// <param name="failure">The response.</param>
        public void Fail(ScriptedResponse failure)
        {
            lock (_gate)
            {
                _failure = failure;
            }
        }

        private ScriptedResponse Answer(HttpRequestMessage request)
        {
            lock (_gate)
            {
                return _failure ?? Healthy(request, _items);
            }
        }
    }
}
