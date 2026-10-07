using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Collection;

/// <summary>Proves size classes come from weight, play time and players, stay put under small drift and move on real change.</summary>
[Trait("Category", "Snapshot")]
public sealed class SizeEstimateTests
{
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1.0, 15, 2, BoxSizeClass.Compact)]
    [InlineData(1.2, 20, 6, BoxSizeClass.Small)]
    [InlineData(2.5, 90, 4, BoxSizeClass.Standard)]
    [InlineData(4.0, 180, 4, BoxSizeClass.Large)]
    [InlineData(4.2, 240, 5, BoxSizeClass.ExtraLarge)]
    public void Weight_play_time_and_players_give_the_class(double weight, int minutes, int players, BoxSizeClass expected)
    {
        SizeEstimate.ClassFor(SizeEstimate.Score(Details(weight, minutes, players))).Should().Be(expected);
        SizeEstimate.Assign(Details(weight, minutes, players), null, null).Should().Be(expected);
    }

    [Fact]
    public void A_game_with_nothing_known_counts_as_the_middle_of_every_band_and_is_standard()
    {
        var nothing = Details(null, null, null);

        SizeEstimate.Score(nothing).Should().Be(4);
        SizeEstimate.Assign(nothing, null, null).Should().Be(BoxSizeClass.Standard);
        SizeEstimate.Assign(null, null, null).Should().BeNull();
    }

    [Fact]
    public void The_play_time_falls_back_from_the_stated_time_to_the_longest_and_then_the_shortest()
    {
        var stated = Details(2.0, null, 4) with { PlayingTime = 90, MaxPlayTime = 10, MinPlayTime = 10 };
        var longest = Details(2.0, null, 4) with { MaxPlayTime = 90, MinPlayTime = 10 };
        var shortest = Details(2.0, null, 4) with { MinPlayTime = 90 };

        SizeEstimate.Score(longest).Should().Be(SizeEstimate.Score(stated));
        SizeEstimate.Score(shortest).Should().Be(SizeEstimate.Score(stated));
    }

    [Fact]
    public void Weight_drifting_from_2_74_to_2_76_keeps_the_stored_class()
    {
        var stored = SizeEstimate.Assign(Details(2.74, 59, 2), null, null);

        SizeEstimate.Assign(Details(2.76, 59, 2), stored, SizeEstimate.ModelVersion).Should().Be(stored);
    }

    [Fact]
    public void Play_time_drifting_from_59_to_61_minutes_keeps_the_stored_class_where_a_fresh_one_would_move()
    {
        var stored = SizeEstimate.Assign(Details(2.74, 59, 2), null, null);

        stored.Should().Be(BoxSizeClass.Small);
        SizeEstimate.Assign(Details(2.74, 61, 2), null, null).Should().Be(BoxSizeClass.Standard);
        SizeEstimate.Assign(Details(2.74, 61, 2), stored, SizeEstimate.ModelVersion).Should().Be(BoxSizeClass.Small);
    }

    [Fact]
    public void Both_drifts_at_once_keep_the_stored_class()
    {
        var stored = SizeEstimate.Assign(Details(2.49, 59, 2), null, null);

        SizeEstimate.Assign(Details(2.51, 61, 2), stored, SizeEstimate.ModelVersion).Should().Be(stored);
    }

    [Fact]
    public void A_real_change_moves_the_class()
    {
        var stored = SizeEstimate.Assign(Details(1.2, 20, 2), null, null);

        SizeEstimate.Assign(Details(3.8, 150, 4), stored, SizeEstimate.ModelVersion).Should().Be(BoxSizeClass.Large);
    }

    [Fact]
    public void A_stored_class_of_another_model_version_is_worked_out_again_without_hysteresis()
    {
        var drifted = Details(2.74, 61, 2);

        SizeEstimate.Assign(drifted, BoxSizeClass.Small, SizeEstimate.ModelVersion + 1).Should().Be(BoxSizeClass.Standard);
        SizeEstimate.Assign(drifted, BoxSizeClass.Small, null).Should().Be(BoxSizeClass.Standard);
    }

    [Fact]
    public void Expansions_use_the_smaller_expansion_dimensions_of_the_same_class_and_an_unknown_weight_is_standard()
    {
        foreach (var sizeClass in Enum.GetValues<BoxSizeClass>())
        {
            var expansion = SizeEstimate.Dimensions(sizeClass, ItemKind.Expansion);
            var game = SizeEstimate.Dimensions(sizeClass, ItemKind.Base);

            ((long)expansion.WidthMm * expansion.HeightMm * expansion.DepthMm).Should().BeLessThan((long)game.WidthMm * game.HeightMm * game.DepthMm);
        }

        SizeEstimate.Dimensions(BoxSizeClass.Standard, ItemKind.Expansion).Should().Be(new BoxDimensions(200, 260, 40));
        SizeEstimate.Assign(Details(0, null, null), null, null).Should().Be(BoxSizeClass.Standard);
    }

    [Fact]
    public async Task The_details_step_stores_the_class_and_a_refresh_with_drifted_inputs_leaves_it_unchanged()
    {
        var clock = new FakeTimeProvider(Moment);
        var source = new SwitchingSource(Details(2.74, 59, 2));
        var sync = new EnrichmentSync(() => source, new EnrichmentOptions(5, 1, TimeSpan.FromDays(1)), clock, NullLogger<EnrichmentSync>.Instance);
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [new SnapshotItem(1, 7, "Invented Game", ItemKind.Base, null, null, null)]);
        var latest = snapshot;

        await sync.RunAsync(snapshot, Moment.AddHours(1), next => { latest = next; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

        var first = latest.Games![7];
        first.EstimatedSize.Should().Be(BoxSizeClass.Small);
        first.EstimateModelVersion.Should().Be(SizeEstimate.ModelVersion);

        clock.Advance(TimeSpan.FromDays(2));
        source.Details = Details(2.76, 61, 2);
        await sync.RunAsync(latest, clock.GetUtcNow().AddHours(1), next => { latest = next; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

        latest.Games![7].PlayingTime.Should().Be(61);
        latest.Games![7].EstimatedSize.Should().Be(BoxSizeClass.Small);
    }

    private static GameDetails Details(double? weight, int? minutes, int? players) =>
        new(Moment, 1, players, minutes, null, null, null, weight, null, null, [], [], [], null);

    private sealed class SwitchingSource(GameDetails initial) : IEnrichmentSource
    {
        public GameDetails Details { get; set; } = initial;

        public Task<EnrichmentFetchResult> FetchDetailsAsync(IReadOnlyList<int> gameIds, CancellationToken cancellationToken) =>
            Task.FromResult<EnrichmentFetchResult>(new EnrichmentFetchResult.Fetched(gameIds.ToDictionary(id => id, _ => Details)));
    }
}
