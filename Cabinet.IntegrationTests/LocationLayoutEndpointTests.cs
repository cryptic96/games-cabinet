using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Cabinet.Domain.Layout;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the by-location layout endpoint serves one cabinet per storage location, revalidates like the layout and carries nothing the page does not draw.</summary>
public sealed class LocationLayoutEndpointTests
{
    private const string Route = "/cabinet/layout-by-location?profile=desktop";

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task The_fixture_answers_one_cabinet_per_location_with_Unknown_last()
    {
        using var storage = new TemporaryDirectory();
        FilterFixture.Write(storage.FullPath);
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var locations = document.RootElement.GetProperty("locations").EnumerateArray().ToList();
        var entriesByLocation = locations.Select(location => EntriesOf(location.GetProperty("sections"))).ToList();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.ShouldHaveMediaType("application/json");
        locations.Select(location => location.TryGetProperty("name", out var name) ? name.GetString() : null)
            .Should().Equal("Cabinet A", "Shelf B", null);
        locations.Select(location => location.TryGetProperty("id", out var id) ? id.GetString() : null)
            .Should().Equal(FilterFixture.CabinetAId, FilterFixture.ShelfBId, null);
        locations.Should().OnlyContain(location => location.GetProperty("sections").GetArrayLength() >= 1);
        entriesByLocation[0].Should().BeEquivalentTo(
            new[] { FilterFixture.HarbourEntry, FilterFixture.QuarryEntries[0], FilterFixture.QuarryEntries[2], FilterFixture.TallyEntry, FilterFixture.TallyExpansionEntry });
        entriesByLocation[1].Should().BeEquivalentTo(
            new[] { FilterFixture.HarbourExpansionEntry, FilterFixture.QuarryEntries[1], FilterFixture.KettleEntry });
        entriesByLocation[2].Should().BeEquivalentTo(
            new[] { FilterFixture.MeadowEntry, FilterFixture.MysteryEntry, FilterFixture.HalfKnownEntry });
        entriesByLocation.SelectMany(entries => entries).Should().OnlyHaveUniqueItems().And.HaveCount(11);
    }

    [Fact]
    public async Task An_expansion_stored_apart_is_drawn_in_its_own_location_with_its_base_title()
    {
        using var storage = new TemporaryDirectory();
        FilterFixture.Write(storage.FullPath);
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync(Route, TestContext.Current.CancellationToken));
        var locations = document.RootElement.GetProperty("locations").EnumerateArray().ToList();
        var apart = PlacementsOf(locations[1].GetProperty("sections"))
            .Single(placement => placement.GetProperty("entryId").GetInt64() == FilterFixture.HarbourExpansionEntry);
        var following = PlacementsOf(locations[0].GetProperty("sections"))
            .Single(placement => placement.GetProperty("entryId").GetInt64() == FilterFixture.TallyExpansionEntry);

        apart.GetProperty("isExpansion").GetBoolean().Should().BeTrue();
        apart.GetProperty("baseTitle").GetString().Should().Be("Lantern & Harbour");
        following.GetProperty("kind").GetString().Should().BeOneOf("expansionLayer", "expansionSpine");
    }

    [Fact]
    public async Task The_payload_is_revalidated_like_the_layout()
    {
        using var storage = new TemporaryDirectory();
        FilterFixture.Write(storage.FullPath);
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var layout = await client.GetAsync("/cabinet/layout?profile=desktop", TestContext.Current.CancellationToken);
        using var first = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var stale = new HttpRequestMessage(HttpMethod.Get, Route);
        stale.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"stale\""));
        using var full = await client.SendAsync(stale, TestContext.Current.CancellationToken);

        document.RootElement.GetProperty("layout").GetString().Should().Be(layout.Headers.ETag!.Tag);
        document.RootElement.GetProperty("layoutVersion").GetInt32().Should().Be(CabinetLayoutEngine.LayoutVersion);
        first.Headers.ETag!.Tag.Should().EndWith("-by-location\"").And.StartWith(layout.Headers.ETag.Tag.TrimEnd('"'));
        first.Headers.CacheControl!.NoCache.Should().BeTrue();
        first.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        second.Headers.ETag.Should().Be(first.Headers.ETag);
        full.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_sample_is_answered_with_its_invented_locations_in_order()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var layout = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var response = await client.GetAsync("/cabinet/layout-by-location?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var names = document.RootElement.GetProperty("locations").EnumerateArray()
            .Select(location => location.TryGetProperty("name", out var name) ? name.GetString() : null)
            .ToList();
        var order = new[] { "Cabinet A", "Cabinet B", "Shelf A", "Shelf B", "Shelf C", "Lent out" };

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("layout").GetString().Should().Be(layout.Headers.ETag!.Tag);
        response.Headers.ETag!.Tag.Should().MatchRegex($"^\"{CabinetLayoutEngine.LayoutVersion}-[0-9a-f]{{16}}-65-desktop-by-location\"$");
        names[^1].Should().BeNull("the games without a location come last");
        names[..^1].Should().OnlyContain(name => name != null);
        names[..^1].Select(name => Array.IndexOf(order, name)).Should().BeInAscendingOrder().And.NotContain(-1);
        names.Count.Should().BeGreaterThan(2);
    }

    [Theory]
    [InlineData("profile=nope")]
    [InlineData("profile=Desktop")]
    [InlineData("sample=65")]
    [InlineData("")]
    public async Task A_bad_or_missing_profile_is_not_found(string query)
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync($"/cabinet/layout-by-location?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Before_the_first_sync_the_answer_has_an_empty_location_list()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("locations").GetArrayLength().Should().Be(0);
        response.Headers.ETag!.Tag.Should().MatchRegex($"^\"{CabinetLayoutEngine.LayoutVersion}-[0-9a-f]{{16}}-collection-empty-desktop-by-location\"$");
    }

    [Fact]
    public async Task An_owner_without_locations_gets_no_location_cabinets()
    {
        using var storage = new TemporaryDirectory();
        FilterFixture.WriteCollection(storage.FullPath);
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync(Route, TestContext.Current.CancellationToken));

        document.RootElement.GetProperty("locations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task The_payload_carries_only_what_the_page_draws()
    {
        using var storage = new TemporaryDirectory();
        FilterFixture.Write(storage.FullPath);
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync(Route, TestContext.Current.CancellationToken));
        using var layout = JsonDocument.Parse(await client.GetStringAsync("/cabinet/layout?profile=desktop", TestContext.Current.CancellationToken));
        var locations = document.RootElement.GetProperty("locations").EnumerateArray().ToList();
        var sections = locations.SelectMany(location => location.GetProperty("sections").EnumerateArray()).ToList();
        var placements = locations.SelectMany(location => PlacementsOf(location.GetProperty("sections"))).ToList();
        var layoutSections = layout.RootElement.GetProperty("sections").EnumerateArray().ToList();
        var layoutPlacements = PlacementsOf(layout.RootElement.GetProperty("sections")).ToList();

        Names(document.RootElement).Should().BeEquivalentTo("layout", "layoutVersion", "palette", "locations");
        locations.SelectMany(Names).Distinct().Should().BeSubsetOf(["id", "name", "sections"]);
        sections.SelectMany(Names).Distinct().Should().BeSubsetOf(layoutSections.SelectMany(Names).Distinct());
        placements.SelectMany(Names).Distinct().Should().BeSubsetOf(layoutPlacements.SelectMany(Names).Distinct());
        document.RootElement.GetProperty("palette").GetRawText().Should().Be(layout.RootElement.GetProperty("palette").GetRawText());
    }

    [Fact]
    public async Task Asking_twice_gives_the_same_bytes()
    {
        using var storage = new TemporaryDirectory();
        FilterFixture.Write(storage.FullPath);
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        using var first = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        using var again = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        (await again.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Be(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        again.Headers.ETag.Should().Be(first.Headers.ETag);
    }

    private static IEnumerable<string> Names(JsonElement element) => element.EnumerateObject().Select(member => member.Name);

    private static IEnumerable<JsonElement> PlacementsOf(JsonElement sections) =>
        sections.EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray());

    private static List<long> EntriesOf(JsonElement sections) =>
        [.. PlacementsOf(sections).Where(placement => placement.GetProperty("kind").GetString() != "moreMarker").Select(placement => placement.GetProperty("entryId").GetInt64())];
}
