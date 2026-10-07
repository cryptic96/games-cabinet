using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Collection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves, through a booted host with scripted answers, that a flat cover gives a game without sizes its shape, and that
/// real boxes in the cabinet differ in size and shape.
/// </summary>
[Trait("Category", "Sync")]
public sealed class TrueProportionsTests
{
    private const long QuietQuarryEntry = SyntheticBggCollection.FirstCollId + 8;
    private const string LayoutPath = "/cabinet/layout?profile=desktop";
    private const string ImageHost = "https://example.org/images/";
    private const int DistinctWanted = 4;
    private const double RatioStep = 0.05;
    private const double LandscapeRatio = 900.0 / 800.0;
    private const double RatioTolerance = 0.02;

    private static readonly Dictionary<string, string?> AllowExampleHost = new()
    {
        ["Images:AllowedHosts"] = "example.org",
        ["Images:MaxDownloadsPerRun"] = "1000",
        ["Layout:FewGamesThreshold"] = "100",
    };

    [Fact]
    public async Task A_game_without_sizes_and_with_a_flat_landscape_cover_is_drawn_in_the_covers_shape_with_no_bars()
    {
        var items = SyntheticBggCollection.Create(65);
        var quiet = items.Single(item => item.CollId == QuietQuarryEntry);
        var images = new ScriptedImageHandler().Serve($"{ImageHost}{quiet.ObjectId}.jpg", SyntheticArt.Encode(SyntheticArtKind.FlatWide));
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock, AllowExampleHost, images);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        using var document = JsonDocument.Parse(await client.GetStringAsync(LayoutPath, TestContext.Current.CancellationToken));
        var placement = Placements(document).Single(candidate => candidate.GetProperty("entryId").GetInt64() == QuietQuarryEntry);

        placement.GetProperty("kind").GetString().Should().Be("cover");
        ((double)placement.GetProperty("widthMm").GetInt32() / placement.GetProperty("heightMm").GetInt32())
            .Should().BeApproximately(LandscapeRatio, LandscapeRatio * RatioTolerance);
        placement.GetProperty("art").GetProperty("fit").GetString().Should().Be("exact");
    }

    [Fact]
    public async Task Real_boxes_differ_in_shape_and_height_and_come_from_real_sizes_and_estimates()
    {
        var items = SyntheticBggCollection.Create(65);
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock);
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var state = factory.ServingServices.GetRequiredService<CollectionStore>().Current;
        var snapshot = state.Snapshot!;
        var sources = snapshot.Items
            .Select(item => BoxShape.Resolve(item, snapshot.Games!.GetValueOrDefault(item.GameId), null, ArtRules.Default).Source)
            .ToHashSet();

        state.Items.Select(item => Math.Round((double)item.Box.WidthMm / item.Box.HeightMm / RatioStep)).Distinct()
            .Should().HaveCountGreaterThanOrEqualTo(DistinctWanted);
        state.Items.Select(item => item.Box.HeightMm).Distinct().Should().HaveCountGreaterThanOrEqualTo(DistinctWanted);
        sources.Should().Contain(BoxSource.RealSize).And.Contain(BoxSource.Estimate);
    }

    private static IEnumerable<JsonElement> Placements(JsonDocument document) =>
        document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray());
}
