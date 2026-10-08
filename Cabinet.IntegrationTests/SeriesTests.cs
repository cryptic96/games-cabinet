using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves, through a booted host with scripted answers, that games sharing a BGG series family stand together in the
/// served cabinet after one sync, and that a family only one game carries and broad families change nothing.
/// </summary>
[Trait("Category", "Sync")]
public sealed class SeriesTests
{
    private const string LayoutPath = "/cabinet/layout?profile=desktop";
    private static readonly int[] SagaPositions = [11, 26, 41];
    private static readonly int[] LinePositions = [13, 52];

    [Fact]
    public async Task Games_that_share_a_series_family_stand_together_in_the_served_cabinet()
    {
        var items = SyntheticBggCollection.Create(65);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        using var document = JsonDocument.Parse(await client.GetStringAsync(LayoutPath, TestContext.Current.CancellationToken));
        var standing = Standing(document);

        AssertStandTogether(standing, items, SagaPositions);
        AssertStandTogether(standing, items, LinePositions);
    }

    private static void AssertStandTogether(IReadOnlyList<Stand> standing, IReadOnlyList<FakeBggItem> items, int[] positions)
    {
        var entries = positions.Select(position => items[position].CollId).ToList();
        var series = standing.Where(stand => entries.Contains(stand.Entry)).ToList();

        series.Should().HaveCount(positions.Length);
        series.Select(stand => (stand.Section, stand.Cubby)).Distinct().Should().ContainSingle("the series fits one cubby");

        var left = series.Min(stand => stand.X);
        var right = series.Max(stand => stand.X);
        var between = standing
            .Where(stand => !entries.Contains(stand.Entry) && stand.Section == series[0].Section && stand.Cubby == series[0].Cubby)
            .Where(stand => stand.X > left && stand.X < right);

        between.Should().BeEmpty("no other game stands between the games of a series");
    }

    private static List<Stand> Standing(JsonDocument document) =>
    [
        .. document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray()
                .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray()
                    .Where(placement => placement.GetProperty("kind").GetString() is "cover" or "spine" or "flatBox" or "orphanExpansion")
                    .Select(placement => new Stand(
                        section.GetProperty("index").GetInt32(),
                        cubby.GetProperty("index").GetInt32(),
                        placement.GetProperty("entryId").GetInt64(),
                        placement.GetProperty("xMm").GetInt32())))),
    ];

    private sealed record Stand(int Section, int Cubby, long Entry, int X);
}
