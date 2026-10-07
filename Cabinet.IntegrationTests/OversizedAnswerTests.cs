using System.Text;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves an answer larger than the limit the app accepts is refused as a failed sync, and nothing is stored or shown. The
/// transport refuses the body before the reader sees it, so the failure is the one for an answer that could not be fetched.
/// </summary>
[Trait("Category", "Sync")]
public class OversizedAnswerTests
{
    private const int OverTheLimitCharacters = 20_100_000;

    [Fact]
    public async Task An_answer_over_the_size_limit_fails_the_sync_and_leaves_the_stored_collection_and_the_layout_untouched()
    {
        using var storage = new TemporaryDirectory();
        var snapshotPath = Path.Combine(storage.FullPath, "snapshot.json");
        var clock = SyncHarness.NewClock();
        var logs = new CapturingLoggerProvider();
        var source = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath },
            services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);
                services.AddSingleton(SyncHarness.Options());
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
                services.AddSingleton<TimeProvider>(clock);
                services.AddHttpClient<ICollectionSource, BggClient>().ConfigurePrimaryHttpMessageHandler(() => source.Handler);
                services.AddHttpClient<IEnrichmentSource, BggThingClient>().ConfigurePrimaryHttpMessageHandler(() => source.Handler);
            });
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var before = await SyncRounds.ReadLayout(client);
        var storedBefore = await File.ReadAllBytesAsync(snapshotPath, TestContext.Current.CancellationToken);

        source.Fail(ScriptedResponse.Xml(Oversized()));
        await SyncRounds.PressAndWait(client, clock);
        var status = await SyncHarness.ReadStatus(client);

        status.LastResult.Should().Be("failed");
        (await SyncRounds.ReadLayout(client)).Should().BeEquivalentTo(before);
        (await File.ReadAllBytesAsync(snapshotPath, TestContext.Current.CancellationToken)).Should().Equal(storedBefore);
        logs.Lines.Should().Contain(line => line == "BGG sync failed: Unavailable");
    }

    private static string Oversized()
    {
        var builder = new StringBuilder("<items totalitems=\"0\" termsofuse=\"https://example.com/terms\"><!--", OverTheLimitCharacters + 100);
        builder.Append('x', OverTheLimitCharacters);
        builder.Append("--></items>");

        return builder.ToString();
    }
}
