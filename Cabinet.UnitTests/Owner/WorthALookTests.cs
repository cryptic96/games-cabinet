using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves the rule that says which games the pictures page opens on: the four reasons a game is worth a look, when the owner's
/// decision resolves it and when a change on BGG or a dead link brings it back, the marks and the why sentence of every row, the
/// order of the worklist and the counts, and the edit that records that a game looks right.
/// </summary>
[Trait("Category", "Owner")]
public sealed class WorthALookTests
{
    private const string OwnedUrl = "https://cf.example.org/owned.jpg";
    private const string OtherOwnedUrl = "https://cf.example.org/owned-new.jpg";
    private const string MainUrl = "https://cf.example.org/main.jpg";
    private const string OtherMainUrl = "https://cf.example.org/main-new.jpg";
    private const string LinkUrl = "https://cf.example.org/pictures/link-shot.jpg";
    private const int GameId = 7;
    private static readonly DateTimeOffset Moment = new(2030, 1, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly ArtFeatures FlatFeatures = new(0.0, 1.0, 0.0, 0.0, 4);
    private static readonly ArtFeatures ThreeDFeatures = new(0.3, 0.70, 0.60, 0.50, 1);
    private static readonly ArtFeatures CutOutFeatures = new(0.3, 0.70, 0.60, 0.50, 1, true);
    private static readonly ArtFeatures TightCropFeatures = new(0.3, 0.70, 0.60, 0.50, 1, false, true);
    private static readonly ArtFeatures UnsureFeatures = new(0.2, 0.95, 0.30, 0.20, 2);

    [Fact]
    public void A_game_whose_owned_edition_is_drawn_is_not_a_candidate()
    {
        var look = Evaluate(Snapshot(Item(), Ok(OwnedUrl, FlatFeatures), Ok(MainUrl, FlatFeatures)), null);

        look.Candidate.Should().BeFalse();
        look.AutomaticSkipsOwned.Should().BeFalse();
    }

    [Fact]
    public void A_game_with_a_usable_owned_picture_that_automatic_skips_is_a_candidate_to_check()
    {
        var look = Evaluate(Snapshot(Item(), Ok(OwnedUrl, ThreeDFeatures), Ok(MainUrl, FlatFeatures)), null);

        look.AutomaticSkipsOwned.Should().BeTrue();
        look.Candidate.Should().BeTrue();
        look.ToCheck.Should().BeTrue();
        look.NeedsAttention.Should().BeFalse();
        look.AttentionRank.Should().Be(3);
    }

    [Fact]
    public void A_game_with_no_owned_picture_whose_pick_is_the_main_picture_is_not_a_candidate()
    {
        var look = Evaluate(Snapshot(Item(ownedUrl: null), Ok(MainUrl, FlatFeatures)), null);

        look.Candidate.Should().BeFalse();
    }

    [Fact]
    public void An_owned_picture_that_failed_to_load_is_not_a_usable_owned_picture_for_the_first_reason()
    {
        var look = Evaluate(Snapshot(Item(), Failed(OwnedUrl), Ok(MainUrl, FlatFeatures)), null);

        look.AutomaticSkipsOwned.Should().BeFalse();
        look.Candidate.Should().BeFalse();
    }

    [Fact]
    public void A_game_drawn_with_the_generated_cover_and_no_picture_address_is_a_candidate_marked_no_picture()
    {
        var look = Evaluate(Snapshot(Item(ownedUrl: null, mainUrl: null)), null);

        look.NoPicture.Should().BeTrue();
        look.WontLoad.Should().BeFalse();
        look.Candidate.Should().BeTrue();
        look.NeedsAttention.Should().BeTrue();
        look.AttentionRank.Should().Be(0);
    }

    [Fact]
    public void A_game_drawn_with_the_generated_cover_while_a_candidate_has_a_failed_record_is_marked_wont_load()
    {
        var look = Evaluate(Snapshot(Item(), Failed(OwnedUrl)), null);

        look.WontLoad.Should().BeTrue();
        look.NoPicture.Should().BeFalse();
        look.AttentionRank.Should().Be(0);
    }

    [Fact]
    public void A_chosen_picture_that_has_no_address_or_a_failed_record_is_missing_or_broken()
    {
        var failed = Evaluate(Snapshot(Item(), Failed(OwnedUrl), Ok(MainUrl, FlatFeatures)), Setting(PictureChoice.OwnedEdition));
        var missing = Evaluate(Snapshot(Item(mainUrl: null), Ok(OwnedUrl, FlatFeatures)), Setting(PictureChoice.MainPicture));

        failed.WontLoad.Should().BeTrue();
        missing.NoPicture.Should().BeTrue();
    }

    [Fact]
    public void A_game_marked_as_looking_right_is_resolved_until_an_address_differs_from_the_one_seen()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, ThreeDFeatures), Ok(MainUrl, FlatFeatures));

        var resolved = Evaluate(snapshot, Setting(PictureChoice.Automatic, true, OwnedUrl, MainUrl));
        var ownedReplaced = Evaluate(snapshot, Setting(PictureChoice.Automatic, true, OtherOwnedUrl, MainUrl));
        var mainReplaced = Evaluate(snapshot, Setting(PictureChoice.Automatic, true, OwnedUrl, OtherMainUrl));

        resolved.Resolved.Should().BeTrue();
        resolved.Candidate.Should().BeTrue();
        resolved.ToCheck.Should().BeFalse();
        resolved.Changed.Should().BeNull();
        ownedReplaced.Changed.Should().Be(ChangedPicture.OwnedEdition);
        ownedReplaced.Resolved.Should().BeFalse();
        ownedReplaced.ToCheck.Should().BeTrue();
        ownedReplaced.AttentionRank.Should().Be(2);
        mainReplaced.Changed.Should().Be(ChangedPicture.MainPicture);
    }

    [Fact]
    public void A_picture_that_was_not_there_when_the_owner_looked_counts_as_changed_when_it_arrives()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, FlatFeatures), Ok(MainUrl, FlatFeatures));

        var look = Evaluate(snapshot, Setting(PictureChoice.Automatic, true, null, MainUrl));

        look.Changed.Should().Be(ChangedPicture.OwnedEdition);
    }

    [Fact]
    public void A_choice_of_the_owned_edition_compares_only_the_owned_address_and_the_main_picture_only_the_main_address()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, FlatFeatures), Ok(MainUrl, FlatFeatures));

        var ownedFollowsOwned = Evaluate(snapshot, Setting(PictureChoice.OwnedEdition, true, OtherOwnedUrl, MainUrl));
        var ownedIgnoresMain = Evaluate(snapshot, Setting(PictureChoice.OwnedEdition, true, OwnedUrl, OtherMainUrl));
        var mainFollowsMain = Evaluate(snapshot, Setting(PictureChoice.MainPicture, true, OwnedUrl, OtherMainUrl));
        var mainIgnoresOwned = Evaluate(snapshot, Setting(PictureChoice.MainPicture, true, OtherOwnedUrl, MainUrl));

        ownedFollowsOwned.Changed.Should().Be(ChangedPicture.OwnedEdition);
        ownedIgnoresMain.Changed.Should().BeNull();
        mainFollowsMain.Changed.Should().Be(ChangedPicture.MainPicture);
        mainIgnoresOwned.Changed.Should().BeNull();
    }

    [Fact]
    public void A_choice_of_the_owners_link_does_not_follow_BGG()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, FlatFeatures), Ok(MainUrl, FlatFeatures));
        var setting = Setting(PictureChoice.Link, true, OtherOwnedUrl, OtherMainUrl, WorkingLink());

        var look = Evaluate(snapshot, setting);

        look.Changed.Should().BeNull();
        look.Resolved.Should().BeTrue();
    }

    [Fact]
    public void An_address_that_differs_does_not_matter_before_the_owner_has_dealt_with_the_game()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, ThreeDFeatures), Ok(MainUrl, FlatFeatures));

        var look = Evaluate(snapshot, Setting(PictureChoice.Automatic, false, OtherOwnedUrl, OtherMainUrl));

        look.Changed.Should().BeNull();
        look.Resolved.Should().BeFalse();
    }

    [Fact]
    public void An_explicit_choice_other_than_automatic_resolves_the_game_even_without_the_checked_flag()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, ThreeDFeatures), Ok(MainUrl, FlatFeatures));

        var look = Evaluate(snapshot, Setting(PictureChoice.MainPicture, false, OwnedUrl, MainUrl));

        look.Resolved.Should().BeTrue();
        look.ToCheck.Should().BeFalse();
    }

    [Fact]
    public void A_dead_link_is_a_candidate_marked_link_no_longer_works_and_undoes_a_resolution()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, FlatFeatures), Ok(MainUrl, FlatFeatures));
        var dead = WorkingLink() with { FailedChecks = 2, FailedSinceUtc = Moment.AddDays(-2) };

        var look = Evaluate(snapshot, Setting(PictureChoice.Link, true, OwnedUrl, MainUrl, dead));

        look.LinkDead.Should().BeTrue();
        look.Candidate.Should().BeTrue();
        look.Resolved.Should().BeFalse();
        look.AttentionRank.Should().Be(1);
        look.Fetching.Should().BeFalse();
    }

    [Fact]
    public void A_link_that_failed_only_briefly_or_once_is_not_dead()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, FlatFeatures), Ok(MainUrl, FlatFeatures));
        var brief = WorkingLink() with { FailedChecks = 2, FailedSinceUtc = Moment.AddHours(-3) };
        var once = WorkingLink() with { FailedChecks = 1, FailedSinceUtc = Moment.AddDays(-5) };

        Evaluate(snapshot, Setting(PictureChoice.Automatic, false, null, null, brief)).LinkDead.Should().BeFalse();
        Evaluate(snapshot, Setting(PictureChoice.Automatic, false, null, null, once)).LinkDead.Should().BeFalse();
    }

    [Fact]
    public void A_link_with_no_record_is_fetching_and_is_not_a_reason_to_look()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, FlatFeatures), Ok(MainUrl, FlatFeatures));

        var look = Evaluate(snapshot, Setting(PictureChoice.Link, true, OwnedUrl, MainUrl, new PictureLink(LinkUrl, null, 0, null)));

        look.Fetching.Should().BeTrue();
        look.Candidate.Should().BeFalse();
    }

    [Fact]
    public void Looks_right_records_the_addresses_and_keeps_the_choice_and_logs_the_title()
    {
        var change = PictureEdits.LooksRight(OwnerData.Empty, GameId, "Invented Game", OwnedUrl, MainUrl, Moment);

        var picture = change.Data.Games.Single().Picture!;

        change.Changed.Should().BeTrue();
        change.LogText.Should().Be("Marked Invented Game as checked");
        change.StatusKey.Should().Be("picture-checked");
        picture.Choice.Should().Be(PictureChoice.Automatic);
        picture.Checked.Should().BeTrue();
        picture.SeenOwnedUrl.Should().Be(OwnedUrl);
        picture.SeenMainUrl.Should().Be(MainUrl);
    }

    [Fact]
    public void Looks_right_on_a_changed_game_rewrites_the_addresses_and_keeps_an_explicit_choice()
    {
        var data = OwnerData.Empty with { Games = [new OwnerGame(GameId, "loc", null, Setting(PictureChoice.Link, true, OwnedUrl, MainUrl, WorkingLink()))] };

        var change = PictureEdits.LooksRight(data, GameId, "Invented Game", OtherOwnedUrl, OtherMainUrl, Moment);

        var game = change.Data.Games.Single();
        game.LocationId.Should().Be("loc");
        game.Picture!.Choice.Should().Be(PictureChoice.Link);
        game.Picture.SeenOwnedUrl.Should().Be(OtherOwnedUrl);
        game.Picture.SeenMainUrl.Should().Be(OtherMainUrl);
        game.Picture.Link.Should().NotBeNull();
    }

    [Fact]
    public void Looks_right_twice_in_a_row_changes_nothing_the_second_time()
    {
        var once = PictureEdits.LooksRight(OwnerData.Empty, GameId, "Invented Game", OwnedUrl, MainUrl, Moment);

        var twice = PictureEdits.LooksRight(once.Data, GameId, "Invented Game", OwnedUrl, MainUrl, Moment);

        twice.Changed.Should().BeFalse();
        twice.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Looks_right_accepts_a_dead_link_by_forgetting_its_failures_and_keeping_its_copy()
    {
        var dead = WorkingLink() with { FailedChecks = 3, FailedSinceUtc = Moment.AddDays(-4) };
        var data = OwnerData.Empty with { Games = [new OwnerGame(GameId, null, null, Setting(PictureChoice.Link, true, OwnedUrl, MainUrl, dead))] };

        var change = PictureEdits.LooksRight(data, GameId, "Invented Game", OwnedUrl, MainUrl, Moment);

        var link = change.Data.Games.Single().Picture!.Link!;
        link.FailedChecks.Should().Be(0);
        link.FailedSinceUtc.Should().BeNull();
        link.Record.Should().NotBeNull();
    }

    [Fact]
    public void Choosing_another_picture_with_a_moment_accepts_a_dead_link_and_without_one_leaves_it()
    {
        var dead = WorkingLink() with { FailedChecks = 2, FailedSinceUtc = Moment.AddDays(-2) };
        var data = OwnerData.Empty with { Games = [new OwnerGame(GameId, null, null, Setting(PictureChoice.Link, true, OwnedUrl, MainUrl, dead))] };

        var accepted = PictureEdits.Choose(data, GameId, PictureChoice.MainPicture, "Invented Game", OwnedUrl, MainUrl, Moment);
        var untouched = PictureEdits.Choose(data, GameId, PictureChoice.MainPicture, "Invented Game", OwnedUrl, MainUrl);

        accepted.Data.Games.Single().Picture!.Link!.FailedChecks.Should().Be(0);
        untouched.Data.Games.Single().Picture!.Link!.FailedChecks.Should().Be(2);
    }

    [Fact]
    public void Every_row_has_exactly_one_why_sentence_that_matches_its_trace()
    {
        var ownedDrawn = Row(Snapshot(Item(), Ok(OwnedUrl, FlatFeatures), Ok(MainUrl, FlatFeatures)));
        var threeD = Row(Snapshot(Item(), Ok(OwnedUrl, ThreeDFeatures), Ok(MainUrl, FlatFeatures)));
        var cutOut = Row(Snapshot(Item(), Ok(OwnedUrl, CutOutFeatures), Ok(MainUrl, FlatFeatures)));
        var tightCrop = Row(Snapshot(Item(), Ok(OwnedUrl, TightCropFeatures), Ok(MainUrl, FlatFeatures)));
        var unsure = Row(Snapshot(Item(), Ok(OwnedUrl, UnsureFeatures), Ok(MainUrl, FlatFeatures)));
        var noOwned = Row(Snapshot(Item(ownedUrl: null), Ok(MainUrl, FlatFeatures)));
        var generated = Row(Snapshot(Item(ownedUrl: null, mainUrl: null)));

        ownedDrawn.Why.Should().Be(AutomaticWhy.OwnedEdition);
        threeD.Why.Should().Be(AutomaticWhy.MainBecauseOwnedReads);
        threeD.OwnedReadsAs.Should().Be(ArtVerdictKind.ThreeD);
        cutOut.OwnedReadsAs.Should().Be(ArtVerdictKind.CutOut);
        tightCrop.OwnedReadsAs.Should().Be(ArtVerdictKind.TightCrop);
        unsure.Why.Should().Be(AutomaticWhy.MainBecauseOwnedReads);
        unsure.OwnedReadsAs.Should().Be(ArtVerdictKind.Unsure);
        noOwned.Why.Should().Be(AutomaticWhy.MainBecauseNoOwned);
        noOwned.OwnedReadsAs.Should().BeNull();
        generated.Why.Should().Be(AutomaticWhy.GeneratedCover);
    }

    [Fact]
    public void The_worklist_holds_only_games_to_check_with_attention_marks_first_and_then_a_to_z()
    {
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [
                Entry(1, "Zebra Crossing", OwnedUrl, MainUrl),
                Entry(2, "Apple Orchard", OwnedUrl, MainUrl),
                Entry(3, "Mango Market", OwnedUrl, MainUrl),
                Entry(4, "Cherry Lane", null, null),
                Entry(5, "Banana Dock", OwnedUrl, MainUrl),
                Entry(6, "Date Palm", OwnedUrl, MainUrl),
                Entry(7, "Fig Garden", OwnedUrl, MainUrl),
            ],
            new Dictionary<string, ImageRecord>(StringComparer.Ordinal)
            {
                [OwnedUrl] = Ok(OwnedUrl, ThreeDFeatures),
                [MainUrl] = Ok(MainUrl, FlatFeatures),
            });
        var dead = WorkingLink() with { FailedChecks = 2, FailedSinceUtc = Moment.AddDays(-3) };
        var data = OwnerData.Empty with
        {
            Games =
            [
                new OwnerGame(3, null, null, Setting(PictureChoice.Automatic, true, OtherOwnedUrl, MainUrl)),
                new OwnerGame(5, null, null, Setting(PictureChoice.Link, true, OwnedUrl, MainUrl, dead)),
                new OwnerGame(6, null, null, Setting(PictureChoice.Automatic, true, OwnedUrl, MainUrl)),
            ],
        };

        var rows = PictureRows.Build(snapshot, ArtRules.Default, data, Moment);
        var worklist = PictureRows.WorthALookList(rows);

        worklist.Select(row => row.Title).Should().Equal(
            "Cherry Lane",
            "Banana Dock",
            "Mango Market",
            "Apple Orchard",
            "Fig Garden",
            "Zebra Crossing");
    }

    [Fact]
    public void The_counts_say_how_many_are_to_check_how_many_are_candidates_and_how_many_games_there_are()
    {
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [Entry(1, "A", OwnedUrl, MainUrl), Entry(2, "B", OwnedUrl, MainUrl), Entry(3, "C", null, null), Entry(4, "D", OwnedUrl, MainUrl)],
            new Dictionary<string, ImageRecord>(StringComparer.Ordinal)
            {
                [OwnedUrl] = Ok(OwnedUrl, FlatFeatures),
                [MainUrl] = Ok(MainUrl, FlatFeatures),
            });
        var data = OwnerData.Empty with { Games = [new OwnerGame(3, null, null, Setting(PictureChoice.Automatic, true, null, null))] };

        var counts = PictureRows.CountsOf(PictureRows.Build(snapshot, ArtRules.Default, data, Moment));

        counts.Total.Should().Be(4);
        counts.Candidates.Should().Be(1);
        counts.ToCheck.Should().Be(0);
    }

    [Fact]
    public void A_resolved_candidate_stays_counted_and_leaves_the_worklist()
    {
        var snapshot = Snapshot(Item(), Ok(OwnedUrl, ThreeDFeatures), Ok(MainUrl, FlatFeatures));
        var before = PictureRows.Build(snapshot, ArtRules.Default, OwnerData.Empty, Moment);
        var data = PictureEdits.LooksRight(OwnerData.Empty, GameId, "Invented Game", OwnedUrl, MainUrl, Moment).Data;
        var after = PictureRows.Build(snapshot, ArtRules.Default, data, Moment);

        PictureRows.CountsOf(before).Should().Be(new PictureCounts(1, 1, 1));
        PictureRows.CountsOf(after).Should().Be(new PictureCounts(0, 1, 1));
        PictureRows.WorthALookList(before).Should().HaveCount(1);
        PictureRows.WorthALookList(after).Should().BeEmpty();
        after.Single().Look.Resolved.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 2, 3, "1 of 2 to check")]
    [InlineData(14, 23, 65, "14 of 23 to check")]
    [InlineData(0, 3, 65, "You've checked all 3 games.")]
    [InlineData(0, 1, 65, "You've checked 1 game.")]
    [InlineData(0, 0, 65, "Nothing needs a look right now.")]
    public void The_counter_counts_down_and_says_when_everything_is_checked_or_nothing_needs_a_look(int toCheck, int candidates, int total, string expected)
    {
        Cabinet.Service.Owner.OwnerStrings.CounterText(new PictureCounts(toCheck, candidates, total)).Should().Be(expected);
    }

    [Fact]
    public void The_why_sentences_and_change_notes_read_as_the_design_words_them()
    {
        var why = Cabinet.Service.Owner.OwnerStrings.WhyLine;

        why(AutomaticWhy.OwnedEdition, null).Should().Be("Automatic picks the owned edition.");
        why(AutomaticWhy.MainBecauseOwnedReads, ArtVerdictKind.ThreeD).Should().Be("Automatic picks the main picture because the owned edition reads as a 3D shot.");
        why(AutomaticWhy.MainBecauseOwnedReads, ArtVerdictKind.CutOut).Should().EndWith("reads as a cut-out.");
        why(AutomaticWhy.MainBecauseOwnedReads, ArtVerdictKind.TightCrop).Should().EndWith("reads as a tight crop.");
        why(AutomaticWhy.MainBecauseOwnedReads, ArtVerdictKind.Unsure).Should().EndWith("reads as an unsure shape.");
        why(AutomaticWhy.MainBecauseNoOwned, null).Should().Be("Automatic picks the main picture because BGG has no owned edition picture.");
        why(AutomaticWhy.GeneratedCover, null).Should().Be("Automatic has no picture to use, so the cabinet shows a generated cover.");
        Cabinet.Service.Owner.OwnerStrings.NoteChanged(ChangedPicture.OwnedEdition).Should().Be("BGG replaced the owned edition picture. Check that it still looks right.");
        Cabinet.Service.Owner.OwnerStrings.NoteChanged(ChangedPicture.MainPicture).Should().Be("BGG replaced the main picture. Check that it still looks right.");
    }

    private static WorthALookResult Evaluate(CollectionSnapshot snapshot, PictureSetting? setting)
    {
        var trace = SnapshotMapper.Explain(snapshot, ArtRules.Default).Single();

        return WorthALook.Evaluate(trace, SnapshotMapper.MainPictureUrl(trace.Item, snapshot), setting, snapshot.Images!, Moment);
    }

    private static PictureRow Row(CollectionSnapshot snapshot) =>
        PictureRows.Build(snapshot, ArtRules.Default, OwnerData.Empty, Moment).Single();

    private static PictureSetting Setting(
        PictureChoice choice,
        bool isChecked = false,
        string? seenOwned = null,
        string? seenMain = null,
        PictureLink? link = null) =>
        new(choice, isChecked, seenOwned, seenMain, link);

    private static PictureLink WorkingLink() =>
        new(LinkUrl, Ok(LinkUrl, FlatFeatures), 0, null);

    private static SnapshotItem Item(string? ownedUrl = OwnedUrl, string? mainUrl = MainUrl) =>
        new(1, GameId, "Invented Game", ItemKind.Base, 2019, null, null, ownedUrl, mainUrl);

    private static SnapshotItem Entry(int id, string title, string? ownedUrl, string? mainUrl) =>
        new(id, id, title, ItemKind.Base, 2019, null, null, ownedUrl, mainUrl);

    private static ImageRecord Ok(string address, ArtFeatures features) =>
        new(address, ImageStatus.Ok, Moment, [new ArtFile(240, 320, $"{Math.Abs(address.GetHashCode()):x8}-240.webp")], features);

    private static ImageRecord Failed(string address) => new(address, ImageStatus.Failed, Moment);

    private static CollectionSnapshot Snapshot(SnapshotItem item, params ImageRecord[] records) =>
        new(CollectionSnapshot.CurrentSchemaVersion, Moment, [item], records.ToDictionary(record => record.SourceUrl, StringComparer.Ordinal));
}
