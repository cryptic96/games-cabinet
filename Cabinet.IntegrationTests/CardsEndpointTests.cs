using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the card endpoint serves one record per owned entry, revalidates by entity tag, refuses unknown profiles and sends no cross-origin headers.</summary>
public sealed class CardsEndpointTests
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task Cards_for_a_sample_are_json_with_a_content_derived_tag_and_the_layout_tag()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var cards = await client.GetAsync("/cabinet/cards?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var layout = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var cardsDocument = JsonDocument.Parse(await cards.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var layoutDocument = JsonDocument.Parse(await layout.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        cards.StatusCode.Should().Be(HttpStatusCode.OK);
        cards.ShouldHaveMediaType("application/json");
        cards.Headers.ETag!.Tag.Should().StartWith("\"cards-");
        cards.Headers.CacheControl!.NoCache.Should().BeTrue();
        cardsDocument.RootElement.GetProperty("layout").GetString().Should().Be(layout.Headers.ETag!.Tag);
        cardsDocument.RootElement.GetProperty("cards").GetArrayLength().Should().Be(EntryIdsOf(layoutDocument.RootElement).Count);
    }

    [Fact]
    public async Task A_repeat_request_with_the_tag_answers_not_modified()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var first = await client.GetAsync("/cabinet/cards?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/cards?sample=65&profile=desktop");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await client.SendAsync(request, TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        second.Headers.ETag.Should().Be(first.Headers.ETag);
    }

    [Fact]
    public async Task A_different_tag_gets_the_full_response()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/cards?sample=12&profile=phone");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"stale\""));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/cabinet/cards?sample=65&profile=tablet")]
    [InlineData("/cabinet/cards?sample=65")]
    [InlineData("/cabinet/cards?sample=65&profile=Desktop")]
    public async Task An_unknown_or_missing_profile_answers_not_found(string path)
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_response_carries_no_cross_origin_header()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = factory.CreatePublicClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/cabinet/cards?sample=5&profile=desktop");
        request.Headers.Add("Origin", "https://example.org");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private static HashSet<long> EntryIdsOf(JsonElement layout) =>
        layout.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray())
            .Where(placement => placement.GetProperty("kind").GetString() != "moreMarker")
            .Select(placement => placement.GetProperty("entryId").GetInt64())
            .ToHashSet();
}
