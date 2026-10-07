using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.FakeBgg;

/// <summary>Verifies the synthetic BoardGameGeek answers and the scripted transport that tests build on.</summary>
[Trait("Category", "FakeBgg")]
public class BggXmlTests
{
    private static readonly CollectionQuery OwnedBaseGames = new(true, null, "boardgameexpansion", true, false);

    [Fact]
    public void Collection_writes_names_as_element_text_and_marks_owned_entries()
    {
        var items = SyntheticBggCollection.Create(5);

        var document = XDocument.Parse(BggXml.Collection(items, new CollectionQuery(false, null, null, true, false)));

        var written = document.Root!.Elements("item").ToList();
        written.Select(item => item.Element("name")!.Value).Should().Equal(items.Select(item => item.Title));
        written.Select(item => (string?)item.Element("status")!.Attribute("own"))
            .Should().Equal(items.Select(item => item.Owned ? "1" : "0"));
        written.Select(item => item.Element("name")!.Attribute("value")).Should().OnlyContain(attribute => attribute == null);
    }

    [Fact]
    public void Collection_writes_version_dimensions_as_value_attributes_in_inches()
    {
        var first = SyntheticBggCollection.Create(1).Single();

        var document = XDocument.Parse(BggXml.Collection([first], OwnedBaseGames));

        var version = document.Descendants("item").Single(item => (string?)item.Attribute("type") == "boardgameversion");
        ((string?)version.Element("width")!.Attribute("value")).Should().Be("6.3");
        ((string?)version.Element("length")!.Attribute("value")).Should().Be("8.27");
        ((string?)version.Element("depth")!.Attribute("value")).Should().Be("2.09");
        version.Element("width")!.Value.Should().BeEmpty();
    }

    [Fact]
    public void Collection_leaves_the_version_out_when_it_was_not_asked_for()
    {
        var items = SyntheticBggCollection.Create(5);

        var document = XDocument.Parse(BggXml.Collection(items, new CollectionQuery(true, null, null, false, false)));

        document.Descendants("version").Should().BeEmpty();
    }

    [Fact]
    public void Collection_declares_a_total_equal_to_the_number_of_items_written()
    {
        var items = SyntheticBggCollection.Create(65);

        var document = XDocument.Parse(BggXml.Collection(items, OwnedBaseGames));

        var total = int.Parse((string)document.Root!.Attribute("totalitems")!);
        total.Should().Be(document.Root!.Elements("item").Count()).And.BeGreaterThan(0);
    }

    [Fact]
    public void Collection_can_declare_a_total_that_differs_from_the_items_written()
    {
        var items = SyntheticBggCollection.Create(5);

        var document = XDocument.Parse(BggXml.Collection(items, OwnedBaseGames, totalItemsOverride: 99));

        ((string?)document.Root!.Attribute("totalitems")).Should().Be("99");
    }

    [Fact]
    public void Title_with_an_ampersand_round_trips_through_the_writer_and_a_standard_reader()
    {
        var item = new FakeBggItem(100001, 5000001, "Lantern & Harbour <Deluxe>", false, true, 2001, null, null);

        var xml = BggXml.Collection([item], OwnedBaseGames);
        using var reader = System.Xml.XmlReader.Create(new StringReader(xml));
        var document = XDocument.Load(reader);

        xml.Should().Contain("Lantern &amp; Harbour &lt;Deluxe&gt;");
        document.Descendants("name").Single().Value.Should().Be("Lantern & Harbour <Deluxe>");
    }

    [Fact]
    public void Query_is_read_from_a_query_string_without_regard_to_name_case()
    {
        var query = CollectionQuery.Parse("?Username=example&OWN=1&subtype=boardgameexpansion&version=1&showprivate=1&stats=1");

        query.Should().Be(new CollectionQuery(true, "boardgameexpansion", null, true, true, true));
    }

    [Fact]
    public void Query_without_parameters_asks_for_nothing_in_particular()
    {
        var query = CollectionQuery.Parse(string.Empty);

        query.Should().Be(new CollectionQuery(false, null, null, false, false, false));
    }

    [Fact]
    public void Expansions_are_labelled_as_base_games_unless_asked_for_by_subtype()
    {
        var items = SyntheticBggCollection.Create(65);

        var unfiltered = XDocument.Parse(BggXml.Collection(items, new CollectionQuery(true, null, null, false, false)));
        var expansions = XDocument.Parse(BggXml.Collection(items, new CollectionQuery(true, "boardgameexpansion", null, false, false)));

        unfiltered.Root!.Elements("item").Select(item => (string?)item.Attribute("subtype")).Should().OnlyContain(subtype => subtype == "boardgame");
        expansions.Root!.Elements("item").Select(item => (string?)item.Attribute("subtype")).Should().OnlyContain(subtype => subtype == "boardgameexpansion");
    }

    [Fact]
    public void Things_carry_two_designers_two_mechanics_play_times_and_ratings_for_every_game()
    {
        var items = SyntheticBggCollection.Create(65);

        var document = XDocument.Parse(BggXml.Things(items.Where(item => item.Owned).Select(item => item.ObjectId).Distinct(), items, stats: true));

        foreach (var thing in document.Root!.Elements("item"))
        {
            LinksOf(thing, "boardgamedesigner").Select(link => (string?)link.Attribute("value")).Should().OnlyHaveUniqueItems().And.HaveCount(2);
            LinksOf(thing, "boardgamemechanic").Select(link => (string?)link.Attribute("value")).Should().OnlyHaveUniqueItems().And.HaveCount(2);
            thing.Element("minplaytime").Should().NotBeNull();
            thing.Element("maxplaytime").Should().NotBeNull();
            thing.Element("statistics")!.Element("ratings")!.Element("bayesaverage").Should().NotBeNull();
        }
    }

    [Fact]
    public void The_ranked_rating_is_zero_for_every_seventh_game_id_and_above_zero_for_the_rest()
    {
        var items = SyntheticBggCollection.Create(65);
        var ids = items.Select(item => item.ObjectId).Distinct().ToList();

        var document = XDocument.Parse(BggXml.Things(ids, items, stats: true));

        foreach (var thing in document.Root!.Elements("item"))
        {
            var id = int.Parse((string)thing.Attribute("id")!);
            var bayes = double.Parse((string)thing.Descendants("bayesaverage").Single().Attribute("value")!, System.Globalization.CultureInfo.InvariantCulture);

            (id % 7 == 0 ? bayes == 0 : bayes > 0).Should().BeTrue($"game {id}");
        }

        ids.Should().Contain(id => id % 7 == 0);
    }

    [Fact]
    public void An_expansion_lists_the_games_it_expands_as_inbound_links_and_a_base_game_lists_its_expansions_as_outbound_links()
    {
        var items = SyntheticBggCollection.Create(65);
        var firstBase = SyntheticBggCollection.FirstObjectId;
        var lanternExtras = firstBase + 3;

        var document = XDocument.Parse(BggXml.Things([firstBase, lanternExtras], items, stats: false));

        var baseGame = document.Root!.Elements("item").Single(thing => (string?)thing.Attribute("id") == firstBase.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var expansion = document.Root!.Elements("item").Single(thing => (string?)thing.Attribute("id") == lanternExtras.ToString(System.Globalization.CultureInfo.InvariantCulture));
        LinksOf(baseGame, "boardgameexpansion").Should().NotBeEmpty().And.OnlyContain(link => link.Attribute("inbound") == null);
        LinksOf(baseGame, "boardgameexpansion").Select(link => (string?)link.Attribute("id"))
            .Should().Contain(lanternExtras.ToString(System.Globalization.CultureInfo.InvariantCulture));
        LinksOf(expansion, "boardgameexpansion").Should().HaveCount(2).And.OnlyContain(link => (string?)link.Attribute("inbound") == "true");
    }

    [Fact]
    public void One_game_carries_an_inbound_link_that_is_not_an_expansion_link()
    {
        var items = SyntheticBggCollection.Create(65);

        var document = XDocument.Parse(BggXml.Things(items.Where(item => item.Owned).Select(item => item.ObjectId).Distinct(), items, stats: false));

        document.Descendants("link").Where(link => (string?)link.Attribute("type") == "boardgamecompilation").Should().ContainSingle()
            .Which.Attribute("inbound")!.Value.Should().Be("true");
    }

    [Fact]
    public void The_edge_cases_name_a_second_unowned_base_game_and_a_second_owned_one_without_changing_the_collection_shape()
    {
        var items = SyntheticBggCollection.Create(65);

        items.Should().HaveCount(65);
        items.Select(item => item.CollId).Should().BeInAscendingOrder();
        var missing = items.Single(item => item.Title == "Distant Orchard: Wind Pack");
        var lantern = items.Single(item => item.Title == "Example Game 1: Lantern Extras");
        missing.AlsoExpands.Should().ContainSingle().Which.Should().NotBe(missing.BaseObjectId);
        items.Select(item => item.ObjectId).Should().NotContain(missing.AlsoExpands!.Concat([missing.BaseObjectId!.Value]));
        lantern.AlsoExpands.Should().ContainSingle();
        items.Where(item => !item.IsExpansion && item.Owned).Select(item => item.ObjectId).Should().Contain(lantern.AlsoExpands!.Single());
    }

    private static IEnumerable<XElement> LinksOf(XElement thing, string type) =>
        thing.Elements("link").Where(link => (string?)link.Attribute("type") == type);

    [Fact]
    public void Synthetic_collections_have_the_offered_sizes_and_stay_the_same_between_calls()
    {
        foreach (var size in SyntheticBggCollection.Sizes)
        {
            var first = SyntheticBggCollection.Create(size);
            var second = SyntheticBggCollection.Create(size);

            first.Should().HaveCount(size);
            first.Should().Equal(second);
        }
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 5)]
    [InlineData(40, 65)]
    [InlineData(10000, 400)]
    public void Other_sizes_are_clamped_to_the_nearest_offered_size(int requested, int expected)
    {
        SyntheticBggCollection.Create(requested).Should().HaveCount(expected);
    }

    [Fact]
    public void Collections_of_five_or_more_contain_the_edge_cases_a_sync_has_to_cope_with()
    {
        var items = SyntheticBggCollection.Create(65);

        items.GroupBy(item => item.ObjectId).Should().Contain(group => group.Count() > 1);
        items.Should().Contain(item => !item.Owned);
        items.Should().Contain(item => item.IsExpansion && items.Any(other => other.ObjectId == item.BaseObjectId));
        items.Should().Contain(item => item.IsExpansion && items.All(other => other.ObjectId != item.BaseObjectId));
        items.Should().Contain(item => item.Title.Contains('&'));
        items.Should().Contain(item => item.Title.Length == 0);
        items.Any(item => item.Version is { Width: 0, Length: 0, Depth: 0 }).Should().BeTrue();
        items.Any(item => item.Version is null).Should().BeTrue();
        items.Count(item => item.Location is not null).Should().Be(2);
    }

    [Fact]
    public async Task Handler_returns_queued_answers_in_order()
    {
        var handler = new ScriptedBggHandler().Enqueue(
            ScriptedResponse.Xml(BggXml.Queued(), HttpStatusCode.Accepted),
            ScriptedResponse.Empty(HttpStatusCode.TooManyRequests),
            ScriptedResponse.Xml("<items totalitems=\"0\" />"));
        using var client = new HttpClient(handler);

        using var first = await client.GetAsync("https://bgg.example.org/xmlapi2/collection", TestContext.Current.CancellationToken);
        using var second = await client.GetAsync("https://bgg.example.org/xmlapi2/collection", TestContext.Current.CancellationToken);
        using var third = await client.GetAsync("https://bgg.example.org/xmlapi2/collection", TestContext.Current.CancellationToken);

        new[] { first.StatusCode, second.StatusCode, third.StatusCode }
            .Should().Equal(HttpStatusCode.Accepted, HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
        (await third.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("<items totalitems=\"0\" />");
        third.Content.Headers.ContentType!.MediaType.Should().Be("text/xml");
    }

    [Fact]
    public async Task Handler_fails_with_a_clear_message_when_no_answer_is_left()
    {
        using var client = new HttpClient(new ScriptedBggHandler());

        var act = () => client.GetAsync("https://bgg.example.org/xmlapi2/collection", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*No scripted BGG answer is left*");
    }

    [Fact]
    public async Task Handler_records_the_address_credentials_and_user_agent_of_every_request()
    {
        var handler = new ScriptedBggHandler().Enqueue(ScriptedResponse.Xml("<items />"), ScriptedResponse.Xml("<items />"));
        using var client = new HttpClient(handler);
        using var withCredentials = new HttpRequestMessage(HttpMethod.Get, "https://bgg.example.org/xmlapi2/collection?own=1");
        withCredentials.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "sentinel-token-value");
        withCredentials.Headers.UserAgent.ParseAdd("ExampleCabinet/1.0");
        using var bare = new HttpRequestMessage(HttpMethod.Get, "https://images.example.org/art.jpg");

        (await client.SendAsync(withCredentials, TestContext.Current.CancellationToken)).Dispose();
        (await client.SendAsync(bare, TestContext.Current.CancellationToken)).Dispose();

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Uri.Should().Be(new Uri("https://bgg.example.org/xmlapi2/collection?own=1"));
        handler.Requests[0].AuthorizationScheme.Should().Be("Bearer");
        handler.Requests[0].AuthorizationParameter.Should().Be("sentinel-token-value");
        handler.Requests[0].UserAgent.Should().Be("ExampleCabinet/1.0");
        handler.Requests[1].Uri.Host.Should().Be("images.example.org");
        handler.Requests[1].AuthorizationScheme.Should().BeNull();
        handler.Requests[1].AuthorizationParameter.Should().BeNull();
        handler.Requests[1].UserAgent.Should().BeNull();
    }

    [Fact]
    public async Task Handler_holds_an_answer_back_until_its_delay_has_passed()
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(start);
        var clock = new TimerCountingClock(fake);
        var handler = new ScriptedBggHandler(clock).Enqueue(
            new ScriptedResponse(HttpStatusCode.OK, "text/xml", "<items />", TimeSpan.FromSeconds(5)));
        using var client = new HttpClient(handler);

        var pending = client.GetAsync("https://bgg.example.org/xmlapi2/collection", TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromSeconds(4));
        var earlyFinish = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken));
        earlyFinish.Should().NotBeSameAs(pending, "the answer is held back for five seconds");
        fake.Advance(TimeSpan.FromSeconds(1));
        using var response = await pending.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.Requests.Single().At.Should().Be(start);
    }

    [Fact]
    public async Task Handler_sends_scripted_headers_and_an_odd_charset_unchanged()
    {
        var headers = new Dictionary<string, string> { ["Retry-After"] = "7" };
        var handler = new ScriptedBggHandler().Enqueue(
            new ScriptedResponse(HttpStatusCode.TooManyRequests, "text/xml; charset=_UTF-8_", "<items />", Headers: headers));
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync("https://bgg.example.org/xmlapi2/collection", TestContext.Current.CancellationToken);

        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(7));
        response.Content.Headers.ContentType!.CharSet.Should().Be("_UTF-8_");
    }

    [Fact]
    public async Task For_collection_answers_the_base_and_expansion_queries_like_the_fake()
    {
        var items = SyntheticBggCollection.Create(65);
        var handler = ScriptedBggHandler.ForCollection(items);
        using var client = new HttpClient(handler);

        var baseGames = XDocument.Parse(await client.GetStringAsync(
            "https://bgg.example.org/xmlapi2/collection?username=example&own=1&excludesubtype=boardgameexpansion&stats=1&version=1",
            TestContext.Current.CancellationToken));
        var expansions = XDocument.Parse(await client.GetStringAsync(
            "https://bgg.example.org/xmlapi2/collection?username=example&own=1&subtype=boardgameexpansion&stats=1&version=1",
            TestContext.Current.CancellationToken));

        baseGames.Root!.Elements("item").Should().HaveCount(items.Count(item => item.Owned && !item.IsExpansion));
        expansions.Root!.Elements("item").Should().HaveCount(items.Count(item => item.Owned && item.IsExpansion));
        expansions.Root!.Elements("item").Select(item => (string?)item.Attribute("subtype")).Should().OnlyContain(subtype => subtype == "boardgameexpansion");
    }

    [Fact]
    public async Task For_collection_answers_single_game_requests_for_each_id()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5));
        using var client = new HttpClient(handler);

        var document = XDocument.Parse(await client.GetStringAsync(
            "https://bgg.example.org/xmlapi2/thing?id=100001,100002&stats=1",
            TestContext.Current.CancellationToken));

        document.Root!.Elements("item").Select(item => (string?)item.Attribute("id")).Should().Equal("100001", "100002");
    }

    [Fact]
    public void Scenario_text_is_parsed_with_its_number()
    {
        FakeBggScenario.Parse("queued=3").Should().Be(new FakeBggScenario("queued", QueuedCount: 3));
        FakeBggScenario.Parse("slow=1500").Should().Be(new FakeBggScenario("slow", SlowMilliseconds: 1500));
        FakeBggScenario.Parse("throttle").Should().Be(new FakeBggScenario("throttle"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("queued")]
    [InlineData("queued=abc")]
    [InlineData("throttle=2")]
    [InlineData("nonsense")]
    public void Scenario_text_that_is_not_a_scenario_is_refused(string text)
    {
        FakeBggScenario.TryParse(text, out _).Should().BeFalse();
    }
}
