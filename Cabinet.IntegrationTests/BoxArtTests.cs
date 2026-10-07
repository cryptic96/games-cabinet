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
        var withArt = Placements(document).Where(HasArt).ToList();

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

    [Fact]
    public async Task A_picture_address_that_changes_is_downloaded_again_and_the_old_file_goes_only_after_the_grace_period()
    {
        using var storage = new TemporaryDirectory();
        var images = new ScriptedImageHandler()
            .ServePicture(FirstVersionImage, 600, 800)
            .ServePicture("https://example.org/images/version-changed.jpg", 600, 800, new SkiaSharp.SKColor(20, 60, 180));
        string? replacement = null;
        var bgg = new ScriptedBggHandler(request => ScriptedResponse.Xml(Swap(
            BggXml.Collection(SyntheticBggCollection.Create(5), CollectionQuery.Parse(request.RequestUri!.Query)),
            replacement)));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(bgg, clock, WithStorage(storage), images);
        using var client = factory.CreatePublicClient();
        var artDirectory = Path.Combine(storage.FullPath, "art");

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var first = ArtOf(await ReadLayoutBody(client), FirstEntryId).GetProperty("url").GetString()!;
        var firstFiles = StoredFiles(artDirectory);
        foreach (var file in Directory.EnumerateFiles(artDirectory))
        {
            File.SetLastWriteTimeUtc(file, clock.GetUtcNow().UtcDateTime);
        }

        replacement = "version-changed.jpg";
        await SyncRounds.PressAndWait(client, clock);
        var second = ArtOf(await ReadLayoutBody(client), FirstEntryId).GetProperty("url").GetString()!;

        second.Should().NotBe(first);
        images.Requests.Count(request => request.Uri.AbsoluteUri == FirstVersionImage).Should().Be(1);
        images.Requests.Count(request => request.Uri.AbsoluteUri.EndsWith("version-changed.jpg", StringComparison.Ordinal)).Should().Be(1);
        StoredFiles(artDirectory).Should().Contain(firstFiles, "the old files stay until the grace period is over");

        clock.Advance(TimeSpan.FromDays(8));
        await SyncRounds.PressAndWait(client, clock);

        StoredFiles(artDirectory).Should().NotContain(firstFiles).And.Contain(Path.GetFileName(second));
    }

    [Fact]
    public async Task A_missing_picture_is_not_requested_again_until_the_retry_time_has_passed()
    {
        var images = new ScriptedImageHandler();
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            AllowExampleHost,
            images);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var afterFirst = images.Requests.Count;
        await SyncRounds.PressAndWait(client, clock);
        var afterSecond = images.Requests.Count;
        clock.Advance(TimeSpan.FromHours(24));
        await SyncRounds.PressAndWait(client, clock);

        afterFirst.Should().BeGreaterThan(0);
        afterSecond.Should().Be(afterFirst, "a picture that failed is not asked for again inside the retry time");
        images.Requests.Count.Should().Be(2 * afterFirst, "after the retry time every failed picture is asked for once more");
    }

    [Fact]
    public async Task A_run_downloads_at_most_the_configured_number_and_the_next_run_takes_the_following_ones()
    {
        var images = new ScriptedImageHandler();
        foreach (var version in Enumerable.Range(900001, 65))
        {
            images.ServePicture($"https://example.org/images/version-{version}.jpg", 600, 800);
        }

        var clock = SyncHarness.NewClock();
        var settings = new Dictionary<string, string?>(AllowExampleHost) { ["Images:MaxDownloadsPerRun"] = "2" };
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(65)),
            clock,
            settings,
            images);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var firstRun = images.Requests.Select(request => request.Uri.AbsoluteUri).ToList();
        await SyncRounds.PressAndWait(client, clock);
        var secondRun = images.Requests.Select(request => request.Uri.AbsoluteUri).Skip(firstRun.Count).ToList();

        firstRun.Should().HaveCount(2).And.StartWith("https://example.org/images/version-900001.jpg");
        secondRun.Should().HaveCount(2).And.NotIntersectWith(firstRun);
        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("changed");
    }

    [Fact]
    public async Task A_run_that_passes_its_picture_deadline_starts_no_further_download_and_does_not_fail()
    {
        var clock = SyncHarness.NewClock();
        var images = new ScriptedImageHandler { OnRequest = _ => clock.Advance(TimeSpan.FromMinutes(7)) };
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(65)),
            clock,
            AllowExampleHost,
            images);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);

        images.Requests.Should().ContainSingle();
        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("changed");
        (await SyncRounds.ReadLayout(client)).Titles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Pictures_that_are_refused_or_unreadable_never_fail_the_sync_and_the_covers_stay_generated()
    {
        var images = new ScriptedImageHandler()
            .Serve(FirstVersionImage, [1, 2, 3], "text/html")
            .Serve("https://example.org/images/version-900002.jpg", [1, 2, 3, 4], "image/png");
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            AllowExampleHost,
            images);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var requests = images.Requests.Count;
        using var document = JsonDocument.Parse(await ReadLayoutBody(client));

        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("changed");
        Placements(document).Should().NotBeEmpty().And.OnlyContain(placement => !HasArt(placement));
        await SyncRounds.PressAndWait(client, clock);
        images.Requests.Count.Should().Be(requests);
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

    private static bool HasArt(JsonElement placement) => placement.TryGetProperty("art", out _);

    private static Dictionary<string, string?> WithStorage(TemporaryDirectory storage) =>
        new(AllowExampleHost) { ["Storage:Directory"] = storage.FullPath };

    private static string Swap(string xml, string? replacement) =>
        replacement is null ? xml : xml.Replace("version-900001.jpg", replacement, StringComparison.Ordinal);

    private static List<string> StoredFiles(string directory) =>
        [.. Directory.EnumerateFiles(directory).Select(path => Path.GetFileName(path))];

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
