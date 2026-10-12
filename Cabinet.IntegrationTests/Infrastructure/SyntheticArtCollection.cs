using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.Repository.Bgg;
using Cabinet.Repository.Images;
using Cabinet.Service.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// A cabinet whose collection and pictures come from the fake BGG with synthetic art, with its storage directory owned by
/// the test so a stored picture can be removed afterwards. Start the fake, build the host from <see cref="Settings"/> and
/// <see cref="ConfigureServices"/>, then call <see cref="SyncAsync"/> to fill the cabinet.
/// </summary>
public sealed class SyntheticArtCollection : IAsyncDisposable
{
    private const string LayoutPath = "/cabinet/layout?profile=desktop";

    private static readonly TimeSpan PictureRunWait = TimeSpan.FromSeconds(90);

    private readonly TemporaryDirectory _storage = new();
    private readonly Microsoft.AspNetCore.Builder.WebApplication _fake;
    private readonly Uri _fakeOrigin;

    private SyntheticArtCollection(Microsoft.AspNetCore.Builder.WebApplication fake)
    {
        _fake = fake;
        _fakeOrigin = new Uri(fake.Urls.Single());
        Clock = SyncHarness.NewClock();
    }

    /// <summary>The clock the host measures its windows on.</summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>The configuration the host needs: the storage directory and a collection made entirely of covers.</summary>
    public IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["Storage:Directory"] = _storage.FullPath,
        ["Images:MaxDownloadsPerRun"] = "1000",
        ["Layout:CoverStrategy"] = "Random",
        ["Layout:CoverSharePercent"] = "100",
        ["Layout:LieFlatBeforeNewSection"] = "false",
    };

    /// <summary>The directory the stored pictures are in.</summary>
    public string ArtDirectory => Path.Combine(_storage.FullPath, ArtCache.DirectoryName);

    /// <summary>Starts the fake source with the given number of games.</summary>
    /// <param name="size">How many games the fake collection holds.</param>
    public static async Task<SyntheticArtCollection> StartAsync(int size)
    {
        var fake = FakeBggServer.Create(FakeBggScenario.Default, size, 0);
        await fake.StartAsync(TestContext.Current.CancellationToken);

        return new SyntheticArtCollection(fake);
    }

    /// <summary>Points the host at the fake source and lets it download pictures from the fake's own origin.</summary>
    /// <param name="services">The services of the host being built.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new BggOptions(
            new Uri(_fakeOrigin, "/xmlapi2/"),
            SyncHarness.Username,
            SyncHarness.Token,
            null,
            BggOptions.MinimumRequestGap,
            false,
            "0.0.0-test"));
        services.AddSingleton(new ArtSourcePolicy(new HashSet<string>(), _fakeOrigin));
        services.AddSingleton<IRequestPacer>(new NoWaitPacer());
        services.AddSingleton<IImagePacer>(new NoWaitPacer());
        services.AddSingleton<TimeProvider>(Clock);
    }

    /// <summary>Syncs the collection and waits until at least one cover has its picture.</summary>
    /// <param name="client">A client on the public listener.</param>
    public async Task SyncAsync(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        await SyncRounds.PressAndWait(client, Clock, advance: false, timeout: PictureRunWait);
        await SyncHarness.WaitUntil(async () => (await ArtPlacements(client)).Count > 0, PictureRunWait);
    }

    /// <summary>Reads the placements of the desktop layout that show a stored picture.</summary>
    /// <param name="client">A client on the public listener.</param>
    /// <returns>The entry identifier and picture address of every placement that shows a picture.</returns>
    public static async Task<IReadOnlyList<(long EntryId, string Url)>> ArtPlacements(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var layout = JsonDocument.Parse(await client.GetStringAsync(LayoutPath, TestContext.Current.CancellationToken));

        return layout.RootElement.GetProperty("sections").EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .SelectMany(cubby => cubby.GetProperty("placements").EnumerateArray())
            .Where(placement => placement.TryGetProperty("art", out _))
            .Select(placement => (
                placement.GetProperty("entryId").GetInt64(),
                placement.GetProperty("art").GetProperty("url").GetString()!))
            .ToList();
    }

    /// <summary>Deletes the stored picture file a picture address names.</summary>
    /// <param name="url">The address of the picture on the site's own origin.</param>
    public void DeleteStoredPicture(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        File.Delete(Path.Combine(ArtDirectory, Path.GetFileName(url)));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _fake.DisposeAsync();
        _storage.Dispose();
    }
}
