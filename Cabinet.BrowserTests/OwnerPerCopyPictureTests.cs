using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the pictures page gives every copy of a game its own row with its own tag and choice, that
/// choosing a picture on one copy's row saves for that copy alone and reaches that copy's box in the public cabinet, and that the
/// single-game view shows every copy with its own location picker.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerPerCopyPictureTests : CabinetPageTest
{
    private const string PicturesPath = "/owner/pictures";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string VersionUrl = "https://cf.example.org/version.jpg";
    private const string MainUrl = "https://cf.example.org/main.jpg";
    private const string Title = "Quiet Quarry";
    private const int GameId = 5101;
    private const int OtherGameId = 5102;
    private const long OtherEntry = 7101;

    private static readonly long[] Copies = [7001, 7002, 7003];

    private readonly TemporaryDirectory _storage = new();
    private HttpClient? _publicClient;

    [Fact]
    public async Task Each_copy_has_its_own_picture_row()
    {
        var factory = await StartHostAsync(["Deluxe edition", null, null]);
        _publicClient = factory.CreatePublicClient();
        await Page.SetViewportSizeAsync(1440, 900);
        var before = await ReadCoversAsync();

        await GotoOwnerAsync($"{PicturesPath}?view=all");
        await Page.WaitForSelectorAsync("#pic-list[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

        var rows = Page.Locator($".pic-row[data-game-id='{GameId}']");
        var first = Page.Locator($".pic-row[data-entry-id='{Copies[0]}']");
        var second = Page.Locator($".pic-row[data-entry-id='{Copies[1]}']");
        var third = Page.Locator($".pic-row[data-entry-id='{Copies[2]}']");

        await Expect(rows).ToHaveCountAsync(3);
        await Expect(first.Locator("[data-copy-tag]")).ToHaveTextAsync("Deluxe edition");
        await Expect(second.Locator("[data-copy-tag]")).ToHaveTextAsync("Copy 2 of 3");
        await Expect(third.Locator("[data-copy-tag]")).ToHaveTextAsync("Copy 3 of 3");
        await Expect(Page.Locator($".pic-row[data-entry-id='{OtherEntry}'] [data-copy-tag]")).ToHaveCountAsync(0);

        await second.Locator("label.pic-tile", new LocatorLocatorOptions { HasText = "Main picture" }).ClickAsync();

        await Expect(second.Locator(".row-status")).ToContainTextAsync("Saved");
        await Expect(second.Locator(".row-status button")).ToHaveTextAsync("Undo");
        await Expect(second.Locator("input[value=MainPicture]")).ToBeCheckedAsync();
        await Expect(first.Locator("input[value=Automatic]")).ToBeCheckedAsync();
        await Expect(third.Locator("input[value=Automatic]")).ToBeCheckedAsync();
        var after = await ReadCoversAsync();
        after[Copies[0]].Should().Be(before[Copies[0]]);
        after[Copies[2]].Should().Be(before[Copies[2]]);
        after[Copies[1]].Should().NotBe(before[Copies[1]]);

        await Page.ReloadAsync();

        await Expect(Page.Locator($".pic-row[data-entry-id='{Copies[1]}'] input[value=MainPicture]")).ToBeCheckedAsync();
        await Expect(Page.Locator($".pic-row[data-entry-id='{Copies[0]}'] input[value=Automatic]")).ToBeCheckedAsync();
    }

    [Fact]
    public async Task The_single_game_view_shows_every_copy()
    {
        var factory = await StartHostAsync(["Deluxe edition", null, null]);
        _publicClient = factory.CreatePublicClient();
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoOwnerAsync($"{PicturesPath}?game={GameId}");

        var rows = Page.Locator(".pic-row");

        await Expect(rows).ToHaveCountAsync(3);
        await Expect(Page.Locator(".pic-row [data-copy-tag]")).ToHaveTextAsync(["Deluxe edition", "Copy 2 of 3", "Copy 3 of 3"]);
        await Expect(Page.Locator(".pic-row fieldset.pic-choices")).ToHaveCountAsync(3);
        await Expect(Page.Locator(".pic-row .game-picker")).ToHaveCountAsync(3);
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = $"Location for {Title}, Deluxe edition" })).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = $"Location for {Title}, copy 2 of 3" })).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = $"Location for {Title}, copy 3 of 3" })).ToHaveCountAsync(1);
        await Expect(Page.Locator($".pic-row[data-entry-id='{Copies[1]}'] select")).ToHaveAttributeAsync("id", $"picker-{GameId}-{Copies[1]}");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        _publicClient?.Dispose();
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<CabinetWebApplicationFactory> StartHostAsync(string?[] editions)
    {
        var moment = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var images = new Dictionary<string, ImageRecord>
        {
            [VersionUrl] = new(VersionUrl, ImageStatus.Ok, moment, [new ArtFile(600, 800, "1111111111111111-600.webp")], new ArtFeatures(0.0, 1.0, 0.0, 0.0, 4)),
            [MainUrl] = new(MainUrl, ImageStatus.Ok, moment, [new ArtFile(600, 800, "2222222222222222-600.webp")], new ArtFeatures(0.3, 0.70, 0.60, 0.50, 1)),
        };
        var items = Copies
            .Select((copy, index) => new SnapshotItem(copy, GameId, Title, ItemKind.Base, 2015, null, null, VersionUrl, MainUrl, editions[index]))
            .Append(new SnapshotItem(OtherEntry, OtherGameId, "Hollow Harbour", ItemKind.Base, 2018, null, null, VersionUrl, MainUrl));

        new SnapshotStore(_storage.FullPath, NullLogger<SnapshotStore>.Instance).Save(new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            moment,
            [.. items],
            images,
            null));

        return await StartAsync(new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath });
    }

    private async Task<Dictionary<long, string>> ReadCoversAsync()
    {
        using var response = await _publicClient!.GetAsync(CardsPath, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("cards").EnumerateArray()
            .ToDictionary(card => card.GetProperty("entryId").GetInt64(), card => card.GetProperty("cover").GetProperty("url").GetString()!);
    }
}
