using System.Net;
using System.Text.RegularExpressions;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests;

/// <summary>Proves the page tells visitors how fresh the cabinet is, and says so calmly when it is showing an older sync.</summary>
[Trait("Category", "Sync")]
public partial class SyncStatusLineTests
{
    private static readonly string[] StatusAttributes =
    [
        "data-server-time",
        "data-last-synced",
        "data-cooldown-ends",
        "data-running",
        "data-held-back",
        "data-stale-after",
        "data-snapshot-version",
    ];

    [Fact]
    public async Task A_fresh_host_says_not_synced_yet_with_no_time_and_no_stale_note()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        html.Should().Contain("<span class=\"sync-time-text\">Not synced yet</span>");
        html.Should().NotContain("<time");
        html.Should().NotContain("class=\"sync-time\"");
        StaleNote(html).Attributes.Should().Contain("hidden");
        StaleNote(html).Text.Should().BeEmpty();
        html.Should().Contain("The cabinet is being filled.");
        html.Should().MatchRegex("<button type=\"button\" class=\"sync-button\" aria-disabled=\"false\" hidden>Sync now</button>");
    }

    [Fact]
    public async Task After_a_sync_the_line_reads_synced_just_now_with_the_time_in_the_markup()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();
        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);

        await SyncHarness.WaitForRunToEnd(client);
        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        html.Should().Contain("<time datetime=\"2030-01-15T12:00:00.000Z\">Synced just now</time>");
        html.Should().Contain("aria-expanded=\"false\" aria-controls=\"sync-exact\"");
        html.Should().NotContain("Not synced yet");
        StaleNote(html).Attributes.Should().Contain("hidden");
    }

    [Fact]
    public async Task A_last_good_sync_four_hours_old_shows_the_recent_syncs_note_with_its_exact_time()
    {
        using var storage = new TemporaryDirectory();
        Seed(storage, SyncHarness.StartTime - TimeSpan.FromHours(4), SyncResult.Changed, heldBack: null);
        await using var factory = CreateSeededFactory(storage);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var note = StaleNote(html);

        note.Attributes.Should().NotContain("hidden");
        note.Text.Should().Be("Showing the last sync from 15 January 2030 at 08:00 UTC. Recent syncs haven't gone through.");
        html.Should().Contain("Synced 4 hours ago");
    }

    [Fact]
    public async Task A_held_back_result_shows_its_variant_even_when_the_last_good_sync_is_recent()
    {
        using var storage = new TemporaryDirectory();
        var heldBack = new HeldBackRecord(HeldBackKind.Shrunk, ShrinkGuard.Fingerprint([1, 2, 3]), 3, SyncHarness.StartTime - TimeSpan.FromMinutes(10));
        Seed(storage, SyncHarness.StartTime - TimeSpan.FromMinutes(50), SyncResult.HeldBack, heldBack);
        await using var factory = CreateSeededFactory(storage);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var note = StaleNote(html);

        note.Attributes.Should().NotContain("hidden");
        note.Text.Should().Be("Showing the last sync from 15 January 2030 at 11:10 UTC. A much smaller collection from BGG is waiting for the next sync to confirm.");
        html.Should().Contain("data-held-back=\"true\"");
    }

    [Fact]
    public async Task A_failed_run_with_the_last_good_sync_under_three_hours_old_stays_quiet()
    {
        using var storage = new TemporaryDirectory();
        Seed(storage, SyncHarness.StartTime - TimeSpan.FromHours(2), SyncResult.Failed, heldBack: null);
        await using var factory = CreateSeededFactory(storage);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        StaleNote(html).Attributes.Should().Contain("hidden");
        StaleNote(html).Text.Should().BeEmpty();
        html.Should().Contain("Synced 2 hours ago");
    }

    [Fact]
    public async Task The_sync_element_carries_all_seven_first_paint_attributes()
    {
        using var storage = new TemporaryDirectory();
        Seed(storage, SyncHarness.StartTime - TimeSpan.FromMinutes(12), SyncResult.Changed, heldBack: null);
        await using var factory = CreateSeededFactory(storage);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var element = SyncElement().Match(html).Value;

        element.Should().NotBeEmpty();
        foreach (var attribute in StatusAttributes)
        {
            element.Should().Contain(attribute + "=\"");
        }

        element.Should().Contain("data-server-time=\"2030-01-15T12:00:00.000Z\"");
        element.Should().Contain("data-last-synced=\"2030-01-15T11:48:00.000Z\"");
        element.Should().Contain("data-running=\"false\"");
        element.Should().Contain("data-stale-after=\"10800\"");
        element.Should().MatchRegex("data-snapshot-version=\"[^\"]+\"");
        html.Should().Contain("Synced 12 minutes ago");
    }

    [Fact]
    public async Task Page_scripts_for_the_status_line_are_served_at_their_original_paths()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        foreach (var path in new[] { "/js/status.js", "/js/sync.js", "/js/copy.js" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK, path);
            response.ShouldHaveMediaType("text/javascript", path);
        }
    }

    [Fact]
    public async Task The_header_never_names_the_data_source()
    {
        using var storage = new TemporaryDirectory();
        Seed(storage, SyncHarness.StartTime - TimeSpan.FromHours(4), SyncResult.Changed, heldBack: null);

        await using (var unsynced = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock()))
        {
            await AssertHeaderNamesNoSource(unsynced);
        }

        await using (var seeded = CreateSeededFactory(storage))
        {
            await AssertHeaderNamesNoSource(seeded);
        }
    }

    private static async Task AssertHeaderNamesNoSource(CabinetWebApplicationFactory factory)
    {
        using var client = factory.CreatePublicClient();
        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var header = html[..html.IndexOf("</header>", StringComparison.Ordinal)];

        header.Should().NotContainEquivalentOf("BGG").And.NotContainEquivalentOf("BoardGameGeek");
    }

    [Fact]
    public async Task The_stale_note_holds_no_digit_other_than_those_of_its_date()
    {
        using var storage = new TemporaryDirectory();
        Seed(storage, SyncHarness.StartTime - TimeSpan.FromHours(4), SyncResult.Changed, heldBack: null);
        await using var factory = CreateSeededFactory(storage);
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var withoutDate = StaleNote(html).Text.Replace("15 January 2030 at 08:00 UTC", string.Empty, StringComparison.Ordinal);

        withoutDate.Should().NotBeEmpty();
        withoutDate.Should().NotMatchRegex("[0-9]");
    }

    [Fact]
    public async Task The_relative_time_never_wraps_and_the_exact_line_may()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var href = SiteStylesheetLink().Match(html).Groups["href"].Value;
        var css = await client.GetStringAsync(href, TestContext.Current.CancellationToken);

        RuleFor(css, ".sync-time").Should().Contain("white-space: nowrap");
        RuleFor(css, ".sync-time-text").Should().Contain("white-space: nowrap");
        RuleFor(css, ".sync-exact").Should().NotContain("white-space");
        RuleFor(css, ".sync-note").Should().Contain("max-width: 40rem");
        RuleFor(css, ".sync-stale").Should().Contain("max-width: 40rem");
    }

    private static string RuleFor(string css, string selector)
    {
        var rules = Regex.Matches(css, @"(?<selectors>[^{}]+)\{(?<body>[^}]*)\}")
            .Where(rule => rule.Groups["selectors"].Value.Split(',').Select(part => part.Trim()).Contains(selector))
            .Select(rule => rule.Groups["body"].Value)
            .ToList();

        rules.Should().NotBeEmpty($"the stylesheet styles {selector}");

        return string.Join(' ', rules);
    }

    private static CabinetWebApplicationFactory CreateSeededFactory(TemporaryDirectory storage) =>
        SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });

    private static void Seed(TemporaryDirectory storage, DateTimeOffset lastSuccess, SyncResult lastResult, HeldBackRecord? heldBack)
    {
        var item = new SnapshotItem(1, 100, "Synthetic Game", ItemKind.Base, 2020, null, null);

        new SnapshotStore(storage.FullPath, NullLogger<SnapshotStore>.Instance)
            .Save(new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, lastSuccess, [item]));
        new SyncStateStore(storage.FullPath, TimeProvider.System, TimeSpan.FromMinutes(10), NullLogger<SyncStateStore>.Instance).Save(new SyncState(
            SyncState.CurrentSchemaVersion,
            lastSuccess,
            lastSuccess,
            lastSuccess,
            lastResult,
            lastResult == SyncResult.Failed ? SyncFailure.Unavailable : SyncFailure.None,
            lastResult == SyncResult.Failed ? 1 : 0,
            null,
            heldBack));
    }

    private static (string Attributes, string Text) StaleNote(string html)
    {
        var match = StaleNoteElement().Match(html);

        match.Success.Should().BeTrue("the page always carries the older-sync note element");

        return (match.Groups["attributes"].Value, WebUtility.HtmlDecode(match.Groups["text"].Value));
    }

    [GeneratedRegex("<p class=\"sync-stale\"(?<attributes>[^>]*)>(?<text>[^<]*)</p>")]
    private static partial Regex StaleNoteElement();

    [GeneratedRegex("<link[^>]*rel=\"stylesheet\"[^>]*href=\"(?<href>/css/site\\.[^\"]+)\"")]
    private static partial Regex SiteStylesheetLink();

    [GeneratedRegex("<div class=\"sync\"[^>]*>")]
    private static partial Regex SyncElement();
}
