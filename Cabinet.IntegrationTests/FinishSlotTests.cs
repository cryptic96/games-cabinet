using System.Net;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the site-wide finish reaches every page: it is read from the owner data, every page on both ports carries it as a
/// class on the body, and a stored value that is not on the allow-list, or data that cannot be read, is never written into a page.
/// </summary>
[Trait("Category", "Owner")]
public sealed class FinishSlotTests
{
    private const string BodyClassPattern = "<body class=\"finish-classic\">";
    private const string SettingsPath = "/owner/settings";
    private static readonly string[] OwnerPagePaths = ["/", "/owner/settings", "/owner/pictures", "/owner/locations"];

    [Fact]
    public async Task Every_page_on_both_ports_renders_the_classic_finish_on_the_body()
    {
        await using var factory = NewFactory();
        using var publicClient = factory.CreatePublicClient();
        using var session = new OwnerSession(factory);

        var publicPage = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);
        var ownerPages = new List<OwnerReply>();

        foreach (var path in OwnerPagePaths)
        {
            ownerPages.Add(await session.GetAsync(path));
        }

        publicPage.Should().Contain(BodyClassPattern);
        ownerPages.Should().OnlyContain(page => page.Status == HttpStatusCode.OK && page.Body.Contains(BodyClassPattern, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_stored_finish_that_is_not_on_the_allow_list_renders_as_classic_and_is_never_written_into_a_page()
    {
        using var storage = new TemporaryDirectory();
        OwnerDataSeed.Write(storage.FullPath, OwnerData.Empty with { Revision = 1, Finish = "walnut\" onload=\"x" });
        await using var factory = NewFactory(storage);
        using var publicClient = factory.CreatePublicClient();
        using var session = new OwnerSession(factory);

        var publicPage = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);
        var settings = await session.GetAsync(SettingsPath);

        publicPage.Should().Contain(BodyClassPattern).And.NotContain("walnut");
        settings.Body.Should().Contain(BodyClassPattern).And.NotContain("walnut");
    }

    [Fact]
    public async Task Owner_data_that_cannot_be_read_still_renders_the_classic_finish_on_both_ports()
    {
        using var storage = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(storage.FullPath, OwnerDataStore.FileName), "this is not owner data", TestContext.Current.CancellationToken);
        await using var factory = NewFactory(storage);
        using var publicClient = factory.CreatePublicClient();
        using var session = new OwnerSession(factory);

        var publicPage = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);
        var settings = await session.GetAsync(SettingsPath);

        factory.ServingServices.GetRequiredService<OwnerDataService>().State.Should().BeOfType<OwnerDataState.Unreadable>();
        publicPage.Should().Contain(BodyClassPattern);
        settings.Body.Should().Contain(BodyClassPattern);
    }

    private static CabinetWebApplicationFactory NewFactory(TemporaryDirectory? storage = null) =>
        SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            storage is null ? null : new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
}
