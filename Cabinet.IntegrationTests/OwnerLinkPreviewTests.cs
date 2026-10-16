using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the happy path of using a picture from a link: a pasted picture address is fetched without credentials and
/// processed, the processed preview is served from the site's own origin under a random token on the owner port only with
/// headers that forbid caching and sniffing, nothing is stored until the owner uses it, using it stores the files and the
/// link and every visitor's card shows that picture, and a preview that was never staged, is malformed or has run out is not found.
/// </summary>
[Trait("Category", "Owner")]
public sealed partial class OwnerLinkPreviewTests
{
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string CheckPath = "/owner/pictures?handler=CheckLink";
    private const string UsePath = "/owner/pictures?handler=UseLink";
    private const string LinkUrl = "https://example.org/owner/linked-picture.png";
    private const string SecondLinkUrl = "https://example.org/owner/second-picture.png";
    private const string ZeroToken = "00000000000000000000000000000000";

    private static readonly byte[] LinkedCover = SyntheticArt.Encode(SyntheticArtKind.CoverColouredField);
    private static readonly byte[] SecondCover = SyntheticArt.Encode(SyntheticArtKind.FlatCover);

    [Fact]
    public async Task Checking_a_link_answers_a_random_token_and_the_preview_of_the_box_and_the_spine_and_stores_nothing()
    {
        await using var host = await PreviewHost.StartAsync();
        var revision = host.Service.Revision;
        var filesBefore = host.ArtFiles();

        var reply = await host.CheckAsync(LinkUrl);

        reply.Status.Should().Be(HttpStatusCode.OK);
        reply.JsonText("outcome").Should().Be("ready");
        TokenPattern().IsMatch(reply.JsonText("token")).Should().BeTrue();
        reply.JsonText("previewUrl").Should().Be($"/owner/preview/{reply.JsonText("token")}");
        var preview = reply.Json.GetProperty("preview");
        preview.GetProperty("boxWidthMm").GetInt32().Should().BeGreaterThan(0);
        preview.GetProperty("boxHeightMm").GetInt32().Should().BeGreaterThan(0);
        preview.GetProperty("spineWidthMm").GetInt32().Should().BeGreaterThan(0);
        preview.GetProperty("spineHeightMm").GetInt32().Should().Be(preview.GetProperty("boxHeightMm").GetInt32());
        preview.GetProperty("shapeSource").GetString().Should().BeOneOf("bgg", "picture", "estimate");
        preview.GetProperty("verdict").GetString().Should().NotBeNullOrEmpty();
        preview.GetProperty("spineBackground").GetString().Should().MatchRegex("^#[0-9a-f]{6}$");
        preview.GetProperty("spineText").GetString().Should().BeOneOf("#ffffff", "#000000");
        preview.GetProperty("title").GetString().Should().Be(host.Title);
        host.Service.Revision.Should().Be(revision);
        host.ArtFiles().Should().Equal(filesBefore);
        host.Images.Requests.Where(request => request.Uri.AbsoluteUri == LinkUrl).Should().ContainSingle()
            .Which.Should().Match<RecordedImageRequest>(request => request.AuthorizationScheme == null && !request.HasCookie);
    }

    [Fact]
    public async Task The_spine_in_the_preview_carries_the_label_and_the_size_the_layout_engine_gives()
    {
        await using var host = await PreviewHost.StartAsync();

        var preview = (await host.CheckAsync(LinkUrl)).Json.GetProperty("preview");

        var height = preview.GetProperty("spineHeightMm").GetInt32();
        preview.GetProperty("spineLabel").GetString().Should().Be(SpineLabel.Shorten(host.Title, height / SectionDesigns.Desktop.LabelCharPitchMm));
        preview.GetProperty("spineWidthMm").GetInt32().Should().BeGreaterThanOrEqualTo(SectionDesigns.Desktop.MinBoxThicknessMm);
    }

    [Fact]
    public async Task The_preview_is_served_from_the_owner_port_as_webp_that_forbids_caching_and_sniffing()
    {
        await using var host = await PreviewHost.StartAsync();
        var token = (await host.CheckAsync(LinkUrl)).JsonText("token");

        using var owner = host.Factory.CreateOwnerClient();
        using var response = await owner.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        bytes.Should().HaveCountGreaterThan(12);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("RIFF");
        System.Text.Encoding.ASCII.GetString(bytes, 8, 4).Should().Be("WEBP");
    }

    [Fact]
    public async Task The_preview_does_not_exist_on_the_public_port()
    {
        await using var host = await PreviewHost.StartAsync();
        var token = (await host.CheckAsync(LinkUrl)).JsonText("token");

        using var response = await host.PublicClient.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdef0")]
    [InlineData("..%2F..%2Fsnapshot.json")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    public async Task A_token_that_was_never_staged_or_is_not_32_lowercase_hex_characters_is_not_found(string token)
    {
        await using var host = await PreviewHost.StartAsync();
        using var owner = host.Factory.CreateOwnerClient();

        using var response = await owner.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_preview_that_is_older_than_the_lifetime_is_not_found()
    {
        await using var host = await PreviewHost.StartAsync();
        var token = (await host.CheckAsync(LinkUrl)).JsonText("token");
        host.Clock.Advance(StagedItems<StagedLink>.Lifetime - TimeSpan.FromSeconds(1));
        using var owner = host.Factory.CreateOwnerClient();

        using var before = await owner.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);
        host.Clock.Advance(TimeSpan.FromSeconds(2));
        using var after = await owner.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Using_the_preview_stores_the_files_and_the_link_logs_the_change_and_every_visitors_card_shows_the_picture()
    {
        await using var host = await PreviewHost.StartAsync();
        var before = await host.ReadCardAsync();
        var token = (await host.CheckAsync(LinkUrl)).JsonText("token");

        var reply = await host.UseAsync(token);
        var after = await host.ReadCardAsync();

        reply.Status.Should().Be(HttpStatusCode.OK);
        reply.JsonText("outcome").Should().Be("saved");
        var setting = host.Setting!;
        setting.Choice.Should().Be(PictureChoice.Link);
        setting.Checked.Should().BeTrue();
        setting.Link!.Url.Should().Be(LinkUrl);
        setting.Link.Record!.Files.Should().NotBeEmpty().And.OnlyContain(file => File.Exists(host.ArtFile(file.Name)));
        host.Changes[0].Text.Should().Be($"Picture of {host.Title}: your link");
        before.CoverUrl.Should().NotBe(after.CoverUrl);
        setting.Link.Record.Files!.Select(file => file.Name).Should().Contain(Path.GetFileName(after.CoverUrl));
        after.Colour.Should().Be(setting.Link.Record.Colour!.Background);
        reply.Json.GetProperty("linkTile").GetProperty("url").GetString().Should().StartWith("/art/");
        reply.Json.GetProperty("linkTile").GetProperty("address").GetString().Should().Be("example.org/.../linked-picture.png");
    }

    [Fact]
    public async Task The_spine_colour_the_preview_showed_is_the_colour_the_card_gets_after_using_it()
    {
        await using var host = await PreviewHost.StartAsync();
        var preview = (await host.CheckAsync(LinkUrl)).Json;
        var token = preview.GetProperty("token").GetString()!;

        await host.UseAsync(token);
        var after = await host.ReadCardAsync();

        after.Colour.Should().Be(preview.GetProperty("preview").GetProperty("spineBackground").GetString());
    }

    [Fact]
    public async Task A_used_preview_is_gone_and_a_second_use_of_the_same_token_changes_nothing()
    {
        await using var host = await PreviewHost.StartAsync();
        var token = (await host.CheckAsync(LinkUrl)).JsonText("token");
        await host.UseAsync(token);
        var revision = host.Service.Revision;
        using var owner = host.Factory.CreateOwnerClient();

        var again = await host.UseAsync(token);
        using var served = await owner.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);

        again.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        host.Service.Revision.Should().Be(revision);
        served.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Using_a_second_link_replaces_the_first_link_and_its_record()
    {
        await using var host = await PreviewHost.StartAsync();
        await host.UseAsync((await host.CheckAsync(LinkUrl)).JsonText("token"));
        var first = host.Setting!.Link!.Record;

        await host.UseAsync((await host.CheckAsync(SecondLinkUrl)).JsonText("token"));

        host.Setting!.Choice.Should().Be(PictureChoice.Link);
        host.Setting.Link!.Url.Should().Be(SecondLinkUrl);
        host.Setting.Link.Record.Should().NotBeSameAs(first);
        host.Setting.Link.Record!.SourceUrl.Should().Be(SecondLinkUrl);
    }

    [Fact]
    public async Task Using_a_token_that_was_never_staged_is_refused_and_changes_nothing()
    {
        await using var host = await PreviewHost.StartAsync();
        var revision = host.Service.Revision;

        var reply = await host.UseAsync(ZeroToken);

        reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        reply.JsonText("status").Should().Be("link-expired");
        host.Service.Revision.Should().Be(revision);
        host.Setting.Should().BeNull();
    }

    [Fact]
    public async Task A_preview_staged_for_one_game_cannot_be_used_for_another()
    {
        await using var host = await PreviewHost.StartAsync();
        var token = (await host.CheckAsync(LinkUrl)).JsonText("token");
        var revision = host.Service.Revision;

        var reply = await host.Session.PostJsonAsync(
            UsePath,
            [Field("gameId", host.OtherGameId.ToString(CultureInfo.InvariantCulture)), Field("token", token), Field("revision", host.Revision)]);

        reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        host.Service.Revision.Should().Be(revision);
    }

    [Fact]
    public async Task Discarding_a_preview_removes_it_and_leaves_the_data_alone()
    {
        await using var host = await PreviewHost.StartAsync();
        var token = (await host.CheckAsync(LinkUrl)).JsonText("token");
        var revision = host.Service.Revision;
        using var owner = host.Factory.CreateOwnerClient();

        var reply = await host.Session.PostJsonAsync("/owner/pictures?handler=DiscardPreview", [Field("token", token)]);
        using var served = await owner.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);

        reply.Status.Should().Be(HttpStatusCode.OK);
        served.StatusCode.Should().Be(HttpStatusCode.NotFound);
        host.Service.Revision.Should().Be(revision);
    }

    [Fact]
    public async Task A_link_that_is_not_on_the_picture_host_answers_a_refusal_and_stages_nothing()
    {
        await using var host = await PreviewHost.StartAsync();
        var revision = host.Service.Revision;

        var reply = await host.CheckAsync("https://img.example.net/owner/linked-picture.png");

        reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        reply.JsonText("outcome").Should().Be("refused");
        host.Service.Revision.Should().Be(revision);
        host.Images.Requests.Should().NotContain(request => request.Uri.Host == "img.example.net");
    }

    [Fact]
    public async Task Without_script_checking_a_link_goes_back_to_the_games_own_view_with_the_token_of_the_staged_preview()
    {
        await using var host = await PreviewHost.StartAsync();

        var reply = await host.Session.PostFormAsync(CheckPath, [Field("gameId", host.GameId.ToString(CultureInfo.InvariantCulture)), Field("link", LinkUrl)]);
        var token = TokenInText().Match(reply.Location ?? string.Empty).Value;
        using var owner = host.Factory.CreateOwnerClient();
        using var served = await owner.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);

        reply.Status.Should().Be(HttpStatusCode.Found);
        reply.Location.Should().Be($"/owner/pictures?game={host.GameId}&preview={token}#entry-{host.FirstEntry}");
        served.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Without_script_a_link_that_cannot_be_used_goes_back_to_the_games_own_view_with_the_words_for_it()
    {
        await using var host = await PreviewHost.StartAsync();

        var reply = await host.Session.PostFormAsync(CheckPath, [Field("gameId", host.GameId.ToString(CultureInfo.InvariantCulture)), Field("link", "not a link")]);
        var page = await host.Session.GetAsync(reply.Location!);

        reply.Status.Should().Be(HttpStatusCode.Found);
        reply.Location.Should().Be($"/owner/pictures?done=refused-link-address&game={host.GameId}#entry-{host.FirstEntry}");
        page.Body.Should().Contain("That isn&#x27;t a web address. Paste the full picture link, starting with https://.");
    }

    [Fact]
    public async Task A_post_without_the_token_is_answered_400_and_the_link_pages_do_not_exist_on_the_public_port()
    {
        await using var host = await PreviewHost.StartAsync();

        var noToken = await host.Session.PostJsonAsync(CheckPath, [Field("gameId", host.GameId.ToString(CultureInfo.InvariantCulture)), Field("link", LinkUrl)], withToken: false);
        using var publicPost = await host.PublicClient.PostAsync(
            CheckPath,
            new FormUrlEncodedContent([Field("gameId", host.GameId.ToString(CultureInfo.InvariantCulture)), Field("link", LinkUrl)]),
            TestContext.Current.CancellationToken);

        noToken.Status.Should().Be(HttpStatusCode.BadRequest);
        publicPost.StatusCode.Should().Be(HttpStatusCode.NotFound);
        host.Images.Requests.Should().NotContain(request => request.Uri.AbsoluteUri == LinkUrl);
    }

    [Fact]
    public async Task Removing_a_link_without_the_token_is_answered_400_and_the_page_does_not_exist_on_the_public_port()
    {
        await using var host = await PreviewHost.StartAsync();
        var fields = new[] { Field("gameId", host.GameId.ToString(CultureInfo.InvariantCulture)), Field("revision", host.Service.Revision.ToString(CultureInfo.InvariantCulture)) };

        var noToken = await host.Session.PostJsonAsync("/owner/pictures?handler=RemoveLink", fields, withToken: false);
        using var publicPost = await host.PublicClient.PostAsync("/owner/pictures?handler=RemoveLink", new FormUrlEncodedContent(fields), TestContext.Current.CancellationToken);

        noToken.Status.Should().Be(HttpStatusCode.BadRequest);
        publicPost.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("", "blank", "Paste a picture link first.")]
    [InlineData("not a link", "notAWebAddress", "That isn't a web address. Paste the full picture link, starting with https://.")]
    [InlineData("https://img.example.net/owner/linked-picture.png", "otherHost", "That link isn't from BGG's picture host. Only links from example.org work here. Copy the picture's address from BGG's gallery.")]
    [InlineData("https://example.org/owner/missing.png", "notFound", "That picture isn't there. Check that you copied the whole link.")]
    public async Task A_link_that_cannot_be_used_answers_its_problem_and_its_own_words_and_changes_nothing(string link, string problem, string message)
    {
        await using var host = await PreviewHost.StartAsync();
        var revision = host.Service.Revision;

        var reply = await host.CheckAsync(link);

        reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        reply.JsonText("problem").Should().Be(problem);
        reply.JsonText("message").Should().Be(message);
        host.Service.Revision.Should().Be(revision);
    }

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("[0-9a-f]{32}")]
    private static partial Regex TokenInText();

    private sealed record Reading(string? CoverUrl, string? Colour);

    private sealed class PreviewHost : IAsyncDisposable
    {
        private readonly TemporaryDirectory _storage;
        private readonly HttpClient _publicClient;
        private readonly int _gameId;
        private readonly int _otherGameId;

        private PreviewHost(
            TemporaryDirectory storage,
            CabinetWebApplicationFactory factory,
            ScriptedImageHandler images,
            FakeTimeProvider clock,
            HttpClient publicClient,
            FakeBggItem game,
            int otherGameId)
        {
            _storage = storage;
            Factory = factory;
            Images = images;
            Clock = clock;
            _publicClient = publicClient;
            _gameId = game.ObjectId;
            _otherGameId = otherGameId;
            Title = game.Title;
            Session = new OwnerSession(factory);
        }

        public CabinetWebApplicationFactory Factory { get; }

        public ScriptedImageHandler Images { get; }

        public FakeTimeProvider Clock { get; }

        public OwnerSession Session { get; }

        public HttpClient PublicClient => _publicClient;

        public string Title { get; }

        public int GameId => _gameId;

        public int OtherGameId => _otherGameId;

        public long FirstEntry => Factory.ServingServices.GetRequiredService<CollectionStore>().Current.Snapshot!.Items.Where(item => item.GameId == _gameId).Min(item => item.CollectionId);

        public OwnerDataService Service => Factory.ServingServices.GetRequiredService<OwnerDataService>();

        public string Revision => Service.Revision.ToString(CultureInfo.InvariantCulture);

        public OwnerData State => Service.State is OwnerDataState.Ready ready ? ready.Data : OwnerData.Empty;

        public IReadOnlyList<ChangeEntry> Changes => State.Changes;

        public PictureSetting? Setting => State.Games.FirstOrDefault(game => game.GameId == _gameId)?.Picture;

        public static async Task<PreviewHost> StartAsync()
        {
            var storage = new TemporaryDirectory();
            var items = SyntheticBggCollection.Create(5);
            var owned = items.Where(candidate => candidate.Owned && !candidate.IsExpansion).ToList();
            var game = owned[0];
            var images = new ScriptedImageHandler().Serve(LinkUrl, LinkedCover).Serve(SecondLinkUrl, SecondCover);
            var clock = SyncHarness.NewClock();
            var factory = SyncHarness.CreateFactory(
                ScriptedBggHandler.ForCollection(items),
                clock,
                new Dictionary<string, string?>
                {
                    ["Images:AllowedHosts"] = "example.org",
                    ["Images:MaxDownloadsPerRun"] = "1000",
                    ["Storage:Directory"] = storage.FullPath,
                },
                images);
            var client = factory.CreatePublicClient();
            await SyncRounds.PressAndWait(client, clock, advance: false);

            return new PreviewHost(storage, factory, images, clock, client, game, owned[1].ObjectId);
        }

        public Task<OwnerReply> CheckAsync(string link) =>
            Session.PostJsonAsync(CheckPath, [Field("gameId", _gameId.ToString(CultureInfo.InvariantCulture)), Field("link", link)]);

        public Task<OwnerReply> UseAsync(string token) =>
            Session.PostJsonAsync(UsePath, [Field("gameId", _gameId.ToString(CultureInfo.InvariantCulture)), Field("token", token), Field("revision", Revision)]);

        public string ArtFile(string name) => Path.Combine(_storage.FullPath, "art", name);

        public IReadOnlyList<string> ArtFiles() =>
            [.. Directory.EnumerateFiles(Path.Combine(_storage.FullPath, "art")).Select(file => Path.GetFileName(file)).Order(StringComparer.Ordinal)];

        public async Task<Reading> ReadCardAsync()
        {
            using var response = await _publicClient.GetAsync(CardsPath, TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var card = document.RootElement.GetProperty("cards").EnumerateArray().First(candidate => candidate.GetProperty("gameId").GetInt32() == _gameId);

            return new Reading(
                card.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object ? cover.GetProperty("url").GetString() : null,
                card.TryGetProperty("colour", out var colour) && colour.ValueKind == JsonValueKind.Object ? colour.GetProperty("background").GetString() : null);
        }

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            _publicClient.Dispose();
            await Factory.DisposeAsync();
            _storage.Dispose();
        }
    }
}
