using System.Net;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using FluentAssertions;

namespace Cabinet.UnitTests.Bgg;

/// <summary>Proves every way BGG can answer badly ends a fetch with the right failure category and never with a collection.</summary>
[Trait("Category", "Bgg")]
public class BggFailureTests
{
    private const string EntityDocument =
        "<?xml version=\"1.0\"?><!DOCTYPE items [<!ENTITY owned \"1\">]><items totalitems=\"0\"></items>";

    private static readonly IReadOnlyList<FakeBggItem> Items = SyntheticBggCollection.Create(5);

    [Theory]
    [InlineData("unauthorized", SyncFailure.Unauthorized)]
    [InlineData("too-many-requests", SyncFailure.Throttled)]
    [InlineData("too-many-requests-with-retry-after", SyncFailure.Throttled)]
    [InlineData("service-unavailable", SyncFailure.Throttled)]
    [InlineData("forbidden-challenge-page", SyncFailure.Unavailable)]
    [InlineData("server-error", SyncFailure.Unavailable)]
    [InlineData("not-found", SyncFailure.Unavailable)]
    [InlineData("moved-permanently", SyncFailure.Unavailable)]
    [InlineData("found", SyncFailure.Unavailable)]
    [InlineData("html-page-with-success-status", SyncFailure.BadAnswer)]
    [InlineData("challenge-page-labelled-as-xml", SyncFailure.BadAnswer)]
    [InlineData("errors-document", SyncFailure.BadAnswer)]
    [InlineData("wrong-root", SyncFailure.BadAnswer)]
    [InlineData("malformed-xml", SyncFailure.BadAnswer)]
    [InlineData("document-type-with-entity", SyncFailure.BadAnswer)]
    [InlineData("total-does-not-match", SyncFailure.BadAnswer)]
    public async Task Each_kind_of_bad_answer_on_the_first_call_ends_the_fetch_with_its_category(string answer, SyncFailure expected)
    {
        var handler = new ScriptedBggHandler(_ => Bad(answer));

        var result = await BggTestKit.Client(handler).FetchOwnedAsync(TestContext.Current.CancellationToken);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(expected);
    }

    [Theory]
    [InlineData("unauthorized", SyncFailure.Unauthorized, 1)]
    [InlineData("too-many-requests", SyncFailure.Throttled, 2)]
    [InlineData("server-error", SyncFailure.Unavailable, 2)]
    [InlineData("errors-document", SyncFailure.BadAnswer, 1)]
    [InlineData("total-does-not-match", SyncFailure.BadAnswer, 1)]
    public async Task A_failure_in_the_expansion_call_fails_the_whole_fetch(string answer, SyncFailure expected, int expansionRequests)
    {
        var handler = new ScriptedBggHandler(request => BggTestKit.IsBaseCall(request) ? BggTestKit.Good(Items, request) : Bad(answer));

        var result = await BggTestKit.Client(handler).FetchOwnedAsync(TestContext.Current.CancellationToken);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(expected);
        handler.Requests.Should().HaveCount(1 + expansionRequests);
    }

    [Theory]
    [InlineData("unauthorized", 1)]
    [InlineData("too-many-requests", 2)]
    [InlineData("service-unavailable", 2)]
    public async Task A_refusal_or_a_throttle_on_the_first_call_means_no_request_for_expansions(string answer, int baseRequests)
    {
        var handler = new ScriptedBggHandler(_ => Bad(answer));

        await BggTestKit.Client(handler).FetchOwnedAsync(TestContext.Current.CancellationToken);

        handler.Requests.Should().HaveCount(baseRequests);
        handler.Requests.Should().OnlyContain(request => request.Uri.Query.Contains("excludesubtype", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_connection_error_is_unavailable()
    {
        var handler = new ScriptedBggHandler(_ => throw new HttpRequestException("connection refused"));

        var result = await BggTestKit.Client(handler).FetchOwnedAsync(TestContext.Current.CancellationToken);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Unavailable);
    }

    [Fact]
    public async Task An_answer_slower_than_the_http_clients_own_timeout_is_a_timeout()
    {
        var handler = new ScriptedBggHandler(request => BggTestKit.Good(Items, request) with { Delay = TimeSpan.FromSeconds(1) });
        var client = BggTestKit.Client(handler, timeout: TimeSpan.FromMilliseconds(200));

        var result = await client.FetchOwnedAsync(TestContext.Current.CancellationToken);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Timeout);
    }

    [Fact]
    public async Task A_cancelled_caller_gets_the_cancellation_and_not_a_failure_category()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var handler = new ScriptedBggHandler(request => BggTestKit.Good(Items, request));

        var fetch = () => BggTestKit.Client(handler).FetchOwnedAsync(cancelled.Token);

        await fetch.Should().ThrowAsync<OperationCanceledException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_healthy_answer_is_fetched_so_the_failures_above_are_not_a_default()
    {
        var handler = new ScriptedBggHandler(request => BggTestKit.Good(Items, request));

        var result = await BggTestKit.Client(handler).FetchOwnedAsync(TestContext.Current.CancellationToken);

        result.Should().BeOfType<CollectionFetchResult.Fetched>().Which.Items.Should().NotBeEmpty();
        handler.Requests.Should().HaveCount(2);
    }

    private static ScriptedResponse Bad(string answer) =>
        answer switch
        {
            "unauthorized" => ScriptedResponse.Empty(HttpStatusCode.Unauthorized),
            "too-many-requests" => ScriptedResponse.Empty(HttpStatusCode.TooManyRequests),
            "too-many-requests-with-retry-after" => ScriptedResponse.Empty(HttpStatusCode.TooManyRequests)
                with { Headers = new Dictionary<string, string> { ["Retry-After"] = "120" } },
            "service-unavailable" => ScriptedResponse.Empty(HttpStatusCode.ServiceUnavailable),
            "forbidden-challenge-page" => new ScriptedResponse(HttpStatusCode.Forbidden, "text/html; charset=utf-8", BggXml.CloudflarePage()),
            "server-error" => ScriptedResponse.Empty(HttpStatusCode.InternalServerError),
            "not-found" => ScriptedResponse.Empty(HttpStatusCode.NotFound),
            "moved-permanently" => Redirect(HttpStatusCode.MovedPermanently),
            "found" => Redirect(HttpStatusCode.Found),
            "html-page-with-success-status" => new ScriptedResponse(HttpStatusCode.OK, "text/html; charset=utf-8", BggXml.CloudflarePage()),
            "challenge-page-labelled-as-xml" => ScriptedResponse.Xml(BggXml.CloudflarePage()),
            "errors-document" => ScriptedResponse.Xml(BggXml.Errors("Invalid request")),
            "wrong-root" => ScriptedResponse.Xml("<?xml version=\"1.0\"?><message>Your request has been accepted.</message>"),
            "malformed-xml" => ScriptedResponse.Xml(BggXml.Malformed()),
            "document-type-with-entity" => ScriptedResponse.Xml(EntityDocument),
            "total-does-not-match" => ScriptedResponse.Xml(
                BggXml.Collection(Items, new CollectionQuery(true, null, null, true, false), totalItemsOverride: Items.Count + 40)),
            _ => throw new ArgumentOutOfRangeException(nameof(answer), answer, "That answer is not scripted."),
        };

    private static ScriptedResponse Redirect(HttpStatusCode status) =>
        ScriptedResponse.Empty(status) with
        {
            Headers = new Dictionary<string, string> { ["Location"] = "https://www.boardgamegeek.com/xmlapi2/collection" },
        };
}
