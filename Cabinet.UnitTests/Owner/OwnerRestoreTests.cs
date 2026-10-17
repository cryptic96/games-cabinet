using Cabinet.Domain.Owner;
using Cabinet.Service.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves what restoring a checked file does to the stored owner data: the data that was there is kept as an earlier version
/// first, the file's data replaces it under a higher revision with an uncounted log line, games the collection does not list
/// start their thirty days at the restore, the chance to undo ends, and the pictures that were linked are asked for again.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerRestoreTests
{
    private const string CabinetA = "Cabinet A";
    private const string ShelfB = "Shelf B";
    private const string FileLocationA = "0a1b2c3d";
    private const string FileLocationB = "4e5f6a7b";
    private const string LinkAddress = "https://example.org/pictures/linked.png";

    [Fact]
    public void The_summary_of_a_file_names_each_count_and_reads_in_the_singular_for_one()
    {
        OwnerStrings.RestoreSummary(1, 1, 1).Should().Be("1 location, 1 placed game, 1 picture choice");
        OwnerStrings.RestoreSummary(12, 41, 9).Should().Be("12 locations, 41 placed games, 9 picture choices");
        OwnerStrings.RestoreSummary(0, 0, 0).Should().Be("0 locations, 0 placed games, 0 picture choices");
    }

    [Fact]
    public void The_line_about_pictures_being_fetched_again_reads_in_the_singular_for_one()
    {
        OwnerStrings.FetchingPictures(1).Should().Be("Fetching 1 picture in the background.");
        OwnerStrings.FetchingPictures(3).Should().Be("Fetching 3 pictures in the background.");
    }

    [Fact]
    public void The_restored_line_holds_the_summary_and_says_the_previous_data_is_kept()
    {
        OwnerStrings.RestoreDone("2 locations, 2 placed games, 2 picture choices")
            .Should().Be("Restored. 2 locations, 2 placed games, 2 picture choices. Your previous data is kept as an earlier version.");
    }

    [Fact]
    public async Task A_restore_keeps_the_data_that_was_there_as_an_earlier_version_before_it_replaces_it()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Listing(100, 101));
        await harness.AddAsync(CabinetA);

        var result = await harness.Service.RestoreAsync(FileData(), RestoreSource.File, new HashSet<int> { 100, 101 }, new Dictionary<long, int>(), CancellationToken.None);

        result.Outcome.Should().Be(OwnerOutcome.Saved);
        harness.Store.KeptAt.Should().ContainSingle().Which.Should().Be(harness.Clock.GetUtcNow());
        harness.Data.Locations.Select(location => location.Name).Should().Equal(CabinetA, ShelfB);
    }

    [Fact]
    public async Task A_restore_keeps_an_earlier_version_even_when_a_recent_one_was_just_kept()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Listing(100, 101));
        await harness.AddAsync(CabinetA);
        await harness.AddAsync(ShelfB);
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        var kept = harness.Store.KeptAt.Count;

        await harness.Service.RestoreAsync(FileData(), RestoreSource.File, new HashSet<int> { 100, 101 }, new Dictionary<long, int>(), CancellationToken.None);

        harness.Store.KeptAt.Should().HaveCount(kept + 1);
    }

    [Fact]
    public async Task A_restore_over_data_that_cannot_be_read_still_keeps_the_file_as_an_earlier_version_and_replaces_it()
    {
        var store = new InMemoryOwnerDataStore { State = new OwnerDataState.Unreadable("malformed"), FileExists = true };
        var harness = OwnerTestKit.Harness(store);

        var result = await harness.Service.RestoreAsync(FileData(), RestoreSource.File, null, null, CancellationToken.None);

        result.Outcome.Should().Be(OwnerOutcome.Saved);
        store.KeptAt.Should().ContainSingle();
        harness.Service.State.Should().BeOfType<OwnerDataState.Ready>();
        harness.Data.Locations.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_restored_data_has_a_revision_higher_than_the_data_it_replaced_and_than_the_file()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Listing(100, 101));
        await harness.AddAsync(CabinetA);
        await harness.AddAsync(ShelfB);
        await harness.AddAsync("Crate C");
        var current = harness.Service.Revision;

        await harness.Service.RestoreAsync(FileData() with { Revision = 10 }, RestoreSource.File, null, null, CancellationToken.None);
        var afterLarger = harness.Service.Revision;
        await harness.Service.RestoreAsync(FileData() with { Revision = 1 }, RestoreSource.File, null, null, CancellationToken.None);

        current.Should().BeLessThan(10);
        afterLarger.Should().Be(11);
        harness.Service.Revision.Should().Be(12);
    }

    [Fact]
    public async Task A_restore_adds_a_log_line_that_does_not_count_toward_the_banner_and_keeps_the_files_own_log()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Listing(100, 101));
        var file = FileData() with
        {
            ChangesSinceDownload = 0,
            LastDownloadAtUtc = OwnerTestKit.Start.AddDays(-1),
            Changes = [new ChangeEntry(OwnerTestKit.Start.AddDays(-2), "Added location \"Cabinet A\"")],
        };

        await harness.Service.RestoreAsync(file, RestoreSource.File, null, null, CancellationToken.None);

        harness.Data.Changes.Select(change => change.Text).Should().Equal("Restored from file", "Added location \"Cabinet A\"");
        harness.Data.Changes[0].AtUtc.Should().Be(harness.Clock.GetUtcNow());
        harness.Data.ChangesSinceDownload.Should().Be(0);
        harness.Data.LastDownloadAtUtc.Should().Be(OwnerTestKit.Start.AddDays(-1));
        harness.Service.Backup.ShowsBanner.Should().BeFalse();
    }

    [Fact]
    public async Task A_restore_from_an_earlier_version_says_so_in_the_log()
    {
        var harness = OwnerTestKit.Harness();

        await harness.Service.RestoreAsync(FileData(), RestoreSource.EarlierVersion, null, null, CancellationToken.None);

        harness.Data.Changes[0].Text.Should().Be("Restored an earlier version");
    }

    [Fact]
    public async Task The_log_of_a_restored_file_is_still_kept_to_its_cap_with_the_new_line_first()
    {
        var harness = OwnerTestKit.Harness();
        var fullLog = Enumerable.Range(0, OwnerData.MaxStoredChanges)
            .Select(index => new ChangeEntry(OwnerTestKit.Start.AddMinutes(-index), $"Change {index}"))
            .ToList();

        await harness.Service.RestoreAsync(FileData() with { Changes = fullLog }, RestoreSource.File, null, null, CancellationToken.None);

        harness.Data.Changes.Should().HaveCount(OwnerData.MaxStoredChanges);
        harness.Data.Changes[0].Text.Should().Be("Restored from file");
        harness.Data.Changes[1].Text.Should().Be("Change 0");
    }

    [Fact]
    public async Task A_game_in_the_file_that_the_collection_does_not_list_starts_its_thirty_days_at_the_restore()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Listing(100));
        var file = FileData() with
        {
            Games =
            [
                new OwnerGame(100, FileLocationA, OwnerTestKit.Start.AddDays(-40), null),
                new OwnerGame(101, FileLocationB, null, null),
            ],
        };

        await harness.Service.RestoreAsync(file, RestoreSource.File, new HashSet<int> { 100 }, new Dictionary<long, int>(), CancellationToken.None);

        harness.Data.Games.Single(game => game.GameId == 100).AbsentSinceUtc.Should().BeNull();
        harness.Data.Games.Single(game => game.GameId == 101).AbsentSinceUtc.Should().Be(harness.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Without_a_collection_yet_no_game_is_stamped_as_missing()
    {
        var harness = OwnerTestKit.Harness();

        await harness.Service.RestoreAsync(FileData(), RestoreSource.File, null, null, CancellationToken.None);

        harness.Data.Games.Should().OnlyContain(game => game.AbsentSinceUtc == null);
    }

    [Fact]
    public async Task A_restore_ends_the_chance_to_undo_asks_for_the_links_to_be_fetched_and_announces_the_change()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Listing(100, 101));
        await harness.AddAsync(CabinetA);
        harness.Service.UndoableRevision.Should().NotBeNull();
        var asked = 0;
        var announced = 0;
        harness.Service.LinkWorkRequested += () => asked++;
        harness.Service.Changed += () => announced++;

        await harness.Service.RestoreAsync(FileData(), RestoreSource.File, null, null, CancellationToken.None);

        harness.Service.UndoableRevision.Should().BeNull();
        asked.Should().Be(1);
        announced.Should().Be(1);
    }

    [Fact]
    public async Task A_restore_that_cannot_be_stored_changes_nothing_and_says_so()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Listing(100, 101));
        await harness.AddAsync(CabinetA);
        var before = harness.Data;
        harness.Store.FailNextSave = true;

        var result = await harness.Service.RestoreAsync(FileData(), RestoreSource.File, null, null, CancellationToken.None);

        result.Outcome.Should().Be(OwnerOutcome.Refused);
        result.StatusKey.Should().Be("save-failed");
        harness.Data.Should().BeSameAs(before);
    }

    private static OwnerData FileData() => OwnerData.Empty with
    {
        Revision = 3,
        SavedAtUtc = OwnerTestKit.Start.AddDays(-1),
        Locations = [new OwnerLocation(FileLocationA, CabinetA), new OwnerLocation(FileLocationB, ShelfB)],
        Games =
        [
            new OwnerGame(100, FileLocationA, null, null),
            new OwnerGame(101, FileLocationB, null, new PictureSetting(PictureChoice.Link, true, null, null, new PictureLink(LinkAddress, null, 0, null))),
        ],
    };
}
