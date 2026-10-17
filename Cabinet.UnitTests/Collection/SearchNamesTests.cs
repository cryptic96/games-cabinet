using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Proves the names index lists only the other titles of owned games, drops what the search does not need, and carries nothing else.</summary>
[Trait("Category", "Search")]
public sealed class SearchNamesTests
{
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Only_owned_games_appear()
    {
        var snapshot = Snapshot(
            [Item(1, 100, "Quiet Quarry")],
            (100, Details(["Stille Groeve"])),
            (200, Details(["Not Owned Alias"])));

        var document = SearchNames.Build(snapshot);

        document.Names.Keys.Should().Equal("100");
        document.Names["100"].Should().Equal("Stille Groeve");
    }

    [Fact]
    public void A_name_equal_to_the_title_is_dropped_and_the_order_of_the_rest_is_kept()
    {
        var snapshot = Snapshot(
            [Item(1, 100, "Quiet Quarry")],
            (100, Details(["Zeta Name", "Quiet Quarry", "Alpha Name"])));

        SearchNames.Build(snapshot).Names["100"].Should().Equal("Zeta Name", "Alpha Name");
    }

    [Fact]
    public void A_name_equal_to_the_title_of_any_copy_is_dropped_and_a_different_case_is_kept()
    {
        var snapshot = Snapshot(
            [Item(1, 100, "Quiet Quarry"), Item(2, 100, "Quiet Quarry (Second Edition)")],
            (100, Details(["Quiet Quarry (Second Edition)", "quiet quarry", "Stille Groeve"])));

        SearchNames.Build(snapshot).Names["100"].Should().Equal("quiet quarry", "Stille Groeve");
    }

    [Fact]
    public void A_game_with_no_names_or_only_its_own_title_is_left_out()
    {
        var snapshot = Snapshot(
            [Item(1, 100, "Quiet Quarry"), Item(2, 101, "Salt Road"), Item(3, 102, "Lantern Harbour"), Item(4, 103, "Not Read Yet")],
            (100, Details(null)),
            (101, Details(["Salt Road"])),
            (102, Details([])));

        SearchNames.Build(snapshot).Names.Should().BeEmpty();
    }

    [Fact]
    public void A_collection_with_no_details_or_no_snapshot_gives_an_empty_document()
    {
        SearchNames.Build(null).Names.Should().BeEmpty();
        SearchNames.Build(new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, Moment, [Item(1, 100, "Quiet Quarry")])).Names.Should().BeEmpty();
        SearchNames.Build(Snapshot([], (100, Details(["Stille Groeve"])))).Names.Should().BeEmpty();
        SearchNames.Serialize(SearchNamesDocument.Empty).Should().Be("{\"names\":{}}");
    }

    [Fact]
    public void Games_come_in_identifier_order_whatever_order_they_were_stored_in()
    {
        var snapshot = Snapshot(
            [Item(1, 300, "Third"), Item(2, 20, "Second"), Item(3, 4, "First")],
            (300, Details(["C Name"])),
            (20, Details(["B Name"])),
            (4, Details(["A Name"])));

        SearchNames.Build(snapshot).Names.Keys.Should().Equal("4", "20", "300");
    }

    [Fact]
    public void No_other_field_of_the_details_appears_in_the_json()
    {
        var details = Details(["Stille Groeve"]) with
        {
            Designers = ["Invented Designer"],
            Mechanics = ["Invented Mechanic"],
            MainImageUrl = "https://example.com/original.jpg",
            Families = [new FamilyLink(77, "Series: Invented Lines")],
        };
        var snapshot = Snapshot([Item(1, 100, "Quiet Quarry")], (100, details));

        var json = SearchNames.Serialize(SearchNames.Build(snapshot));

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(member => member.Name).Should().Equal("names");
        document.RootElement.GetProperty("names").EnumerateObject().Select(member => member.Name).Should().Equal("100");
        document.RootElement.GetProperty("names").GetProperty("100").EnumerateArray().Select(name => name.GetString()).Should().Equal("Stille Groeve");
        json.Should().NotContain("Invented").And.NotContain("example.com").And.NotContain("Quiet Quarry");
    }

    [Fact]
    public void The_same_names_always_give_the_same_bytes()
    {
        var snapshot = Snapshot([Item(1, 100, "Quiet Quarry"), Item(2, 101, "Salt Road")], (100, Details(["A", "B"])), (101, Details(["C"])));

        SearchNames.Serialize(SearchNames.Build(snapshot)).Should().Be(SearchNames.Serialize(SearchNames.Build(snapshot)));
    }

    private static SnapshotItem Item(long entryId, int gameId, string title) =>
        new(entryId, gameId, title, ItemKind.Base, 2012, null, null);

    private static GameDetails Details(IReadOnlyList<string>? alternateNames) =>
        new(Moment, 2, 4, 60, null, null, 10, 2.4, 7.1, 6.9, [], [], [], null, AlternateNames: alternateNames);

    private static CollectionSnapshot Snapshot(IReadOnlyList<SnapshotItem> items, params (int GameId, GameDetails Details)[] games) =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            items,
            null,
            games.ToDictionary(entry => entry.GameId, entry => entry.Details));
}
