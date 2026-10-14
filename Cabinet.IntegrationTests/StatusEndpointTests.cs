using System.Net;
using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Proves the status route tells a page when the collection was synced without revealing anything about a failure.</summary>
[Trait("Category", "Sync")]
public class StatusEndpointTests
{
    [Fact]
    public async Task Before_any_sync_nothing_is_synced_and_nothing_is_running()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        var status = await SyncHarness.ReadStatus(client);

        status.Json.GetProperty("lastSyncedUtc").ValueKind.Should().Be(JsonValueKind.Null);
        status.Json.GetProperty("snapshotVersion").ValueKind.Should().Be(JsonValueKind.Null);
        status.Running.Should().BeFalse();
        status.CooldownEndsUtc.Should().BeNull();
        status.LastResult.Should().BeNull();
        status.Json.GetProperty("staleAfterSeconds").GetInt32().Should().Be(3 * 60 * 60);
    }

    [Fact]
    public async Task After_a_sync_the_version_and_time_are_set_and_the_result_is_changed()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();
        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);

        await SyncHarness.WaitForRunToEnd(client);
        var status = await SyncHarness.ReadStatus(client);

        status.LastResult.Should().Be("changed");
        status.Json.GetProperty("snapshotVersion").GetString().Should().NotBeNullOrEmpty();
        status.Json.GetProperty("lastSyncedUtc").GetDateTimeOffset().Should().Be(SyncHarness.StartTime);
        status.Json.GetProperty("lastResultAtUtc").GetDateTimeOffset().Should().Be(SyncHarness.StartTime);
        status.Json.GetProperty("serverTimeUtc").GetDateTimeOffset().Should().Be(SyncHarness.StartTime);
        status.CooldownEndsUtc.Should().Be(SyncHarness.StartTime + TimeSpan.FromMinutes(10));
        status.Json.GetProperty("heldBack").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task The_status_carries_the_default_look_when_the_owner_has_chosen_none()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        var status = await SyncHarness.ReadStatus(client);

        var look = status.Json.GetProperty("look");
        look.GetProperty("design").GetString().Should().Be("classic");
        look.GetProperty("wood").GetString().Should().Be("brown");
        look.GetProperty("backs").GetString().Should().Be("wood");
        look.GetProperty("lights").GetString().Should().Be("off");
        look.GetProperty("depth").GetString().Should().Be("shaded");
        look.GetProperty("lightWall").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task The_status_look_is_exactly_the_five_public_values_and_the_wall_tone_and_no_owner_data_leaks_into_it()
    {
        const string LocationName = "Cabinet A";
        const string ChangeText = "Moved a game to Shelf B";
        using var storage = new TemporaryDirectory();
        OwnerDataSeed.Write(
            storage.FullPath,
            OwnerData.Empty with
            {
                Look = new CabinetLook("library", "birch", "painted", "evenings", "deep"),
                Locations = [new OwnerLocation("0a1b2c3d", LocationName)],
                Games = [new OwnerGame(100, "0a1b2c3d", null, null)],
                Changes = [new ChangeEntry(SyncHarness.StartTime, ChangeText)],
            });
        await using var factory = SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        var status = await SyncHarness.ReadStatus(client);

        var look = status.Json.GetProperty("look");
        look.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo("design", "wood", "backs", "lights", "depth", "lightWall");
        look.GetProperty("design").GetString().Should().Be("library");
        look.GetProperty("wood").GetString().Should().Be("birch");
        look.GetProperty("backs").GetString().Should().Be("painted");
        look.GetProperty("lights").GetString().Should().Be("evenings");
        look.GetProperty("depth").GetString().Should().Be("deep");
        look.GetProperty("lightWall").GetBoolean().Should().BeTrue();
        status.Body.Should().NotContain(LocationName).And.NotContain("0a1b2c3d").And.NotContain(ChangeText);
        status.Json.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "serverTimeUtc", "snapshotVersion", "lastSyncedUtc", "running", "cooldownEndsUtc", "heldBack", "staleAfterSeconds", "lastResult", "lastResultAtUtc", "look");
    }

    [Fact]
    public async Task The_status_is_never_cached_and_carries_no_cross_origin_permission()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(SyncHarness.StatusRoute, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task A_failed_sync_shows_only_failed_and_no_credential_or_failure_category()
    {
        await using var factory = SyncHarness.CreateFactory(SyncHarness.Refusing(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();
        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);

        await SyncHarness.WaitForRunToEnd(client);
        var status = await SyncHarness.ReadStatus(client);

        status.LastResult.Should().Be("failed");
        status.Body.Should().NotContain(SyncHarness.Token).And.NotContain(SyncHarness.Username);
        foreach (var failure in Enum.GetValues<SyncFailure>().Where(failure => failure != SyncFailure.None))
        {
            status.Body.Should().NotContainEquivalentOf(failure.ToString());
        }
    }
}
