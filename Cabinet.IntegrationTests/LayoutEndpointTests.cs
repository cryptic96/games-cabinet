using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Cabinet.Domain.Layout;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the layout endpoint serves the synced collection, honours allowlisted samples only when they are switched on, and refuses unknown profiles.</summary>
public class LayoutEndpointTests
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task Layout_for_the_default_sample_is_json_with_sections_cubbies_and_placements()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var sections = root.GetProperty("sections");
        var placements = sections.EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray())
            .ToList();
        var placementCount = placements.Sum(placement =>
            placement.GetProperty("kind").GetString() == "moreMarker" ? placement.GetProperty("moreCount").GetInt32() : 1);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.ShouldHaveMediaType("application/json");
        root.GetProperty("layoutVersion").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        root.GetProperty("profile").GetString().Should().Be("desktop");
        sections.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        sections[0].GetProperty("cubbies").GetArrayLength().Should().Be(SectionDesigns.Desktop.Cubbies.Count);
        placementCount.Should().Be(65, "every item is a placement, a layer or part of a marker's count");
        placements.Select(placement => placement.GetProperty("kind").GetString())
            .Should().Contain(["expansionLayer", "expansionSpine", "moreMarker", "orphanExpansion"]);
        placements.Where(placement => placement.GetProperty("kind").GetString() == "expansionSpine")
            .Select(placement => (placement.TryGetProperty("familyId", out var familyId), placement.TryGetProperty("baseTitle", out var baseTitle)))
            .Should().OnlyContain(found => found.Item1 && found.Item2);
        placements.Where(placement => placement.GetProperty("kind").GetString() == "orphanExpansion")
            .Select(placement => placement.TryGetProperty("baseTitle", out var baseTitle) ? baseTitle.GetString() : null)
            .Should().OnlyContain(baseTitle => !string.IsNullOrEmpty(baseTitle));
    }

    [Fact]
    public async Task Layout_for_the_phone_profile_is_the_narrow_design_with_at_least_as_many_sections_as_the_desktop()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/cabinet/layout?sample=65&profile=phone", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var desktopBody = await client.GetStringAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        using var desktopDocument = JsonDocument.Parse(desktopBody);
        var sections = document.RootElement.GetProperty("sections");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("profile").GetString().Should().Be("phone");
        sections[0].GetProperty("cubbies").GetArrayLength().Should().Be(SectionDesigns.Phone.Cubbies.Count);
        sections[0].GetProperty("widthMm").GetInt32().Should().Be(SectionDesigns.Phone.InteriorWidthMm);
        sections.GetArrayLength().Should().BeGreaterThanOrEqualTo(desktopDocument.RootElement.GetProperty("sections").GetArrayLength());
    }

    [Fact]
    public async Task Layout_for_the_empty_sample_is_one_section_of_bare_cubbies()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        var body = await client.GetStringAsync("/cabinet/layout?sample=0&profile=desktop", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var sections = document.RootElement.GetProperty("sections");
        var cubbies = sections[0].GetProperty("cubbies");

        sections.GetArrayLength().Should().Be(1);
        cubbies.GetArrayLength().Should().Be(
            SectionDesigns.Desktop.Rows.Take(CabinetLayoutEngine.MinTrimmedRows).Sum(row => row.CubbyWidthsMm.Count));
        cubbies.EnumerateArray().Should().OnlyContain(cubby => cubby.GetProperty("placements").GetArrayLength() == 0);
    }

    [Fact]
    public async Task Layout_without_a_sample_is_the_empty_collections_one_section_of_bare_cubbies()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/cabinet/layout?profile=desktop", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var sections = document.RootElement.GetProperty("sections");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sections.GetArrayLength().Should().Be(1);
        sections[0].GetProperty("cubbies").EnumerateArray()
            .Should().OnlyContain(cubby => cubby.GetProperty("placements").GetArrayLength() == 0);
        response.Headers.ETag!.Tag.Should().MatchRegex($"^\"{CabinetLayoutEngine.LayoutVersion}-[0-9a-f]{{16}}-collection-empty-desktop\"$");
    }

    [Theory]
    [InlineData("profile=tablet")]
    [InlineData("profile=Desktop")]
    [InlineData("profile=")]
    [InlineData("sample=65")]
    [InlineData("sample=65&profile=tablet")]
    [InlineData("")]
    public async Task Layout_answers_not_found_for_a_bad_or_missing_profile(string query)
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync($"/cabinet/layout?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("sample=64&profile=desktop")]
    [InlineData("sample=-1&profile=desktop")]
    [InlineData("sample=abc&profile=desktop")]
    [InlineData("sample=&profile=desktop")]
    public async Task An_unknown_sample_shows_the_synced_collection_whether_the_prototype_is_on_or_off(string query)
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        await using var production = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();
        using var otherClient = production.CreatePublicClient();

        using var response = await client.GetAsync($"/cabinet/layout?{query}", TestContext.Current.CancellationToken);
        using var other = await otherClient.GetAsync($"/cabinet/layout?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.Should().EndWith("-collection-empty-desktop\"");
        other.Headers.ETag.Should().Be(response.Headers.ETag);
    }

    [Fact]
    public async Task Layout_response_carries_a_quoted_etag_built_from_version_settings_sample_and_profile()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        response.Headers.ETag!.Tag.Should().MatchRegex($"^\"{CabinetLayoutEngine.LayoutVersion}-[0-9a-f]{{16}}-65-desktop\"$");
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Layout_request_with_a_matching_if_none_match_answers_not_modified_with_an_empty_body()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var first = await client.GetAsync("/cabinet/layout?sample=12&profile=desktop", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/layout?sample=12&profile=desktop");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        body.Should().BeEmpty();
        second.Headers.ETag.Should().Be(first.Headers.ETag);
        second.Headers.CacheControl!.NoCache.Should().BeTrue();
    }

    [Fact]
    public async Task Layout_request_with_a_different_if_none_match_gets_the_full_response()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/layout?sample=12&profile=desktop");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"stale\""));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Repeated_requests_return_identical_bodies_and_etags_while_other_samples_differ()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var first = await client.GetAsync("/cabinet/layout?sample=5&profile=desktop", TestContext.Current.CancellationToken);
        using var again = await client.GetAsync("/cabinet/layout?sample=5&profile=desktop", TestContext.Current.CancellationToken);
        using var other = await client.GetAsync("/cabinet/layout?sample=12&profile=desktop", TestContext.Current.CancellationToken);

        var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var againBody = await again.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        againBody.Should().Be(firstBody);
        again.Headers.ETag.Should().Be(first.Headers.ETag);
        other.Headers.ETag.Should().NotBe(first.Headers.ETag);
    }
}
