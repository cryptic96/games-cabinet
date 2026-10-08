using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves, through a booted host with scripted answers, that a base game with two owned expansions faces out in the served
/// cabinet whatever the cover share, and that the other base games do not.
/// </summary>
[Trait("Category", "Sync")]
public sealed class FamilyCoverTests
{
    private const string LayoutPath = "/cabinet/layout?profile=desktop";
    private const int BaseCount = 14;
    private const int FamilyIndex = 6;
    private const int ExpansionCount = 2;

    private static readonly Dictionary<string, string?> NoCoversByChance = new()
    {
        ["Layout:CoverSharePercent"] = "0",
        ["Layout:FewGamesThreshold"] = "0",
    };

    [Fact]
    public async Task A_base_game_with_two_owned_expansions_faces_out_in_the_served_cabinet()
    {
        var items = Collection();
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock, NoCoversByChance);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        using var document = JsonDocument.Parse(await client.GetStringAsync(LayoutPath, TestContext.Current.CancellationToken));
        var placements = Placements(document).ToList();
        var family = items[FamilyIndex];
        var kinds = placements
            .Where(placement => placement.GetProperty("kind").GetString() is "cover" or "spine" or "flatBox")
            .ToDictionary(
            placement => placement.GetProperty("entryId").GetInt64(),
            placement => placement.GetProperty("kind").GetString(),
            EqualityComparer<long>.Default);

        kinds[family.CollId].Should().Be("cover");

        foreach (var other in items.Where(item => !item.IsExpansion && item.CollId != family.CollId))
        {
            kinds[other.CollId].Should().NotBe("cover", "a base game with fewer than two owned expansions follows the share, which is zero");
        }

        placements.Count(placement => placement.GetProperty("kind").GetString() is "expansionSpine" or "expansionLayer")
            .Should().Be(ExpansionCount, "both expansions stand beside their base game");
    }

    private static List<FakeBggItem> Collection()
    {
        var items = new List<FakeBggItem>();

        for (var index = 0; index < BaseCount; index++)
        {
            items.Add(new FakeBggItem(
                SyntheticBggCollection.FirstObjectId + index,
                SyntheticBggCollection.FirstCollId + index,
                $"Invented Cover Rule Game {index + 1}",
                IsExpansion: false,
                Owned: true,
                Year: 2000 + index,
                Version: new FakeVersion(7.0 + (index % 3), 9.0 + (index % 4), 1.5 + (index % 3) * 0.5),
                Location: null));
        }

        var familyId = items[FamilyIndex].ObjectId;

        for (var number = 0; number < ExpansionCount; number++)
        {
            items.Add(new FakeBggItem(
                SyntheticBggCollection.FirstObjectId + BaseCount + number,
                SyntheticBggCollection.FirstCollId + BaseCount + number,
                $"Invented Cover Rule Extra {number + 1}",
                IsExpansion: true,
                Owned: true,
                Year: 2010,
                Version: new FakeVersion(7.0, 9.0, 0.8),
                Location: null,
                BaseObjectId: familyId));
        }

        return items;
    }

    private static IEnumerable<JsonElement> Placements(JsonDocument document) =>
        document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray());
}
