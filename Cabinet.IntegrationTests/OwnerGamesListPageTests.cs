using System.Net;
using System.Text.RegularExpressions;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves what the locations page writes into its games list before any script runs: one picker per game made by the shared
/// partial, the toolbar, and the one query value that opens the list narrowed to the games that are not placed.
/// </summary>
[Trait("Category", "Owner")]
public sealed partial class OwnerGamesListPageTests
{
    private const string LocationsPath = "/owner/locations";

    [Fact]
    public async Task Every_game_gets_one_picker_from_the_shared_partial_that_posts_to_the_assign_handler()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var owner = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await owner.MutateAsync(owner.Revision, data => LocationEdits.Add(data, "Cabinet A", OwnerDataService.NewId), TestContext.Current.CancellationToken);
        using var session = new OwnerSession(factory);

        var page = await session.GetAsync(LocationsPath);

        page.Status.Should().Be(HttpStatusCode.OK);
        var rows = GameRows().Matches(page.Body).Count;
        rows.Should().BeGreaterThan(1);
        Pickers().Matches(page.Body).Count.Should().Be(rows);
        ForwardForms().Matches(page.Body).Count.Should().Be(rows);
        page.Body.Should().Contain("aria-label=\"Location for Example Game 1\"");
        var expansionPicker = PickerOf(page.Body, "Example Game 1: Lantern Extras");
        expansionPicker.Should().Contain("Same as Example Game 1").And.NotContain(">Not placed<").And.Contain(">Cabinet A<");
        PickerOf(page.Body, "Lantern &amp; Harbour").Should().Contain(">Not placed<").And.Contain(">Cabinet A<");
        page.Body.Should().Contain("id=\"games-toolbar\"").And.Contain("type=\"search\"").And.Contain("role=\"switch\"");
    }

    [Fact]
    public async Task Only_the_unplaced_value_of_the_filter_query_opens_the_switch_and_it_counts_the_games()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        using var session = new OwnerSession(factory);

        var plain = await session.GetAsync(LocationsPath);
        var unplaced = await session.GetAsync($"{LocationsPath}?filter=unplaced");
        var other = await session.GetAsync($"{LocationsPath}?filter=%3Cscript%3E");
        var rows = GameRows().Matches(plain.Body).Count;

        SwitchTag().Match(plain.Body).Value.Should().NotContain("checked");
        SwitchTag().Match(unplaced.Body).Value.Should().Contain("checked");
        SwitchTag().Match(other.Body).Value.Should().NotContain("checked");
        other.Body.Should().NotContain("<script>");
        UnplacedLabel().Match(plain.Body).Groups[1].Value.Should().Be(rows.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string PickerOf(string html, string title)
    {
        var match = Regex.Match(html, "<select[^>]*aria-label=\"Location for " + Regex.Escape(title) + "\"[^>]*>(.*?)</select>", RegexOptions.Singleline);

        return match.Success ? match.Groups[1].Value : throw new InvalidOperationException($"The page has no picker for {title}.");
    }

    [GeneratedRegex("<li class=\"game-row\"")]
    private static partial Regex GameRows();

    [GeneratedRegex("<select[^>]*data-picker")]
    private static partial Regex Pickers();

    [GeneratedRegex("<form[^>]*class=\"game-form\"[^>]*action=\"[^\"]*handler=Assign\"|<form[^>]*action=\"[^\"]*handler=Assign\"[^>]*class=\"game-form\"")]
    private static partial Regex ForwardForms();

    [GeneratedRegex("<input[^>]*id=\"unplaced-switch\"[^>]*>")]
    private static partial Regex SwitchTag();

    [GeneratedRegex("id=\"unplaced-label\">Not placed yet \\((\\d+)\\)<")]
    private static partial Regex UnplacedLabel();
}
