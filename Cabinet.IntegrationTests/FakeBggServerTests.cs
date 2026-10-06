using System.Net;
using System.Xml.Linq;
using Cabinet.FakeBgg;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the local fake BoardGameGeek answers in the real service's shape and misbehaves on request.</summary>
[Trait("Category", "FakeBgg")]
public class FakeBggServerTests
{
    private const string BaseGamesQuery = "/xmlapi2/collection?username=example&own=1&excludesubtype=boardgameexpansion&stats=1&version=1";
    private const string ExpansionsQuery = "/xmlapi2/collection?username=example&own=1&subtype=boardgameexpansion&stats=1&version=1";
    private const string DefaultQuery = "/xmlapi2/collection?username=example&own=1&stats=1";
    private const string UnfilteredQuery = "/xmlapi2/collection?username=example&stats=1";

    [Fact]
    public async Task Base_games_query_returns_only_owned_base_games_with_a_matching_total()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 65);

        var document = await fake.GetXml(BaseGamesQuery);

        var expected = SyntheticBggCollection.Create(65).Where(item => item.Owned && !item.IsExpansion);
        CollectionIds(document).Should().BeEquivalentTo(expected.Select(item => item.CollId));
        Subtypes(document).Should().OnlyContain(subtype => subtype == "boardgame");
        TotalItems(document).Should().Be(document.Root!.Elements("item").Count());
    }

    [Fact]
    public async Task Expansion_query_returns_only_owned_expansions_labelled_as_expansions()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 65);

        var document = await fake.GetXml(ExpansionsQuery);

        var expected = SyntheticBggCollection.Create(65).Where(item => item.Owned && item.IsExpansion);
        CollectionIds(document).Should().BeEquivalentTo(expected.Select(item => item.CollId));
        Subtypes(document).Should().OnlyContain(subtype => subtype == "boardgameexpansion");
        TotalItems(document).Should().Be(document.Root!.Elements("item").Count());
    }

    [Fact]
    public async Task Default_query_returns_everything_owned_with_expansions_labelled_as_base_games()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 65);

        var document = await fake.GetXml(DefaultQuery);

        var owned = SyntheticBggCollection.Create(65).Where(item => item.Owned).ToList();
        CollectionIds(document).Should().BeEquivalentTo(owned.Select(item => item.CollId));
        owned.Should().Contain(item => item.IsExpansion);
        Subtypes(document).Should().OnlyContain(subtype => subtype == "boardgame");
    }

    [Fact]
    public async Task Own_filter_drops_the_unowned_entry()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 65);
        var unowned = SyntheticBggCollection.Create(65).Single(item => !item.Owned);

        var everything = await fake.GetXml(UnfilteredQuery);
        var ownedOnly = await fake.GetXml(DefaultQuery);

        CollectionIds(everything).Should().Contain(unowned.CollId);
        CollectionIds(ownedOnly).Should().NotContain(unowned.CollId);
    }

    [Fact]
    public async Task Version_and_private_information_appear_only_when_requested()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 65);

        var plain = await fake.GetXml("/xmlapi2/collection?username=example&own=1&stats=1");
        var detailed = await fake.GetXml("/xmlapi2/collection?username=example&own=1&stats=1&version=1&showprivate=1");

        plain.Descendants("version").Should().BeEmpty();
        plain.Descendants("privateinfo").Should().BeEmpty();
        detailed.Descendants("version").Should().NotBeEmpty();
        detailed.Descendants("privateinfo").Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(65)]
    [InlineData(400)]
    public async Task Every_offered_size_answers_with_that_many_entries(int size)
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, size);

        var document = await fake.GetXml(UnfilteredQuery);

        document.Root!.Elements("item").Should().HaveCount(size);
        TotalItems(document).Should().Be(size);
    }

    [Fact]
    public async Task Queued_scenario_answers_with_a_wait_twice_then_with_the_data()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse("queued=2"), 5);

        var first = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);
        var second = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);
        var third = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);

        new[] { first.StatusCode, second.StatusCode, third.StatusCode }
            .Should().Equal(HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Queued_scenario_counts_each_distinct_request_separately()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse("queued=1"), 5);

        var firstQuery = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);
        var secondQuery = await fake.Client.GetAsync(ExpansionsQuery, TestContext.Current.CancellationToken);

        firstQuery.StatusCode.Should().Be(HttpStatusCode.Accepted);
        secondQuery.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Throttle_scenario_answers_429_without_a_retry_after_header()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse("throttle"), 5);

        var response = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.Contains("Retry-After").Should().BeFalse();
    }

    [Fact]
    public async Task Broken_scenario_answers_a_web_page_with_a_success_status()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse("broken"), 5);

        var response = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    [Theory]
    [InlineData("unauthorized", HttpStatusCode.Unauthorized)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    public async Task Failure_scenarios_answer_their_status(string scenario, HttpStatusCode expected)
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse(scenario), 5);

        var response = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Malformed_scenario_answers_xml_that_cannot_be_read()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse("malformed"), 5);

        var body = await fake.Client.GetStringAsync(DefaultQuery, TestContext.Current.CancellationToken);

        var parse = () => XDocument.Parse(body);
        parse.Should().Throw<System.Xml.XmlException>();
    }

    [Fact]
    public async Task Errors_scenario_answers_an_error_document_with_a_success_status()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse("errors"), 5);

        var response = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.Root!.Name.LocalName.Should().Be("errors");
    }

    [Fact]
    public async Task Mismatch_scenario_declares_more_items_than_it_writes()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse("mismatch"), 65);

        var document = await fake.GetXml(DefaultQuery);

        TotalItems(document).Should().Be(document.Root!.Elements("item").Count() + 3);
    }

    [Fact]
    public async Task Empty_and_shrunk_scenarios_write_fewer_items_with_an_honest_total()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse("empty"), 65);
        var empty = await fake.GetXml(DefaultQuery);
        await fake.Switch("shrunk");
        var shrunk = await fake.GetXml(DefaultQuery);

        empty.Root!.Elements("item").Should().BeEmpty();
        TotalItems(empty).Should().Be(0);
        shrunk.Root!.Elements("item").Count().Should().BeInRange(1, 64);
        TotalItems(shrunk).Should().Be(shrunk.Root.Elements("item").Count());
    }

    [Fact]
    public async Task Slow_scenario_holds_back_the_answer()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Parse("slow=300"), 5);

        var started = DateTimeOffset.UtcNow;
        var response = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (DateTimeOffset.UtcNow - started).Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public async Task Scenario_can_be_switched_while_running()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 5);

        var before = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);
        var switched = await fake.Switch("throttle&size=65");
        var after = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        switched.StatusCode.Should().Be(HttpStatusCode.OK);
        after.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Unknown_scenario_is_refused_and_leaves_the_current_one_in_place()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 5);

        var refused = await fake.Switch("nonsense");
        var after = await fake.Client.GetAsync(DefaultQuery, TestContext.Current.CancellationToken);

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Thing_answers_one_item_per_requested_id_and_refuses_more_than_twenty()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 65);

        var document = await fake.GetXml("/xmlapi2/thing?id=100001,100002,999999&stats=1");
        var tooMany = await fake.Client.GetAsync($"/xmlapi2/thing?id={string.Join(',', Enumerable.Range(100001, 21))}", TestContext.Current.CancellationToken);

        document.Root!.Elements("item").Select(item => (string?)item.Attribute("id"))
            .Should().Equal("100001", "100002", "999999");
        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Fake_listens_on_the_loopback_interface_only()
    {
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 5);

        fake.BaseAddress.Host.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task Credentials_a_caller_sends_are_never_echoed()
    {
        const string sentinel = "sentinel-token-value";
        await using var fake = await RunningFake.Start(FakeBggScenario.Default, 65);

        using var request = new HttpRequestMessage(HttpMethod.Get, DefaultQuery);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {sentinel}");
        using var collection = await fake.Client.SendAsync(request, TestContext.Current.CancellationToken);
        using var thingRequest = new HttpRequestMessage(HttpMethod.Get, "/xmlapi2/thing?id=100001");
        thingRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {sentinel}");
        using var thing = await fake.Client.SendAsync(thingRequest, TestContext.Current.CancellationToken);

        foreach (var response in new[] { collection, thing })
        {
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var headers = response.Headers.ToString() + response.Content.Headers;
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotBeEmpty().And.NotContain(sentinel);
            headers.Should().NotContain(sentinel);
        }
    }

    private static IEnumerable<long> CollectionIds(XDocument document) =>
        document.Root!.Elements("item").Select(item => long.Parse((string)item.Attribute("collid")!));

    private static IEnumerable<string> Subtypes(XDocument document) =>
        document.Root!.Elements("item").Select(item => (string)item.Attribute("subtype")!);

    private static int TotalItems(XDocument document) => int.Parse((string)document.Root!.Attribute("totalitems")!);

    /// <summary>A fake started on a free loopback port chosen by the system, so no port is picked and then lost to a race.</summary>
    private sealed class RunningFake : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private RunningFake(WebApplication app)
        {
            _app = app;
            BaseAddress = new Uri(app.Urls.Single());
            Client = new HttpClient { BaseAddress = BaseAddress };
        }

        public Uri BaseAddress { get; }

        public HttpClient Client { get; }

        public static async Task<RunningFake> Start(FakeBggScenario scenario, int size)
        {
            var app = FakeBggServer.Create(scenario, size, 0);
            await app.StartAsync(TestContext.Current.CancellationToken);
            return new RunningFake(app);
        }

        public async Task<XDocument> GetXml(string pathAndQuery)
        {
            var response = await Client.GetAsync(pathAndQuery, TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return XDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        public Task<HttpResponseMessage> Switch(string nameAndMore) =>
            Client.PostAsync($"/fake/scenario?name={nameAndMore}", content: null, TestContext.Current.CancellationToken);

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync(TestContext.Current.CancellationToken);
            await _app.DisposeAsync();
        }
    }
}
