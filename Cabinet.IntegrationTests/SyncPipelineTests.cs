using System.Net;
using System.Text.Json;
using System.Web;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves a press on the sync button carries the owned collection from a BGG-shaped source through the client, the
/// snapshot on disk and the layout into the page, and that the collection survives a restart or a damaged file.
/// </summary>
[Trait("Category", "Sync")]
public class SyncPipelineTests
{
    private const string Username = "sentinel-user-name";
    private const string Token = "sentinel-token-value";
    private const string BeingFilled = "The cabinet is being filled.";
    private const string LayoutPath = "/cabinet/layout?profile=desktop";

    [Fact]
    public async Task Pressing_sync_now_shows_the_owned_collection_after_two_collection_calls()
    {
        var items = SyntheticBggCollection.Create(5);
        var handler = ScriptedBggHandler.ForCollection(items);
        await using var factory = CreateFactory(handler);
        using var client = factory.CreatePublicClient();
        var initial = await ReadLayout(client);

        (await client.GetStringAsync("/", TestContext.Current.CancellationToken)).Should().Contain(BeingFilled);
        using var press = await client.PostAsync(SyncEndpointRoute, content: null, TestContext.Current.CancellationToken);
        await WaitUntil(async () => (await ReadLayout(client)).ETag != initial.ETag);
        var synced = await ReadLayout(client);

        press.StatusCode.Should().Be(HttpStatusCode.Accepted);
        synced.Titles.Should().BeEquivalentTo(items.Where(item => item.Owned).Select(item => item.Title));
        (await client.GetStringAsync("/", TestContext.Current.CancellationToken)).Should().NotContain(BeingFilled);
        AssertTwoCollectionCalls(handler);
    }

    [Fact]
    public async Task A_sync_over_real_http_fills_the_cabinet_from_a_local_source()
    {
        var items = SyntheticBggCollection.Create(5);
        await using var fake = FakeBggServer.Create(FakeBggScenario.Default, 5, 0);
        await fake.StartAsync(TestContext.Current.CancellationToken);
        var fakeBase = new Uri(new Uri(fake.Urls.Single()), "/xmlapi2/");
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?>(),
            services =>
            {
                services.AddSingleton(Options(fakeBase));
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
            });
        using var client = factory.CreatePublicClient();
        var initial = await ReadLayout(client);

        using var press = await client.PostAsync(SyncEndpointRoute, content: null, TestContext.Current.CancellationToken);
        await WaitUntil(async () => (await ReadLayout(client)).ETag != initial.ETag);
        var synced = await ReadLayout(client);

        press.StatusCode.Should().Be(HttpStatusCode.Accepted);
        synced.Titles.Should().BeEquivalentTo(items.Where(item => item.Owned).Select(item => item.Title));
    }

    [Fact]
    public async Task A_press_while_a_sync_is_running_answers_409_and_starts_nothing_more()
    {
        var items = SyntheticBggCollection.Create(5);
        var handler = new ScriptedBggHandler(request => ScriptedResponse.Xml(
            BggXml.Collection(items, CollectionQuery.Parse(request.RequestUri!.Query))) with { Delay = TimeSpan.FromSeconds(1) });
        await using var factory = CreateFactory(handler);
        using var client = factory.CreatePublicClient();
        var initial = await ReadLayout(client);

        using var first = await client.PostAsync(SyncEndpointRoute, content: null, TestContext.Current.CancellationToken);
        using var second = await client.PostAsync(SyncEndpointRoute, content: null, TestContext.Current.CancellationToken);
        await WaitUntil(async () => (await ReadLayout(client)).ETag != initial.ETag);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("\"running\"");
        second.ShouldBeNoStore();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_restart_shows_the_same_cabinet_with_the_same_etag_before_any_new_sync()
    {
        using var storage = new TemporaryDirectory();
        var storageSettings = new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath };
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5));
        string syncedETag;
        List<string> syncedTitles;

        await using (var first = CreateFactory(handler, storageSettings))
        {
            using var client = first.CreatePublicClient();
            var initial = await ReadLayout(client);
            using var press = await client.PostAsync(SyncEndpointRoute, content: null, TestContext.Current.CancellationToken);
            await WaitUntil(async () => (await ReadLayout(client)).ETag != initial.ETag);
            (syncedETag, syncedTitles) = await ReadLayout(client);
        }

        await using var second = CreateFactory(new ScriptedBggHandler(), storageSettings);
        using var restarted = second.CreatePublicClient();
        var afterRestart = await ReadLayout(restarted);

        File.Exists(Path.Combine(storage.FullPath, "snapshot.json")).Should().BeTrue();
        afterRestart.ETag.Should().Be(syncedETag);
        afterRestart.Titles.Should().BeEquivalentTo(syncedTitles);
        (await restarted.GetStringAsync("/", TestContext.Current.CancellationToken)).Should().NotContain(BeingFilled);
    }

    [Fact]
    public async Task A_damaged_snapshot_is_set_aside_and_the_site_shows_the_being_filled_state_and_stays_healthy()
    {
        using var storage = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(storage.FullPath, "snapshot.json"), "{ not json");
        File.WriteAllText(Path.Combine(storage.FullPath, ".snapshot.json.0123456789abcdef.tmp"), "partial");

        await using var factory = CreateFactory(new ScriptedBggHandler(), new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();
        using var ops = factory.CreateOpsClient();

        (await client.GetStringAsync("/", TestContext.Current.CancellationToken)).Should().Contain(BeingFilled);
        using var health = await ops.GetAsync("/health", TestContext.Current.CancellationToken);
        var healthBody = await health.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        healthBody.Should().Contain("Healthy");
        File.Exists(Path.Combine(storage.FullPath, "snapshot.json")).Should().BeFalse();
        File.Exists(Path.Combine(storage.FullPath, "snapshot.json.bad")).Should().BeTrue();
        Directory.EnumerateFiles(storage.FullPath, ".*.tmp").Should().BeEmpty();
    }

    private static string SyncEndpointRoute => "/cabinet/sync";

    private static BggOptions Options(Uri? baseUri = null) =>
        new(baseUri ?? BggOptions.DefaultBaseUri, Username, Token, null, BggOptions.MinimumRequestGap, false, "0.0.0-test");

    private static CabinetWebApplicationFactory CreateFactory(HttpMessageHandler handler, IReadOnlyDictionary<string, string?>? settings = null) =>
        new(
            settings ?? new Dictionary<string, string?>(),
            services =>
            {
                services.AddSingleton(Options());
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
                services.AddHttpClient<ICollectionSource, BggClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
            });

    private static void AssertTwoCollectionCalls(ScriptedBggHandler handler)
    {
        var requests = handler.Requests;

        requests.Should().HaveCount(2);
        requests.Should().OnlyContain(request =>
            request.Uri.Host == BggOptions.ApiHost
            && request.AuthorizationScheme == "Bearer"
            && request.AuthorizationParameter == Token
            && request.UserAgent!.StartsWith("GamesCabinet/", StringComparison.Ordinal));

        var baseGames = HttpUtility.ParseQueryString(requests[0].Uri.Query);
        var expansions = HttpUtility.ParseQueryString(requests[1].Uri.Query);

        baseGames["excludesubtype"].Should().Be("boardgameexpansion");
        baseGames["subtype"].Should().BeNull();
        expansions["subtype"].Should().Be("boardgameexpansion");
        expansions["excludesubtype"].Should().BeNull();
        foreach (var query in new[] { baseGames, expansions })
        {
            query["own"].Should().Be("1");
            query["version"].Should().Be("1");
            query["username"].Should().Be(Username);
            query["showprivate"].Should().BeNull();
        }
    }

    private static async Task<(string ETag, List<string> Titles)> ReadLayout(HttpClient client)
    {
        using var response = await client.GetAsync(LayoutPath, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var titles = document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray())
            .Select(placement => placement.GetProperty("title").GetString()!)
            .ToList();

        return (response.Headers.ETag!.Tag, titles);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() =>
            Directory.CreateDirectory(FullPath = Path.Combine(Path.GetTempPath(), $"cabinet-sync-tests-{Guid.NewGuid():N}"));

        public string FullPath { get; }

        public void Dispose() => Directory.Delete(FullPath, recursive: true);
    }

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not hold within ten seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }
    }
}
