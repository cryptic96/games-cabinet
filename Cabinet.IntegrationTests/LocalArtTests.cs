using System.Net;
using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using Cabinet.Repository.Images;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the local review setup works over real HTTP: the cabinet syncs from the fake BGG, downloads the synthetic pictures
/// the fake serves from its own origin, and shows them as covers, while pictures that cannot be used leave their games bare.
/// </summary>
[Trait("Category", "Sync")]
public sealed class LocalArtTests
{
    private const int CollectionSize = 65;
    private const int UndecodablePosition = 16;
    private const int NotFoundPosition = 17;
    private const string LayoutPath = "/cabinet/layout?profile=desktop";

    /// <summary>
    /// How long the picture run may take. It downloads, analyses and resizes well over a hundred pictures over real loopback
    /// HTTP, which a small shared build machine running the rest of the suite in parallel can take longer than the default
    /// wait to finish.
    /// </summary>
    private static readonly TimeSpan PictureRunWait = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task The_cabinet_fills_with_synthetic_art_served_by_the_fake_and_games_with_unusable_pictures_stay_bare()
    {
        using var storage = new TemporaryDirectory();
        await using var fake = FakeBggServer.Create(FakeBggScenario.Default, CollectionSize, 0);
        await fake.StartAsync(TestContext.Current.CancellationToken);
        var fakeOrigin = new Uri(fake.Urls.Single());
        var clock = SyncHarness.NewClock();
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Storage:Directory"] = storage.FullPath,
                ["Images:MaxDownloadsPerRun"] = "1000",
                ["Layout:CoverStrategy"] = "Random",
                ["Layout:CoverSharePercent"] = "100",
            },
            services =>
            {
                services.AddSingleton(new BggOptions(
                    new Uri(fakeOrigin, "/xmlapi2/"),
                    SyncHarness.Username,
                    SyncHarness.Token,
                    null,
                    BggOptions.MinimumRequestGap,
                    false,
                    "0.0.0-test"));
                services.AddSingleton(new ArtSourcePolicy(new HashSet<string>(), fakeOrigin));
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
                services.AddSingleton<IImagePacer>(new NoWaitPacer());
                services.AddSingleton<TimeProvider>(clock);
            });
        using var client = factory.CreatePublicClient();

        await SyncRounds.PressAndWait(client, clock, advance: false, timeout: PictureRunWait);
        await SyncHarness.WaitUntil(
            async () => Placements(await ReadLayout(client)).Any(placement => ArtUrl(placement) is not null),
            PictureRunWait);
        using var layout = JsonDocument.Parse(await ReadLayout(client));
        var placements = Placements(layout).ToList();
        var covers = placements.Where(placement => placement.GetProperty("kind").GetString() == "cover").ToList();
        var bare = covers.Where(placement => ArtUrl(placement) is null).ToList();

        covers.Should().HaveCountGreaterThan(CollectionSize / 2);
        bare.Select(placement => placement.GetProperty("entryId").GetInt64())
            .Should()
            .BeEquivalentTo([SyntheticBggCollection.FirstCollId + UndecodablePosition, SyntheticBggCollection.FirstCollId + NotFoundPosition]);
        foreach (var placement in covers.Except(bare))
        {
            var url = ArtUrl(placement)!;
            File.Exists(Path.Combine(storage.FullPath, "art", Path.GetFileName(url))).Should().BeTrue();
            using var served = await client.GetAsync(url, TestContext.Current.CancellationToken);
            served.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    private static string? ArtUrl(JsonElement placement) =>
        placement.TryGetProperty("art", out var art) ? art.GetProperty("url").GetString() : null;

    private static async Task<string> ReadLayout(HttpClient client) =>
        await client.GetStringAsync(LayoutPath, TestContext.Current.CancellationToken);

    private static IEnumerable<JsonElement> Placements(string layout)
    {
        using var document = JsonDocument.Parse(layout);

        return Placements(document).Select(placement => placement.Clone()).ToList();
    }

    private static IEnumerable<JsonElement> Placements(JsonDocument document) =>
        document.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray());
}
