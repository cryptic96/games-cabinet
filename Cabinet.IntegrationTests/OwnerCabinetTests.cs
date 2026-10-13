using System.Net;
using System.Text.RegularExpressions;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves what the cabinet page writes at the owner address before any script runs: the plate beside the language toggle, the strip
/// under the header with the number of games that are not placed yet, and the recovery wording while the owner data cannot be read.
/// It also proves that none of it, and no count of any kind, reaches the public address.
/// </summary>
[Trait("Category", "Owner")]
public sealed partial class OwnerCabinetTests
{
    private const string StripSentence = "Owner view. Changes you make here show for every visitor.";
    private const string PlaceThemPath = "/owner/locations?filter=unplaced";
    private const string SettingsPath = "/owner/settings";
    private const string Quarry = "Quarry Lights";
    private const string Cafe = "Cafe Quarter";
    private const string CafeExpansion = "Cafe Quarter: Extra Pack";
    private const string Cartographers = "Cartographers of the Endless Amber Hills";

    [Fact]
    public async Task At_the_owner_address_the_plate_shares_one_header_cell_with_the_toggle_and_the_strip_sits_under_the_header_block()
    {
        await using var host = await Host.StartAsync();
        using var session = new OwnerSession(host.Factory);

        var page = await session.GetAsync("/");

        page.Status.Should().Be(HttpStatusCode.OK);
        var cell = HeaderAside().Match(page.Body);
        cell.Success.Should().BeTrue("the plate and the toggle share one cell");
        cell.Groups["inner"].Value.Should().Contain("class=\"owner-plate\"").And.Contain("class=\"lang-toggle\"");
        cell.Groups["inner"].Value.IndexOf("owner-plate", StringComparison.Ordinal)
            .Should().BeLessThan(cell.Groups["inner"].Value.IndexOf("lang-toggle", StringComparison.Ordinal), "the plate comes first");
        page.Body.Should().Contain(">Owner tools<").And.Contain(StripSentence);
        Strip().Match(page.Body).Success.Should().BeTrue();
        page.Body.IndexOf("class=\"sync\"", StringComparison.Ordinal)
            .Should().BeLessThan(page.Body.IndexOf("data-variant=\"strip\"", StringComparison.Ordinal), "the strip comes under the sync block");
        page.Body.IndexOf("data-variant=\"strip\"", StringComparison.Ordinal)
            .Should().BeLessThan(page.Body.IndexOf("<main", StringComparison.Ordinal), "the strip comes before the page content");
    }

    [Fact]
    public async Task The_strip_holds_the_pictures_locations_and_settings_links_but_no_cabinet_link_and_no_live_region()
    {
        await using var host = await Host.StartAsync();
        using var session = new OwnerSession(host.Factory);

        var strip = Strip().Match((await session.GetAsync("/")).Body).Value;

        strip.Should().Contain("href=\"/owner/pictures\"").And.Contain("href=\"/owner/locations\"").And.Contain($"href=\"{SettingsPath}\"");
        strip.Should().NotContain(">Cabinet<").And.NotContain("href=\"/\"");
        strip.Should().NotContain("aria-live").And.NotContain("role=\"status\"").And.NotContain("role=\"alert\"");
    }

    [Fact]
    public async Task Three_games_that_are_not_placed_are_counted_with_a_link_to_the_filtered_locations_list()
    {
        await using var host = await Host.StartAsync();
        await host.PlaceAsync(Quarry);
        using var session = new OwnerSession(host.Factory);

        var strip = Strip().Match((await session.GetAsync("/")).Body).Value;

        strip.Should().Contain("3 games are not placed yet.");
        strip.Should().Contain($"<a class=\"owner-strip-action\" href=\"{PlaceThemPath}\">Place them</a>");
    }

    [Fact]
    public async Task One_game_that_is_not_placed_reads_in_the_singular_and_an_expansion_following_its_placed_base_is_not_counted()
    {
        await using var host = await Host.StartAsync();
        await host.PlaceAsync(Quarry);
        await host.PlaceAsync(Cafe);
        using var session = new OwnerSession(host.Factory);

        var strip = Strip().Match((await session.GetAsync("/")).Body).Value;

        strip.Should().Contain("1 game is not placed yet.").And.NotContain("1 games");
        strip.Should().Contain("Place them");
    }

    [Fact]
    public async Task With_every_game_placed_the_strip_adds_no_count_sentence_and_no_link()
    {
        await using var host = await Host.StartAsync();
        await host.PlaceAsync(Quarry);
        await host.PlaceAsync(Cafe);
        await host.PlaceAsync(Cartographers);
        using var session = new OwnerSession(host.Factory);

        var strip = Strip().Match((await session.GetAsync("/")).Body).Value;

        strip.Should().Contain(StripSentence);
        strip.Should().NotContain("not placed yet").And.NotContain("Place them").And.NotContain("filter=unplaced");
    }

    [Fact]
    public async Task With_nothing_placed_every_synced_game_is_counted_once_and_the_count_matches_the_effective_places()
    {
        await using var host = await Host.StartAsync();
        using var session = new OwnerSession(host.Factory);

        var strip = Strip().Match((await session.GetAsync("/")).Body).Value;

        strip.Should().Contain("4 games are not placed yet.");
        host.UnplacedByOverlay().Should().Be(4);
    }

    [Fact]
    public async Task While_the_owner_data_cannot_be_read_the_strip_says_so_with_a_link_to_settings_and_no_count()
    {
        using var storage = new TemporaryDirectory();
        OwnerDataSeed.WriteRaw(storage.FullPath, [0x7b, 0x7b]);
        await using var host = await Host.StartAsync(storage);
        using var session = new OwnerSession(host.Factory);

        var page = await session.GetAsync("/");
        var strip = Strip().Match(page.Body).Value;

        host.Owner.State.Should().BeOfType<OwnerDataState.Unreadable>();
        page.Status.Should().Be(HttpStatusCode.OK);
        strip.Should().Contain("Owner data couldn&#x27;t be read.").And.Contain($"<a class=\"owner-strip-action\" href=\"{SettingsPath}\">Fix it</a>");
        strip.Should().NotContain("not placed yet").And.NotContain("Place them");
    }

    [Fact]
    public async Task The_count_is_written_by_the_server_so_it_does_not_depend_on_the_card_data_script()
    {
        await using var host = await Host.StartAsync();
        using var session = new OwnerSession(host.Factory);

        var page = await session.GetAsync("/");

        page.Body.Should().Contain("4 games are not placed yet.");
        Regex.Matches(page.Body, "games are not placed yet").Should().HaveCount(1);
    }

    [Fact]
    public async Task At_the_public_address_there_is_no_plate_no_strip_no_count_and_no_owner_path()
    {
        await using var host = await Host.StartAsync();
        using var client = host.Factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        html.Should().Contain("lang-toggle");
        html.Should().NotContain("Owner tools").And.NotContain("Owner view").And.NotContain("not placed").And.NotContain("Place them");
        html.Should().NotContain("header-aside").And.NotContain("owner-bar").And.NotContain("owner-plate").And.NotContain("/owner");
    }

    [Fact]
    public async Task At_the_public_address_the_unreadable_wording_and_the_fix_link_never_appear()
    {
        using var storage = new TemporaryDirectory();
        OwnerDataSeed.WriteRaw(storage.FullPath, [0x7b, 0x7b]);
        await using var host = await Host.StartAsync(storage);
        using var client = host.Factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        html.Should().NotContain("Owner data couldn").And.NotContain("Fix it").And.NotContain("/owner");
    }

    [GeneratedRegex("<div class=\"header-aside\">(?<inner>.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex HeaderAside();

    [GeneratedRegex("<nav class=\"owner-bar\" data-variant=\"strip\".*?</nav>", RegexOptions.Singleline)]
    private static partial Regex Strip();

    private sealed class Host : IAsyncDisposable
    {
        private readonly TemporaryDirectory? _ownedStorage;

        private Host(CabinetWebApplicationFactory factory, TemporaryDirectory? ownedStorage)
        {
            Factory = factory;
            _ownedStorage = ownedStorage;
        }

        public CabinetWebApplicationFactory Factory { get; }

        public OwnerDataService Owner => Factory.ServingServices.GetRequiredService<OwnerDataService>();

        public static async Task<Host> StartAsync(TemporaryDirectory? storage = null)
        {
            var owned = storage is null ? new TemporaryDirectory() : null;
            var directory = storage ?? owned!;
            var clock = SyncHarness.NewClock();
            var factory = SyncHarness.CreateFactory(
                ScriptedBggHandler.ForCollection(Collection()),
                clock,
                new Dictionary<string, string?> { ["Storage:Directory"] = directory.FullPath });
            using var client = factory.CreatePublicClient();

            await SyncRounds.PressAndWait(client, clock, advance: false);

            return new Host(factory, owned);
        }

        public async Task PlaceAsync(string title)
        {
            var gameId = GameIdOf(title);
            var data = Owner.State is OwnerDataState.Ready ready ? ready.Data : OwnerData.Empty;
            var locationId = data.Locations.Count > 0 ? data.Locations[0].Id : null;

            if (locationId is null)
            {
                var added = await Owner.MutateAsync(
                    Owner.Revision,
                    current => LocationEdits.Add(current, "Cabinet A", OwnerDataService.NewId),
                    TestContext.Current.CancellationToken);

                added.Outcome.Should().Be(OwnerOutcome.Saved);
                locationId = added.Data!.Locations[0].Id;
            }

            var placed = await Owner.MutateAsync(
                Owner.Revision,
                current => LocationEdits.Assign(current, gameId, locationId, title),
                TestContext.Current.CancellationToken);

            placed.Outcome.Should().Be(OwnerOutcome.Saved);
        }

        public int UnplacedByOverlay()
        {
            var snapshot = Factory.ServingServices.GetRequiredService<Cabinet.Service.Collection.CollectionStore>().Current.Snapshot!;
            var data = Owner.State is OwnerDataState.Ready ready ? ready.Data : null;
            var overlay = OwnerOverlay.From(snapshot, data);

            return snapshot.Items.Select(item => item.GameId).Distinct().Count(id => overlay.EffectiveLocationId(id) is null);
        }

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            _ownedStorage?.Dispose();
        }

        private static int GameIdOf(string title) =>
            SyntheticBggCollection.FirstObjectId + Array.FindIndex(Titles, candidate => candidate == title);

        private static readonly string[] Titles = [Quarry, Cafe, CafeExpansion, Cartographers];

        private static IReadOnlyList<FakeBggItem> Collection()
        {
            var first = SyntheticBggCollection.FirstObjectId;

            return
            [
                Entry(0, Quarry, false, null),
                Entry(1, Cafe, false, null),
                Entry(2, CafeExpansion, true, first + 1),
                Entry(3, Cartographers, false, null),
            ];
        }

        private static FakeBggItem Entry(int index, string title, bool isExpansion, int? baseObjectId) =>
            new(
                SyntheticBggCollection.FirstObjectId + index,
                SyntheticBggCollection.FirstCollId + index,
                title,
                isExpansion,
                true,
                2000 + index,
                null,
                null,
                baseObjectId);
    }
}
