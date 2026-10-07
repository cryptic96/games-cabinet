using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cabinet.FakeBgg;

/// <summary>
/// A look-alike of BoardGameGeek's XML API that listens on the loopback interface only, answers with invented data
/// and never looks at the credentials a caller sends.
/// </summary>
public static class FakeBggServer
{
    private const string XmlContentType = "text/xml; charset=utf-8";
    private const string HtmlContentType = "text/html; charset=utf-8";
    private const int MaxThingIds = 20;

    /// <summary>
    /// Builds the fake, bound to the loopback interface. Pass port 0 to let the system pick a free port, then read it
    /// from <see cref="WebApplication.Urls"/> once the application has started.
    /// </summary>
    /// <param name="scenario">How the fake answers at first.</param>
    /// <param name="size">The collection size at first; see <see cref="SyntheticBggCollection.Create"/>.</param>
    /// <param name="port">The loopback port to listen on, or 0 for any free port.</param>
    public static WebApplication Create(FakeBggScenario scenario, int size, int port)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var builder = WebApplication.CreateBuilder();
        IgnoreAmbientConfiguration(builder);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls($"http://{IPAddress.Loopback}:{port.ToString(CultureInfo.InvariantCulture)}");

        var app = builder.Build();
        var state = new FakeBggState(scenario, size);

        app.MapGet("/xmlapi2/collection", context => Collection(context, state));
        app.MapGet("/xmlapi2/thing", context => Thing(context, state));
        app.MapPost("/fake/scenario", context => SwitchScenario(context, state));
        return app;
    }

    /// <summary>
    /// Drops every configuration source so settings of a host that happens to share the working directory, such as the
    /// cabinet's own listener addresses, cannot change where or how the fake listens.
    /// </summary>
    private static void IgnoreAmbientConfiguration(WebApplicationBuilder builder)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
    }

    private static async Task Collection(HttpContext context, FakeBggState state)
    {
        var (scenario, _, items) = state.Current;
        if (await TryAnswerFailure(context, scenario))
        {
            return;
        }

        if (scenario.Name == "queued" && state.NextQueuedAttempt(context.Request.QueryString.Value ?? string.Empty) <= scenario.QueuedCount)
        {
            await Write(context, StatusCodes.Status202Accepted, XmlContentType, BggXml.Queued());
            return;
        }

        var query = CollectionQuery.Parse(context.Request.Query);
        var body = scenario.Name switch
        {
            "mismatch" => BggXml.Collection(items, query, BggXml.Select(items, query).Count + 3),
            "empty" => BggXml.Collection([], query),
            "shrunk" => BggXml.Collection(FirstQuarter(BggXml.Select(items, query)), query),
            _ => BggXml.Collection(items, query),
        };
        await Write(context, StatusCodes.Status200OK, XmlContentType, body);
    }

    private static async Task Thing(HttpContext context, FakeBggState state)
    {
        var (scenario, _, items) = state.Current;
        if (await TryAnswerFailure(context, scenario))
        {
            return;
        }

        var ids = ParseIds(context.Request.Query["id"].ToString());
        if (ids.Count == 0 || ids.Count > MaxThingIds)
        {
            await Write(context, StatusCodes.Status400BadRequest, XmlContentType, BggXml.Errors($"Provide between 1 and {MaxThingIds} ids."));
            return;
        }

        var stats = context.Request.Query["stats"].ToString() == "1";
        await Write(context, StatusCodes.Status200OK, XmlContentType, BggXml.Things(ids, items, stats));
    }

    private static async Task SwitchScenario(HttpContext context, FakeBggState state)
    {
        if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var current = state.Current;
        var name = context.Request.Query["name"].ToString();
        var scenario = current.Scenario;
        if (name.Length > 0 && !FakeBggScenario.TryParse(name, out scenario))
        {
            await Write(context, StatusCodes.Status400BadRequest, "text/plain; charset=utf-8", $"Unknown scenario. Use one of: {string.Join(", ", FakeBggScenario.Names)}.");
            return;
        }

        var sizeText = context.Request.Query["size"].ToString();
        var size = current.Size;
        if (sizeText.Length > 0 && !int.TryParse(sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out size))
        {
            await Write(context, StatusCodes.Status400BadRequest, "text/plain; charset=utf-8", "The size must be a whole number.");
            return;
        }

        state.Switch(scenario, size);
        var applied = state.Current;
        await Write(context, StatusCodes.Status200OK, "text/plain; charset=utf-8", $"scenario {applied.Scenario}, size {applied.Size.ToString(CultureInfo.InvariantCulture)}");
    }

    private static async Task<bool> TryAnswerFailure(HttpContext context, FakeBggScenario scenario)
    {
        switch (scenario.Name)
        {
            case "slow":
                await Task.Delay(scenario.SlowMilliseconds, context.RequestAborted);
                return false;
            case "throttle":
                await Write(context, StatusCodes.Status429TooManyRequests, "text/plain; charset=utf-8", "Too Many Requests");
                return true;
            case "unauthorized":
                await Write(context, StatusCodes.Status401Unauthorized, HtmlContentType, string.Empty);
                return true;
            case "unavailable":
                await Write(context, StatusCodes.Status503ServiceUnavailable, HtmlContentType, "Service Unavailable");
                return true;
            case "broken":
                await Write(context, StatusCodes.Status200OK, HtmlContentType, BggXml.CloudflarePage());
                return true;
            case "malformed":
                await Write(context, StatusCodes.Status200OK, XmlContentType, BggXml.Malformed());
                return true;
            case "errors":
                await Write(context, StatusCodes.Status200OK, XmlContentType, BggXml.Errors("Invalid username specified"));
                return true;
            default:
                return false;
        }
    }

    private static IEnumerable<FakeBggItem> FirstQuarter(IReadOnlyList<FakeBggItem> selected) =>
        selected.Take(selected.Count / 4);

    private static List<int> ParseIds(string text)
    {
        var ids = new List<int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static async Task Write(HttpContext context, int statusCode, string contentType, string body)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = contentType;
        await context.Response.WriteAsync(body, context.RequestAborted);
    }

    private sealed class FakeBggState
    {
        private readonly object _gate = new();
        private readonly ConcurrentDictionary<string, int> _queuedAttempts = new();
        private (FakeBggScenario Scenario, int Size, IReadOnlyList<FakeBggItem> Items) _current;

        public FakeBggState(FakeBggScenario scenario, int size) => Switch(scenario, size);

        public (FakeBggScenario Scenario, int Size, IReadOnlyList<FakeBggItem> Items) Current
        {
            get
            {
                lock (_gate)
                {
                    return _current;
                }
            }
        }

        public void Switch(FakeBggScenario scenario, int size)
        {
            var clamped = SyntheticBggCollection.Clamp(size);
            var items = SyntheticBggCollection.Create(clamped);
            lock (_gate)
            {
                _current = (scenario, clamped, items);
                _queuedAttempts.Clear();
            }
        }

        public int NextQueuedAttempt(string queryString) =>
            _queuedAttempts.AddOrUpdate(queryString, 1, (_, attempts) => attempts + 1);
    }
}
