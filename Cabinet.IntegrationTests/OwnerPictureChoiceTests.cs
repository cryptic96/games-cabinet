using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves that a picture the owner chooses on the owner port reaches every visitor: the cover on the card, the spine colour
/// and both entity tags change, choosing Automatic brings the earlier picture back, and the pictures page itself exists only on
/// the owner port.
/// </summary>
[Trait("Category", "Owner")]
public sealed partial class OwnerPictureChoiceTests
{
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string LayoutPath = "/cabinet/layout?profile=desktop";
    private const string ChoosePath = "/owner/pictures?handler=Choose";
    private const string ImageHost = "https://example.org/images/";

    private static readonly byte[] FlatCover = SyntheticArt.Encode(SyntheticArtKind.FlatCover);
    private static readonly byte[] ColouredCover = SyntheticArt.Encode(SyntheticArtKind.CoverColouredField);

    [Fact]
    public async Task Choosing_the_main_picture_changes_the_card_cover_the_spine_colour_and_both_tags_and_Automatic_restores_them()
    {
        await using var host = await StartAsync();
        var before = await host.ReadAsync();

        var chosen = await host.Session.PostJsonAsync(ChoosePath, host.Fields("MainPicture"));
        var during = await host.ReadAsync();
        var restored = await host.Session.PostJsonAsync(ChoosePath, host.Fields("Automatic"));
        var after = await host.ReadAsync();

        before.Colour.Should().NotBe(host.MainColour, "the two candidates have to differ for the choice to show");
        chosen.Status.Should().Be(HttpStatusCode.OK);
        host.MainFiles.Should().Contain(Path.GetFileName(during.CoverUrl));
        during.CoverUrl.Should().NotBe(before.CoverUrl);
        during.Colour.Should().Be(host.MainColour);
        during.CardsTag.Should().NotBe(before.CardsTag);
        during.LayoutTag.Should().NotBe(before.LayoutTag);
        restored.Status.Should().Be(HttpStatusCode.OK);
        after.CoverUrl.Should().Be(before.CoverUrl);
        after.Colour.Should().Be(before.Colour);
        host.Changes.Take(2).Select(change => change.Text).Should().Equal(
            $"Picture of {host.Title}: back to Automatic",
            $"Picture of {host.Title}: main picture");
    }

    [Fact]
    public async Task A_choice_saved_for_one_copy_changes_only_that_copys_box()
    {
        const int gameId = 5101;
        const string versionUrl = "https://cf.example.org/version.jpg";
        const string mainUrl = "https://cf.example.org/main.jpg";
        long[] copies = [7001, 7002, 7003];
        using var storage = new TemporaryDirectory();
        var moment = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var images = new Dictionary<string, ImageRecord>
        {
            [versionUrl] = new(versionUrl, ImageStatus.Ok, moment, [new ArtFile(600, 800, "1111111111111111-600.webp")], new ArtFeatures(0.0, 1.0, 0.0, 0.0, 4)),
            [mainUrl] = new(mainUrl, ImageStatus.Ok, moment, [new ArtFile(600, 800, "2222222222222222-600.webp")], new ArtFeatures(0.3, 0.70, 0.60, 0.50, 1)),
        };
        new SnapshotStore(storage.FullPath, NullLogger<SnapshotStore>.Instance).Save(new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            moment,
            [.. copies.Select(copy => new SnapshotItem(copy, gameId, "Quiet Quarry", ItemKind.Base, 2015, null, null, versionUrl, mainUrl))],
            images,
            null));
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();
        var owner = factory.ServingServices.GetRequiredService<OwnerDataService>();
        var before = await CoversAsync(client);
        var layoutBefore = await LayoutTagAsync(client);

        var saved = await owner.MutateAsync(
            owner.Revision,
            data => PictureEdits.Choose(data, gameId, copies[1], copies, PictureChoice.MainPicture, "Quiet Quarry", versionUrl, mainUrl),
            TestContext.Current.CancellationToken);
        var after = await CoversAsync(client);

        saved.Outcome.Should().Be(OwnerOutcome.Saved);
        before.Values.Should().OnlyContain(cover => cover.Contains("1111111111111111", StringComparison.Ordinal));
        after[7001].Should().Be(before[7001]);
        after[7003].Should().Be(before[7003]);
        after[7002].Should().Contain("2222222222222222");
        (await LayoutTagAsync(client)).Should().NotBe(layoutBefore);
    }

    [Fact]
    public async Task Choosing_the_picture_the_automatic_pick_already_shows_still_changes_the_collection_version()
    {
        await using var host = await StartAsync();
        var before = await host.ReadAsync();

        await host.Session.PostJsonAsync(ChoosePath, host.Fields("OwnedEdition"));
        var after = await host.ReadAsync();

        after.CoverUrl.Should().Be(before.CoverUrl);
        after.LayoutTag.Should().NotBe(before.LayoutTag);
        after.CardsTag.Should().NotBe(before.CardsTag);
    }

    [Fact]
    public async Task A_choice_posted_twice_changes_nothing_the_second_time_and_a_choice_that_does_not_exist_is_refused()
    {
        await using var host = await StartAsync();

        await host.Session.PostJsonAsync(ChoosePath, host.Fields("MainPicture"));
        var revision = host.Service.Revision;
        var again = await host.Session.PostJsonAsync(ChoosePath, host.Fields("MainPicture"));
        var noLink = await host.Session.PostJsonAsync(ChoosePath, host.Fields("Link"));
        var number = await host.Session.PostJsonAsync(ChoosePath, host.Fields("99"));
        var unknownGame = await host.Session.PostJsonAsync(ChoosePath, [Field("gameId", "1"), Field("choice", "MainPicture"), Field("revision", host.Revision)]);

        again.JsonText("outcome").Should().Be("noChange");
        noLink.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        noLink.JsonText("status").Should().Be("no-link");
        number.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        unknownGame.JsonText("status").Should().Be("unknown-game");
        host.Service.Revision.Should().Be(revision);
    }

    [Fact]
    public async Task A_post_without_the_token_is_answered_400_and_changes_nothing()
    {
        await using var host = await StartAsync();

        var json = await host.Session.PostJsonAsync(ChoosePath, host.Fields("MainPicture"), withToken: false);
        var form = await host.Session.PostFormAsync(ChoosePath, host.Fields("MainPicture"), withToken: false);

        json.Status.Should().Be(HttpStatusCode.BadRequest);
        form.Status.Should().Be(HttpStatusCode.BadRequest);
        host.State.Games.Should().BeEmpty();
    }

    [Fact]
    public async Task A_form_post_goes_back_to_the_row_and_Undo_takes_the_choice_back()
    {
        await using var host = await StartAsync();

        var saved = await host.Session.PostFormAsync(ChoosePath, host.Fields("MainPicture"));
        var undone = await host.Session.PostJsonAsync("/owner/pictures?handler=Undo", [Field("revision", host.Revision)]);

        saved.Status.Should().Be(HttpStatusCode.Found);
        saved.Location.Should().Be($"/owner/pictures?done=saved#game-{host.GameId}");
        undone.JsonText("outcome").Should().Be("saved");
        host.State.Games.Should().BeEmpty();
    }

    [Fact]
    public async Task The_pictures_page_is_missing_on_the_public_port_and_lists_every_game_with_four_choices_on_the_owner_port()
    {
        await using var host = await StartAsync();
        using var publicClient = host.Factory.CreatePublicClient();

        using var onPublic = await publicClient.GetAsync("/owner/pictures", TestContext.Current.CancellationToken);
        var page = await host.Session.GetAsync("/owner/pictures?view=all");
        var rows = GameRows().Matches(page.Body).Count;

        onPublic.StatusCode.Should().Be(HttpStatusCode.NotFound);
        page.Status.Should().Be(HttpStatusCode.OK);
        page.Body.Should().Contain("Owner tools").And.Contain("<h2>Pictures</h2>");
        CurrentBarItem().Match(page.Body).Groups[1].Value.Should().Be("Pictures");
        rows.Should().BeGreaterThan(1);
        Regex.Matches(page.Body, "type=\"radio\"").Count.Should().Be(rows * 4);
        Regex.Matches(page.Body, ">Save picture<").Count.Should().Be(rows);
        page.Body.Should().Contain($"id=\"game-{host.GameId}\"");
    }

    [Fact]
    public void The_synthetic_collection_holds_every_picture_case_the_choices_are_checked_against()
    {
        var items = SyntheticBggCollection.Create(65).Where(item => item.Owned).ToList();
        var ownedBases = items.Where(item => !item.IsExpansion).Select(item => item.ObjectId).ToHashSet();
        var threeDShots = new[] { SyntheticArtKind.BoxOnWhite, SyntheticArtKind.BoxOnGreyGradient, SyntheticArtKind.BoxOnBlack, SyntheticArtKind.BoxTransparent };
        var expansions = items.Where(item => item.IsExpansion).ToList();
        var ownedBasesOf = (FakeBggItem expansion) => (expansion.AlsoExpands ?? []).Append(expansion.BaseObjectId ?? 0).Count(ownedBases.Contains);

        items.Any(item => item.Version is not null
            && SyntheticBggCollection.ArtFor(item, true) is { } owned
            && SyntheticBggCollection.ArtFor(item, false) is { } main
            && owned != main).Should().BeTrue("a game whose owned edition and main picture differ");
        items.Any(item => item.Version is not null
            && SyntheticBggCollection.ArtFor(item, true) is { } owned
            && threeDShots.Contains(owned)).Should().BeTrue("a game whose owned edition reads as a 3D shot");
        items.Any(item => item.Version is null
            && SyntheticBggCollection.ArtFor(item, false) is not null).Should().BeTrue("a game with no owned-edition picture");
        items.Any(item => IsUnusable(SyntheticBggCollection.ArtFor(item, true))
            && IsUnusable(SyntheticBggCollection.ArtFor(item, false))).Should().BeTrue("a game with no usable picture at all");
        expansions.Any(expansion => ownedBasesOf(expansion) == 1).Should().BeTrue("an expansion with one owned base");
        expansions.Any(expansion => ownedBasesOf(expansion) > 1).Should().BeTrue("an expansion with several owned bases");
        expansions.Any(expansion => ownedBasesOf(expansion) == 0).Should().BeTrue("an expansion with no owned base");
    }

    private static async Task<Dictionary<long, string>> CoversAsync(HttpClient client)
    {
        using var response = await client.GetAsync(CardsPath, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("cards").EnumerateArray()
            .ToDictionary(card => card.GetProperty("entryId").GetInt64(), card => card.GetProperty("cover").GetProperty("url").GetString()!);
    }

    private static async Task<string> LayoutTagAsync(HttpClient client)
    {
        using var response = await client.GetAsync(LayoutPath, TestContext.Current.CancellationToken);

        return response.Headers.ETag!.Tag;
    }

    private static bool IsUnusable(SyntheticArtKind? kind) => kind is null or SyntheticArtKind.Undecodable;

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    private static string VersionUrl(FakeBggItem item) => $"{ImageHost}version-{BggXml.VersionId(item)}.jpg";

    private static string MainUrl(FakeBggItem item) => $"{ImageHost}{item.ObjectId}.jpg";

    private static async Task<PictureHost> StartAsync()
    {
        var storage = new TemporaryDirectory();
        var items = SyntheticBggCollection.Create(5);
        var item = items.First(candidate => candidate.Owned && !candidate.IsExpansion && candidate.Version is not null);
        var images = new ScriptedImageHandler().Serve(VersionUrl(item), FlatCover).Serve(MainUrl(item), ColouredCover);
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

        var host = new PictureHost(storage, factory, client, item);
        await host.InitialiseAsync();

        return host;
    }

    [GeneratedRegex("<li class=\"pic-row\" id=\"game-\\d+\"")]
    private static partial Regex GameRows();

    [GeneratedRegex("<a href=\"[^\"]*\" aria-current=\"page\">([^<]*)</a>")]
    private static partial Regex CurrentBarItem();

    private sealed record Reading(string CoverUrl, string Colour, string CardsTag, string LayoutTag);

    private sealed class PictureHost(TemporaryDirectory storage, CabinetWebApplicationFactory factory, HttpClient client, FakeBggItem item) : IAsyncDisposable
    {
        public CabinetWebApplicationFactory Factory => factory;

        public OwnerSession Session { get; } = new(factory);

        public string Title { get; private set; } = string.Empty;

        public string GameId => item.ObjectId.ToString(CultureInfo.InvariantCulture);

        public string MainColour { get; private set; } = string.Empty;

        public IReadOnlyList<string> MainFiles { get; private set; } = [];

        public OwnerDataService Service => factory.ServingServices.GetRequiredService<OwnerDataService>();

        public string Revision => Service.Revision.ToString(CultureInfo.InvariantCulture);

        public OwnerData State => Service.State is OwnerDataState.Ready ready ? ready.Data : OwnerData.Empty;

        public IReadOnlyList<ChangeEntry> Changes => State.Changes;

        public async Task InitialiseAsync()
        {
            using var response = await client.GetAsync(CardsPath, TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Title = CardOf(document).GetProperty("title").GetString()!;

            using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(storage.FullPath, "snapshot.json")));
            var record = snapshot.RootElement.GetProperty("images").GetProperty(MainUrl(item));
            MainColour = record.GetProperty("colour").GetProperty("background").GetString()!;
            MainFiles = [.. record.GetProperty("files").EnumerateArray().Select(file => file.GetProperty("name").GetString()!)];
        }

        public List<KeyValuePair<string, string>> Fields(string choice) =>
        [
            Field("gameId", GameId),
            Field("choice", choice),
            Field("revision", Revision),
        ];

        public async Task<Reading> ReadAsync()
        {
            using var cards = await client.GetAsync(CardsPath, TestContext.Current.CancellationToken);
            using var layout = await client.GetAsync(LayoutPath, TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(await cards.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var card = CardOf(document);

            return new Reading(
                card.GetProperty("cover").GetProperty("url").GetString()!,
                card.GetProperty("colour").GetProperty("background").GetString()!,
                cards.Headers.ETag!.Tag,
                layout.Headers.ETag!.Tag);
        }

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            client.Dispose();
            await factory.DisposeAsync();
            storage.Dispose();
        }

        private JsonElement CardOf(JsonDocument document) =>
            document.RootElement.GetProperty("cards").EnumerateArray()
                .First(card => card.GetProperty("gameId").GetInt32() == item.ObjectId)
                .Clone();
    }
}
