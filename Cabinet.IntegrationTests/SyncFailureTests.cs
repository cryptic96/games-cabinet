using System.Net;
using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Proves that whatever BGG answers, a failed sync leaves the served cabinet and the stored collection untouched.</summary>
[Trait("Category", "Sync")]
public class SyncFailureTests
{
    private const string BeingFilled = "The cabinet is being filled.";

    [Theory]
    [InlineData("unauthorized")]
    [InlineData("forbidden-challenge-page")]
    [InlineData("too-many-requests")]
    [InlineData("server-error")]
    [InlineData("service-unavailable")]
    [InlineData("html-page-with-success-status")]
    [InlineData("errors-document")]
    [InlineData("malformed-xml")]
    [InlineData("moved-to-the-www-host")]
    public async Task A_failed_sync_leaves_the_layout_and_the_stored_collection_exactly_as_they_were(string answer)
    {
        using var storage = new TemporaryDirectory();
        var snapshotPath = Path.Combine(storage.FullPath, "snapshot.json");
        var clock = SyncHarness.NewClock();
        var source = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        await using var factory = SyncHarness.CreateFactory(
            source.Handler,
            clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var before = await SyncRounds.ReadLayout(client);
        var storedBefore = await File.ReadAllBytesAsync(snapshotPath, TestContext.Current.CancellationToken);
        var syncedBefore = (await SyncHarness.ReadStatus(client)).Json.GetProperty("lastSyncedUtc").GetDateTimeOffset();

        source.Fail(Failure(answer));
        await SyncRounds.PressAndWait(client, clock);
        var status = await SyncHarness.ReadStatus(client);

        (await SyncRounds.ReadLayout(client)).Should().BeEquivalentTo(before);
        (await File.ReadAllBytesAsync(snapshotPath, TestContext.Current.CancellationToken)).Should().Equal(storedBefore);
        status.LastResult.Should().Be("failed");
        status.Json.GetProperty("heldBack").GetBoolean().Should().BeFalse();
        status.Json.GetProperty("lastSyncedUtc").GetDateTimeOffset().Should().Be(syncedBefore);
        status.Body.Should().NotContain(SyncHarness.Token).And.NotContain(SyncHarness.Username);
    }

    [Fact]
    public async Task A_failure_in_the_expansion_call_alone_changes_nothing()
    {
        using var storage = new TemporaryDirectory();
        var snapshotPath = Path.Combine(storage.FullPath, "snapshot.json");
        var clock = SyncHarness.NewClock();
        var items = SyntheticBggCollection.Create(5);
        var calls = 0;
        var handler = new ScriptedBggHandler(request =>
            calls++ >= 2 && !request.RequestUri!.Query.Contains("excludesubtype", StringComparison.Ordinal)
                ? ScriptedResponse.Empty(HttpStatusCode.InternalServerError)
                : SyncRounds.Healthy(request, items));
        await using var factory = SyncHarness.CreateFactory(
            handler,
            clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        var before = await SyncRounds.ReadLayout(client);
        var storedBefore = await File.ReadAllBytesAsync(snapshotPath, TestContext.Current.CancellationToken);

        await SyncRounds.PressAndWait(client, clock);

        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("failed");
        (await SyncRounds.ReadLayout(client)).Should().BeEquivalentTo(before);
        (await File.ReadAllBytesAsync(snapshotPath, TestContext.Current.CancellationToken)).Should().Equal(storedBefore);
        handler.Requests.Should().HaveCount(5, "two for the first sync, then the base call, the failing expansion call and its one retry");
    }

    [Fact]
    public async Task A_failure_before_any_good_sync_is_not_an_empty_collection()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        var source = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        source.Fail(Failure("server-error"));
        await using var factory = SyncHarness.CreateFactory(
            source.Handler,
            clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false);
        var status = await SyncHarness.ReadStatus(client);

        status.LastResult.Should().Be("failed");
        status.Json.GetProperty("snapshotVersion").ValueKind.Should().Be(JsonValueKind.Null);
        File.Exists(Path.Combine(storage.FullPath, "snapshot.json")).Should().BeFalse();
        (await client.GetStringAsync("/", TestContext.Current.CancellationToken)).Should().Contain(BeingFilled);
    }

    [Fact]
    public async Task The_next_press_after_a_failure_recovers_the_collection()
    {
        var clock = SyncHarness.NewClock();
        var source = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        await using var factory = SyncHarness.CreateFactory(source.Handler, clock);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        source.Fail(Failure("too-many-requests"));
        await SyncRounds.PressAndWait(client, clock);
        var during = await SyncRounds.ReadLayout(client);

        source.Serve(SyntheticBggCollection.Create(5));
        await SyncRounds.PressAndWait(client, clock);
        var status = await SyncHarness.ReadStatus(client);

        status.LastResult.Should().Be("unchanged");
        (await SyncRounds.ReadLayout(client)).Should().BeEquivalentTo(during);
    }

    private static ScriptedResponse Failure(string answer) =>
        answer switch
        {
            "unauthorized" => ScriptedResponse.Empty(HttpStatusCode.Unauthorized),
            "forbidden-challenge-page" => new ScriptedResponse(HttpStatusCode.Forbidden, "text/html; charset=utf-8", BggXml.CloudflarePage()),
            "too-many-requests" => ScriptedResponse.Empty(HttpStatusCode.TooManyRequests),
            "server-error" => ScriptedResponse.Empty(HttpStatusCode.InternalServerError),
            "service-unavailable" => ScriptedResponse.Empty(HttpStatusCode.ServiceUnavailable),
            "html-page-with-success-status" => new ScriptedResponse(HttpStatusCode.OK, "text/html; charset=utf-8", BggXml.CloudflarePage()),
            "errors-document" => ScriptedResponse.Xml(BggXml.Errors("Invalid request")),
            "malformed-xml" => ScriptedResponse.Xml(BggXml.Malformed()),
            "moved-to-the-www-host" => ScriptedResponse.Empty(HttpStatusCode.MovedPermanently) with
            {
                Headers = new Dictionary<string, string> { ["Location"] = "https://www.boardgamegeek.com/xmlapi2/collection" },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(answer), answer, "That answer is not scripted."),
        };
}
