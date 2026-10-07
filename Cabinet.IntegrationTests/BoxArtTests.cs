using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves a box picture travels from the collection answer, through a polite download and a resize, onto a face-out cover
/// that the browser loads from the site's own origin, and that picture trouble never touches the collection.
/// </summary>
[Trait("Category", "Sync")]
public sealed class BoxArtTests
{
    private const long FirstEntryId = SyntheticBggCollection.FirstCollId;
    private const string FirstVersionImage = "https://example.org/images/version-900001.jpg";
    private const string LayoutPath = "/cabinet/layout?profile=desktop";

    private static readonly Dictionary<string, string?> AllowExampleHost = new() { ["Images:AllowedHosts"] = "example.org" };

    [Fact]
    public async Task A_version_picture_in_the_collection_becomes_a_face_out_cover_served_from_the_sites_own_origin()
    {
        var images = new ScriptedImageHandler().ServePicture(FirstVersionImage, 600, 800);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            AllowExampleHost,
            images);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var layout = await ReadLayoutBody(client);
        var art = ArtOf(layout, FirstEntryId);

        art.GetProperty("url").GetString().Should().MatchRegex(@"^/art/[0-9a-f]{16}-[0-9]{1,4}\.webp$");
        art.GetProperty("width").GetInt32().Should().Be(240);
        art.GetProperty("height").GetInt32().Should().Be(320);
        art.GetProperty("fit").GetString().Should().BeOneOf("width", "height", "exact");
        layout.Should().NotContain("example.org").And.NotContain("geekdo").And.NotContain("boardgamegeek");
        images.Requests.Should().NotBeEmpty();
        images.Requests.Should().OnlyContain(request =>
            request.AuthorizationScheme == null
            && request.AuthorizationParameter == null
            && request.UserAgent!.StartsWith("GamesCabinet/", StringComparison.Ordinal));

        using var served = await client.GetAsync(art.GetProperty("url").GetString(), TestContext.Current.CancellationToken);

        served.StatusCode.Should().Be(HttpStatusCode.OK);
        served.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        served.Headers.CacheControl!.ToString().Should().Be("public, max-age=31536000, immutable");
        served.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        (await served.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Only_face_out_covers_carry_a_picture()
    {
        var images = new ScriptedImageHandler();

        foreach (var version in Enumerable.Range(900001, 65))
        {
            images.ServePicture($"https://example.org/images/version-{version}.jpg", 600, 800);
        }

        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(65)),
            clock,
            AllowExampleHost,
            images);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        using var document = JsonDocument.Parse(await ReadLayoutBody(client));
        var withArt = Placements(document).Where(placement => placement.TryGetProperty("art", out _)).ToList();

        withArt.Should().NotBeEmpty();
        withArt.Should().OnlyContain(placement => placement.GetProperty("kind").GetString() == "cover");
    }

    [Fact]
    public async Task A_second_sync_with_an_unchanged_answer_sends_no_picture_request_and_every_cover_keeps_its_art()
    {
        var images = new ScriptedImageHandler().ServePicture(FirstVersionImage, 600, 800);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            AllowExampleHost,
            images);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var before = await ReadLayoutBody(client);
        var requestsBefore = images.Requests.Count;

        await SyncRounds.PressAndWait(client, clock);
        var after = await ReadLayoutBody(client);

        images.Requests.Should().HaveCount(requestsBefore);
        ArtOf(after, FirstEntryId).GetProperty("url").GetString().Should().Be(ArtOf(before, FirstEntryId).GetProperty("url").GetString());
    }

    [Theory]
    [InlineData("/art/../snapshot.json")]
    [InlineData("/art/x.json")]
    [InlineData("/art/0123456789abcdef-240.webp")]
    [InlineData("/art/%2e%2e/snapshot.json")]
    public async Task A_name_that_is_not_a_stored_picture_answers_not_found(string target)
    {
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            AllowExampleHost,
            new ScriptedImageHandler().ServePicture(FirstVersionImage, 600, 800));
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);

        var status = await RawStatusLine(factory.PublicPort, target);

        status.Should().Contain(" 404 ");
    }

    private static async Task<string> RawStatusLine(int port, string target)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken);
        await using var stream = tcp.GetStream();
        var request = Encoding.ASCII.GetBytes($"GET {target} HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(request, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream, Encoding.ASCII);

        return await reader.ReadLineAsync(TestContext.Current.CancellationToken) ?? string.Empty;
    }

    private static async Task<string> ReadLayoutBody(HttpClient client) =>
        await client.GetStringAsync(LayoutPath, TestContext.Current.CancellationToken);

    private static IEnumerable<JsonElement> Placements(JsonDocument document) =>
        document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray());

    private static JsonElement ArtOf(string layout, long entryId)
    {
        using var document = JsonDocument.Parse(layout);
        var placement = Placements(document).Single(candidate => candidate.GetProperty("entryId").GetInt64() == entryId
            && candidate.GetProperty("kind").GetString() == "cover");

        placement.TryGetProperty("art", out var art).Should().BeTrue("the cover of the game with a picture carries it");

        return art.Clone();
    }
}
