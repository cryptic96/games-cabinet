using System.Text.Json;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the ops health answer counts stored pictures the collection names but the art directory no longer holds, that it
/// stays healthy while it does, and that neither the count nor the warning it logs names a file.
/// </summary>
[Trait("Category", "Sync")]
public sealed class ArtFileAuditTests
{
    private const int CollectionSize = 20;

    [Fact]
    public async Task Health_counts_the_missing_pictures_stays_healthy_and_logs_one_warning_without_a_file_name()
    {
        await using var collection = await SyntheticArtCollection.StartAsync(CollectionSize);
        var logs = new CapturingLoggerProvider();
        await using var factory = new CabinetWebApplicationFactory(
            collection.Settings,
            services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);
                collection.ConfigureServices(services);
            });
        using var publicClient = factory.CreatePublicClient();
        using var ops = factory.CreateOpsClient();
        await collection.SyncAsync(publicClient);

        var before = await ReadHealth(ops);
        var url = (await SyntheticArtCollection.ArtPlacements(publicClient))[0].Url;
        collection.DeleteStoredPicture(url);
        var after = await ReadHealth(ops);
        var again = await ReadHealth(ops);

        before.Should().Be(("Healthy", 0));
        after.Should().Be(("Healthy", 1));
        again.Should().Be(("Healthy", 1));
        var warnings = logs.Lines.Where(line => line.Contains("missing on disk", StringComparison.Ordinal)).ToList();
        warnings.Should().ContainSingle();
        warnings[0].Should().NotContain(Path.GetFileName(url)).And.NotContain(".webp");
    }

    private static async Task<(string Status, int MissingArt)> ReadHealth(HttpClient ops)
    {
        var body = await ops.GetStringAsync("/health", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);

        return (document.RootElement.GetProperty("status").GetString()!, document.RootElement.GetProperty("missingArt").GetInt32());
    }
}
