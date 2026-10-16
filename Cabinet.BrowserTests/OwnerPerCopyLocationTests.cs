using System.Globalization;
using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the locations page gives every copy of a game its own row, tag and picker, that choosing a place
/// for one copy saves without a reload and moves only that copy, and that an expansion's row sits under its base's first copy.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerPerCopyLocationTests : CabinetPageTest
{
    private const string LocationsPath = "/owner/locations";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string GameTitle = "Example Game 1";

    private HttpClient? _publicClient;

    [Fact]
    public async Task Changing_the_second_copy_saves_without_a_reload()
    {
        var clock = SyncHarness.NewClock();
        var factory = await StartAsync(SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)), clock));
        _publicClient = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(_publicClient, clock, advance: false);
        var owner = factory.ServingServices.GetRequiredService<OwnerDataService>();
        var locations = await SeedAsync(owner, "Cabinet A", "Shelf B");
        var cards = await ReadCardsAsync();
        var copies = cards.Where(card => card.Title == GameTitle).OrderBy(card => card.EntryId).ToList();
        var expansion = cards.Single(card => card.IsExpansion && card.Title.StartsWith(GameTitle, StringComparison.Ordinal));
        await PlaceEverywhereAsync(owner, copies[0], locations[0]);
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoOwnerAsync(LocationsPath);
        await Page.WaitForSelectorAsync("#your-locations[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Page.EvaluateAsync("window.__loaded = 'yes'");

        var rows = Page.Locator($".game-row[data-game-id='{copies[0].GameId}']");
        var firstCopy = Page.Locator($".game-row[data-entry-id='{copies[0].EntryId}']");
        var secondCopy = Page.Locator($".game-row[data-entry-id='{copies[1].EntryId}']");

        copies.Should().HaveCountGreaterThan(1);
        await Expect(rows).ToHaveCountAsync(copies.Count);
        await Expect(firstCopy.Locator(".pic-tag-plain")).ToHaveTextAsync($"Copy 1 of {copies.Count}");
        await Expect(secondCopy.Locator(".pic-tag-plain")).ToHaveTextAsync($"Copy 2 of {copies.Count}");
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = $"Location for {GameTitle}, copy 2 of {copies.Count}" })).ToHaveCountAsync(1);
        await Expect(Page.Locator($".game-row[data-entry-id='{expansion.EntryId}']")).ToHaveAttributeAsync("data-base-entry-id", Id(copies[0]));
        var order = (await Page.Locator(".game-row").EvaluateAllAsync<string[]>("rows => rows.map(row => row.dataset.entryId)")).ToList();
        order.IndexOf(Id(expansion)).Should().Be(order.IndexOf(Id(copies[0])) + 1);

        await secondCopy.Locator("select").SelectOptionAsync(new SelectOptionValue { Label = "Shelf B" });

        await Expect(secondCopy.Locator(".row-status")).ToContainTextAsync("Saved");
        await Expect(secondCopy.Locator(".row-status button")).ToHaveTextAsync("Undo");
        await Expect(secondCopy.Locator(".game-where")).ToHaveTextAsync("Shelf B");
        await Expect(firstCopy.Locator(".game-where")).ToHaveTextAsync("Cabinet A");
        (await Page.EvaluateAsync<string>("window.__loaded")).Should().Be("yes");
        var after = await ReadCardsAsync();
        after.Single(card => card.EntryId == copies[0].EntryId).Location.Should().Be("Cabinet A");
        after.Single(card => card.EntryId == copies[1].EntryId).Location.Should().Be("Shelf B");
    }

    private static string Id(CardReading card) => card.EntryId.ToString(CultureInfo.InvariantCulture);

    private async Task<IReadOnlyList<CardReading>> ReadCardsAsync()
    {
        using var response = await _publicClient!.GetAsync(CardsPath, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return
        [
            .. document.RootElement.GetProperty("cards").EnumerateArray().Select(card => new CardReading(
                card.GetProperty("entryId").GetInt64(),
                card.GetProperty("gameId").GetInt32(),
                card.GetProperty("title").GetString()!,
                card.GetProperty("isExpansion").GetBoolean(),
                card.TryGetProperty("location", out var location) ? location.GetString() : null)),
        ];
    }

    private static async Task<IReadOnlyList<string>> SeedAsync(OwnerDataService owner, params string[] names)
    {
        var ids = new List<string>();

        foreach (var name in names)
        {
            var result = await owner.MutateAsync(owner.Revision, data => LocationEdits.Add(data, name, OwnerDataService.NewId), TestContext.Current.CancellationToken);

            result.Outcome.Should().Be(OwnerOutcome.Saved);
            ids.Add(result.Data!.Locations[^1].Id);
        }

        return ids;
    }

    private static async Task PlaceEverywhereAsync(OwnerDataService owner, CardReading card, string locationId)
    {
        var result = await owner.MutateAsync(
            owner.Revision,
            data => LocationEdits.Assign(data, card.GameId, locationId, card.Title),
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OwnerOutcome.Saved);
    }

    private sealed record CardReading(long EntryId, int GameId, string Title, bool IsExpansion, string? Location);
}
