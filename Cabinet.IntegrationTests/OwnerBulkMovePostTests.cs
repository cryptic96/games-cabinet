using System.Globalization;
using System.Net;
using System.Text.Json;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the post behind the dock's move: several ticked copies go in one request and one change, every copy and the target are
/// checked against what the owner has, only the ticked copies move, and a request without the token changes nothing.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerBulkMovePostTests
{
    private const string BulkMove = "/owner/locations?handler=BulkMove";

    [Fact]
    public async Task Ticked_games_move_in_one_request_as_one_change_and_Undo_takes_them_all_back()
    {
        await using var host = await StartAsync();
        var ids = await host.AddLocationAsync("Shelf B");

        var moved = await host.Session.PostJsonAsync(BulkMove, host.Fields(["Example Game 1", "Lantern & Harbour"], "location:" + ids));
        var undone = await host.Session.PostJsonAsync("/owner/locations?handler=Undo", [Field("revision", host.Revision)]);

        moved.Status.Should().Be(HttpStatusCode.OK);
        moved.JsonText("status").Should().Be("game-moved");
        moved.Json.GetProperty("canUndo").GetBoolean().Should().BeTrue();
        undone.JsonText("outcome").Should().Be("saved");
        host.State.Games.Should().BeEmpty();
        host.State.Changes.Should().ContainSingle().Which.Text.Should().Be("Added location \"Shelf B\"");
    }

    [Fact]
    public async Task One_post_makes_one_revision_and_one_log_line_for_every_game()
    {
        await using var host = await StartAsync();
        var id = await host.AddLocationAsync("Shelf B");
        var revisionBefore = host.State.Revision;
        var changesBefore = host.State.Changes.Count;

        await host.Session.PostJsonAsync(BulkMove, host.Fields(["Example Game 1", "Lantern & Harbour"], "location:" + id));

        host.State.Revision.Should().Be(revisionBefore + 1);
        host.State.Changes.Count.Should().Be(changesBefore + 1);
        host.State.Changes[0].Text.Should().Be("Moved 2 games to Shelf B");
        host.State.Games.Should().HaveCount(2).And.OnlyContain(game => game.LocationId == id);
    }

    [Fact]
    public async Task An_unknown_game_an_unknown_target_and_no_games_are_refused_and_change_nothing()
    {
        await using var host = await StartAsync();
        var id = await host.AddLocationAsync("Shelf B");
        var revision = host.State.Revision;

        var unknownGame = await host.Session.PostJsonAsync(BulkMove, [Field("collectionIds", "1"), Field("collectionIds", host.EntryId("Example Game 1")), Field("target", "location:" + id), Field("revision", host.Revision)]);
        var foreignGame = await host.Session.PostJsonAsync(BulkMove, [Field("collectionIds", host.GameId("Example Game 1")), Field("target", "location:" + id), Field("revision", host.Revision)]);
        var unknownTarget = await host.Session.PostJsonAsync(BulkMove, host.Fields(["Example Game 1"], "location:nope"));
        var garbageTarget = await host.Session.PostJsonAsync(BulkMove, host.Fields(["Example Game 1"], "anything"));
        var none = await host.Session.PostJsonAsync(BulkMove, [Field("target", "not-placed"), Field("revision", host.Revision)]);

        unknownGame.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        unknownGame.JsonText("status").Should().Be("unknown-game");
        foreignGame.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        foreignGame.JsonText("status").Should().Be("unknown-game");
        unknownTarget.JsonText("status").Should().Be("unknown-location");
        garbageTarget.JsonText("status").Should().Be("unknown-location");
        none.JsonText("status").Should().Be("nothing-selected");
        host.State.Revision.Should().Be(revision);
        host.State.Games.Should().BeEmpty();
    }

    [Fact]
    public async Task A_post_without_the_token_is_answered_400_and_changes_nothing()
    {
        await using var host = await StartAsync();
        var id = await host.AddLocationAsync("Shelf B");

        var json = await host.Session.PostJsonAsync(BulkMove, host.Fields(["Example Game 1"], "location:" + id), withToken: false);
        var form = await host.Session.PostFormAsync(BulkMove, host.Fields(["Example Game 1"], "location:" + id), withToken: false);

        json.Status.Should().Be(HttpStatusCode.BadRequest);
        form.Status.Should().Be(HttpStatusCode.BadRequest);
        host.State.Games.Should().BeEmpty();
    }

    [Fact]
    public async Task A_stale_revision_is_refused_as_out_of_date()
    {
        await using var host = await StartAsync();
        var id = await host.AddLocationAsync("Shelf B");

        var stale = await host.Session.PostJsonAsync(BulkMove, [Field("collectionIds", host.EntryId("Example Game 1")), Field("target", "location:" + id), Field("revision", "0")]);

        stale.Status.Should().Be(HttpStatusCode.Conflict);
        stale.JsonText("outcome").Should().Be("outOfDate");
        host.State.Games.Should().BeEmpty();
    }

    [Fact]
    public async Task Same_as_base_game_clears_only_expansions_with_an_owned_base_and_a_form_post_goes_back_to_the_games()
    {
        await using var host = await StartAsync();
        var id = await host.AddLocationAsync("Shelf B");
        var expansion = host.EntryId("Example Game 1: Lantern Extras");
        var baseGame = host.EntryId("Example Game 1");
        await host.Session.PostJsonAsync(BulkMove, host.Fields(["Example Game 1", "Example Game 1: Lantern Extras"], "location:" + id));

        var cleared = await host.Session.PostFormAsync(BulkMove, [Field("collectionIds", baseGame), Field("collectionIds", expansion), Field("target", "same-as-base"), Field("revision", host.Revision)]);

        cleared.Status.Should().Be(HttpStatusCode.Found);
        cleared.Location.Should().Be("/owner/locations?done=saved#games-heading");
        host.State.Games.Select(game => game.CollectionId).Should().Equal(long.Parse(baseGame, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Ticking_one_copy_of_a_game_owned_twice_moves_only_that_copy_and_Undo_takes_it_back()
    {
        await using var host = await StartAsync();
        var shelf = await host.AddLocationAsync("Shelf B");
        var cabinet = await host.AddLocationAsync("Cabinet A");
        var copies = host.EntriesOf("Example Game 1");
        var first = long.Parse(copies[0], CultureInfo.InvariantCulture);
        var second = long.Parse(copies[1], CultureInfo.InvariantCulture);
        await host.Session.PostJsonAsync(BulkMove, [Field("collectionIds", copies[0]), Field("collectionIds", copies[1]), Field("target", "location:" + cabinet), Field("revision", host.Revision)]);

        var moved = await host.Session.PostJsonAsync(BulkMove, [Field("collectionIds", copies[1]), Field("target", "location:" + shelf), Field("revision", host.Revision)]);

        copies.Should().HaveCount(2);
        moved.Status.Should().Be(HttpStatusCode.OK);
        moved.JsonText("status").Should().Be("game-moved");
        host.State.Games.Select(game => (game.CollectionId, game.LocationId)).Should().Equal((first, cabinet), (second, shelf));

        var undone = await host.Session.PostJsonAsync("/owner/locations?handler=Undo", [Field("revision", host.Revision)]);

        undone.JsonText("outcome").Should().Be("saved");
        host.State.Games.Select(game => (game.CollectionId, game.LocationId)).Should().Equal((first, cabinet), (second, cabinet));
    }

    [Fact]
    public async Task A_decision_for_every_copy_is_split_so_the_unticked_copy_keeps_its_place()
    {
        await using var host = await StartAsync();
        var shelf = await host.AddLocationAsync("Shelf B");
        var cabinet = await host.AddLocationAsync("Cabinet A");
        var copies = host.EntriesOf("Example Game 1");
        var assigned = await host.Owner.MutateAsync(
            host.Owner.Revision,
            data => LocationEdits.Assign(data, int.Parse(host.GameId("Example Game 1"), CultureInfo.InvariantCulture), cabinet, "Example Game 1"),
            TestContext.Current.CancellationToken);

        var moved = await host.Session.PostJsonAsync(BulkMove, [Field("collectionIds", copies[1]), Field("target", "location:" + shelf), Field("revision", host.Revision)]);

        assigned.Outcome.Should().Be(OwnerOutcome.Saved);
        moved.JsonText("status").Should().Be("game-moved");
        host.State.Games.Select(game => (game.CollectionId, game.LocationId)).Should().Equal(
            (long.Parse(copies[0], CultureInfo.InvariantCulture), cabinet),
            (long.Parse(copies[1], CultureInfo.InvariantCulture), shelf));
    }

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    private static async Task<BulkHost> StartAsync()
    {
        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)), clock);
        var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);

        var host = new BulkHost(factory, client);
        await host.ReadCardsAsync();

        return host;
    }

    private sealed class BulkHost(CabinetWebApplicationFactory factory, HttpClient client) : IAsyncDisposable
    {
        private readonly Dictionary<string, string> _gameIds = [];
        private readonly Dictionary<string, List<string>> _entryIds = [];

        public OwnerSession Session { get; } = new(factory);

        public string Revision => Service.Revision.ToString(CultureInfo.InvariantCulture);

        public OwnerDataService Owner => Service;

        public OwnerData State => Service.State is OwnerDataState.Ready ready ? ready.Data : OwnerData.Empty;

        private OwnerDataService Service => factory.ServingServices.GetRequiredService<OwnerDataService>();

        public async Task ReadCardsAsync()
        {
            using var response = await client.GetAsync("/cabinet/cards?profile=desktop", TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            foreach (var card in document.RootElement.GetProperty("cards").EnumerateArray())
            {
                var title = card.GetProperty("title").GetString()!;

                _gameIds.TryAdd(title, card.GetProperty("gameId").GetInt32().ToString(CultureInfo.InvariantCulture));

                if (!_entryIds.TryGetValue(title, out var entries))
                {
                    _entryIds[title] = entries = [];
                }

                entries.Add(card.GetProperty("entryId").GetInt64().ToString(CultureInfo.InvariantCulture));
            }

            foreach (var entries in _entryIds.Values)
            {
                entries.Sort((left, right) => long.Parse(left, CultureInfo.InvariantCulture).CompareTo(long.Parse(right, CultureInfo.InvariantCulture)));
            }
        }

        public string GameId(string title) => _gameIds[title];

        public string EntryId(string title) => _entryIds[title][0];

        public IReadOnlyList<string> EntriesOf(string title) => _entryIds[title];

        public async Task<string> AddLocationAsync(string name)
        {
            var added = await Session.PostJsonAsync("/owner/locations?handler=AddLocation", [Field("name", name), Field("revision", Revision)]);

            return added.JsonText("id");
        }

        public List<KeyValuePair<string, string>> Fields(string[] titles, string target) =>
        [
            .. titles.Select(title => Field("collectionIds", EntryId(title))),
            Field("target", target),
            Field("revision", Revision),
        ];

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            client.Dispose();
            await factory.DisposeAsync();
        }
    }
}
