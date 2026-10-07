using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Proves the details plan: new games first, then the longest-known, in calls of at most twenty, within the run's limits.</summary>
[Trait("Category", "Enrichment")]
public sealed class EnrichmentPlannerTests
{
    private static readonly DateTimeOffset Now = new(2030, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(40, 2)]
    [InlineData(41, 3)]
    public void Games_without_details_go_in_calls_of_at_most_twenty(int count, int expectedCalls)
    {
        var plan = EnrichmentPlanner.Plan(Items(count), null, Now, EnrichmentOptions.Default);

        plan.Should().HaveCount(expectedCalls);
        plan.Should().OnlyContain(batch => batch.Count >= 1 && batch.Count <= EnrichmentPlanner.MaxIdsPerRequest);
        plan.SelectMany(batch => batch).Should().Equal(Enumerable.Range(1, count));
    }

    [Fact]
    public void An_empty_collection_plans_no_call()
    {
        EnrichmentPlanner.Plan([], null, Now, EnrichmentOptions.Default).Should().BeEmpty();
    }

    [Fact]
    public void A_game_exactly_one_refresh_period_old_is_due_and_one_a_minute_younger_is_not()
    {
        var known = new Dictionary<int, GameDetails>
        {
            [1] = Details(Now - TimeSpan.FromDays(7)),
            [2] = Details(Now - TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1)),
        };

        var plan = EnrichmentPlanner.Plan(Items(2), known, Now, EnrichmentOptions.Default);

        plan.Should().ContainSingle().Which.Should().Equal(1);
    }

    [Fact]
    public void New_games_come_before_old_ones_and_old_ones_are_ordered_by_age_then_game_id()
    {
        var known = new Dictionary<int, GameDetails>
        {
            [1] = Details(Now - TimeSpan.FromDays(9)),
            [2] = Details(Now - TimeSpan.FromDays(30)),
            [3] = Details(Now - TimeSpan.FromDays(9)),
        };
        var options = EnrichmentOptions.Default with { RefreshBatchesPerRun = 5 };

        var plan = EnrichmentPlanner.Plan(Items(5), known, Now, options);

        plan.Should().HaveCount(2);
        plan[0].Should().Equal(4, 5);
        plan[1].Should().Equal(2, 1, 3);
    }

    [Fact]
    public void A_run_refreshes_at_most_one_call_of_old_games_by_default()
    {
        var known = Enumerable.Range(1, 50).ToDictionary(id => id, id => Details(Now - TimeSpan.FromDays(10) - TimeSpan.FromMinutes(id)));

        var plan = EnrichmentPlanner.Plan(Items(50), known, Now, EnrichmentOptions.Default);

        plan.Should().ContainSingle();
        plan[0].Should().HaveCount(20).And.Equal(Enumerable.Range(1, 50).OrderByDescending(id => id).Take(20));
    }

    [Fact]
    public void The_total_number_of_calls_never_passes_the_limit_of_the_run()
    {
        var options = EnrichmentOptions.Default with { MaxThingRequestsPerRun = 2 };

        EnrichmentPlanner.Plan(Items(50), null, Now, options).Should().HaveCount(2);
    }

    [Fact]
    public void Games_follow_collection_order_and_a_game_owned_twice_is_asked_for_once()
    {
        var items = new[] { Item(30, 9), Item(10, 5), Item(20, 5), Item(40, 9) };

        var plan = EnrichmentPlanner.Plan(items, null, Now, EnrichmentOptions.Default);

        plan.Should().ContainSingle().Which.Should().Equal(5, 9);
    }

    [Fact]
    public void Equal_inputs_give_equal_plans()
    {
        var known = Enumerable.Range(1, 30).ToDictionary(id => id, id => Details(Now - TimeSpan.FromDays(8)));

        var first = EnrichmentPlanner.Plan(Items(60), known, Now, EnrichmentOptions.Default);
        var second = EnrichmentPlanner.Plan(Items(60), known, Now, EnrichmentOptions.Default);

        first.Select(batch => batch.ToArray()).Should().BeEquivalentTo(second.Select(batch => batch.ToArray()), options => options.WithStrictOrdering());
    }

    private static SnapshotItem[] Items(int count) => [.. Enumerable.Range(1, count).Select(id => Item(id, id))];

    private static SnapshotItem Item(long collectionId, int gameId) =>
        new(collectionId, gameId, $"Example {gameId}", ItemKind.Base, null, null, null);

    private static GameDetails Details(DateTimeOffset at) =>
        new(at, null, null, null, null, null, null, null, null, null, [], [], [], null);
}
