using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>Proves background syncs are off in test hosts unless a test opts in, and then run on the serving host only.</summary>
[Trait("Category", "Sync")]
public class BackgroundSyncTests
{
    private static readonly Dictionary<string, string?> OptIn = new() { ["Sync:BackgroundEnabled"] = "true" };

    [Fact]
    public async Task By_default_background_syncs_are_off_on_both_hosts()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());

        factory.Services.GetRequiredService<SyncOptions>().BackgroundEnabled.Should().BeFalse();
        factory.ServingServices.GetRequiredService<SyncOptions>().BackgroundEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Opting_in_turns_background_syncs_on_for_the_serving_host_only()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock(), OptIn);

        factory.ServingServices.GetRequiredService<SyncOptions>().BackgroundEnabled.Should().BeTrue();
        factory.Services.GetRequiredService<SyncOptions>().BackgroundEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task An_opted_in_host_syncs_once_after_a_cold_start_with_no_doubled_calls()
    {
        var clock = SyncHarness.NewClock();
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, clock, OptIn);
        using var client = factory.CreatePublicClient();

        clock.Advance(TimeSpan.FromSeconds(120));
        await SyncHarness.WaitForRunToEnd(client);

        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("changed");
        handler.CollectionRequests().Should().HaveCount(2);
        handler.ThingRequests().Should().ContainSingle();
    }
}
