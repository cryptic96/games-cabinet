using System.Text.Json;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.BrowserTests.Infrastructure;

/// <summary>One game of the cabinet as the public card data names it.</summary>
/// <param name="GameId">The BGG game identifier.</param>
/// <param name="Title">The title the card shows.</param>
public sealed record CardReading(int GameId, string Title);

/// <summary>
/// A running cabinet host with a synced collection and a handle on the owner's data, for browser tests that need owner locations
/// or placements: the games the host serves, and helpers that add locations, place a game or delete a location the way the owner
/// tools would. Build one by syncing a scripted collection or by attaching to a host that already holds a stored collection.
/// </summary>
public sealed class OwnerHost
{
    private readonly FakeTimeProvider? _clock;

    private OwnerHost(OwnerDataService owner, IReadOnlyList<CardReading> cards, FakeTimeProvider? clock)
    {
        Owner = owner;
        Cards = cards;
        _clock = clock;
    }

    /// <summary>The owner's data as the host holds it.</summary>
    public OwnerDataService Owner { get; }

    /// <summary>Every game the public card data lists.</summary>
    public IReadOnlyList<CardReading> Cards { get; }

    /// <summary>
    /// Starts a host whose BGG answers are scripted with the given collection, runs one sync so the cabinet holds it and attaches to it.
    /// </summary>
    /// <param name="start">Hands the built host to the test page, which keeps it for the rest of the test.</param>
    /// <param name="collection">The invented items the scripted BGG serves.</param>
    public static async Task<OwnerHost> SyncAsync(Func<CabinetWebApplicationFactory, Task> start, IReadOnlyList<FakeBggItem> collection)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(collection);

        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(collection), clock);

        await start(factory);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);

        return await AttachAsync(factory, clock);
    }

    /// <summary>
    /// Starts a host on a state directory that already holds a stored collection and owner data, such as the one the filter fixture
    /// writes, and attaches to it. No sync runs; the host serves what the directory holds.
    /// </summary>
    /// <param name="start">Hands the built host to the test page, which keeps it for the rest of the test.</param>
    /// <param name="stateDirectory">The directory the collection and owner data were written to.</param>
    public static async Task<OwnerHost> StoredAsync(Func<CabinetWebApplicationFactory, Task> start, string stateDirectory)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);

        var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Storage:Directory"] = stateDirectory });

        await start(factory);

        return await AttachAsync(factory);
    }

    /// <summary>Attaches to a running host: reads the games its public card data lists and takes hold of its owner data.</summary>
    /// <param name="factory">The running host.</param>
    /// <param name="clock">The host's clock when the test controls it; it lets <see cref="TellOpenPages"/> make the quiet period pass.</param>
    public static async Task<OwnerHost> AttachAsync(CabinetWebApplicationFactory factory, FakeTimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using var client = factory.CreatePublicClient();
        using var response = await client.GetAsync("/cabinet/cards?profile=desktop", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var cards = document.RootElement.GetProperty("cards").EnumerateArray()
            .Select(card => new CardReading(card.GetProperty("gameId").GetInt32(), card.GetProperty("title").GetString()!))
            .ToList();

        return new OwnerHost(factory.ServingServices.GetRequiredService<OwnerDataService>(), cards, clock);
    }

    /// <summary>
    /// Lets the quiet period after the owner's last save pass on the host's clock, so open pages are told the collection changed and
    /// redraw. Only a host built by <see cref="SyncAsync"/> has a clock the test controls.
    /// </summary>
    public void TellOpenPages()
    {
        var clock = _clock ?? throw new InvalidOperationException("This host's clock is not under the test's control.");

        clock.Advance(OwnerChangeNotifier.QuietPeriod);
    }

    /// <summary>Finds a game by its exact title.</summary>
    /// <param name="title">The title the card shows.</param>
    public CardReading Card(string title) => Cards.First(card => string.Equals(card.Title, title, StringComparison.Ordinal));

    /// <summary>Places every copy of a game in a location.</summary>
    /// <param name="card">The game.</param>
    /// <param name="locationId">The location to place it in.</param>
    public async Task PlaceAsync(CardReading card, string locationId)
    {
        ArgumentNullException.ThrowIfNull(card);

        var result = await Owner.MutateAsync(
            Owner.Revision,
            data => LocationEdits.Assign(data, card.GameId, locationId, card.Title),
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OwnerOutcome.Saved);
    }

    /// <summary>Adds locations to the owner's list in the given order.</summary>
    /// <param name="names">The names of the locations to add.</param>
    /// <returns>The identifier of each added location, in the same order.</returns>
    public async Task<IReadOnlyList<string>> SeedAsync(params string[] names)
    {
        var ids = new List<string>();

        foreach (var name in names)
        {
            var result = await Owner.MutateAsync(
                Owner.Revision,
                data => LocationEdits.Add(data, name, OwnerDataService.NewId),
                TestContext.Current.CancellationToken);

            result.Outcome.Should().Be(OwnerOutcome.Saved);
            ids.Add(result.Data!.Locations[^1].Id);
        }

        return ids;
    }

    /// <summary>
    /// Deletes a location. The games kept there become not placed; the count the change reports is not needed here, so the delete is
    /// made without looking at the collection.
    /// </summary>
    /// <param name="locationId">The location to delete.</param>
    public async Task DeleteLocationAsync(string locationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);

        var result = await Owner.MutateAsync(
            Owner.Revision,
            data => LocationEdits.Delete(data, null, locationId),
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OwnerOutcome.Saved);
    }
}
